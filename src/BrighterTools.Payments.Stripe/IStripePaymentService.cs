using Stripe;

namespace BrighterTools.Payments.Stripe;

public interface IStripePaymentService
{
    Task<StripeCheckoutSessionResponse> CreateDonationCheckoutSession(StripeCheckoutSessionRequest request);
    Task<StripeCheckoutSessionResponse> CreateOrganisationFundingSession(StripeCheckoutSessionRequest request);
    Task<IReadOnlyList<StripeBalanceSnapshot>> GetStripeAvailableBalance(string connectedAccountId);
    Task<string> CreateConnectedAccount(StripeConnectedAccountRequest request);
    Task EnsureConnectedAccountCapabilities(string connectedAccountId, bool requestCardPayments, bool requestTransfers);
    Task<string> CreateConnectedAccountOnboardingLink(string connectedAccountId, string refreshUrl, string returnUrl);
    Task<StripeConnectedAccountStatus> GetConnectedAccountStatus(string connectedAccountId);
    Task<string> CreateCharityTransfer(StripeTransferRequest request);
    Task<string> CreateOrganisationPayout(StripePayoutRequest request);
    Task<StripeSettlementBreakdown?> GetPaymentIntentSettlement(string paymentIntentId, string? connectedAccountId = null);
    Task<StripeSettlementBreakdown?> GetTransferSettlement(string transferId, string? connectedAccountId = null);
    Task<StripeSettlementBreakdown?> GetPayoutSettlement(string payoutId, string connectedAccountId);
    Event ConstructWebhookEvent(string payload, string signatureHeader);
}
