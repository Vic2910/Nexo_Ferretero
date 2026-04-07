using System.Text.Json;
using Ferre.Models.Support;

namespace Ferre.Services.Support;

public sealed class FileClientContactMessageService : IClientContactMessageService
{
    private const string StoreFileName = "client-contact-messages.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim SyncRoot = new(1, 1);
    private readonly string _storeFilePath;

    public FileClientContactMessageService(IWebHostEnvironment webHostEnvironment)
    {
        _storeFilePath = Path.Combine(webHostEnvironment.ContentRootPath, "App_Data", StoreFileName);
    }

    public async Task SaveAsync(ClientContactMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        await SyncRoot.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadUnsafeAsync(cancellationToken);
            items.Insert(0, message);
            await WriteUnsafeAsync(items, cancellationToken);
        }
        finally
        {
            SyncRoot.Release();
        }
    }

    public async Task<IReadOnlyList<ClientContactMessage>> GetAllAsync(DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        await SyncRoot.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadUnsafeAsync(cancellationToken);
            var filtered = date.HasValue
                ? items.Where(x => DateOnly.FromDateTime(x.CreatedAtUtc) == date.Value)
                : items;

            return filtered
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToList();
        }
        finally
        {
            SyncRoot.Release();
        }
    }

    private async Task<List<ClientContactMessage>> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_storeFilePath))
            {
                return new List<ClientContactMessage>();
            }

            var raw = await File.ReadAllTextAsync(_storeFilePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<ClientContactMessage>();
            }

            return JsonSerializer.Deserialize<List<ClientContactMessage>>(raw, JsonOptions) ?? new List<ClientContactMessage>();
        }
        catch
        {
            return new List<ClientContactMessage>();
        }
    }

    private async Task WriteUnsafeAsync(List<ClientContactMessage> items, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_storeFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(items, JsonOptions);
        await File.WriteAllTextAsync(_storeFilePath, json, cancellationToken);
    }
}
