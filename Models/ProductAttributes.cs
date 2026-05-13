namespace JaneERP.Models
{
    public class ProductAttribute
    {
        public int AttributeID { get; set; }
        public int ProductID { get; set; }
        public string AttributeName { get; set; } = string.Empty;
        public string AttributeValue { get; set; } = string.Empty;
    }

    /// <summary>
    /// Definition of an attribute name — its category, data type, optional unit,
    /// and the comma-separated list of allowed values (null = any value accepted).
    /// </summary>
    public class AttributeDefinition
    {
        public int     Id            { get; set; }
        public string  Name          { get; set; } = "";
        /// <summary>Manufacturing | Marketing | General</summary>
        public string  Category      { get; set; } = "General";
        /// <summary>Text | Number | List</summary>
        public string  DataType      { get; set; } = "Text";
        /// <summary>Unit label shown next to numeric values, e.g. "ml", "mg/ml", "%".</summary>
        public string? Unit          { get; set; }
        /// <summary>Comma-separated allowed values; null means any value is accepted.</summary>
        public string? AllowedValues { get; set; }

        public string[] GetValues() =>
            string.IsNullOrWhiteSpace(AllowedValues)
                ? []
                : AllowedValues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}