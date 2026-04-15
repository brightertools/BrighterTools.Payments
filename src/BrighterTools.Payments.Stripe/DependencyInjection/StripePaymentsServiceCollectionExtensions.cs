using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BrighterTools.Payments.Stripe.DependencyInjection;

public static class StripePaymentsServiceCollectionExtensions
{
    public static IServiceCollection AddBrighterToolsStripePayments(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StripePaymentsOptions>(configuration.GetSection(StripePaymentsOptions.SectionName));
        services.AddScoped<IStripePaymentService, StripePaymentService>();
        return services;
    }
}
