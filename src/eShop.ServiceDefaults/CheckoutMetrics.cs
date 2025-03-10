using System.Diagnostics.Metrics;

namespace eShop.ServiceDefaults;

/// <summary>
/// Provides checkout metrics for the eShop application
/// </summary>
public class CheckoutMetrics
{
    private readonly Counter<long> _checkoutInitiatedCounter;
    private readonly Counter<long> _ordersCreatedCounter;
    private readonly Counter<long> _checkoutSuccessCounter;
    private readonly Counter<long> _checkoutFailureCounter;
    private readonly Histogram<double> _checkoutDurationHistogram;

    public CheckoutMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("eShop.Checkout.Metrics");
        
        _checkoutInitiatedCounter = meter.CreateCounter<long>(
            "checkout_initiated_total",
            description: "Total number of checkout processes initiated");
            
        _ordersCreatedCounter = meter.CreateCounter<long>(
            "orders_created_total",
            description: "Total number of orders successfully created");
            
        _checkoutSuccessCounter = meter.CreateCounter<long>(
            "checkout_success_total",
            description: "Total number of successful checkouts");
            
        _checkoutFailureCounter = meter.CreateCounter<long>(
            "checkout_failure_total", 
            description: "Total number of failed checkouts");
            
        _checkoutDurationHistogram = meter.CreateHistogram<double>(
            "checkout_duration_seconds",
            unit: "s",
            description: "Duration of checkout process in seconds");
    }

    /// <summary>
    /// Records that a checkout process was initiated
    /// </summary>
    public void RecordCheckoutInitiated() => _checkoutInitiatedCounter.Add(1);

    /// <summary>
    /// Records that an order was successfully created
    /// </summary>
    public void RecordOrderCreated() => _ordersCreatedCounter.Add(1);

    /// <summary>
    /// Records a successful checkout
    /// </summary>
    public void RecordCheckoutSuccess() => _checkoutSuccessCounter.Add(1);

    /// <summary>
    /// Records a failed checkout
    /// </summary>
    public void RecordCheckoutFailure() => _checkoutFailureCounter.Add(1);

    /// <summary>
    /// Records the duration of a checkout process
    /// </summary>
    /// <param name="seconds">Duration in seconds</param>
    public void RecordCheckoutDuration(double seconds) => _checkoutDurationHistogram.Record(seconds);
}
