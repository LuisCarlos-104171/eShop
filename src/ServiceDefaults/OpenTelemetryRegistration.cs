using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace eShop.ServiceDefaults;

public static class OpenTelemetryRegistration
{
    public static IServiceCollection AddCheckoutOpenTelemetry(this IServiceCollection services, string serviceName)
    {
        return services.ConfigureOpenTelemetry(options =>
        {
            options.AddSource(OpenTelemetryCheckoutExtensions.BasketActivitySource.Name);
            options.AddSource(OpenTelemetryCheckoutExtensions.CheckoutActivitySource.Name);
            options.AddSource(OpenTelemetryCheckoutExtensions.OrderingActivitySource.Name);
            options.AddSource(OpenTelemetryCheckoutExtensions.PaymentActivitySource.Name);
            
            options.AddMeter("eShop.Checkout.Metrics");
        });
    }
    
    public static void AddCheckoutMetrics(this IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("eShop.Checkout.Metrics");
        
        meter.CreateCounter<long>("checkout_initiated_total", "Count of checkout processes initiated");
        meter.CreateCounter<long>("orders_created_total", "Count of orders successfully created");
        meter.CreateCounter<long>("payments_processed_total", "Count of payments processed");
        meter.CreateCounter<long>("payments_succeeded_total", "Count of payments that succeeded");
        meter.CreateCounter<long>("payments_failed_total", "Count of payments that failed");
        meter.CreateHistogram<double>("checkout_duration_seconds", "Duration of the checkout process in seconds");
    }
}
