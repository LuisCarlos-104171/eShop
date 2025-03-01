using System.Diagnostics.Metrics;

namespace eShop.ServiceDefaults.Telemetry;

/// <summary>
/// Helper class for tracking checkout-related metrics
/// </summary>
public class CheckoutMetrics
{
    private readonly Counter<long> _checkoutInitiatedCounter;
    private readonly Counter<long> _ordersCreatedCounter;
    private readonly Counter<long> _paymentsProcessedCounter;
    private readonly Counter<long> _paymentsSucceededCounter;
    private readonly Counter<long> _paymentsFailedCounter;
    private readonly Histogram<double> _checkoutDurationHistogram;

    public CheckoutMetrics(Meter meter)
    {
        _checkoutInitiatedCounter = meter.CreateCounter<long>(
            "checkout_initiated_total", 
            description: "Count of checkout processes initiated");
            
        _ordersCreatedCounter = meter.CreateCounter<long>(
            "orders_created_total", 
            description: "Count of orders successfully created");
            
        _paymentsProcessedCounter = meter.CreateCounter<long>(
            "payments_processed_total", 
            description: "Count of payments processed");
            
        _paymentsSucceededCounter = meter.CreateCounter<long>(
            "payments_succeeded_total", 
            description: "Count of payments that succeeded");
            
        _paymentsFailedCounter = meter.CreateCounter<long>(
            "payments_failed_total", 
            description: "Count of payments that failed");
            
        _checkoutDurationHistogram = meter.CreateHistogram<double>(
            "checkout_duration_seconds", 
            description: "Duration of the checkout process in seconds");
    }

    /// <summary>
    /// Increment the counter for checkout initiations
    /// </summary>
    public void CheckoutInitiated() => _checkoutInitiatedCounter.Add(1);

    /// <summary>
    /// Increment the counter for created orders
    /// </summary>
    public void OrderCreated() => _ordersCreatedCounter.Add(1);

    /// <summary>
    /// Increment the counter for processed payments
    /// </summary>
    public void PaymentProcessed() => _paymentsProcessedCounter.Add(1);

    /// <summary>
    /// Increment the counter for successful payments
    /// </summary>
    public void PaymentSucceeded() => _paymentsSucceededCounter.Add(1);

    /// <summary>
    /// Increment the counter for failed payments
    /// </summary>
    public void PaymentFailed() => _paymentsFailedCounter.Add(1);

    /// <summary>
    /// Record the duration of a checkout process
    /// </summary>
    /// <param name="durationSeconds">Duration in seconds</param>
    public void RecordCheckoutDuration(double durationSeconds) => _checkoutDurationHistogram.Record(durationSeconds);
}
