using System.Diagnostics;
using eShop.ServiceDefaults;

namespace eShop.PaymentProcessor.IntegrationEvents.EventHandling;

public class OrderStatusChangedToStockConfirmedIntegrationEventHandler(
    IEventBus eventBus,
    IOptionsMonitor<PaymentOptions> options,
    ILogger<OrderStatusChangedToStockConfirmedIntegrationEventHandler> logger) :
    IIntegrationEventHandler<OrderStatusChangedToStockConfirmedIntegrationEvent>
{
    public async Task Handle(OrderStatusChangedToStockConfirmedIntegrationEvent @event)
    {
        // Start activity for payment processing
        using var activity = OpenTelemetryCheckoutExtensions.PaymentActivitySource.StartActivity(
            OpenTelemetryCheckoutExtensions.CheckoutOperations.ProcessPayment);
        
        activity?.SetTag("order.id", @event.OrderId);
        
        logger.LogInformation("Handling integration event: {IntegrationEventId} - ({@IntegrationEvent})", @event.Id, @event);

        IntegrationEvent orderPaymentIntegrationEvent;

        // Business feature comment:
        // When OrderStatusChangedToStockConfirmed Integration Event is handled.
        // Here we're simulating that we'd be performing the payment against any payment gateway
        // Instead of a real payment we just take the env. var to simulate the payment 
        // The payment can be successful or it can fail

        try
        {
            if (options.CurrentValue.PaymentSucceeded)
            {
                orderPaymentIntegrationEvent = new OrderPaymentSucceededIntegrationEvent(@event.OrderId);
                activity?.SetTag("payment.status", "succeeded");
            }
            else
            {
                orderPaymentIntegrationEvent = new OrderPaymentFailedIntegrationEvent(@event.OrderId);
                activity?.SetTag("payment.status", "failed");
                activity?.SetStatus(ActivityStatusCode.Error, "Payment failed");
            }

            logger.LogInformation("Publishing integration event: {IntegrationEventId} - ({@IntegrationEvent})", orderPaymentIntegrationEvent.Id, orderPaymentIntegrationEvent);

            await eventBus.PublishAsync(orderPaymentIntegrationEvent);
            
            if (options.CurrentValue.PaymentSucceeded)
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Error processing payment for order {OrderId}", @event.OrderId);
            throw;
        }
    }
}
