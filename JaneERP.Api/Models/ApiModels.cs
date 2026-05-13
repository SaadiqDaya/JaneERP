namespace JaneERP.Api.Models;

// ── Auth ─────────────────────────────────────────────────────────────────────

public record LoginRequest(string Company, string Username, string Password);

public record LoginResponse(string Token, string Username, string Role, string Company, int ExpiryHours);

// ── Dashboard ─────────────────────────────────────────────────────────────────

public class DashboardResponse
{
    public int     TotalProducts    { get; set; }
    public int     LowStockItems    { get; set; }
    public decimal SalesTotal       { get; set; }
    public int     SalesDays        { get; set; }
    public int     OrdersToPack     { get; set; }
    public int     ItemsToReceive   { get; set; }
    public int     OverduePOs       { get; set; }
    public int     OverdueCycleCount{ get; set; }

    public List<PoSummary>  PosToReceive { get; set; } = [];
    public List<SoSummary>  SosToPack    { get; set; } = [];
}

public class PoSummary
{
    public int      POID             { get; set; }
    public string   PONumber         { get; set; } = "";
    public string   SupplierName     { get; set; } = "";
    public DateTime? ExpectedDate    { get; set; }
    public string   Status           { get; set; } = "";
    public int      ItemsOutstanding { get; set; }
}

public class SoSummary
{
    public int      SalesOrderID  { get; set; }
    public int      OrderNumber   { get; set; }
    public string   CustomerName  { get; set; } = "";
    public decimal  TotalPrice    { get; set; }
    public string   Status        { get; set; } = "";
    public DateTime OrderDate     { get; set; }
    public int      LineCount     { get; set; }
    public int      TotalQty      { get; set; }
}

// ── Inventory ─────────────────────────────────────────────────────────────────

public class ProductSearchResult
{
    public int     ProductID   { get; set; }
    public string  SKU         { get; set; } = "";
    public string  ProductName { get; set; } = "";
    public int     CurrentStock{ get; set; }
    public decimal RetailPrice { get; set; }
    public int     ReorderPoint{ get; set; }
    public bool    IsLowStock  { get; set; }
}

public class StockByLocation
{
    public int    LocationID   { get; set; }
    public string LocationName { get; set; } = "";
    public int    Stock        { get; set; }
}

// ── Orders ────────────────────────────────────────────────────────────────────

public class OrderListItem
{
    public int      SalesOrderID  { get; set; }
    public int      OrderNumber   { get; set; }
    public string   CustomerName  { get; set; } = "";
    public string   CustomerEmail { get; set; } = "";
    public DateTime OrderDate     { get; set; }
    public decimal  TotalPrice    { get; set; }
    public string?  Currency      { get; set; }
    public string   Status        { get; set; } = "";
    public string   OrderType     { get; set; } = "";
    public bool     IsPaid        { get; set; }
}

public class OrderDetail : OrderListItem
{
    public string?  Notes        { get; set; }
    public decimal  ShippingCost { get; set; }
    public DateTime? PaidAt      { get; set; }
    public List<OrderLineItem> Items { get; set; } = [];
}

public class OrderLineItem
{
    public int     SalesOrderItemID { get; set; }
    public int     ProductID        { get; set; }
    public string  SKU              { get; set; } = "";
    public string  Title            { get; set; } = "";
    public int     Quantity         { get; set; }
    public decimal UnitPrice        { get; set; }
    public decimal LineTotal        => Quantity * UnitPrice;
}

public class CreateOrderRequest
{
    public string   CustomerEmail  { get; set; } = "";
    public string   CustomerName   { get; set; } = "";
    public DateTime OrderDate      { get; set; } = DateTime.Now;
    public string   Currency       { get; set; } = "CAD";
    public string   OrderType      { get; set; } = "Manual";
    public string?  Notes          { get; set; }
    public decimal  ShippingCost   { get; set; }
    public decimal  DiscountAmount { get; set; }
    public List<CreateOrderLine> Items { get; set; } = [];
}

public class CreateOrderLine
{
    public int     ProductId { get; set; }
    public string  Sku       { get; set; } = "";
    public string  Title     { get; set; } = "";
    public int     Quantity  { get; set; }
    public decimal UnitPrice { get; set; }
}

