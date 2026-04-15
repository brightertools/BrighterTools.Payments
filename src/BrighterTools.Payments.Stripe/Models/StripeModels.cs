namespace BrighterTools.Payments.Stripe;

public class StripeCheckoutSessionRequest
{
    public string AccountId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public string SuccessUrl { get; set; } = "";
    public string CancelUrl { get; set; } = "";
    public string Description { get; set; } = "";
    public string Reference { get; set; } = "";
    public string? CustomerEmail { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public class StripeCheckoutSessionResponse
{
    public string SessionId { get; set; } = "";
    public string CheckoutUrl { get; set; } = "";
    public string Reference { get; set; } = "";
}

public class StripeTransferRequest
{
    public string SourceAccountId { get; set; } = "";
    public string DestinationAccountId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public string Reference { get; set; } = "";
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public class StripePayoutRequest
{
    public string AccountId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public string Reference { get; set; } = "";
}

public class StripeBalanceSnapshot
{
    public string Currency { get; set; } = "GBP";
    public decimal Available { get; set; }
    public decimal Pending { get; set; }
}

public class StripeConnectedAccountRequest
{
    public string? Email { get; set; }
    public string Country { get; set; } = "GB";
    public string DefaultCurrency { get; set; } = "GBP";
    public Dictionary<string, string> Metadata { get; set; } = [];
    public bool IsCompany { get; set; } = true;
    public string? BusinessProfileName { get; set; }
    public string? BusinessProfileUrl { get; set; }
    public string FeesPayer { get; set; } = "account";
    public string LossesPayments { get; set; } = "stripe";
    public string RequirementCollection { get; set; } = "stripe";
    public string StripeDashboardType { get; set; } = "full";
    public bool RequestCardPayments { get; set; } = true;
    public bool RequestTransfers { get; set; } = false;
}

public class StripeConnectedAccountStatus
{
    public string StripeAccountId { get; set; } = "";
    public bool ChargesEnabled { get; set; }
    public bool PayoutsEnabled { get; set; }
    public bool DetailsSubmitted { get; set; }
    public string? DefaultCurrency { get; set; }
}

public class StripeSettlementBreakdown
{
    public decimal AmountSettlement { get; set; }
    public string CurrencySettlement { get; set; } = "GBP";
    public decimal FeeAmount { get; set; }
    public string FeeCurrency { get; set; } = "GBP";
    public decimal NetAmount { get; set; }
    public decimal? ExchangeRate { get; set; }
    public string? BalanceTransactionId { get; set; }
    public DateTimeOffset? AvailableOnUtc { get; set; }
}
