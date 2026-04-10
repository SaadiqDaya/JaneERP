using System;

namespace JaneERP.Models
{
    public class Order
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public int OrderNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal TotalPrice { get; set; }
        public string? ShippingMethod { get; set; }

        // New fields
        public string? Currency { get; set; }
        public string? ContactEmail { get; set; }

        public string? StoreDomain { get; set; }
        public string? StoreName   { get; set; }
        public string? SyncStatus  { get; set; } // "Synced" | "Not Synced"

        // Selection state for the grid checkbox (bind the checkbox column to this)
        public bool Selected { get; set; } = false;

        // ERP-side fields (populated from SalesOrders table after sync or for manual orders)
        public int?    ErpSalesOrderID { get; set; }
        public string? ErpStatus       { get; set; }  // Draft, Live, WIP, Complete

        // Discount fields (populated from SalesOrders for ERP orders)
        public string?  DiscountType    { get; set; }
        public decimal  DiscountAmount  { get; set; }
        public decimal  DiscountPercent { get; set; }
    }
}
