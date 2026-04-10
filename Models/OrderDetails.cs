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
    }
}
