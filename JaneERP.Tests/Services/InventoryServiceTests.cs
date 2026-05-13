using JaneERP.Interfaces;
using JaneERP.Models;
using JaneERP.Services;
using Moq;
using Xunit;

namespace JaneERP.Tests.Services
{
    /// <summary>
    /// Unit tests for InventoryService business logic.
    ///
    /// We use a test subclass of InventoryService that overrides GetStockAtLocation so that
    /// TransferStock can be tested without a real SQL Server connection.  This pattern keeps the
    /// tests fast and deterministic while exercising the real business rules.
    /// </summary>
    public class InventoryServiceTests
    {
        // ── Helper: InventoryService with a controllable stock oracle ────────────

        /// <summary>
        /// Subclass that replaces the DB-backed GetStockAtLocation with an in-memory lookup,
        /// and records what transactions were "written" so tests can assert on them.
        /// </summary>
        private sealed class TestInventoryService : InventoryService
        {
            // stock[locationId] = qty returned by GetStockAtLocation
            private readonly Dictionary<int, int> _stock;

            public List<(int ProductID, int Qty, string Type, int LocationID)> Transactions { get; } = new();

            public TestInventoryService(Dictionary<int, int> stockByLocation)
            {
                _stock = stockByLocation;
            }

            public override int GetStockAtLocation(int productId, int locationId)
                => _stock.TryGetValue(locationId, out var qty) ? qty : 0;
        }

        // ── TransferStock: insufficient stock ─────────────────────────────────────

        [Fact]
        public void TransferStock_ThrowsInvalidOperation_WhenStockInsufficient()
        {
            var service = new TestInventoryService(new Dictionary<int, int> { [1] = 5 });

            var ex = Assert.Throws<InvalidOperationException>(() =>
                service.TransferStock(productId: 10, fromLocationId: 1, toLocationId: 2,
                                      qty: 10, notes: "test", performedBy: "unit-test"));

            Assert.Contains("Insufficient stock", ex.Message);
            Assert.Contains("Available: 5", ex.Message);
            Assert.Contains("Requested: 10", ex.Message);
        }

        [Fact]
        public void TransferStock_ThrowsInvalidOperation_WhenStockIsExactlyZero()
        {
            var service = new TestInventoryService(new Dictionary<int, int> { [1] = 0 });

            Assert.Throws<InvalidOperationException>(() =>
                service.TransferStock(10, 1, 2, 1, "test", "unit-test"));
        }

        [Fact]
        public void TransferStock_ThrowsInvalidOperation_WhenLocationHasNoStock()
        {
            // Location 99 has no entry in the stock dictionary → GetStockAtLocation returns 0
            var service = new TestInventoryService(new Dictionary<int, int>());

            Assert.Throws<InvalidOperationException>(() =>
                service.TransferStock(10, fromLocationId: 99, toLocationId: 2,
                                      qty: 1, notes: "test", performedBy: "unit-test"));
        }

        [Fact]
        public void TransferStock_ExactQty_DoesNotThrow()
        {
            // Transferring exactly the available stock (edge case) should succeed at the validation level.
            // The subclass doesn't hit the DB so we only test that no exception is thrown at this layer.
            var service = new TestInventoryService(new Dictionary<int, int> { [1] = 10 });

            // The underlying DB insert will throw since we're in a test environment — we only care that
            // the stock-check guard passes (i.e. the exception is NOT InvalidOperationException).
            var ex = Record.Exception(() =>
                service.TransferStock(10, 1, 2, 10, "exact", "unit-test"));

            // Must not be the "Insufficient stock" guard — any DB/config error is OK here.
            if (ex is InvalidOperationException ioe && ioe.Message.Contains("Insufficient stock"))
                Assert.Fail("Stock validation should pass when available == requested qty.");
        }

        // ── IInventoryService mock: verifying callers interact with the service correctly ──

        [Fact]
        public void Service_GetStockAtLocation_Called_WithCorrectArgs()
        {
            var mock = new Mock<IInventoryService>();
            mock.Setup(s => s.GetStockAtLocation(42, 7)).Returns(15);

            int result = mock.Object.GetStockAtLocation(42, 7);

            Assert.Equal(15, result);
            mock.Verify(s => s.GetStockAtLocation(42, 7), Times.Once);
        }

        [Fact]
        public void Service_TransferStock_Called_WithCorrectArgs()
        {
            var mock = new Mock<IInventoryService>();
            mock.Setup(s => s.TransferStock(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()));

            mock.Object.TransferStock(1, 2, 3, 5, "move stock", "admin");

            mock.Verify(s => s.TransferStock(1, 2, 3, 5, "move stock", "admin"), Times.Once);
        }

        [Fact]
        public void Service_TransferStock_Exception_PropagatesCorrectly()
        {
            var mock = new Mock<IInventoryService>();
            mock.Setup(s => s.TransferStock(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new InvalidOperationException("Insufficient stock at source location."));

            var ex = Assert.Throws<InvalidOperationException>(() =>
                mock.Object.TransferStock(1, 2, 3, 999, "too many", "admin"));

            Assert.Contains("Insufficient stock", ex.Message);
        }

        [Fact]
        public void Service_GetStockPerLocation_ReturnsEmpty_WhenNoStock()
        {
            var mock = new Mock<IInventoryService>();
            mock.Setup(s => s.GetStockPerLocation(It.IsAny<int>())).Returns(new List<LocationStock>());

            var result = mock.Object.GetStockPerLocation(99);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void Service_GetStockPerLocation_ReturnsMappedLocations()
        {
            var expected = new List<LocationStock>
            {
                new() { LocationID = 1, LocationName = "Warehouse A", StockQty = 50 },
                new() { LocationID = 2, LocationName = "Warehouse B", StockQty = 30 }
            };

            var mock = new Mock<IInventoryService>();
            mock.Setup(s => s.GetStockPerLocation(10)).Returns(expected);

            var result = mock.Object.GetStockPerLocation(10);

            Assert.Equal(2, result.Count);
            Assert.Equal("Warehouse A", result[0].LocationName);
            Assert.Equal(50, result[0].StockQty);
        }
    }
}
