using Ferre.Models.Catalog;
using Ferre.Services.Common;
using Postgrest;
using Postgrest.Exceptions;
using SupabaseClient = Supabase.Client;

namespace Ferre.Services.Catalog;

public sealed class SupabaseProductService : IProductService
{
    private readonly SupabaseClient _client;
    private readonly Lazy<Task> _initializer;

    public SupabaseProductService(SupabaseClient client)
    {
        _client = client;
        _initializer = new Lazy<Task>(() => _client.InitializeAsync());
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync()
    {
        await _initializer.Value.ConfigureAwait(false);

        var response = await _client.From<Product>()
            .Order(x => x.CreatedAt, Postgrest.Constants.Ordering.Descending)
            .Get()
            .ConfigureAwait(false);

        return response.Models;
    }

    public async Task<Product?> GetByIdAsync(Guid id)
    {
        await _initializer.Value.ConfigureAwait(false);

        return await _client.From<Product>()
            .Where(x => x.Id == id)
            .Single()
            .ConfigureAwait(false);
    }

    public async Task<OperationResult> CreateAsync(ProductFormModel model)
    {
        await _initializer.Value.ConfigureAwait(false);

        if (!model.CategoryId.HasValue)
        {
            return OperationResult.Failure("La categoría es obligatoria.");
        }

        try
        {
            var product = new Product
            {
                Name = model.Name.Trim(),
                Price = model.Price,
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                Stock = model.Stock,
                MinStock = model.MinStock,
                CategoryId = model.CategoryId.Value,
                ImageUrl1 = FirstImage(model),
                ImageUrl2 = string.IsNullOrWhiteSpace(model.ImageUrl2) ? null : model.ImageUrl2.Trim(),
                ImageUrl3 = string.IsNullOrWhiteSpace(model.ImageUrl3) ? null : model.ImageUrl3.Trim()
            };

            await _client.From<Product>().Insert(product).ConfigureAwait(false);
            return OperationResult.Success();
        }
        catch (PostgrestException ex)
        {
            return OperationResult.Failure(ResolveProductError(ex, "No se pudo registrar el producto."));
        }
    }

    public async Task<OperationResult> UpdateAsync(ProductFormModel model)
    {
        await _initializer.Value.ConfigureAwait(false);

        if (!model.Id.HasValue)
        {
            return OperationResult.Failure("Identificador inválido.");
        }

        if (!model.CategoryId.HasValue)
        {
            return OperationResult.Failure("La categoría es obligatoria.");
        }

        try
        {
            var product = new Product
            {
                Id = model.Id.Value,
                Name = model.Name.Trim(),
                Price = model.Price,
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                Stock = model.Stock,
                MinStock = model.MinStock,
                CategoryId = model.CategoryId.Value,
                ImageUrl1 = FirstImage(model),
                ImageUrl2 = string.IsNullOrWhiteSpace(model.ImageUrl2) ? null : model.ImageUrl2.Trim(),
                ImageUrl3 = string.IsNullOrWhiteSpace(model.ImageUrl3) ? null : model.ImageUrl3.Trim()
            };

            await _client.From<Product>().Update(product).ConfigureAwait(false);
            return OperationResult.Success();
        }
        catch (PostgrestException ex)
        {
            return OperationResult.Failure(ResolveProductError(ex, "No se pudo actualizar el producto."));
        }
    }

    public async Task<OperationResult> DeleteAsync(Guid id)
    {
        await _initializer.Value.ConfigureAwait(false);

        try
        {
            await _client.From<Product>()
                .Where(x => x.Id == id)
                .Delete()
                .ConfigureAwait(false);

            return OperationResult.Success();
        }
        catch (PostgrestException)
        {
            return OperationResult.Failure("No se pudo eliminar el producto.");
        }
    }

    private static string FirstImage(ProductFormModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.ImageUrl1))
        {
            return model.ImageUrl1.Trim();
        }

        if (!string.IsNullOrWhiteSpace(model.ImageUrl2))
        {
            return model.ImageUrl2.Trim();
        }

        return model.ImageUrl3?.Trim() ?? string.Empty;
    }

    private static string ResolveProductError(PostgrestException exception, string fallbackMessage)
    {
        var detail = exception.Message ?? string.Empty;
        if ((detail.Contains("stock", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("stock_minimo", StringComparison.OrdinalIgnoreCase))
            && detail.Contains("column", StringComparison.OrdinalIgnoreCase))
        {
            return "Falta la columna de stock en la tabla productos. Ejecuta el script SQL de actualización en Supabase.";
        }

        return fallbackMessage;
    }
}
