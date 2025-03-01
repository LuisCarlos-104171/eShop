using System.Diagnostics;
using eShop.ServiceDefaults;

namespace eShop.WebApp.Services;

public class OrderingService(HttpClient httpClient)
{
    private readonly string remoteServiceBaseUrl = "/api/Orders/";

    public async Task<OrderRecord[]> GetOrders()
    {
        using var activity = OpenTelemetryCheckoutExtensions.OrderingActivitySource.StartActivity("GetOrders");
        
        try 
        {
            var orders = await httpClient.GetFromJsonAsync<OrderRecord[]>(remoteServiceBaseUrl)!;
            activity?.SetTag("order.count", orders?.Length ?? 0);
            return orders ?? [];
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task CreateOrder(CreateOrderRequest request, Guid requestId)
    {
        // Start activity for order creation
        using var activity = OpenTelemetryCheckoutExtensions.OrderingActivitySource.StartActivity(
            OpenTelemetryCheckoutExtensions.CheckoutOperations.SubmitOrder);
        
        activity?.SetTag("order.request_id", requestId);
        activity?.SetTag("order.items_count", request.Items.Count);
        
        // Add user info and address data that will be masked
        activity?.SetTag("order.user.id", request.UserId);
        activity?.SetTag("order.user.name", request.UserName);
        activity?.SetTag("order.country", request.Country);
        activity?.SetTag("order.shipping.address", request.Street);
        
        try
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, remoteServiceBaseUrl);
            requestMessage.Headers.Add("x-requestid", requestId.ToString());
            
            // Add trace context to the outgoing request (standard headers)
            if (Activity.Current != null)
            {
                requestMessage.Headers.Add("traceparent", Activity.Current.Id);
                if (!string.IsNullOrEmpty(Activity.Current.TraceStateString))
                {
                    requestMessage.Headers.Add("tracestate", Activity.Current.TraceStateString);
                }
            }
            
            requestMessage.Content = JsonContent.Create(request);
            var response = await httpClient.SendAsync(requestMessage);
            
            if (!response.IsSuccessStatusCode)
            {
                activity?.SetStatus(ActivityStatusCode.Error, $"Order creation failed with status {response.StatusCode}");
                throw new HttpRequestException($"Order creation failed with status {response.StatusCode}");
            }
            
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}

public record OrderRecord(
    int OrderNumber,
    DateTime Date,
    string Status,
    decimal Total);
