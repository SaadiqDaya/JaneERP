using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IProductTypeRepository
    {
        List<(int Id, string Name, string? Values)> GetAttributeDefinitions();
        void         UpsertAttributeDefinition(string name, string? allowedValues);
        void         DeleteAttributeDefinition(int id);
        string[]     GetAllowedValues(string attributeName);
        List<ProductType> GetAll();
        ProductType? GetById(int id);
        void         Add(string typeName, IEnumerable<ProductTypeAttr> attributes);
        void         Update(ProductType type);
        void         Delete(int id);
    }
}
