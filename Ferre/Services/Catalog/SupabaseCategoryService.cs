using Ferre.Models.Catalog;
using Ferre.Services.Common;
using System.Globalization;
using System.Text;
using SupabaseClient = Supabase.Client;
using Postgrest;
using Postgrest.Exceptions;

namespace Ferre.Services.Catalog;

public sealed class SupabaseCategoryService : ICategoryService
{
    private readonly SupabaseClient _client;
    private readonly Lazy<Task> _initializer;

    public SupabaseCategoryService(SupabaseClient client)
    {
        _client = client;
        _initializer = new Lazy<Task>(() => _client.InitializeAsync());
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync()
    {
        await _initializer.Value.ConfigureAwait(false);

        var response = await _client.From<Category>()
            .Order(x => x.CreatedAt, Postgrest.Constants.Ordering.Descending)
            .Get()
            .ConfigureAwait(false);

        return response.Models;
    }

    public async Task<Category?> GetByIdAsync(Guid id)
    {
        await _initializer.Value.ConfigureAwait(false);

        var response = await _client.From<Category>()
            .Where(x => x.Id == id)
            .Single()
            .ConfigureAwait(false);

        return response;
    }

    public async Task<OperationResult> CreateAsync(CategoryFormModel model)
    {
        await _initializer.Value.ConfigureAwait(false);

        var normalizedName = model.Name.Trim();
        if (await ExistsByNameAsync(normalizedName).ConfigureAwait(false))
        {
            return OperationResult.Failure("La categoría ya existe.");
        }

        try
        {
            var category = new Category
            {
                Name = normalizedName,
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim()
            };

            await _client.From<Category>().Insert(category).ConfigureAwait(false);
            return OperationResult.Success();
        }
        catch (PostgrestException)
        {
            return OperationResult.Failure("No se pudo registrar la categoría.");
        }
    }

    public async Task<OperationResult> UpdateAsync(CategoryFormModel model)
    {
        await _initializer.Value.ConfigureAwait(false);

        if (!model.Id.HasValue)
        {
            return OperationResult.Failure("Identificador inválido.");
        }

        var normalizedName = model.Name.Trim();
        if (await ExistsByNameAsync(normalizedName, model.Id.Value).ConfigureAwait(false))
        {
            return OperationResult.Failure("La categoría ya existe.");
        }

        try
        {
            var category = new Category
            {
                Id = model.Id.Value,
                Name = normalizedName,
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim()
            };

            await _client.From<Category>().Update(category).ConfigureAwait(false);
            return OperationResult.Success();
        }
        catch (PostgrestException)
        {
            return OperationResult.Failure("No se pudo actualizar la categoría.");
        }
    }

    public async Task<OperationResult> DeleteAsync(Guid id)
    {
        await _initializer.Value.ConfigureAwait(false);

        try
        {
            await _client.From<Category>()
                .Where(x => x.Id == id)
                .Delete()
                .ConfigureAwait(false);

            return OperationResult.Success();
        }
        catch (PostgrestException)
        {
            return OperationResult.Failure("No se pudo eliminar la categoría.");
        }
    }

    private async Task<bool> ExistsByNameAsync(string name, Guid? excludedId = null)
    {
        var normalizedCandidate = NormalizeCategoryName(name);
        var response = await _client.From<Category>()
            .Get()
            .ConfigureAwait(false);

        return response.Models.Any(category =>
            (!excludedId.HasValue || category.Id != excludedId.Value) &&
            string.Equals(NormalizeCategoryName(category.Name), normalizedCandidate, StringComparison.Ordinal));
    }

    private static string NormalizeCategoryName(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
