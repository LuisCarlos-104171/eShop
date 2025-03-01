using Microsoft.AspNetCore.Http.HttpResults;
using System.Diagnostics;
using eShop.ServiceDefaults;
using CardType = eShop.Ordering.API.Application.Queries.CardType;
using Order = eShop.Ordering.API.Application.Queries.Order;

public static class OrdersApi
{
    public static RouteGroupBuilder MapOrdersApiV1(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("api/orders").HasApiVersion(1.0);

        api.MapPut("/cancel", CancelOrderAsync);
        api.MapPut("/ship", ShipOrderAsync);
        api.MapGet("{orderId:int}", GetOrderAsync);
        api.MapGet("/", GetOrdersByUserAsync);
        api.MapGet("/cardtypes", GetCardTypesAsync);
        api.MapPost("/draft", CreateOrderDraftAsync);
        api.MapPost("/", CreateOrderAsync);

        return api;
    }

    public static async Task<Results<Ok, BadRequest<string>, ProblemHttpResult>> CancelOrderAsync(
        [FromHeader(Name = "x-requestid")] Guid requestId,
        CancelOrderCommand command,
        [AsParameters] OrderServices services)
    {
        using var activity = OpenTelemetryCheckoutExtensions.OrderingActivitySource.StartActivity("CancelOrder");
        activity?.SetTag("order.number", command.OrderNumber);
        activity?.SetTag("order.request_id", requestId);
        
        if (requestId == Guid.Empty)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Empty request ID");
            return TypedResults.BadRequest("Empty GUID is not valid for request ID");
        }

        var requestCancelOrder = new IdentifiedCommand<CancelOrderCommand, bool>(command, requestId);

        services.Logger.LogInformation(
            "Sending command: {CommandName} - {IdProperty}: {CommandId} ({@Command})",
            requestCancelOrder.GetGenericTypeName(),
            nameof(requestCancelOrder.Command.OrderNumber),
            requestCancelOrder.Command.OrderNumber,
            requestCancelOrder);

        var commandResult = await services.Mediator.Send(requestCancelOrder);

