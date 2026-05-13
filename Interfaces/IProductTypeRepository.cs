using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IProductTypeRepository
    {
        List<AttributeDefinition> GetAttributeDefinitions();
        void UpsertAttributeDefinition(string name, string? allowedValues,
            string category = "General", string dataType = "Text", string? unit = null);
        void         DeleteAttributeDefinition(int id);
        string[]     GetAllowedValues(string attributeName);
        List<ProductType> GetAll();
        ProductType? GetById(int id);
        void         Add(string typeName, IEnumerable<ProductTypeAttr> attributes);
        void         Update(ProductType type);
        void         Delete(int id);
        List<string> GetAttributeNamesForType(int typeId);
    }
}
