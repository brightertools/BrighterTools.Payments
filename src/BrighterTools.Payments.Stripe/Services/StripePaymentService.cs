using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using System.Net.Mail;
using System.Text.Json;

namespace BrighterTools.Payments.Stripe;

public class StripePaymentService : IStripePaymentService
{
    private readonly StripePaymentsOptions _stripeSettings;
    private readonly ILogger<StripePaymentService> _logger;

    public StripePaymentService(IOptions<StripePaymentsOptions> stripeSettings, ILogger<StripePaymentService> logger)
    {
        _stripeSettings = stripeSettings.Value;
        _logger = logger;
        StripeConfiguration.ApiKey = _stripeSettings.ApiKey;
    }

    public Task<StripeCheckoutSessionResponse> CreateDonationCheckoutSession(StripeCheckoutSessionRequest request) => CreateCheckoutSession(request);
    public Task<StripeCheckoutSessionResponse> CreateOrganisationFundingSession(StripeCheckoutSessionRequest request) => CreateCheckoutSession(request);

    private static bool IsValidEmailAddress(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<StripeCheckoutSessionResponse> CreateCheckoutSession(StripeCheckoutSessionRequest request)
    {
        var customerEmail = IsValidEmailAddress(request.CustomerEmail) ? request.CustomerEmail : null;
        if (customerEmail == null && !string.IsNullOrWhiteSpace(request.CustomerEmail))
        {
            _logger.LogWarning("Ignoring invalid Stripe checkout customer email '{CustomerEmail}' for reference {Reference}", request.CustomerEmail, request.Reference);
        }

        var service = new SessionService();
        var session = await service.CreateAsync(
            new SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = request.SuccessUrl,
                CancelUrl = request.CancelUrl,
                CustomerEmail = customerEmail,
                PaymentMethodTypes = ["card", "link", "sepa_debit", "us_bank_account"],
                PaymentIntentData = new SessionPaymentIntentDataOptions
                {
                    Metadata = request.Metadata
                },
                LineItems =
                [
                    new SessionLineItemOptions
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = request.Currency.ToLowerInvariant(),
                            UnitAmountDecimal = request.Amount * 100m,
                            ProductData = new SessionLineItemPriceDataProductDataOptions { Name = request.Description }
                        }
                    }
                ],
                Metadata = request.Metadata
            },
            new RequestOptions { StripeAccount = string.IsNullOrWhiteSpace(request.AccountId) ? null : request.AccountId });

        return new StripeCheckoutSessionResponse
        {
            SessionId = session.Id,
            CheckoutUrl = session.Url ?? "",
            Reference = request.Reference
        };
    }

    public async Task<IReadOnlyList<StripeBalanceSnapshot>> GetStripeAvailableBalance(string connectedAccountId)
    {
        var service = new BalanceService();
        var balance = await service.GetAsync(new BalanceGetOptions(), new RequestOptions { StripeAccount = connectedAccountId });

        var result = balance.Available.Select(x => new StripeBalanceSnapshot
        {
            Currency = x.Currency.ToUpperInvariant(),
            Available = x.Amount / 100m
        }).ToList();

        foreach (var pending in balance.Pending)
        {
            var existing = result.FirstOrDefault(x => x.Currency == pending.Currency.ToUpperInvariant());
            if (existing == null)
            {
                result.Add(new StripeBalanceSnapshot
                {
                    Currency = pending.Currency.ToUpperInvariant(),
                    Pending = pending.Amount / 100m
                });
            }
            else
            {
                existing.Pending = pending.Amount / 100m;
            }
        }

        _logger.LogInformation(
            "Stripe balance snapshots for account {StripeAccountId}: {Snapshots}",
            connectedAccountId,
            string.Join(", ", result.Select(x => $"{x.Currency}:available={x.Available:0.00},pending={x.Pending:0.00}")));

        return result;
    }

    public async Task<string> CreateConnectedAccount(StripeConnectedAccountRequest request)
    {
        var service = new AccountService();
        var account = await service.CreateAsync(new AccountCreateOptions
        {
            Email = request.Email,
            Country = request.Country,
            DefaultCurrency = request.DefaultCurrency.ToLowerInvariant(),
            BusinessType = request.IsCompany ? "company" : "individual",
            BusinessProfile = new AccountBusinessProfileOptions
            {
                Name = request.BusinessProfileName,
                Url = request.BusinessProfileUrl
            },
            Controller = new AccountControllerOptions
            {
                Fees = new AccountControllerFeesOptions { Payer = request.FeesPayer },
                Losses = new AccountControllerLossesOptions { Payments = request.LossesPayments },
                RequirementCollection = request.RequirementCollection,
                StripeDashboard = new AccountControllerStripeDashboardOptions { Type = request.StripeDashboardType }
            },
            Capabilities = new AccountCapabilitiesOptions
            {
                CardPayments = request.RequestCardPayments ? new AccountCapabilitiesCardPaymentsOptions { Requested = true } : null,
                Transfers = request.RequestTransfers ? new AccountCapabilitiesTransfersOptions { Requested = true } : null
            },
            Metadata = request.Metadata
        });

        return account.Id;
    }

    public async Task EnsureConnectedAccountCapabilities(string connectedAccountId, bool requestCardPayments, bool requestTransfers)
    {
        if (!requestCardPayments && !requestTransfers)
        {
            return;
        }

        var service = new AccountService();
        await service.UpdateAsync(connectedAccountId, new AccountUpdateOptions
        {
            Capabilities = new AccountCapabilitiesOptions
            {
                CardPayments = requestCardPayments ? new AccountCapabilitiesCardPaymentsOptions { Requested = true } : null,
                Transfers = requestTransfers ? new AccountCapabilitiesTransfersOptions { Requested = true } : null
            }
        });
    }

    public async Task<string> CreateConnectedAccountOnboardingLink(string connectedAccountId, string refreshUrl, string returnUrl)
    {
        var service = new AccountLinkService();
        var link = await service.CreateAsync(new AccountLinkCreateOptions
        {
            Account = connectedAccountId,
            RefreshUrl = refreshUrl,
            ReturnUrl = returnUrl,
            Type = "account_onboarding"
        });

        return link.Url;
    }

    public async Task<StripeConnectedAccountStatus> GetConnectedAccountStatus(string connectedAccountId)
    {
        var service = new AccountService();
        var account = await service.GetAsync(connectedAccountId);

        return new StripeConnectedAccountStatus
        {
            StripeAccountId = account.Id,
            ChargesEnabled = account.ChargesEnabled,
            PayoutsEnabled = account.PayoutsEnabled,
            DetailsSubmitted = account.DetailsSubmitted,
            DefaultCurrency = account.DefaultCurrency?.ToUpperInvariant()
        };
    }

    public async Task<string> CreateCharityTransfer(StripeTransferRequest request)
    {
        var service = new TransferService();
        var transfer = await service.CreateAsync(
            new TransferCreateOptions
            {
                Amount = (long)Math.Round(request.Amount * 100m, MidpointRounding.AwayFromZero),
                Currency = request.Currency.ToLowerInvariant(),
                Destination = request.DestinationAccountId,
                TransferGroup = request.Reference,
                Metadata = request.Metadata
            },
            new RequestOptions { StripeAccount = string.IsNullOrWhiteSpace(request.SourceAccountId) ? null : request.SourceAccountId });

        return transfer.Id;
    }

    public async Task<string> CreateOrganisationPayout(StripePayoutRequest request)
    {
        var service = new PayoutService();
        var payout = await service.CreateAsync(
            new PayoutCreateOptions
            {
                Amount = (long)Math.Round(request.Amount * 100m, MidpointRounding.AwayFromZero),
                Currency = request.Currency.ToLowerInvariant(),
                Metadata = new Dictionary<string, string> { ["reference"] = request.Reference }
            },
            new RequestOptions { StripeAccount = request.AccountId });

        return payout.Id;
    }

    public async Task<StripeSettlementBreakdown?> GetPaymentIntentSettlement(string paymentIntentId, string? connectedAccountId = null)
    {
        var paymentIntentService = new PaymentIntentService();
        var paymentIntent = await paymentIntentService.GetAsync(paymentIntentId, null, new RequestOptions
        {
            StripeAccount = string.IsNullOrWhiteSpace(connectedAccountId) ? null : connectedAccountId
        });

        if (string.IsNullOrWhiteSpace(paymentIntent.LatestChargeId))
        {
            return null;
        }

        var chargeService = new ChargeService();
        var charge = await chargeService.GetAsync(paymentIntent.LatestChargeId, null, new RequestOptions
        {
            StripeAccount = string.IsNullOrWhiteSpace(connectedAccountId) ? null : connectedAccountId
        });

        return await GetBalanceTransactionBreakdown(charge.BalanceTransactionId, connectedAccountId);
    }

    public async Task<StripeSettlementBreakdown?> GetTransferSettlement(string transferId, string? connectedAccountId = null)
    {
        var transferService = new TransferService();
        var transfer = await transferService.GetAsync(transferId, null, new RequestOptions
        {
            StripeAccount = string.IsNullOrWhiteSpace(connectedAccountId) ? null : connectedAccountId
        });

        return await GetBalanceTransactionBreakdown(transfer.BalanceTransactionId, connectedAccountId);
    }

    public async Task<StripeSettlementBreakdown?> GetPayoutSettlement(string payoutId, string connectedAccountId)
    {
        var payoutService = new PayoutService();
        var payout = await payoutService.GetAsync(payoutId, null, new RequestOptions { StripeAccount = connectedAccountId });

        return await GetBalanceTransactionBreakdown(payout.BalanceTransactionId, connectedAccountId);
    }

    private async Task<StripeSettlementBreakdown?> GetBalanceTransactionBreakdown(string? balanceTransactionId, string? connectedAccountId = null)
    {
        if (string.IsNullOrWhiteSpace(balanceTransactionId))
        {
            return null;
        }

        var balanceTransactionService = new BalanceTransactionService();
        var balanceTransaction = await balanceTransactionService.GetAsync(balanceTransactionId, null, new RequestOptions
        {
            StripeAccount = string.IsNullOrWhiteSpace(connectedAccountId) ? null : connectedAccountId
        });

        return new StripeSettlementBreakdown
        {
            AmountSettlement = balanceTransaction.Amount / 100m,
            CurrencySettlement = (balanceTransaction.Currency ?? "GBP").ToUpperInvariant(),
            FeeAmount = balanceTransaction.Fee / 100m,
            FeeCurrency = (balanceTransaction.Currency ?? "GBP").ToUpperInvariant(),
            NetAmount = balanceTransaction.Net / 100m,
            ExchangeRate = balanceTransaction.ExchangeRate,
            BalanceTransactionId = balanceTransaction.Id,
            AvailableOnUtc = new DateTimeOffset(DateTime.SpecifyKind(balanceTransaction.AvailableOn, DateTimeKind.Utc))
        };
    }

    public Event ConstructWebhookEvent(string payload, string signatureHeader)
    {
        var secrets = new[] { _stripeSettings.WebhookSecret, _stripeSettings.ConnectedWebhookSecret }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        if (secrets.Count == 0)
        {
            _logger.LogWarning("Stripe webhook secrets are not configured.");
            throw new InvalidOperationException("Stripe webhook secrets are not configured.");
        }

        Exception? lastException = null;

        foreach (var secret in secrets)
        {
            try
            {
                return EventUtility.ConstructEvent(payload, signatureHeader, secret);
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("type", out var typeProperty))
            {
                _logger.LogWarning("Stripe webhook signature validation failed for event type {StripeEventType}.", typeProperty.GetString());
            }
        }
        catch
        {
        }

        throw lastException ?? new InvalidOperationException("Stripe webhook signature validation failed.");
    }
}
