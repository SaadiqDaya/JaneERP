using CsvHelper.Configuration;
using JaneERP.Models;

namespace JaneERP.Data
{
    public class ProductCsvMap : ClassMap<Product>
    {
        public ProductCsvMap()
        {
            Map(p => p.SKU).Name("SKU");
            Map(p => p.ProductName).Name("ProductName");
            Map(p => p.RetailPrice).Name("RetailPrice");
            Map(p => p.CurrentStock).Name("CurrentStock");
            Map(p => p.ProductID).Ignore();
            Map(p => p.IsActive).Ignore();
        }
    }
}
