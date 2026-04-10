namespace JaneERP.Models
{
    /// <summary>An attribute definition for a product type — name + required/optional flag.</summary>
    public record ProductTypeAttr(string AttributeName, bool IsRequired = true);

    public class ProductType
    {
        public int    ProductTypeID { get; set; }
        public string TypeName      { get; set; } = "";

        /// <summary>All attribute configs (required and optional) for this type.</summary>
        public List<ProductTypeAttr> AllAttributes { get; set; } = [];

        /// <summary>Convenience — attribute names that are mandatory for this type.</summary>
        public List<string> RequiredAttributes
            => AllAttributes.Where(a => a.IsRequired).Select(a => a.AttributeName).ToList();

        public override string ToString() => TypeName;
    }
}
