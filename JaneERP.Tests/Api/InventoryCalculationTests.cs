using FluentAssertions;
using JaneERP.Api.Models;
using Xunit;

namespace JaneERP.Tests.Api
{
    /// <summary>
    /// Unit tests for inventory-related calculations.
    ///
    /// The core inventory logic in JaneERP is a running-sum ledger:
    ///   CurrentStock = SUM(InventoryTransactions.QuantityChange) for a given ProductID.
    ///
    /// The repository queries are tightly coupled to SQL Server (Dapper + raw SQL), so they
    /// cannot be unit tested without a DB connection. Instead, these tests:
    ///   1. Verify the ledger arithmetic model in isolation (the same logic the queries implement).
    ///   2. Test the low-stock detection rules expressed on the model layer.
    ///   3. Test the StockAdjustRequest / InventoryController validation guards.
    ///
    /// To make the repository layer unit-testable in the future, ApiProductRepository and
    /// ApiOrderRepository would need to be extracted behind IApiProductRepository /
    /// IApiOrderRepository interfaces so Moq can substitute them without SQL Server.
    /// </summary>
    public class InventoryCalculationTests
    {
        // ── Ledger arithmetic ─────────────────────────────────────────────────────

        /// <summary>Simulate SUM(QuantityChange) for a product.</summary>
        private static int CurrentStock(IEnumerable<int> changes) => changes.Sum();

        [Fact]
        public void Stock_AfterReceivingInventory_IncreasesCorrectly()
        {
            // Opening balance 0, receive 50 units
            var transactions = new[] { 50 };

            CurrentStock(transactions).Should().Be(50);
        }

        [Fact]
        public void Stock_AfterFulfillingOrder_DecreasesCorrectly()
        {
            // Receive 100, sell 40 → 60 remaining
            var transactions = new[] { 100, -40 };

            CurrentStock(transactions).Should().Be(60);
        }

        [Fact]
        public void Stock_MultipleReceiptsAndSales_LedgerIsCorrect()
        {
            var transactions = new[]
            {
                200,   // receive PO 1
                100,   // receive PO 2
                -50,   // order 1 shipped
                -30,   // order 2 shipped
                25,    // return / adjustment
            };

            CurrentStock(transactions).Should().Be(245);
        }

        [Fact]
        public void Stock_AfterFulfillingEntireStock_IsZero()
        {
            var transactions = new[] { 10, -10 };

            CurrentStock(transactions).Should().Be(0);
        }

        [Fact]
        public void Stock_NegativeStockIsArithmeticallyPossible_ButShouldBeBlockedByBusiness()
        {
            // The ledger math can produce negative stock if validation is bypassed.
            // The controller guard (UpdateStatus → "Packed" check) prevents this in practice.
            var transactions = new[] { 5, -10 };

            var result = CurrentStock(transactions);

            // Negative stock CAN result from unguarded adjustments — document and assert.
            result.Should().Be(-5);
            result.Should().BeLessThan(0, "negative stock indicates a guard was bypassed");
        }

        // ── Low stock detection ───────────────────────────────────────────────────

        [Fact]
        public void LowStock_IsTrue_WhenStockAtOrBelowReorderPoint()
        {
            var product = new ProductSearchResult
            {
                CurrentStock = 5,
                ReorderPoint = 10
            };

            product.IsLowStock.Should().BeFalse("model property is set by the query, not computed");

            // Simulate what the SQL query sets: stock <= reorderPoint AND reorderPoint > 0
            var queryResult = (product.CurrentStock <= product.ReorderPoint) && (product.ReorderPoint > 0);
            queryResult.Should().BeTrue();
        }

        [Fact]
        public void LowStock_IsFalse_WhenStockAboveReorderPoint()
        {
            var queryResult = IsLowStockByRule(currentStock: 50, reorderPoint: 10);
            queryResult.Should().BeFalse();
        }

        [Fact]
        public void LowStock_IsFalse_WhenReorderPointIsZero()
        {
            // ReorderPoint = 0 means "no reorder monitoring" — never flagged as low stock
            var queryResult = IsLowStockByRule(currentStock: 0, reorderPoint: 0);
            queryResult.Should().BeFalse("reorder point of 0 disables the low-stock flag");
        }

        [Fact]
        public void LowStock_IsTrue_WhenStockIsExactlyAtReorderPoint()
        {
            // Edge case: stock == reorderPoint → still flagged (≤ not <)
            var queryResult = IsLowStockByRule(currentStock: 10, reorderPoint: 10);
            queryResult.Should().BeTrue("stock at the reorder point boundary should trigger reorder");
        }

        [Fact]
        public void LowStock_IsTrue_WhenStockIsZeroAndReorderPointPositive()
        {
            var queryResult = IsLowStockByRule(currentStock: 0, reorderPoint: 5);
            queryResult.Should().BeTrue();
        }

