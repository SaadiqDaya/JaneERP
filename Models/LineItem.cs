using System;

namespace JaneERP.Models
{
    public class LineItem
    {
        public long Id { get; set; }
        public string? Title { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string? Sku { get; set; }
    }
}
