using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.Data.SqlClient;

namespace JaneERP.Api.Data;

[SupportedOSPlatform("windows")]
public class ApiSyncRepository
{
    private readonly CompanyContext    _ctx;
    private readonly ApiShopifyClient _shopify;

    public ApiSyncRepository(CompanyContext ctx, ApiShopifyClient shopify)
    {
        _ctx     = ctx;
        _shopify = shopify;
    }

    // ── Stores ────────────────────────────────────────────────────────────────

    public List<SyncStoreInfo> GetStores()
    {
        using var db = new SqlConnection(_ctx.ConnectionString);
        var stores = db.Query<SyncStoreInfo>(@"
            SELECT StoreID, StoreName, StoreDomain, IsActive, LastSyncAt
            FROM   Stores
            WHERE  IsActive = 1
            ORDER  BY StoreName").ToList();

        foreach (var s in stores)
            s.HasCredentials = GetToken(s.StoreDomain) != null;

        return stores;
    }

    // ── Sync ──────────────────────────────────────────────────────────────────

    public async Task<SyncResult> SyncStoreAsync(int storeId)
    {
        using var db = new SqlConnection(_ctx.ConnectionString);
        db.Open();

        var store = db.QueryFirstOrDefault<SyncStoreInfo>(@"
            SELECT StoreID, StoreName, StoreDomain, IsActive, LastSyncAt
            FROM   Stores WHERE StoreID = @storeId AND IsActive = 1",
            new { storeId });

        if (store == null)
            throw new InvalidOperationException($"Store {storeId} not found or inactive.");

        var token = GetToken(store.StoreDomain);
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException(
                $"No access token found for {store.StoreDomain}. Configure credentials in the desktop app.");

        // Pull from last sync (or last 30 days on first run)
        var createdAtMin = store.LastSyncAt ?? DateTime.UtcNow.AddDays(-30);
        var orders = await _shopify.GetOrdersAsync(store.StoreDomain, token, createdAtMin);

        var result = new SyncResult { SyncedAt = DateTime.UtcNow };

        foreach (var order in orders)
        {
            try
            {
                var saved = ProcessShopifyOrder(db, order, storeId);
                if (saved) result.NewOrders++;
                else       result.SkippedOrders++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Order #{order.OrderNumber}: {ex.Message}");
            }
        }

        db.Execute("UPDATE Stores SET LastSyncAt = @now WHERE StoreID = @storeId",
            new { now = result.SyncedAt, storeId });

        return result;
    }

    // ── Order processing (ported from WinForms ShopifySyncService) ────────────

    private static bool ProcessShopifyOrder(SqlConnection db, ShopifyApiOrder order, int storeId)
    {
        using var tx = db.BeginTransaction();
        try
        {
            // Idempotency: skip if already imported
            var exists = db.ExecuteScalar<int>(
                "SELECT COUNT(1) FROM SalesOrders WHERE ShopifyOrderID = @id",
                new { id = order.Id }, tx);
            if (exists > 0) { tx.Rollback(); return false; }

            // Customer: find by email or create
            var email = string.IsNullOrWhiteSpace(order.ContactEmail)
                ? $"guest_{order.Id}@shopify.local"
                : order.ContactEmail.Trim().ToLowerInvariant();
            var customerName = order.Customer?.FullName ?? email;

            var customerId = db.ExecuteScalar<int?>(
                "SELECT CustomerID FROM Customers WHERE Email = @email",
                new { email }, tx);
            if (customerId == null)
            {
                customerId = db.QuerySingle<int>(@"
                    INSERT INTO Customers (Email, FullName) VALUES (@email, @fullName);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new { email, fullName = customerName }, tx);
            }

            // SalesOrder
            var totalPrice = decimal.TryParse(order.TotalPrice,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var tp) ? tp : 0m;

            var salesOrderId = db.QuerySingle<int>(@"
                INSERT INTO SalesOrders
                    (ShopifyOrderID, OrderNumber, CustomerID, StoreID,
                     OrderDate, TotalPrice, Currency, Status, InventoryAffected, OrderType)
                VALUES
                    (@ShopifyOrderID, @OrderNumber, @CustomerID, @StoreID,
                     @OrderDate, @TotalPrice, @Currency, 'Draft', 0, 'Shopify');
                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    ShopifyOrderID = order.Id,
                    OrderNumber    = order.OrderNumber,
                    CustomerID     = customerId,
                    StoreID        = storeId,
                    OrderDate      = order.CreatedAt,
                    TotalPrice     = totalPrice,
                    Currency       = order.Currency
                }, tx);

            // Line items
            foreach (var li in order.LineItems)
            {
                var sku = string.IsNullOrWhiteSpace(li.Sku)
                    ? $"SHOPIFY-{li.Id}"
                    : li.Sku.Trim();
                var unitPrice = decimal.TryParse(li.Price,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var up) ? up : 0m;

                // Product: find by SKU or auto-create
                var productId = db.ExecuteScalar<int?>(
                    "SELECT ProductID FROM Products WHERE SKU = @sku", new { sku }, tx);

                if (productId == null)
                {
                    productId = db.QuerySingle<int>(@"
                        INSERT INTO Products (SKU, ProductName, RetailPrice, IsActive, IsAutoCreated, IsVerified)
                        VALUES (@SKU, @ProductName, @RetailPrice, 1, 1, 0);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);",
                        new { SKU = sku, ProductName = li.Title ?? sku, RetailPrice = unitPrice }, tx);

                    // Every auto-created product gets a matching Part + BOM entry
                    var partId = db.ExecuteScalar<int?>(
                        "SELECT PartID FROM Parts WHERE PartNumber = @sku", new { sku }, tx);
                    if (partId == null)
                    {
                        partId = db.QuerySingle<int>(@"
                            INSERT INTO Parts (PartNumber, PartName, UnitCost, CurrentStock, IsActive, IsAutoCreated, IsVerified)
                            VALUES (@sku, @name, 0, 0, 1, 1, 0);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);",
                            new { sku, name = li.Title ?? sku }, tx);
                    }
                    db.Execute(@"
                        IF NOT EXISTS (SELECT 1 FROM ProductParts WHERE ProductID=@pid AND PartID=@partId)
                        INSERT INTO ProductParts (ProductID, PartID, Quantity) VALUES (@pid, @partId, 1);",
                        new { pid = productId, partId }, tx);
                }

                db.Execute(@"
                    INSERT INTO SalesOrderItems (SalesOrderID, ProductID, SKU, Title, Quantity, UnitPrice)
                    VALUES (@SalesOrderID, @ProductID, @SKU, @Title, @Quantity, @UnitPrice);",
                    new
                    {
                        SalesOrderID = salesOrderId,
                        ProductID    = productId,
                        SKU          = sku,
                        Title        = li.Title,
                        Quantity     = li.Quantity,
                        UnitPrice    = unitPrice
                    }, tx);
            }

            tx.Commit();
            return true;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // ── Token loading (mirrors WinForms SecureStore.GetSecret) ───────────────

    private static string? GetToken(string domain)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "JaneERP");
        var key  = "store_" + domain.Trim().ToLowerInvariant();
        var path = Path.Combine(folder, key + ".bin");
        if (!File.Exists(path)) return null;
        try
        {
            var encrypted = File.ReadAllBytes(path);
            var plain     = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch { return null; }
    }
}