        /// <summary>
        /// Mirrors the CASE expression in ApiProductRepository.Search / GetLowStock.
        /// CASE WHEN stock <= reorderPoint AND reorderPoint > 0 THEN 1 ELSE 0 END
        /// </summary>
        private static bool IsLowStockByRule(int currentStock, int reorderPoint)
            => currentStock <= reorderPoint && reorderPoint > 0;

        // ── StockAdjustRequest validation (mirrors InventoryController.Adjust) ───

        [Fact]
        public void StockAdjust_ZeroQuantity_FailsValidation()
        {
            var req = new StockAdjustRequest { Qty = 0, Reason = "cycle count" };
            var (isValid, error) = ValidateAdjustRequest(req);

            isValid.Should().BeFalse();
            error.Should().Be("Quantity cannot be zero.");
        }

        [Fact]
        public void StockAdjust_NullReason_FailsValidation()
        {
            var req = new StockAdjustRequest { Qty = 5, Reason = "" };
            var (isValid, error) = ValidateAdjustRequest(req);

            isValid.Should().BeFalse();
            error.Should().Be("Reason is required.");
        }

        [Fact]
        public void StockAdjust_NullRequest_FailsValidation()
        {
            var (isValid, error) = ValidateAdjustRequest(null);
            isValid.Should().BeFalse();
            error.Should().Be("Request body required.");
        }

        [Fact]
        public void StockAdjust_PositiveQtyWithReason_PassesValidation()
        {
            var req = new StockAdjustRequest { Qty = 10, Reason = "received extra units" };
            var (isValid, error) = ValidateAdjustRequest(req);

            isValid.Should().BeTrue();
            error.Should().BeNull();
        }

        [Fact]
        public void StockAdjust_NegativeQtyWithReason_PassesValidation()
        {
            // Negative adjustments (write-offs, damage) are valid — the sign encodes direction
            var req = new StockAdjustRequest { Qty = -5, Reason = "damaged in transit" };
            var (isValid, error) = ValidateAdjustRequest(req);

            isValid.Should().BeTrue();
            error.Should().BeNull();
        }

        /// <summary>
        /// Mirrors the validation guards in InventoryController.Adjust.
        /// </summary>
        private static (bool IsValid, string? Error) ValidateAdjustRequest(StockAdjustRequest? req)
        {
            if (req == null) return (false, "Request body required.");
            if (req.Qty == 0) return (false, "Quantity cannot be zero.");
            if (string.IsNullOrWhiteSpace(req.Reason)) return (false, "Reason is required.");
            return (true, null);
        }

        // ── ReceiveItem / PO outstanding calculation ──────────────────────────────

        [Fact]
        public void PoLineItem_Outstanding_DecreasesAsItemsAreReceived()
        {
            var line = new PoLineItem { QuantityOrdered = 50, QuantityReceived = 0 };
            line.Outstanding.Should().Be(50);

            line.QuantityReceived = 20;
            line.Outstanding.Should().Be(30);

            line.QuantityReceived = 50;
            line.Outstanding.Should().Be(0);
        }

        [Fact]
        public void ReceiveItems_TotalReceivedSumIsCorrect()
        {
            // Simulate receiving partial quantities across multiple PO lines
            var items = new List<ReceiveItem>
            {
                new ReceiveItem { PoItemId = 1, QtyReceived = 10 },
                new ReceiveItem { PoItemId = 2, QtyReceived = 25 },
                new ReceiveItem { PoItemId = 3, QtyReceived = 5  },
            };

            items.Sum(i => i.QtyReceived).Should().Be(40);
        }

        // ── CookIngredientDto gram conversion ─────────────────────────────────────

        [Fact]
        public void CookIngredient_TotalRequiredGrams_ComputedFromDensity()
        {
            // TotalRequiredGrams = TotalRequired * Density (rounded to 2dp)
            var ingredient = new CookIngredientDto
            {
                TotalRequired = 500m,   // ml
                Density       = 1.06m   // g/ml (typical for PG/VG blends)
            };

            ingredient.TotalRequiredGrams.Should().Be(530.00m);
        }

        [Fact]
        public void CookIngredient_TotalRequiredGrams_IsNull_WhenNoDensity()
        {
            var ingredient = new CookIngredientDto
            {
                TotalRequired = 100m,
                Density       = null
            };

            ingredient.TotalRequiredGrams.Should().BeNull("count items have no density");
        }

        [Fact]
        public void CookStep_RequiredGrams_ComputedFromDensity()
        {
            var step = new CookStepDto
            {
                RequiredQty = 250m,
                Density     = 0.789m   // ethanol density
            };

            // Math.Round(250 * 0.789, 2) = Math.Round(197.25, 2) = 197.25
            step.RequiredGrams.Should().Be(197.25m);
        }
    }
}
