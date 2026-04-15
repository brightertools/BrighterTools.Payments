namespace BrighterTools.Payments.Stripe;

public class StripePaymentsOptions
{
    public const string SectionName = "Stripe";

    public string ApiKey { get; set; } = string.Empty;
    public string SuccessUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string ConnectedWebhookSecret { get; set; } = string.Empty;
}
