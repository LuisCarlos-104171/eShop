﻿using System.Diagnostics;
using System.Net.Http.Json;
using System.Web;
using eShop.WebAppComponents.Catalog;
using eShop.ServiceDefaults;

namespace eShop.WebAppComponents.Services;

public class CatalogService(HttpClient httpClient) : ICatalogService
{
    private readonly string remoteServiceBaseUrl = "api/catalog/";

    public Task<CatalogItem?> GetCatalogItem(int id)
    {
        var uri = $"{remoteServiceBaseUrl}items/{id}";
        return httpClient.GetFromJsonAsync<CatalogItem>(uri);
    }

    public async Task<CatalogResult> GetCatalogItems(int pageIndex, int pageSize, int? brand, int? type)
    {
        using var activity = OpenTelemetryCheckoutExtensions.CatalogActivitySource.StartActivity("GetCatalogItems");
        activity?.SetTag("catalog.page_index", pageIndex);
        activity?.SetTag("catalog.page_size", pageSize);
        activity?.SetTag("catalog.filter.brand", brand);
        activity?.SetTag("catalog.filter.type", type);
        
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var uri = GetAllCatalogItemsUri(remoteServiceBaseUrl, pageIndex, pageSize, brand, type);
            var result = await httpClient.GetFromJsonAsync<CatalogResult>(uri);
            stopwatch.Stop();
            
            activity?.SetTag("catalog.items_count", result!.Data.Count);
            activity?.SetTag("catalog.total_items", result!.Count);
            activity?.SetTag("catalog.duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            return result!;
        }
        catch (Exception ex)
        {
            activity?.SetExceptionTags(ex);
            throw;
        }
    }

    public async Task<List<CatalogItem>> GetCatalogItems(IEnumerable<int> ids)
    {
        var uri = $"{remoteServiceBaseUrl}items/by?ids={string.Join("&ids=", ids)}";
        var result = await httpClient.GetFromJsonAsync<List<CatalogItem>>(uri);
        return result!;
    }

    public async Task<CatalogResult> GetCatalogItemsWithSemanticRelevance(int page, int take, string text)
    {
        using var activity = OpenTelemetryCheckoutExtensions.CatalogActivitySource.StartActivity("GetCatalogItemsWithSemanticRelevance");
        activity?.SetTag("catalog.page_index", page);
        activity?.SetTag("catalog.page_size", take);
        activity?.SetTag("catalog.search_text", text);
        
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var url = $"{remoteServiceBaseUrl}items/withsemanticrelevance?text={HttpUtility.UrlEncode(text)}&pageIndex={page}&pageSize={take}";
            var result = await httpClient.GetFromJsonAsync<CatalogResult>(url);
            stopwatch.Stop();
            
            activity?.SetTag("catalog.items_count", result!.Data.Count);
            activity?.SetTag("catalog.total_items", result!.Count);
            activity?.SetTag("catalog.search_duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            return result!;
        }
        catch (Exception ex)
        {
            activity?.SetExceptionTags(ex);
            throw;
        }
    }

    public async Task<IEnumerable<CatalogBrand>> GetBrands()
    {
        var uri = $"{remoteServiceBaseUrl}catalogBrands";
        var result = await httpClient.GetFromJsonAsync<CatalogBrand[]>(uri);
        return result!;
    }

    public async Task<IEnumerable<CatalogItemType>> GetTypes()
    {
        var uri = $"{remoteServiceBaseUrl}catalogTypes";
        var result = await httpClient.GetFromJsonAsync<CatalogItemType[]>(uri);
        return result!;
    }

    private static string GetAllCatalogItemsUri(string baseUri, int pageIndex, int pageSize, int? brand, int? type)
    {
        string filterQs = string.Empty;

        if (type.HasValue)
        {
            filterQs += $"type={type.Value}&";
        }
        if (brand.HasValue)
        {
            filterQs += $"brand={brand.Value}&";
        }

        return $"{baseUri}items?{filterQs}pageIndex={pageIndex}&pageSize={pageSize}";
    }
}
