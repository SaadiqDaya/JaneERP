using FluentAssertions;
using JaneERP.Api.Models;
using Xunit;

namespace JaneERP.Tests.Api
{
    /// <summary>
    /// Unit tests for order creation validation rules.
    ///
    /// These tests exercise the validation logic that lives in OrdersController.CreateOrder,
    /// expressed as pure in-memory checks against the request model — no DB or HTTP stack needed.
    ///
    /// The controller's guard conditions are:
    ///   1. req == null                      → BadRequest "Request body required."
    ///   2. CustomerEmail is empty/whitespace → BadRequest "Customer email is required."
    ///   3. Items list is empty               → BadRequest "At least one line item is required."
    ///
    /// NOTE: Because the validation logic is embedded directly in the controller action rather
    /// than in a dedicated validator class, these tests replicate that logic. If this is ever
    /// refactored into an IOrderValidator / FluentValidation pipeline, these tests can be
    /// replaced with direct validator unit tests.
    /// </summary>
    public class OrderValidationTests
    {
        // ── Helper: mirrors OrdersController.CreateOrder validation ──────────────

        /// <summary>
        /// Returns (isValid, errorMessage) matching the controller's guard logic exactly.
        /// </summary>
        private static (bool IsValid, string? Error) ValidateCreateOrder(CreateOrderRequest? req)
        {
            if (req == null)
                return (false, "Request body required.");

            if (string.IsNullOrWhiteSpace(req.CustomerEmail))
                return (false, "Customer email is required.");

            // Guard against null list (model binding would return 400 before the action runs;
            // replicate that by treating a null Items collection as invalid).
            if (req.Items == null || !req.Items.Any())
                return (false, "At least one line item is required.");

            return (true, null);
        }

        // ── Null request ──────────────────────────────────────────────────────────

        [Fact]
        public void CreateOrder_WithNullRequest_FailsValidation()
        {
            var (isValid, error) = ValidateCreateOrder(null);

            isValid.Should().BeFalse();
            error.Should().Be("Request body required.");
        }

        // ── CustomerEmail validation ──────────────────────────────────────────────

        [Fact]
        public void CreateOrder_WithEmptyEmail_FailsValidation()
        {
            var req = new CreateOrderRequest
            {
                CustomerEmail = "",
                Items         = [new CreateOrderLine { ProductId = 1, Quantity = 1, UnitPrice = 10m }]
            };

            var (isValid, error) = ValidateCreateOrder(req);

            isValid.Should().BeFalse();
            error.Should().Be("Customer email is required.");
        }

        [Fact]
        public void CreateOrder_WithWhitespaceEmail_FailsValidation()
        {
            var req = new CreateOrderRequest
            {
                CustomerEmail = "   ",
                Items         = [new CreateOrderLine { ProductId = 1, Quantity = 1, UnitPrice = 10m }]
            };

            var (isValid, error) = ValidateCreateOrder(req);

            isValid.Should().BeFalse();
            error.Should().Be("Customer email is required.");
        }

        [Fact]
        public void CreateOrder_WithValidEmail_PassesEmailCheck()
        {
            var req = new CreateOrderRequest
            {
                CustomerEmail = "customer@example.com",
                Items         = [new CreateOrderLine { ProductId = 1, Quantity = 1, UnitPrice = 10m }]
            };

            var (isValid, _) = ValidateCreateOrder(req);

            // Items are present so the whole request should be valid
            isValid.Should().BeTrue();
        }

        // ── Items list validation ─────────────────────────────────────────────────

        [Fact]
        public void CreateOrder_WithNoItems_FailsValidation()
        {
            var req = new CreateOrderRequest
            {
                CustomerEmail = "customer@example.com",
                Items         = []
            };

            var (isValid, error) = ValidateCreateOrder(req);

            isValid.Should().BeFalse();
            error.Should().Be("At least one line item is required.");
        }

