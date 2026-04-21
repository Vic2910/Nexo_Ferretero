using System.Text.Json;
using Ferre.Models.Catalog;
using Ferre.Models.Orders;

namespace Ferre.Services.Orders;

public sealed class FileClientPurchaseService : IClientPurchaseService
{
    private const string StoreFileName = "client-purchases.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim SyncRoot = new(1, 1);
    private readonly string _storeFilePath;

    private sealed class PurchaseStore
    {
        public Dictionary<string, List<ClientPurchaseReceipt>> ItemsByOwner { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    public FileClientPurchaseService(IWebHostEnvironment webHostEnvironment)
    {
        _storeFilePath = Path.Combine(webHostEnvironment.ContentRootPath, "App_Data", StoreFileName);
    }

    public async Task<(bool Succeeded, ClientPurchaseReceipt? Receipt, string? ErrorMessage)> RegisterPurchaseAsync(
        string userEmail,
        string paymentMethod,
        IReadOnlyCollection<(Product Product, int Quantity)> lines,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(userEmail);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return (false, null, "No se pudo identificar al usuario de la compra.");
        }

        if (lines.Count == 0)
        {
            return (false, null, "No hay productos para registrar en la compra.");
        }

        var nowUtc = DateTime.UtcNow;
        var normalizedPaymentMethod = (paymentMethod ?? string.Empty).Trim().ToLowerInvariant();
        var receipt = new ClientPurchaseReceipt
        {
            Id = Guid.NewGuid(),
            UserEmail = normalizedEmail,
            ReceiptNumber = $"NEXO-{nowUtc:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            CreatedAtUtc = nowUtc,
            PaymentMethod = normalizedPaymentMethod switch
            {
                "tarjeta" => "Tarjeta",
                "paypal" => "PayPal",
                "efectivo" => "Efectivo",
                _ => "Otro"
            },
            Status = normalizedPaymentMethod == "efectivo" ? "pendiente" : "pagado",
            PaidAtUtc = normalizedPaymentMethod == "efectivo" ? null : nowUtc,
            Lines = lines.Select(line => new ClientPurchaseReceiptLine
            {
                ProductId = line.Product.Id,
                ProductName = line.Product.Name,
                Quantity = line.Quantity,
                UnitPrice = line.Product.Price,
                LineTotal = line.Product.Price * line.Quantity
            }).ToList()
        };

        receipt.Total = receipt.Lines.Sum(line => line.LineTotal);

        await SyncRoot.WaitAsync(cancellationToken);
        try
        {
            var store = await ReadStoreUnsafeAsync(cancellationToken);
            if (!store.ItemsByOwner.TryGetValue(normalizedEmail, out var items) || items is null)
            {
                items = new List<ClientPurchaseReceipt>();
                store.ItemsByOwner[normalizedEmail] = items;
            }

            items.Insert(0, receipt);
            await WriteStoreUnsafeAsync(store, cancellationToken);
        }
        catch
        {
            return (false, null, "No fue posible registrar el comprobante de compra.");
        }
        finally
        {
            SyncRoot.Release();
        }

        return (true, receipt, null);
    }

    public async Task<IReadOnlyList<ClientPurchaseReceipt>> GetPurchaseHistoryAsync(
        string userEmail,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(userEmail);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return Array.Empty<ClientPurchaseReceipt>();
        }

        await SyncRoot.WaitAsync(cancellationToken);
        try
        {
            var store = await ReadStoreUnsafeAsync(cancellationToken);
            if (!store.ItemsByOwner.TryGetValue(normalizedEmail, out var items) || items is null)
            {
                return Array.Empty<ClientPurchaseReceipt>();
            }

            return items
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToList();
        }
        catch
        {
            return Array.Empty<ClientPurchaseReceipt>();
        }
        finally
        {
            SyncRoot.Release();
        }
    }

