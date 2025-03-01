using System.Diagnostics;
using eShop.ServiceDefaults;

namespace eShop.Ordering.API.Application.Commands;

using eShop.Ordering.Domain.AggregatesModel.OrderAggregate;

// Regular CommandHandler
public class CreateOrderCommandHandler
    : IRequestHandler<CreateOrderCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IIdentityService _identityService;
    private readonly IMediator _mediator;
    private readonly IOrderingIntegrationEventService _orderingIntegrationEventService;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    // Using DI to inject infrastructure persistence Repositories
    public CreateOrderCommandHandler(IMediator mediator,
        IOrderingIntegrationEventService orderingIntegrationEventService,
        IOrderRepository orderRepository,
        IIdentityService identityService,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _orderingIntegrationEventService = orderingIntegrationEventService ?? throw new ArgumentNullException(nameof(orderingIntegrationEventService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(CreateOrderCommand message, CancellationToken cancellationToken)
    {
        // Create activity for order creation
        using var activity = OpenTelemetryCheckoutExtensions.OrderingActivitySource.StartActivity(
            OpenTelemetryCheckoutExtensions.CheckoutOperations.SubmitOrder);
        
        try
        {
            // Add relevant information to the activity context
            activity?.SetTag("order.items_count", message.OrderItems.Count());
            activity?.SetTag("order.user.id", message.UserId);
            activity?.SetTag("order.shipping.country", message.Country);
            
            // Add Integration event to clean the basket
            using (var basketCleanupActivity = OpenTelemetryCheckoutExtensions.BasketActivitySource.StartActivity("CleanBasket"))
            {
                basketCleanupActivity?.SetTag("user.id", message.UserId);
                
                var orderStartedIntegrationEvent = new OrderStartedIntegrationEvent(message.UserId);
                await _orderingIntegrationEventService.AddAndSaveEventAsync(orderStartedIntegrationEvent);
                
                // Set status for this activity - use explicit status code
                if (basketCleanupActivity != null)
                {
                    basketCleanupActivity.SetStatus(ActivityStatusCode.Ok);
                }
            }

            // Add/Update the Buyer AggregateRoot
            // DDD patterns comment: Add child entities and value-objects through the Order Aggregate-Root
            // methods and constructor so validations, invariants and business logic 
            // make sure that consistency is preserved across the whole aggregate
            var address = new Address(message.Street, message.City, message.State, message.Country, message.ZipCode);
            var order = new Order(message.UserId, message.UserName, address, message.CardTypeId, message.CardNumber, message.CardSecurityNumber, message.CardHolderName, message.CardExpiration);

            foreach (var item in message.OrderItems)
            {
                order.AddOrderItem(item.ProductId, item.ProductName, item.UnitPrice, item.Discount, item.PictureUrl, item.Units);
            }

            _logger.LogInformation("Creating Order - Order: {@Order}", order);

            _orderRepository.Add(order);

            var result = await _orderRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
            
            if (result)
            {
                activity?.SetTag("order.id", order.Id);
                // Set status for this activity - use explicit status code
                if (activity != null)
                {
                    activity.SetStatus(ActivityStatusCode.Ok);
                }
            }
            else
            {
                // Set status for this activity - use explicit status code and description
                if (activity != null)
                {
                    activity.SetStatus(ActivityStatusCode.Error);
                    activity.SetTag("error", "Failed to save order");
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            // Set status for this activity - use explicit status code and description
            if (activity != null)
            {
                activity.SetStatus(ActivityStatusCode.Error);
                activity.SetTag("error", ex.Message);
            }
            
            _logger.LogError(ex, "Error creating order");
            throw;
        }
    }
}


// Use for Idempotency in Command process
public class CreateOrderIdentifiedCommandHandler : IdentifiedCommandHandler<CreateOrderCommand, bool>
{
    public CreateOrderIdentifiedCommandHandler(
        IMediator mediator,
        IRequestManager requestManager,
        ILogger<IdentifiedCommandHandler<CreateOrderCommand, bool>> logger)
        : base(mediator, requestManager, logger)
    {
    }

    protected override bool CreateResultForDuplicateRequest()
    {
        return true; // Ignore duplicate requests for creating order.
    }
}
