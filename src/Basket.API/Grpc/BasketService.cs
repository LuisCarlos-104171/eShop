using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using eShop.Basket.API.Repositories;
using eShop.Basket.API.Extensions;
using eShop.Basket.API.Model;
using eShop.ServiceDefaults;

namespace eShop.Basket.API.Grpc;

public class BasketService(
    IBasketRepository repository,
    ILogger<BasketService> logger) : Basket.BasketBase
{
    [AllowAnonymous]
    public override async Task<CustomerBasketResponse> GetBasket(GetBasketRequest request, ServerCallContext context)
    {
        using var activity = OpenTelemetryCheckoutExtensions.BasketActivitySource.StartActivity("GetBasket");
        
        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "User not authenticated");
            return new();
        }

        // Add user ID for masking
        activity?.SetTag("user.id", userId);
        
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Begin GetBasketById call from method {Method} for basket id {Id}", context.Method, userId);
        }

        var data = await repository.GetBasketAsync(userId);

        if (data is not null)
        {
            activity?.SetTag("basket.items_count", data.Items.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return MapToCustomerBasketResponse(data);
        }

        activity?.SetTag("basket.empty", true);
        activity?.SetStatus(ActivityStatusCode.Ok);
        return new();
    }

    public override async Task<CustomerBasketResponse> UpdateBasket(UpdateBasketRequest request, ServerCallContext context)
    {
        using var activity = OpenTelemetryCheckoutExtensions.BasketActivitySource.StartActivity("UpdateBasket");
        
        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "User not authenticated");
            ThrowNotAuthenticated();
        }

        // Add user ID for masking
        activity?.SetTag("user.id", userId);
        activity?.SetTag("basket.items_count", request.Items.Count);
        
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Begin UpdateBasket call from method {Method} for basket id {Id}", context.Method, userId);
        }

        var customerBasket = MapToCustomerBasket(userId, request);
        var response = await repository.UpdateBasketAsync(customerBasket);
        if (response is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Basket not found");
            ThrowBasketDoesNotExist(userId);
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
        return MapToCustomerBasketResponse(response);
    }

    public override async Task<DeleteBasketResponse> DeleteBasket(DeleteBasketRequest request, ServerCallContext context)
    {
        using var activity = OpenTelemetryCheckoutExtensions.BasketActivitySource.StartActivity("DeleteBasket");
        
        var userId = context.GetUserIdentity();
        if (string.IsNullOrEmpty(userId))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "User not authenticated");
            ThrowNotAuthenticated();
        }

        // Add user ID for masking
        activity?.SetTag("user.id", userId);
        
        await repository.DeleteBasketAsync(userId);
        
        activity?.SetStatus(ActivityStatusCode.Ok);
        return new();
    }

    [DoesNotReturn]
    private static void ThrowNotAuthenticated() => throw new RpcException(new Status(StatusCode.Unauthenticated, "The caller is not authenticated."));

    [DoesNotReturn]
    private static void ThrowBasketDoesNotExist(string userId) => throw new RpcException(new Status(StatusCode.NotFound, $"Basket with buyer id {userId} does not exist"));

    private static CustomerBasketResponse MapToCustomerBasketResponse(CustomerBasket customerBasket)
    {
        var response = new CustomerBasketResponse();

        foreach (var item in customerBasket.Items)
        {
            response.Items.Add(new BasketItem()
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
            });
        }

        return response;
    }

    private static CustomerBasket MapToCustomerBasket(string userId, UpdateBasketRequest customerBasketRequest)
    {
        var response = new CustomerBasket
        {
            BuyerId = userId
        };

        foreach (var item in customerBasketRequest.Items)
        {
            response.Items.Add(new()
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
            });
        }

        return response;
    }
}
