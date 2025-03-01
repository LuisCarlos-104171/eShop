using System.Diagnostics;
using eShop.EventBus.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using eShop.OrderProcessor.Events;
using eShop.ServiceDefaults;

namespace eShop.OrderProcessor.Services
{
    public class GracePeriodManagerService(
        IOptions<BackgroundTaskOptions> options,
        IEventBus eventBus,
        ILogger<GracePeriodManagerService> logger,
        NpgsqlDataSource dataSource) : BackgroundService
    {
        private readonly BackgroundTaskOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var delayTime = TimeSpan.FromSeconds(_options.CheckUpdateTime);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("GracePeriodManagerService is starting.");
                stoppingToken.Register(() => logger.LogDebug("GracePeriodManagerService background task is stopping."));
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("GracePeriodManagerService background task is doing background work.");
                }

                await CheckConfirmedGracePeriodOrders();

                await Task.Delay(delayTime, stoppingToken);
            }

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("GracePeriodManagerService background task is stopping.");
            }
        }

        private async Task CheckConfirmedGracePeriodOrders()
        {
            // Create a new activity for processing grace period orders
            using var activity = OpenTelemetryCheckoutExtensions.OrderingActivitySource.StartActivity("CheckGracePeriodOrders");
            
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Checking confirmed grace period orders");
            }

            try 
            {
                var orderIds = await GetConfirmedGracePeriodOrders();
                
                activity?.SetTag("orders.count", orderIds.Count);
                
                if (orderIds.Count == 0)
                {
                    // No orders to process
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    return;
                }

                foreach (var orderId in orderIds)
                {
                    // Create a child activity for each order being processed
                    using var orderActivity = OpenTelemetryCheckoutExtensions.OrderingActivitySource.StartActivity(
                        OpenTelemetryCheckoutExtensions.CheckoutOperations.ConfirmStock);
                    
                    orderActivity?.SetTag("order.id", orderId);
                    
                    try 
                    {
                        var confirmGracePeriodEvent = new GracePeriodConfirmedIntegrationEvent(orderId);

                        logger.LogInformation("Publishing integration event: {IntegrationEventId} - ({@IntegrationEvent})", 
                            confirmGracePeriodEvent.Id, confirmGracePeriodEvent);

                        await eventBus.PublishAsync(confirmGracePeriodEvent);
                        
                        orderActivity?.SetStatus(ActivityStatusCode.Ok);
                    }
                    catch (Exception ex)
                    {
                        orderActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                        logger.LogError(ex, "Error publishing GracePeriodConfirmedIntegrationEvent for order {OrderId}", orderId);
                    }
                }
                
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger.LogError(ex, "Error checking confirmed grace period orders");
            }
        }

        private async ValueTask<List<int>> GetConfirmedGracePeriodOrders()
        {
            try
            {
                using var activity = OpenTelemetryCheckoutExtensions.OrderingActivitySource.StartActivity("QueryGracePeriodOrders");
                
                using var conn = dataSource.CreateConnection();
                using var command = conn.CreateCommand();
                command.CommandText = """
                    SELECT "Id"
                    FROM ordering.orders
                    WHERE CURRENT_TIMESTAMP - "OrderDate" >= @GracePeriodTime AND "OrderStatus" = 'Submitted'
                    """;
                command.Parameters.AddWithValue("GracePeriodTime", TimeSpan.FromMinutes(_options.GracePeriodTime));

                List<int> ids = [];

                await conn.OpenAsync();
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    ids.Add(reader.GetInt32(0));
                }
                
                activity?.SetTag("orders.count", ids.Count);
                activity?.SetStatus(ActivityStatusCode.Ok);

                return ids;
            }
            catch (NpgsqlException exception)
            {
                logger.LogError(exception, "Fatal error establishing database connection");
                throw;
            }
        }
    }
}