public record UpdateStatusRequest(string Status);

// ── Purchase Orders ───────────────────────────────────────────────────────────

public class PurchaseOrderListItem
{
    public int       POID         { get; set; }
    public string    PONumber     { get; set; } = "";
    public string    SupplierName { get; set; } = "";
    public string    Status       { get; set; } = "";
    public DateTime  OrderDate    { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public decimal   TotalCost    { get; set; }
    public string    CreatedBy    { get; set; } = "";
    public bool      IsOverdue    { get; set; }
}

public class PurchaseOrderDetail : PurchaseOrderListItem
{
    public string?  Notes        { get; set; }
    public decimal  ShippingCost { get; set; }
    public List<PoLineItem> Items { get; set; } = [];
}

public class PoLineItem
{
    public int     POItemID          { get; set; }
    public int?    PartID            { get; set; }
    public int?    ProductID         { get; set; }
    public string  SKU               { get; set; } = "";
    public string  ItemName          { get; set; } = "";
    public int     QuantityOrdered   { get; set; }
    public int     QuantityReceived  { get; set; }
    public decimal UnitCost          { get; set; }
    public int     Outstanding       => QuantityOrdered - QuantityReceived;
}

public class ReceiveItemsRequest
{
    public List<ReceiveItem> Items { get; set; } = [];
}

public class ReceiveItem
{
    public int PoItemId     { get; set; }
    public int QtyReceived  { get; set; }
}

// ── Cycle Count ───────────────────────────────────────────────────────────────

public class CycleCountEntry
{
    public int      ProductID      { get; set; }
    public string   SKU            { get; set; } = "";
    public string   ProductName    { get; set; } = "";
    public int      SystemQty      { get; set; }
    public int?     LocationID     { get; set; }
    public string?  LocationName   { get; set; }
    public DateTime? LastVerifiedAt{ get; set; }
    public string?  LastVerifiedBy { get; set; }
}

public class VerifyRequest
{
    public int    ProductId  { get; set; }
    public int    LocationId { get; set; }
    public int    SystemQty  { get; set; }
    public int    ActualQty  { get; set; }
}

// ── Picking ───────────────────────────────────────────────────────────────────

public class PickListItem
{
    public int     SalesOrderItemID { get; set; }
    public int     ProductID        { get; set; }
    public string  SKU              { get; set; } = "";
    public string  Title            { get; set; } = "";
    public int     QuantityNeeded   { get; set; }
    public int     TotalStock       { get; set; }
    public string? PrimaryLocation  { get; set; }
}

// ── Locations ─────────────────────────────────────────────────────────────────

public class LocationItem
{
    public int    LocationID   { get; set; }
    public string LocationName { get; set; } = "";
}

// ── Stock adjust / history ────────────────────────────────────────────────────

public class StockAdjustRequest
{
    public int    Qty    { get; set; }
    public string Reason { get; set; } = "";
}

public class StockTransaction
{
    public int      TransactionID   { get; set; }
    public int      QuantityChange  { get; set; }
    public string   TransactionType { get; set; } = "";
    public string?  Notes           { get; set; }
    public DateTime TransactionDate { get; set; }
    public string?  LocationName    { get; set; }
}

// ── Customers ─────────────────────────────────────────────────────────────────

public class CustomerListItem
{
    public int     CustomerID { get; set; }
    public string  FullName   { get; set; } = "";
    public string  Email      { get; set; } = "";
    public int     OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
}

public class CustomerDetail : CustomerListItem
{
    public List<OrderListItem> RecentOrders { get; set; } = [];
}

// ── Work orders ───────────────────────────────────────────────────────────────

public class WorkOrderItem
{
    public int      WorkOrderID  { get; set; }
    public int      MOID         { get; set; }
    public string   MONumber     { get; set; } = "";
    public string   ProductName  { get; set; } = "";
    public string   SKU          { get; set; } = "";
    public int      Quantity     { get; set; }
    public int      CompletedQty { get; set; }
    public string   Status       { get; set; } = "";
    public string?  AssignedTo   { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string?  Notes        { get; set; }
}

public record UpdateNotesRequest(string? Notes);
