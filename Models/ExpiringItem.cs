namespace JaneERP.Models
{
    /// <summary>
    /// An inventory lot approaching or past its expiration date.
    /// Returned by IInventoryService.GetExpiringItems().
    /// </summary>
    public class ExpiringItem
    {
        public string?   SKU            { get; set; }
        public string?   ProductName    { get; set; }
        public string?   LocationName   { get; set; }
        public string?   LotNumber      { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public int       Quantity       { get; set; }
    }
}
