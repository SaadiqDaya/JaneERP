using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JaneERP.Models
{
    // used by the multi-order details view
    public class AggregatedLineItem
    {
        public string? OrderNumber { get; set; }   // order name or number label
        public string? PartNumber { get; set; }    // SKU / part number
        public string? Title { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
