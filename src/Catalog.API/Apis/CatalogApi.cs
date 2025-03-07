using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Pgvector.EntityFrameworkCore;
using eShop.ServiceDefaults;

namespace eShop.Catalog.API;

public static class CatalogApi
{
    public static IEndpointRouteBuilder MapCatalogApi(this IEndpointRouteBuilder app)
    {
        // RouteGroupBuilder for catalog endpoints
        var vApi = app.NewVersionedApi("Catalog");
        var api = vApi.MapGroup("api/catalog").HasApiVersion(1, 0).HasApiVersion(2, 0);
        var v1 = vApi.MapGroup("api/catalog").HasApiVersion(1, 0);
        var v2 = vApi.MapGroup("api/catalog").HasApiVersion(2, 0);

        // Routes for querying catalog items.
        v1.MapGet("/items", GetAllItemsV1)
            .WithName("ListItems")
            .WithSummary("List catalog items")
            .WithDescription("Get a paginated list of items in the catalog.")
            .WithTags("Items");
        v2.MapGet("/items", GetAllItems)
            .WithName("ListItems-V2")
            .WithSummary("List catalog items")
            .WithDescription("Get a paginated list of items in the catalog.")
            .WithTags("Items");
        api.MapGet("/items/by", GetItemsByIds)
            .WithName("BatchGetItems")
            .WithSummary("Batch get catalog items")
            .WithDescription("Get multiple items from the catalog")
            .WithTags("Items");
        api.MapGet("/items/{id:int}", GetItemById)
            .WithName("GetItem")
            .WithSummary("Get catalog item")
            .WithDescription("Get an item from the catalog")
            .WithTags("Items");
        v1.MapGet("/items/by/{name:minlength(1)}", GetItemsByName)
            .WithName("GetItemsByName")
            .WithSummary("Get catalog items by name")
            .WithDescription("Get a paginated list of catalog items with the specified name.")
            .WithTags("Items");
        api.MapGet("/items/{id:int}/pic", GetItemPictureById)
            .WithName("GetItemPicture")
            .WithSummary("Get catalog item picture")
            .WithDescription("Get the picture for a catalog item")
            .WithTags("Items");

        // Routes for resolving catalog items using AI.
        v1.MapGet("/items/withsemanticrelevance/{text:minlength(1)}", GetItemsBySemanticRelevanceV1)
            .WithName("GetRelevantItems")
            .WithSummary("Search catalog for relevant items")
            .WithDescription("Search the catalog for items related to the specified text")
            .WithTags("Search");

                // Routes for resolving catalog items using AI.
        v2.MapGet("/items/withsemanticrelevance", GetItemsBySemanticRelevance)
            .WithName("GetRelevantItems-V2")
            .WithSummary("Search catalog for relevant items")
            .WithDescription("Search the catalog for items related to the specified text")
            .WithTags("Search");

        // Routes for resolving catalog items by type and brand.
        v1.MapGet("/items/type/{typeId}/brand/{brandId?}", GetItemsByBrandAndTypeId)
            .WithName("GetItemsByTypeAndBrand")
            .WithSummary("Get catalog items by type and brand")
            .WithDescription("Get catalog items of the specified type and brand")
            .WithTags("Types");
        v1.MapGet("/items/type/all/brand/{brandId:int?}", GetItemsByBrandId)
            .WithName("GetItemsByBrand")
            .WithSummary("List catalog items by brand")
            .WithDescription("Get a list of catalog items for the specified brand")
            .WithTags("Brands");
        api.MapGet("/catalogtypes",
            [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
            async (CatalogContext context) => await context.CatalogTypes.OrderBy(x => x.Type).ToListAsync())
            .WithName("ListItemTypes")
            .WithSummary("List catalog item types")
            .WithDescription("Get a list of the types of catalog items")
            .WithTags("Types");
        api.MapGet("/catalogbrands",
            [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
            async (CatalogContext context) => await context.CatalogBrands.OrderBy(x => x.Brand).ToListAsync())
            .WithName("ListItemBrands")
            .WithSummary("List catalog item brands")
            .WithDescription("Get a list of the brands of catalog items")
            .WithTags("Brands");

        // Routes for modifying catalog items.
        v1.MapPut("/items", UpdateItemV1)
            .WithName("UpdateItem")
            .WithSummary("Create or replace a catalog item")
            .WithDescription("Create or replace a catalog item")
            .WithTags("Items");
        v2.MapPut("/items/{id:int}", UpdateItem)
            .WithName("UpdateItem-V2")
            .WithSummary("Create or replace a catalog item")
            .WithDescription("Create or replace a catalog item")
            .WithTags("Items");
        api.MapPost("/items", CreateItem)
            .WithName("CreateItem")
            .WithSummary("Create a catalog item")
            .WithDescription("Create a new item in the catalog");
        api.MapDelete("/items/{id:int}", DeleteItemById)
            .WithName("DeleteItem")
            .WithSummary("Delete catalog item")
            .WithDescription("Delete the specified catalog item");

        return app;
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Ok<PaginatedItems<CatalogItem>>> GetAllItemsV1(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services)
    {
        return await GetAllItems(paginationRequest, services, null, null, null);
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Ok<PaginatedItems<CatalogItem>>> GetAllItems(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("The name of the item to return")] string name,
        [Description("The type of items to return")] int? type,
        [Description("The brand of items to return")] int? brand)
    {
        using var activity = OpenTelemetryCheckoutExtensions.CatalogActivitySource.StartActivity(
            OpenTelemetryCheckoutExtensions.CatalogOperations.GetItems);
        
        var pageSize = paginationRequest.PageSize;
        var pageIndex = paginationRequest.PageIndex;

        // Add relevant tags
        activity?.SetTag("catalog.page_size", pageSize);
        activity?.SetTag("catalog.page_index", pageIndex);
        activity?.SetTag("catalog.filter.name", name);
        activity?.SetTag("catalog.filter.type_id", type);
        activity?.SetTag("catalog.filter.brand_id", brand);
        
        try
        {
            var root = (IQueryable<CatalogItem>)services.Context.CatalogItems;

            if (name is not null)
            {
                root = root.Where(c => c.Name.StartsWith(name));
            }
            if (type is not null)
            {
                root = root.Where(c => c.CatalogTypeId == type);
            }
            if (brand is not null)
            {
                root = root.Where(c => c.CatalogBrandId == brand);
            }

            var totalItems = await root
                .LongCountAsync();

            var itemsOnPage = await root
                .OrderBy(c => c.Name)
                .Skip(pageSize * pageIndex)
                .Take(pageSize)
                .ToListAsync();

            // Add results to span
            activity?.SetTag("catalog.total_items", totalItems);
            activity?.SetTag("catalog.items_returned", itemsOnPage.Count);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return TypedResults.Ok(new PaginatedItems<CatalogItem>(pageIndex, pageSize, totalItems, itemsOnPage));
        }
        catch (Exception ex)
        {
            // Set exception information on the span
            activity?.SetExceptionTags(ex);
            throw;
        }
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Ok<List<CatalogItem>>> GetItemsByIds(
        [AsParameters] CatalogServices services,
        [Description("List of ids for catalog items to return")] int[] ids)
    {
        // Convert the array to a list to avoid issues with array parameters in EF Core queries
        var idsList = ids.ToList();
        
        // Use the List<int> instead of the array directly
        var items = await services.Context.CatalogItems
            .Where(item => idsList.Contains(item.Id))
            .ToListAsync();
            
        return TypedResults.Ok(items);
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Results<Ok<CatalogItem>, NotFound, BadRequest<ProblemDetails>>> GetItemById(
        HttpContext httpContext,
        [AsParameters] CatalogServices services,
        [Description("The catalog item id")] int id)
    {
        using var activity = OpenTelemetryCheckoutExtensions.CatalogActivitySource.StartActivity(
            OpenTelemetryCheckoutExtensions.CatalogOperations.GetItemById);
        
        activity?.SetTag("catalog.item.id", id);
        
        try
        {
            if (id <= 0)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Invalid ID");
                activity?.SetTag("catalog.error", "Invalid ID");
                return TypedResults.BadRequest<ProblemDetails>(new (){
                    Detail = "Id is not valid"
                });
            }

            var item = await services.Context.CatalogItems.Include(ci => ci.CatalogBrand).SingleOrDefaultAsync(ci => ci.Id == id);

            if (item == null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Item not found");
                activity?.SetTag("catalog.error", "Item not found");
                return TypedResults.NotFound();
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("catalog.item.name", item.Name);
            activity?.SetTag("catalog.item.brand", item.CatalogBrand?.Brand);
            activity?.SetTag("catalog.item.price", item.Price);
            
            return TypedResults.Ok(item);
        }
        catch (Exception ex)
        {
            activity?.SetExceptionTags(ex);
            throw;
        }
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Ok<PaginatedItems<CatalogItem>>> GetItemsByName(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("The name of the item to return")] string name)
    {
        return await GetAllItems(paginationRequest, services, name, null, null);
    }

    [ProducesResponseType<byte[]>(StatusCodes.Status200OK, "application/octet-stream",
        [ "image/png", "image/gif", "image/jpeg", "image/bmp", "image/tiff",
          "image/wmf", "image/jp2", "image/svg+xml", "image/webp" ])]
    public static async Task<Results<PhysicalFileHttpResult,NotFound>> GetItemPictureById(
        CatalogContext context,
        IWebHostEnvironment environment,
        [Description("The catalog item id")] int id)
    {
        var item = await context.CatalogItems.FindAsync(id);

        if (item is null)
        {
            return TypedResults.NotFound();
        }

        var path = GetFullPath(environment.ContentRootPath, item.PictureFileName);

        string imageFileExtension = Path.GetExtension(item.PictureFileName);
        string mimetype = GetImageMimeTypeFromImageFileExtension(imageFileExtension);
        DateTime lastModified = File.GetLastWriteTimeUtc(path);

        return TypedResults.PhysicalFile(path, mimetype, lastModified: lastModified);
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Results<Ok<PaginatedItems<CatalogItem>>, RedirectToRouteHttpResult>> GetItemsBySemanticRelevanceV1(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("The text string to use when search for related items in the catalog")] string text)

    {
        return await GetItemsBySemanticRelevance(paginationRequest, services, text);
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Results<Ok<PaginatedItems<CatalogItem>>, RedirectToRouteHttpResult>> GetItemsBySemanticRelevance(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("The text string to use when search for related items in the catalog"), Required, MinLength(1)] string text)
    {
        using var activity = OpenTelemetryCheckoutExtensions.CatalogActivitySource.StartActivity(
            OpenTelemetryCheckoutExtensions.CatalogOperations.GetItemsBySemanticRelevance);
        
        var pageSize = paginationRequest.PageSize;
        var pageIndex = paginationRequest.PageIndex;

        // Add relevant tags
        activity?.SetTag("catalog.page_size", pageSize);
        activity?.SetTag("catalog.page_index", pageIndex);
        activity?.SetTag("catalog.search_text", text);
        
        try
        {
            if (!services.CatalogAI.IsEnabled)
            {
                activity?.SetTag("catalog.ai_enabled", false);
                activity?.SetTag("catalog.fallback", "text_search");
                return await GetItemsByName(paginationRequest, services, text);
            }
            
            activity?.SetTag("catalog.ai_enabled", true);
            
            // Create child span for vector embedding generation
            using var embeddingActivity = OpenTelemetryCheckoutExtensions.CatalogActivitySource.StartActivity("GenerateEmbedding");
            embeddingActivity?.SetTag("catalog.search_text", text);
            
            // Create an embedding for the input search
            var startTime = Stopwatch.GetTimestamp();
            var vector = await services.CatalogAI.GetEmbeddingAsync(text);
            var embeddingDuration = Stopwatch.GetElapsedTime(startTime);
            
            embeddingActivity?.SetTag("catalog.embedding_duration_ms", embeddingDuration.TotalMilliseconds);
            embeddingActivity?.SetStatus(ActivityStatusCode.Ok);

            // Get the total number of items
            var totalItems = await services.Context.CatalogItems
                .LongCountAsync();

            // Create child span for vector search
            using var searchActivity = OpenTelemetryCheckoutExtensions.CatalogActivitySource.StartActivity("VectorSearch");
            searchActivity?.SetTag("catalog.search_text", text);
            
            startTime = Stopwatch.GetTimestamp();
            
            // Get the next page of items, ordered by most similar (smallest distance) to the input search
            List<CatalogItem> itemsOnPage;
            if (services.Logger.IsEnabled(LogLevel.Debug))
            {
                var itemsWithDistance = await services.Context.CatalogItems
                    .Select(c => new { Item = c, Distance = c.Embedding.CosineDistance(vector) })
                    .OrderBy(c => c.Distance)
                    .Skip(pageSize * pageIndex)
                    .Take(pageSize)
                    .ToListAsync();

                services.Logger.LogDebug("Results from {text}: {results}", text, string.Join(", ", itemsWithDistance.Select(i => $"{i.Item.Name} => {i.Distance}")));

                itemsOnPage = itemsWithDistance.Select(i => i.Item).ToList();
                
                // Record distances in the span
                searchActivity?.SetTag("catalog.top_distances", string.Join(",", itemsWithDistance.Take(3).Select(i => i.Distance.ToString("F3"))));
            }
            else
            {
                itemsOnPage = await services.Context.CatalogItems
                    .OrderBy(c => c.Embedding.CosineDistance(vector))
                    .Skip(pageSize * pageIndex)
                    .Take(pageSize)
                    .ToListAsync();
            }
            
            var searchDuration = Stopwatch.GetElapsedTime(startTime);
            searchActivity?.SetTag("catalog.search_duration_ms", searchDuration.TotalMilliseconds);
            searchActivity?.SetTag("catalog.items_found", itemsOnPage.Count);
            searchActivity?.SetStatus(ActivityStatusCode.Ok);
            
            // Add results to parent span
            activity?.SetTag("catalog.total_items", totalItems);
            activity?.SetTag("catalog.items_returned", itemsOnPage.Count);
            activity?.SetTag("catalog.total_duration_ms", embeddingDuration.TotalMilliseconds + searchDuration.TotalMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return TypedResults.Ok(new PaginatedItems<CatalogItem>(pageIndex, pageSize, totalItems, itemsOnPage));
        }
        catch (Exception ex)
        {
            activity?.SetExceptionTags(ex);
            throw;
        }
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Ok<PaginatedItems<CatalogItem>>> GetItemsByBrandAndTypeId(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("The type of items to return")] int typeId,
        [Description("The brand of items to return")] int? brandId)
    {
        return await GetAllItems(paginationRequest, services, null, typeId, brandId);
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Ok<PaginatedItems<CatalogItem>>> GetItemsByBrandId(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("The brand of items to return")] int? brandId)
    {
        return await GetAllItems(paginationRequest, services, null, null, brandId);
    }

    public static async Task<Results<Created, BadRequest<ProblemDetails>, NotFound<ProblemDetails>>> UpdateItemV1(
        HttpContext httpContext,
        [AsParameters] CatalogServices services,
        CatalogItem productToUpdate)
    {
        if (productToUpdate?.Id == null)
        {
            return TypedResults.BadRequest<ProblemDetails>(new (){
                Detail = "Item id must be provided in the request body."
            });
        }
        return await UpdateItem(httpContext, productToUpdate.Id, services, productToUpdate);
    }

    public static async Task<Results<Created, BadRequest<ProblemDetails>, NotFound<ProblemDetails>>> UpdateItem(
        HttpContext httpContext,
        [Description("The id of the catalog item to delete")] int id,
        [AsParameters] CatalogServices services,
        CatalogItem productToUpdate)
    {
        var catalogItem = await services.Context.CatalogItems.SingleOrDefaultAsync(i => i.Id == id);

        if (catalogItem == null)
        {
            return TypedResults.NotFound<ProblemDetails>(new (){
                Detail = $"Item with id {id} not found."
            });
        }

        // Update current product
        var catalogEntry = services.Context.Entry(catalogItem);
        catalogEntry.CurrentValues.SetValues(productToUpdate);

        catalogItem.Embedding = await services.CatalogAI.GetEmbeddingAsync(catalogItem);

        var priceEntry = catalogEntry.Property(i => i.Price);

        if (priceEntry.IsModified) // Save product's data and publish integration event through the Event Bus if price has changed
        {
            //Create Integration Event to be published through the Event Bus
            var priceChangedEvent = new ProductPriceChangedIntegrationEvent(catalogItem.Id, productToUpdate.Price, priceEntry.OriginalValue);

            // Achieving atomicity between original Catalog database operation and the IntegrationEventLog thanks to a local transaction
            await services.EventService.SaveEventAndCatalogContextChangesAsync(priceChangedEvent);

            // Publish through the Event Bus and mark the saved event as published
            await services.EventService.PublishThroughEventBusAsync(priceChangedEvent);
        }
        else // Just save the updated product because the Product's Price hasn't changed.
        {
            await services.Context.SaveChangesAsync();
        }
        return TypedResults.Created($"/api/catalog/items/{id}");
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Created> CreateItem(
        [AsParameters] CatalogServices services,
        CatalogItem product)
    {
        var item = new CatalogItem
        {
            Id = product.Id,
            CatalogBrandId = product.CatalogBrandId,
            CatalogTypeId = product.CatalogTypeId,
            Description = product.Description,
            Name = product.Name,
            PictureFileName = product.PictureFileName,
            Price = product.Price,
            AvailableStock = product.AvailableStock,
            RestockThreshold = product.RestockThreshold,
            MaxStockThreshold = product.MaxStockThreshold
        };
        item.Embedding = await services.CatalogAI.GetEmbeddingAsync(item);

        services.Context.CatalogItems.Add(item);
        await services.Context.SaveChangesAsync();

        return TypedResults.Created($"/api/catalog/items/{item.Id}");
    }

    public static async Task<Results<NoContent, NotFound>> DeleteItemById(
        [AsParameters] CatalogServices services,
        [Description("The id of the catalog item to delete")] int id)
    {
        var item = services.Context.CatalogItems.SingleOrDefault(x => x.Id == id);

        if (item is null)
        {
            return TypedResults.NotFound();
        }

        services.Context.CatalogItems.Remove(item);
        await services.Context.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    private static string GetImageMimeTypeFromImageFileExtension(string extension) => extension switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".bmp" => "image/bmp",
        ".tiff" => "image/tiff",
        ".wmf" => "image/wmf",
        ".jp2" => "image/jp2",
        ".svg" => "image/svg+xml",
        ".webp" => "image/webp",
        _ => "application/octet-stream",
    };

    public static string GetFullPath(string contentRootPath, string pictureFileName) =>
        Path.Combine(contentRootPath, "Pics", pictureFileName);
}
