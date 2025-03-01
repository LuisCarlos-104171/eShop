using System.Text.Json.Serialization;
using eShop.Basket.API.Model;
using System.Diagnostics;
using eShop.ServiceDefaults;

namespace eShop.Basket.API.Repositories;

public class RedisBasketRepository(ILogger<RedisBasketRepository> logger, IConnectionMultiplexer redis) : IBasketRepository
{
    private readonly IDatabase _database = redis.GetDatabase();

    // implementation:

    // - /basket/{id} "string" per unique basket
    private static RedisKey BasketKeyPrefix = "/basket/"u8.ToArray();
    // note on UTF8 here: library limitation (to be fixed) - prefixes are more efficient as blobs

    private static RedisKey GetBasketKey(string userId) => BasketKeyPrefix.Append(userId);

    public async Task<bool> DeleteBasketAsync(string id)
    {
        using var activity = OpenTelemetryCheckoutExtensions.BasketActivitySource.StartActivity("DeleteBasketFromRepository");
        activity?.SetTag("basket.id", id);
        
        var result = await _database.KeyDeleteAsync(GetBasketKey(id));
        
        activity?.SetStatus(result ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        
        return result;
    }

    public async Task<CustomerBasket> GetBasketAsync(string customerId)
    {
        using var activity = OpenTelemetryCheckoutExtensions.BasketActivitySource.StartActivity("GetBasketFromRepository");
        activity?.SetTag("basket.id", customerId);
        
        using var data = await _database.StringGetLeaseAsync(GetBasketKey(customerId));

        if (data is null || data.Length == 0)
        {
            activity?.SetTag("basket.found", false);
            activity?.SetStatus(ActivityStatusCode.Ok, "Basket not found");
            return null;
        }
        
        var basket = JsonSerializer.Deserialize(data.Span, BasketSerializationContext.Default.CustomerBasket);
        
        if (basket != null)
        {
            activity?.SetTag("basket.items_count", basket.Items.Count);
        }
        
        activity?.SetTag("basket.found", true);
        activity?.SetStatus(ActivityStatusCode.Ok);
        
        return basket;
    }

    public async Task<CustomerBasket> UpdateBasketAsync(CustomerBasket basket)
    {
        using var activity = OpenTelemetryCheckoutExtensions.BasketActivitySource.StartActivity("UpdateBasketInRepository");
        activity?.SetTag("basket.id", basket.BuyerId);
        activity?.SetTag("basket.items_count", basket.Items.Count);
        
        var json = JsonSerializer.SerializeToUtf8Bytes(basket, BasketSerializationContext.Default.CustomerBasket);
        var created = await _database.StringSetAsync(GetBasketKey(basket.BuyerId), json);

        if (!created)
        {
            logger.LogInformation("Problem occurred persisting the item.");
            activity?.SetStatus(ActivityStatusCode.Error, "Failed to persist basket");
            return null;
        }

        logger.LogInformation("Basket item persisted successfully.");
        activity?.SetStatus(ActivityStatusCode.Ok);
        
        return await GetBasketAsync(basket.BuyerId);
    }
}

[JsonSerializable(typeof(CustomerBasket))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
public partial class BasketSerializationContext : JsonSerializerContext
{

}
