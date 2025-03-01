using System.Diagnostics;
using eShop.Basket.API.Grpc;
using eShop.ServiceDefaults;
using GrpcBasketItem = eShop.Basket.API.Grpc.BasketItem;
using GrpcBasketClient = eShop.Basket.API.Grpc.Basket.BasketClient;

namespace eShop.WebApp.Services;

public class BasketService(GrpcBasketClient basketClient)
{
    public async Task<IReadOnlyCollection<BasketQuantity>> GetBasketAsync()
    {
        using var activity = OpenTelemetryCheckoutExtensions.BasketActivitySource.StartActivity("GetBasket");
        
        try
        {
            var result = await basketClient.GetBasketAsync(new ());
            activity?.SetTag("basket.items_count", result.Items.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            return MapToBasket(result);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task DeleteBasketAsync()
    {
        using var activity = OpenTelemetryCheckoutExtensions.BasketActivitySource.StartActivity("DeleteBasket");
        
        try
        {
            await basketClient.DeleteBasketAsync(new DeleteBasketRequest());
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task UpdateBasketAsync(IReadOnlyCollection<BasketQuantity> basket)
    {
        using var activity = OpenTelemetryCheckoutExtensions.BasketActivitySource.StartActivity("UpdateBasket");
        activity?.SetTag("basket.items_count", basket.Count);
        
        try
        {
            var updatePayload = new UpdateBasketRequest();

            foreach (var item in basket)
            {
                var updateItem = new GrpcBasketItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                };
                updatePayload.Items.Add(updateItem);
            }

            await basketClient.UpdateBasketAsync(updatePayload);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private static List<BasketQuantity> MapToBasket(CustomerBasketResponse response)
    {
        var result = new List<BasketQuantity>();
        foreach (var item in response.Items)
        {
            result.Add(new BasketQuantity(item.ProductId, item.Quantity));
        }

        return result;
    }
}

public record BasketQuantity(int ProductId, int Quantity);
