namespace JaneERP.Models
{
    public class ProductAttribute
    {
        public int AttributeID { get; set; }
        public int ProductID { get; set; }
        public string AttributeName { get; set; } = string.Empty;
        public string AttributeValue { get; set; } = string.Empty;
    }
}