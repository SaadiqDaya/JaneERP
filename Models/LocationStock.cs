namespace JaneERP.Models
{
    /// <summary>
    /// Current stock of a product at a specific warehouse location.
    /// Used by the stock transfer workflow and inventory service.
    /// </summary>
    public class LocationStock
    {
        public int    LocationID   { get; set; }
        public string LocationName { get; set; } = "";
        public int    StockQty     { get; set; }

        /// <summary>Display label for combo boxes: "Name — N available".</summary>
        public string Display => $"{LocationName}  \u2014  {StockQty} available";
    }
}
