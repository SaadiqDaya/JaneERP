using System;
using System.Collections.Generic;

namespace JaneERP.Models
{
    public class OrderDetails
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public int OrderNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal TotalPrice { get; set; }
        public string? Currency { get; set; }
        public string? ContactEmail { get; set; }
        public string? CustomerName { get; set; }
        public string? ShippingAddress { get; set; }
        public List<LineItem> LineItems { get; set; } = new List<LineItem>();

        // Shopify payment info (populated by ShopifyClient)
        public string? FinancialStatus { get; set; }   // "paid" | "pending" | "refunded" etc.
        public string? PaymentGateway  { get; set; }   // e.g. "shopify_payments", "manual", "paypal"
    }
}
