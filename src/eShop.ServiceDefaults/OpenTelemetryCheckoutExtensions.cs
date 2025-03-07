using System.Diagnostics;

namespace eShop.ServiceDefaults;

public static class OpenTelemetryCheckoutExtensions
{
    public static readonly ActivitySource BasketActivitySource = new("eShop.Basket", "1.0.0");
    public static readonly ActivitySource OrderingActivitySource = new("eShop.Ordering", "1.0.0");
    public static readonly ActivitySource CheckoutActivitySource = new("eShop.Checkout", "1.0.0");
    public static readonly ActivitySource PaymentActivitySource = new("eShop.Payment", "1.0.0");
    public static readonly ActivitySource CatalogActivitySource = new("eShop.Catalog", "1.0.0");

    public static class CheckoutOperations
    {
        public const string InitiateCheckout = "InitiateCheckout";
        public const string ValidateCart = "ValidateCart";
        public const string SubmitOrder = "SubmitOrder";
        public const string ProcessPayment = "ProcessPayment";
        public const string ConfirmStock = "ConfirmStock";
        public const string CompleteOrder = "CompleteOrder";
    }
    
    public static class CatalogOperations
    {
        public const string GetItems = "GetItems";
        public const string GetItemById = "GetItemById";
        public const string GetItemsByIds = "GetItemsByIds";
        public const string GetItemsByName = "GetItemsByName";
        public const string GetItemPicture = "GetItemPicture";
        public const string GetItemsBySemanticRelevance = "GetItemsBySemanticRelevance";
        public const string UpdateItem = "UpdateItem";
        public const string CreateItem = "CreateItem";
        public const string DeleteItem = "DeleteItem";
    }
}
