using System.Diagnostics;

namespace eShop.ServiceDefaults;

/// <summary>
/// Central class for OpenTelemetry instrumentation for the checkout process.
/// </summary>
public static class OpenTelemetryCheckoutExtensions
{
    /// <summary>
    /// Activity source for basket operations
    /// </summary>
    public static readonly ActivitySource BasketActivitySource = new("eShop.Basket", "1.0.0");
    
    /// <summary>
    /// Activity source for order processing operations
    /// </summary>
    public static readonly ActivitySource OrderingActivitySource = new("eShop.Ordering", "1.0.0");
    
    /// <summary>
    /// Activity source for checkout end-to-end process
    /// </summary>
    public static readonly ActivitySource CheckoutActivitySource = new("eShop.Checkout", "1.0.0");
    
    /// <summary>
    /// Activity source for payment processing
    /// </summary>
    public static readonly ActivitySource PaymentActivitySource = new("eShop.Payment", "1.0.0");

    /// <summary>
    /// Standard checkout operation names to ensure consistency
    /// </summary>
    public static class CheckoutOperations
    {
        public const string InitiateCheckout = "InitiateCheckout";
        public const string ValidateCart = "ValidateCart";
        public const string SubmitOrder = "SubmitOrder";
        public const string ProcessPayment = "ProcessPayment";
        public const string ConfirmStock = "ConfirmStock";
        public const string CompleteOrder = "CompleteOrder";
    }
}
