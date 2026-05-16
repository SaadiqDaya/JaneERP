using System;
using System.Collections.Generic;

namespace JaneERP.Models
{
    public class Shipment
    {
        public int     ShipmentID     { get; set; }
        public int     SalesOrderID   { get; set; }
        public int?    BoxTypeID      { get; set; }
        public string  BoxLabel       { get; set; } = "";
        public string  BoxTypeName    { get; set; } = "";  // denormalised for display
        public string  TrackingNumber { get; set; } = "";
        public string  Carrier        { get; set; } = "";
        public string  Status         { get; set; } = "Open";   // Open | Packed | Shipped
        public string  Notes          { get; set; } = "";
        public string  ShippedBy      { get; set; } = "";
        public string  CreatedBy      { get; set; } = "";
        public DateTime? ShippedAt    { get; set; }
        public DateTime  CreatedAt    { get; set; }

        public List<ShipmentItem> Items { get; set; } = new();

        public bool IsShipped => Status == "Shipped";
        public string DisplayLabel => string.IsNullOrWhiteSpace(BoxLabel)
            ? $"Box {ShipmentID}" : BoxLabel;
        public override string ToString() => $"{DisplayLabel} [{Status}]";
    }

    public class ShipmentItem
    {
        public int      ShipmentItemID     { get; set; }
        public int      ShipmentID         { get; set; }
        public int      SalesOrderItemID   { get; set; }
        public string   ProductTitle       { get; set; } = "";  // denormalised
        public string   SKU                { get; set; } = "";
        public int      Quantity           { get; set; }
        public string   PackedBy           { get; set; } = "";
        public DateTime? PackedAt          { get; set; }
    }
}
