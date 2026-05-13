namespace JaneERP.Models
{
    public class LotStockRow
    {
        public int      ProductID      { get; set; }
        public string   SKU            { get; set; } = string.Empty;
        public string   ProductName    { get; set; } = string.Empty;
        public string?  LotNumber      { get; set; }
        public DateTime ExpirationDate { get; set; }
        public decimal  CurrentQty     { get; set; }
        public int?     LocationID     { get; set; }
        public string?  LocationName   { get; set; }

        public int DaysUntilExpiry =>
            (int)(ExpirationDate.Date - DateTime.Today).TotalDays;

        public string ExpiryStatus =>
            DaysUntilExpiry <  0  ? "Expired"  :
            DaysUntilExpiry <= 7  ? "Critical" :
            DaysUntilExpiry <= 30 ? "Warning"  : "OK";
    }
}
