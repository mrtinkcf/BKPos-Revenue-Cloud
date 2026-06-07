using BKPos.Core.Models;

namespace BKPos.Core.Interfaces;

public interface IProductRepository
{
    IReadOnlyList<ProductCategory> GetCategories(bool orderVisibleOnly = false);

    IReadOnlyList<ProductUnit> GetUnits();

    IReadOnlyList<Product> GetByCategory(int categoryId, bool orderVisibleOnly = false);

    void AddProduct(Product product);

    void UpdateProduct(Product product);

    bool ProductHasOpenOrders(string productExternalId);

    void DeleteProduct(string productExternalId);

    void AddCategory(ProductCategory category);

    void UpdateCategory(ProductCategory category);

    void AddUnit(ProductUnit unit);

    bool CategoryHasProducts(string categoryExternalId);

    void DeleteCategory(string categoryExternalId);

    void SaveCategoryOrder(IReadOnlyList<string> categoryExternalIdsInOrder);
}
