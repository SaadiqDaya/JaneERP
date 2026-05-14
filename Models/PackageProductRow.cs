namespace JaneERP.Models
{
    /// <summary>Row returned by IPackageRepository.GetAllPackageProducts().</summary>
    public class PackageProductRow
    {
        public int    ProductID      { get; set; }
        public string SKU            { get; set; } = "";
        public string ProductName    { get; set; } = "";
        public int    ComponentCount { get; set; }
    }
}