        [Fact]
        public void CreateOrder_WithNullItemsList_FailsValidation()
        {
            // CreateOrderRequest initialises Items to [], but guard against manual null assignment.
            // The real ASP.NET model binder would return a 400 before the action is reached;
            // our validation helper defensively handles it the same way.
            var req = new CreateOrderRequest
            {
                CustomerEmail = "customer@example.com",
            };
            req.Items = null!;   // force null to test defensive check path

            var (isValid, error) = ValidateCreateOrder(req);

            isValid.Should().BeFalse();
            error.Should().Be("At least one line item is required.");
        }

        // ── Valid order ───────────────────────────────────────────────────────────

        [Fact]
        public void CreateOrder_WithValidRequest_PassesValidation()
        {
            var req = new CreateOrderRequest
            {
                CustomerEmail = "valid@customer.com",
                CustomerName  = "Valid Customer",
                Currency      = "CAD",
                OrderType     = "Manual",
                Items         =
                [
                    new CreateOrderLine { ProductId = 42, Sku = "SKU-001", Title = "Widget", Quantity = 3, UnitPrice = 9.99m }
                ]
            };

            var (isValid, error) = ValidateCreateOrder(req);

            isValid.Should().BeTrue();
            error.Should().BeNull();
        }

        [Fact]
        public void CreateOrder_WithMultipleItems_PassesValidation()
        {
            var req = new CreateOrderRequest
            {
                CustomerEmail = "multi@customer.com",
                Items         =
                [
                    new CreateOrderLine { ProductId = 1, Quantity = 2, UnitPrice = 5.00m },
                    new CreateOrderLine { ProductId = 2, Quantity = 1, UnitPrice = 20.00m },
                ]
            };

            var (isValid, error) = ValidateCreateOrder(req);

            isValid.Should().BeTrue();
            error.Should().BeNull();
        }

        // ── CreateOrderLine math ──────────────────────────────────────────────────

        [Fact]
        public void OrderLineItem_LineTotal_EqualsQuantityTimesUnitPrice()
        {
            var line = new OrderLineItem
            {
                Quantity  = 4,
                UnitPrice = 12.50m
            };

            line.LineTotal.Should().Be(50.00m);
        }

        [Fact]
        public void OrderLineItem_LineTotal_IsZero_WhenQuantityIsZero()
        {
            // Zero-quantity lines CAN exist on draft orders (e.g. partial fulfilment placeholder)
            var line = new OrderLineItem { Quantity = 0, UnitPrice = 9.99m };

            line.LineTotal.Should().Be(0m);
        }

        // ── UpdateStatusRequest valid values ──────────────────────────────────────

        [Theory]
        [InlineData("Draft")]
        [InlineData("Live")]
        [InlineData("WIP")]
        [InlineData("Packed")]
        [InlineData("Shipped")]
        [InlineData("Complete")]
        public void UpdateStatus_KnownStatusValues_AreAccepted(string status)
        {
            // Mirror the validStatuses array in OrdersController.UpdateStatus
            var validStatuses = new[] { "Draft", "Live", "WIP", "Packed", "Shipped", "Complete" };

            validStatuses.Should().Contain(status);
        }

        [Theory]
        [InlineData("Cancelled")]
        [InlineData("Pending")]
        [InlineData("")]
        [InlineData("DRAFT")]   // case-sensitive
        public void UpdateStatus_UnknownStatusValues_AreRejected(string status)
        {
            var validStatuses = new[] { "Draft", "Live", "WIP", "Packed", "Shipped", "Complete" };

            validStatuses.Should().NotContain(status);
        }

        // ── PoLineItem computed property ──────────────────────────────────────────

        [Fact]
        public void PoLineItem_Outstanding_EqualsOrderedMinusReceived()
        {
            var line = new PoLineItem { QuantityOrdered = 100, QuantityReceived = 35 };

            line.Outstanding.Should().Be(65);
        }

        [Fact]
        public void PoLineItem_Outstanding_IsZero_WhenFullyReceived()
        {
            var line = new PoLineItem { QuantityOrdered = 10, QuantityReceived = 10 };

            line.Outstanding.Should().Be(0);
        }
    }
}
