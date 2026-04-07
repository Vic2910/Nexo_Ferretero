using Ferre.Models.Catalog;
using Ferre.Services.Common;

namespace Ferre.Services.Catalog;

public interface ICategoryService
{
    Task<IReadOnlyList<Category>> GetAllAsync();

    Task<Category?> GetByIdAsync(Guid id);

    Task<OperationResult> CreateAsync(CategoryFormModel model);

    Task<OperationResult> UpdateAsync(CategoryFormModel model);

    Task<OperationResult> DeleteAsync(Guid id);
}