    public async Task<IReadOnlyList<ClientPurchaseReceipt>> GetAllPurchasesAsync(
        DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        await SyncRoot.WaitAsync(cancellationToken);
        try
        {
            var store = await ReadStoreUnsafeAsync(cancellationToken);
            var allItems = store.ItemsByOwner
                .SelectMany(x => (x.Value ?? new List<ClientPurchaseReceipt>())
                    .Select(receipt =>
                    {
                        if (string.IsNullOrWhiteSpace(receipt.UserEmail))
                        {
                            receipt.UserEmail = NormalizeEmail(x.Key);
                        }

                        return receipt;
                    }))
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToList();

            if (!date.HasValue)
            {
                return allItems;
            }

            return allItems
                .Where(x => DateOnly.FromDateTime(x.CreatedAtUtc) == date.Value)
                .ToList();
        }
        catch
        {
            return Array.Empty<ClientPurchaseReceipt>();
        }
        finally
        {
            SyncRoot.Release();
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> CancelPurchaseAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        if (receiptId == Guid.Empty)
        {
            return (false, "El pedido seleccionado no es válido.");
        }

        await SyncRoot.WaitAsync(cancellationToken);
        try
        {
            var store = await ReadStoreUnsafeAsync(cancellationToken);
            var receipt = store.ItemsByOwner
                .SelectMany(x => x.Value ?? new List<ClientPurchaseReceipt>())
                .FirstOrDefault(x => x.Id == receiptId);

            if (receipt is null)
            {
                return (false, "No se encontró el pedido seleccionado.");
            }

            if (string.Equals(receipt.Status, "cancelado", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "El pedido ya fue cancelado.");
            }

            if (string.Equals(receipt.Status, "entregado", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "No se puede anular un pedido entregado.");
            }

            receipt.Status = "cancelado";
            await WriteStoreUnsafeAsync(store, cancellationToken);
            return (true, null);
        }
        catch
        {
            return (false, "No fue posible anular el pedido.");
        }
        finally
        {
            SyncRoot.Release();
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> RegisterCashPaymentAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        if (receiptId == Guid.Empty)
        {
            return (false, "El pedido seleccionado no es válido.");
        }

        await SyncRoot.WaitAsync(cancellationToken);
        try
        {
            var store = await ReadStoreUnsafeAsync(cancellationToken);
            var receipt = store.ItemsByOwner
                .SelectMany(x => x.Value ?? new List<ClientPurchaseReceipt>())
                .FirstOrDefault(x => x.Id == receiptId);

            if (receipt is null)
            {
                return (false, "No se encontró el pedido seleccionado.");
            }

            if (!string.Equals(receipt.Status, "pendiente", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(receipt.PaymentMethod, "Efectivo", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Solo se puede registrar cobro en efectivo para pedidos pendientes.");
            }

            receipt.Status = "pagado";
            receipt.PaidAtUtc = DateTime.UtcNow;
            await WriteStoreUnsafeAsync(store, cancellationToken);
            return (true, null);
        }
        catch
        {
            return (false, "No fue posible registrar el pago en efectivo.");
        }
        finally
        {
            SyncRoot.Release();
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> MarkPurchaseAsDeliveredAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        if (receiptId == Guid.Empty)
        {
            return (false, "El pedido seleccionado no es válido.");
        }

        await SyncRoot.WaitAsync(cancellationToken);
        try
        {
            var store = await ReadStoreUnsafeAsync(cancellationToken);
            var receipt = store.ItemsByOwner
                .SelectMany(x => x.Value ?? new List<ClientPurchaseReceipt>())
                .FirstOrDefault(x => x.Id == receiptId);

            if (receipt is null)
            {
                return (false, "No se encontró el pedido seleccionado.");
            }

            if (string.Equals(receipt.Status, "entregado", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "El pedido ya fue marcado como entregado.");
            }

            if (!string.Equals(receipt.Status, "pagado", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Solo se puede marcar como entregado un pedido pagado.");
            }

            receipt.Status = "entregado";
            receipt.DeliveredAtUtc = DateTime.UtcNow;
            await WriteStoreUnsafeAsync(store, cancellationToken);
            return (true, null);
        }
        catch
        {
            return (false, "No fue posible actualizar el estado del pedido.");
        }
        finally
        {
            SyncRoot.Release();
        }
    }

    public async Task<ClientPurchaseReceipt?> GetPurchaseByIdAsync(
        string userEmail,
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        if (receiptId == Guid.Empty)
        {
            return null;
        }

        var normalizedEmail = NormalizeEmail(userEmail);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        await SyncRoot.WaitAsync(cancellationToken);
        try
        {
            var store = await ReadStoreUnsafeAsync(cancellationToken);
            if (!store.ItemsByOwner.TryGetValue(normalizedEmail, out var items) || items is null)
            {
                return null;
            }

            return items.FirstOrDefault(x => x.Id == receiptId);
        }
        catch
        {
            return null;
        }
        finally
        {
            SyncRoot.Release();
        }
    }

    private async Task<PurchaseStore> ReadStoreUnsafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_storeFilePath))
            {
                return new PurchaseStore();
            }

            var raw = await File.ReadAllTextAsync(_storeFilePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new PurchaseStore();
            }

            return JsonSerializer.Deserialize<PurchaseStore>(raw, JsonOptions) ?? new PurchaseStore();
        }
        catch
        {
            return new PurchaseStore();
        }
    }

    private async Task WriteStoreUnsafeAsync(PurchaseStore store, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_storeFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(store, JsonOptions);
        await File.WriteAllTextAsync(_storeFilePath, json, cancellationToken);
    }

    private static string NormalizeEmail(string? email)
    {
        return email?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}
