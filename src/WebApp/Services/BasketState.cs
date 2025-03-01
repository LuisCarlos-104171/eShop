using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using eShop.ServiceDefaults;
using eShop.WebAppComponents.Catalog;
using eShop.WebAppComponents.Services;

namespace eShop.WebApp.Services;

public class BasketState(
    BasketService basketService,
    CatalogService catalogService,
    OrderingService orderingService,
    AuthenticationStateProvider authenticationStateProvider) : IBasketState
{
    private Task<IReadOnlyCollection<BasketItem>>? _cachedBasket;
    private HashSet<BasketStateChangedSubscription> _changeSubscriptions = new();

    public Task DeleteBasketAsync()
        => basketService.DeleteBasketAsync();

    public async Task<IReadOnlyCollection<BasketItem>> GetBasketItemsAsync()
        => (await GetUserAsync()).Identity?.IsAuthenticated == true
        ? await FetchBasketItemsAsync()
        : [];

    public IDisposable NotifyOnChange(EventCallback callback)
    {
        var subscription = new BasketStateChangedSubscription(this, callback);
        _changeSubscriptions.Add(subscription);
        return subscription;
    }

    public async Task AddAsync(CatalogItem item)
    {
        using var activity = OpenTelemetryCheckoutExtensions.BasketActivitySource.StartActivity("AddToBasket");
        activity?.SetTag("product.id", item.Id);
        activity?.SetTag("product.name", item.Name);
        
        var items = (await FetchBasketItemsAsync()).Select(i => new BasketQuantity(i.ProductId, i.Quantity)).ToList();
        bool found = false;
        for (var i = 0; i < items.Count; i++)
        {
            var existing = items[i];
            if (existing.ProductId == item.Id)
            {
                items[i] = existing with { Quantity = existing.Quantity + 1 };
                found = true;
                break;
            }
        }

        if (!found)
        {
            items.Add(new BasketQuantity(item.Id, 1));
        }

        _cachedBasket = null;
        await basketService.UpdateBasketAsync(items);
        await NotifyChangeSubscribersAsync();
    }

    public async Task SetQuantityAsync(int productId, int quantity)
    {
        using var activity = OpenTelemetryCheckoutExtensions.BasketActivitySource.StartActivity("UpdateBasketQuantity");
        activity?.SetTag("product.id", productId);
        activity?.SetTag("quantity", quantity);
        
        var existingItems = (await FetchBasketItemsAsync()).ToList();
        if (existingItems.FirstOrDefault(row => row.ProductId == productId) is { } row)
        {
            if (quantity > 0)
            {
                row.Quantity = quantity;
            }
            else
            {
                existingItems.Remove(row);
            }

            _cachedBasket = null;
            await basketService.UpdateBasketAsync(existingItems.Select(i => new BasketQuantity(i.ProductId, i.Quantity)).ToList());
            await NotifyChangeSubscribersAsync();
        }
    }

    public async Task CheckoutAsync(BasketCheckoutInfo checkoutInfo)
    {
        // Start a new activity for the checkout process
        using var checkoutActivity = OpenTelemetryCheckoutExtensions.CheckoutActivitySource.StartActivity(
            OpenTelemetryCheckoutExtensions.CheckoutOperations.InitiateCheckout);
        
        if (checkoutInfo.RequestId == default)
        {
            checkoutInfo.RequestId = Guid.NewGuid();
        }
        
        // Add the request ID for correlation
        checkoutActivity?.SetTag("checkout.request_id", checkoutInfo.RequestId);

        try
        {
            var buyerId = await authenticationStateProvider.GetBuyerIdAsync() ?? throw new InvalidOperationException("User does not have a buyer ID");
            var userName = await authenticationStateProvider.GetUserNameAsync() ?? throw new InvalidOperationException("User does not have a user name");

            // Add masked user information
            checkoutActivity?.SetTag("user.id", buyerId);

            // Start an activity for getting basket items
            using (var validateActivity = OpenTelemetryCheckoutExtensions.CheckoutActivitySource.StartActivity(
                OpenTelemetryCheckoutExtensions.CheckoutOperations.ValidateCart))
            {
                // Get details for the items in the basket
                var basketItems = await FetchBasketItemsAsync();
                
                validateActivity?.SetTag("basket.item_count", basketItems.Count);
                validateActivity?.SetTag("basket.total_items", basketItems.Sum(i => i.Quantity));
                
                if (basketItems.Count == 0)
                {
                    validateActivity?.SetStatus(ActivityStatusCode.Error, "Basket is empty");
                    throw new InvalidOperationException("Cannot checkout with an empty basket");
                }
            }

            // Get details for the items in the basket
            var orderItems = await FetchBasketItemsAsync();

            // Start an activity for submitting the order
            using (var submitActivity = OpenTelemetryCheckoutExtensions.CheckoutActivitySource.StartActivity(
                OpenTelemetryCheckoutExtensions.CheckoutOperations.SubmitOrder))
            {
                // Add shipping and order info (will be masked)
                submitActivity?.SetTag("order.shipping.country", checkoutInfo.Country);
                submitActivity?.SetTag("order.shipping.state", checkoutInfo.State);
                submitActivity?.SetTag("order.shipping.city", checkoutInfo.City);
                
                // Calculate order metrics for telemetry
                var totalAmount = orderItems.Sum(i => i.UnitPrice * i.Quantity);
                submitActivity?.SetTag("order.total_amount", totalAmount);
                submitActivity?.SetTag("order.item_count", orderItems.Count);

                // Call into Ordering.API to create the order using those details
                var request = new CreateOrderRequest(
                    UserId: buyerId,
                    UserName: userName,
                    City: checkoutInfo.City!,
                    Street: checkoutInfo.Street!,
                    State: checkoutInfo.State!,
                    Country: checkoutInfo.Country!,
                    ZipCode: checkoutInfo.ZipCode!,
                    CardNumber: "1111222233334444",
                    CardHolderName: "TESTUSER",
                    CardExpiration: DateTime.UtcNow.AddYears(1),
                    CardSecurityNumber: "111",
                    CardTypeId: checkoutInfo.CardTypeId,
                    Buyer: buyerId,
                    Items: [.. orderItems]);
                
                await orderingService.CreateOrder(request, checkoutInfo.RequestId);
                await DeleteBasketAsync();
                
                submitActivity?.SetStatus(ActivityStatusCode.Ok);
            }
            
            checkoutActivity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            // Record the exception in the telemetry
            checkoutActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw; // Re-throw to preserve the original exception
        }
    }

    private Task NotifyChangeSubscribersAsync()
        => Task.WhenAll(_changeSubscriptions.Select(s => s.NotifyAsync()));

    private async Task<ClaimsPrincipal> GetUserAsync()
        => (await authenticationStateProvider.GetAuthenticationStateAsync()).User;

    private Task<IReadOnlyCollection<BasketItem>> FetchBasketItemsAsync()
    {
        return _cachedBasket ??= FetchCoreAsync();

        async Task<IReadOnlyCollection<BasketItem>> FetchCoreAsync()
        {
            using var activity = OpenTelemetryCheckoutExtensions.BasketActivitySource.StartActivity("FetchBasketItems");
            
            var quantities = await basketService.GetBasketAsync();
            if (quantities.Count == 0)
            {
                activity?.SetTag("basket.empty", true);
                return [];
            }

            // Get details for the items in the basket
            var basketItems = new List<BasketItem>();
            var productIds = quantities.Select(row => row.ProductId);
            var catalogItems = (await catalogService.GetCatalogItems(productIds)).ToDictionary(k => k.Id, v => v);
            
            activity?.SetTag("basket.item_count", quantities.Count);
            
            foreach (var item in quantities)
            {
                var catalogItem = catalogItems[item.ProductId];
                var orderItem = new BasketItem
                {
                    Id = Guid.NewGuid().ToString(), // TODO: this value is meaningless, use ProductId instead.
                    ProductId = catalogItem.Id,
                    ProductName = catalogItem.Name,
                    UnitPrice = catalogItem.Price,
                    Quantity = item.Quantity,
                };
                basketItems.Add(orderItem);
            }

            return basketItems;
        }
    }

    private class BasketStateChangedSubscription(BasketState Owner, EventCallback Callback) : IDisposable
    {
        public Task NotifyAsync() => Callback.InvokeAsync();
        public void Dispose() => Owner._changeSubscriptions.Remove(this);
    }
}

public record CreateOrderRequest(
    string UserId,
    string UserName,
    string City,
    string Street,
    string State,
    string Country,
    string ZipCode,
    string CardNumber,
    string CardHolderName,
    DateTime CardExpiration,
    string CardSecurityNumber,
    int CardTypeId,
    string Buyer,
    List<BasketItem> Items);