        if (!commandResult)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Cancel order failed to process");
            return TypedResults.Problem(detail: "Cancel order failed to process.", statusCode: 500);
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
        return TypedResults.Ok();
    }

    public static async Task<Results<Ok, BadRequest<string>, ProblemHttpResult>> ShipOrderAsync(
        [FromHeader(Name = "x-requestid")] Guid requestId,
        ShipOrderCommand command,
        [AsParameters] OrderServices services)
    {
        using var activity = OpenTelemetryCheckoutExtensions.OrderingActivitySource.StartActivity("ShipOrder");
        activity?.SetTag("order.number", command.OrderNumber);
        activity?.SetTag("order.request_id", requestId);
        
        if (requestId == Guid.Empty)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Empty request ID");
            return TypedResults.BadRequest("Empty GUID is not valid for request ID");
        }

        var requestShipOrder = new IdentifiedCommand<ShipOrderCommand, bool>(command, requestId);

        services.Logger.LogInformation(
            "Sending command: {CommandName} - {IdProperty}: {CommandId} ({@Command})",
            requestShipOrder.GetGenericTypeName(),
            nameof(requestShipOrder.Command.OrderNumber),
            requestShipOrder.Command.OrderNumber,
            requestShipOrder);

        var commandResult = await services.Mediator.Send(requestShipOrder);

        if (!commandResult)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Ship order failed to process");
            return TypedResults.Problem(detail: "Ship order failed to process.", statusCode: 500);
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
        return TypedResults.Ok();
    }

    public static async Task<Results<Ok<Order>, NotFound>> GetOrderAsync(int orderId, [AsParameters] OrderServices services)
    {
        using var activity = OpenTelemetryCheckoutExtensions.OrderingActivitySource.StartActivity("GetOrder");
        activity?.SetTag("order.id", orderId);
        
        try
        {
            var order = await services.Queries.GetOrderAsync(orderId);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return TypedResults.Ok(order);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return TypedResults.NotFound();
        }
    }

    public static async Task<Ok<IEnumerable<OrderSummary>>> GetOrdersByUserAsync([AsParameters] OrderServices services)
    {
        using var activity = OpenTelemetryCheckoutExtensions.OrderingActivitySource.StartActivity("GetOrdersByUser");
        
        var userId = services.IdentityService.GetUserIdentity();
        // Mask user ID in telemetry
        activity?.SetTag("user.id.masked", $"{userId[0]}***");
        
        var orders = await services.Queries.GetOrdersFromUserAsync(userId);
        activity?.SetTag("order.count", orders.Count());
        activity?.SetStatus(ActivityStatusCode.Ok);
        
        return TypedResults.Ok(orders);
    }

    public static async Task<Ok<IEnumerable<CardType>>> GetCardTypesAsync(IOrderQueries orderQueries)
    {
        using var activity = OpenTelemetryCheckoutExtensions.OrderingActivitySource.StartActivity("GetCardTypes");
        
        var cardTypes = await orderQueries.GetCardTypesAsync();
        activity?.SetTag("cardtype.count", cardTypes.Count());
        activity?.SetStatus(ActivityStatusCode.Ok);
        
        return TypedResults.Ok(cardTypes);
    }

    public static async Task<OrderDraftDTO> CreateOrderDraftAsync(CreateOrderDraftCommand command, [AsParameters] OrderServices services)
    {
        using var activity = OpenTelemetryCheckoutExtensions.OrderingActivitySource.StartActivity("CreateOrderDraft");
        // Mask buyer ID in telemetry
        activity?.SetTag("buyer.id.masked", $"{command.BuyerId[0]}***");
        activity?.SetTag("order.items", command.Items.Count());
        
        services.Logger.LogInformation(
            "Sending command: {CommandName} - {IdProperty}: {CommandId} ({@Command})",
            command.GetGenericTypeName(),
            nameof(command.BuyerId),
            command.BuyerId,
            command);

        var result = await services.Mediator.Send(command);
        activity?.SetStatus(ActivityStatusCode.Ok);
        
        return result;
    }

    public static async Task<Results<Ok, BadRequest<string>>> CreateOrderAsync(
        [FromHeader(Name = "x-requestid")] Guid requestId,
        CreateOrderRequest request,
        [AsParameters] OrderServices services)
    {
        using var activity = OpenTelemetryCheckoutExtensions.OrderingActivitySource.StartActivity(
            OpenTelemetryCheckoutExtensions.CheckoutOperations.SubmitOrder);
        
        activity?.SetTag("order.request_id", requestId);
        activity?.SetTag("order.items", request.Items.Count);
        // Only include safe information in telemetry
        activity?.SetTag("order.country", request.Country);
        
        // Mask the credit card number for logging
        var maskedCCNumber = request.CardNumber.Substring(request.CardNumber.Length - 4).PadLeft(request.CardNumber.Length, 'X');
        
        services.Logger.LogInformation(
            "Sending command: {CommandName} - {IdProperty}: {CommandId}",
            request.GetGenericTypeName(),
            nameof(request.UserId),
            request.UserId); // Don't log the request as it has CC number

        if (requestId == Guid.Empty)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "RequestId is missing");
            services.Logger.LogWarning("Invalid IntegrationEvent - RequestId is missing - {@IntegrationEvent}", request);
            return TypedResults.BadRequest("RequestId is missing.");
        }

        using (services.Logger.BeginScope(new List<KeyValuePair<string, object>> { new("IdentifiedCommandId", requestId) }))
        {
            var createOrderCommand = new CreateOrderCommand(request.Items, request.UserId, request.UserName, request.City, request.Street,
                request.State, request.Country, request.ZipCode,
                maskedCCNumber, request.CardHolderName, request.CardExpiration,
                request.CardSecurityNumber, request.CardTypeId);

            var requestCreateOrder = new IdentifiedCommand<CreateOrderCommand, bool>(createOrderCommand, requestId);

            services.Logger.LogInformation(
                "Sending command: {CommandName} - {IdProperty}: {CommandId} ({@Command})",
                requestCreateOrder.GetGenericTypeName(),
                nameof(requestCreateOrder.Id),
                requestCreateOrder.Id,
                requestCreateOrder);

            var result = await services.Mediator.Send(requestCreateOrder);

            if (result)
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
                services.Logger.LogInformation("CreateOrderCommand succeeded - RequestId: {RequestId}", requestId);
            }
            else
            {
                activity?.SetStatus(ActivityStatusCode.Error, "CreateOrderCommand failed");
                services.Logger.LogWarning("CreateOrderCommand failed - RequestId: {RequestId}", requestId);
            }

            return TypedResults.Ok();
        }
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
