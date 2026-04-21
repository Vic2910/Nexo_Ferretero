using Ferre.Models.Catalog;
using Ferre.Models.Orders;
using Postgrest.Attributes;
using Postgrest.Exceptions;
using Postgrest.Models;
using Microsoft.Extensions.Logging;
using SupabaseClient = Supabase.Client;

namespace Ferre.Services.Orders;

public sealed class SupabaseClientPurchaseService : IClientPurchaseService
{
    private readonly SupabaseClient _client;
    private readonly ILogger<SupabaseClientPurchaseService> _logger;
    private readonly Lazy<Task> _initializer;

    public SupabaseClientPurchaseService(
        SupabaseClient client,
        ILogger<SupabaseClientPurchaseService> logger)
    {
        _client = client;
        _logger = logger;
        _initializer = new Lazy<Task>(() => _client.InitializeAsync());
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

        await _initializer.Value.ConfigureAwait(false);

        var nowUtc = DateTime.UtcNow;
        var normalizedPaymentMethod = (paymentMethod ?? string.Empty).Trim().ToLowerInvariant();
        var paymentLabel = normalizedPaymentMethod switch
        {
            "tarjeta" => "Tarjeta",
            "paypal" => "PayPal",
            "efectivo" => "Efectivo",
            _ => "Otro"
        };

        var receipt = new ClientPurchaseReceipt
        {
            Id = Guid.NewGuid(),
            UserEmail = normalizedEmail,
            ReceiptNumber = $"NEXO-{nowUtc:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            CreatedAtUtc = nowUtc,
            PaymentMethod = paymentLabel,
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

        try
        {
            var purchaseRow = new ClientPurchaseRow
            {
                Id = receipt.Id,
                UserEmail = normalizedEmail,
                ReceiptNumber = receipt.ReceiptNumber,
                CreatedAtUtc = receipt.CreatedAtUtc,
                PaymentMethod = receipt.PaymentMethod,
                Status = receipt.Status,
                PaidAtUtc = receipt.PaidAtUtc,
                Total = receipt.Total
            };

            await _client.From<ClientPurchaseRow>().Insert(purchaseRow).ConfigureAwait(false);

            var lineRows = receipt.Lines.Select(line => new ClientPurchaseLineRow
            {
                Id = Guid.NewGuid(),
                PurchaseId = receipt.Id,
                ProductId = line.ProductId,
                ProductName = line.ProductName,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                LineTotal = line.LineTotal,
                CreatedAtUtc = nowUtc
            }).ToList();

            if (lineRows.Count > 0)
            {
                try
                {
                    await _client.From<ClientPurchaseLineRow>().Insert(lineRows).ConfigureAwait(false);
                }
                catch
                {
                    await _client.From<ClientPurchaseRow>()
                        .Where(x => x.Id == receipt.Id)
                        .Delete()
                        .ConfigureAwait(false);

                    throw;
                }
            }

            return (true, receipt, null);
        }
        catch (PostgrestException ex)
        {
            _logger.LogWarning(ex, "Error registrando comprobante de compra en Supabase. StatusCode: {StatusCode}", ex.StatusCode);
            return (false, null, ResolveRegisterError(ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado registrando comprobante de compra.");
            return (false, null, "No fue posible registrar el comprobante de compra.");
        }
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

        await _initializer.Value.ConfigureAwait(false);

        try
        {
            var purchaseResponse = await _client.From<ClientPurchaseRow>()
                .Get()
                .ConfigureAwait(false);

            var purchases = purchaseResponse.Models
                .Where(x => string.Equals(x.UserEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToList();

            if (purchases.Count == 0)
            {
                return Array.Empty<ClientPurchaseReceipt>();
            }

            var lineResponse = await _client.From<ClientPurchaseLineRow>()
                .Get()
                .ConfigureAwait(false);

            return MapReceipts(purchases, lineResponse.Models);
        }
        catch
        {
            return Array.Empty<ClientPurchaseReceipt>();
        }
    }

    public async Task<ClientPurchaseReceipt?> GetPurchaseByIdAsync(
        string userEmail,
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(userEmail);
        if (receiptId == Guid.Empty || string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        await _initializer.Value.ConfigureAwait(false);

        try
        {
            var purchaseResponse = await _client.From<ClientPurchaseRow>()
                .Get()
                .ConfigureAwait(false);

            var purchase = purchaseResponse.Models
                .FirstOrDefault(x => x.Id == receiptId && string.Equals(x.UserEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase));

            if (purchase is null)
            {
                return null;
            }

            var lineResponse = await _client.From<ClientPurchaseLineRow>()
                .Get()
                .ConfigureAwait(false);

            return MapReceipts(new List<ClientPurchaseRow> { purchase }, lineResponse.Models)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<ClientPurchaseReceipt>> GetAllPurchasesAsync(
        DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        await _initializer.Value.ConfigureAwait(false);

        try
        {
            var purchaseResponse = await _client.From<ClientPurchaseRow>()
                .Get()
                .ConfigureAwait(false);

            var purchases = purchaseResponse.Models
                .Where(x => !date.HasValue || DateOnly.FromDateTime(x.CreatedAtUtc) == date.Value)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToList();

            if (purchases.Count == 0)
            {
                return Array.Empty<ClientPurchaseReceipt>();
            }

            var lineResponse = await _client.From<ClientPurchaseLineRow>()
                .Get()
                .ConfigureAwait(false);

            return MapReceipts(purchases, lineResponse.Models);
        }
        catch
        {
            return Array.Empty<ClientPurchaseReceipt>();
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

        await _initializer.Value.ConfigureAwait(false);

        try
        {
            var purchaseResponse = await _client.From<ClientPurchaseRow>()
                .Get()
                .ConfigureAwait(false);

            var target = purchaseResponse.Models.FirstOrDefault(x => x.Id == receiptId);
            if (target is null)
            {
                return (false, "No se encontró el pedido seleccionado.");
            }

            if (string.Equals(target.Status, "cancelado", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "El pedido ya fue cancelado.");
            }

            if (string.Equals(target.Status, "entregado", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "No se puede anular un pedido entregado.");
            }

            target.Status = "cancelado";
            await _client.From<ClientPurchaseRow>().Update(target).ConfigureAwait(false);
            return (true, null);
        }
        catch
        {
            return (false, "No fue posible anular el pedido.");
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

        await _initializer.Value.ConfigureAwait(false);

        try
        {
            var purchaseResponse = await _client.From<ClientPurchaseRow>()
                .Get()
                .ConfigureAwait(false);

            var target = purchaseResponse.Models.FirstOrDefault(x => x.Id == receiptId);
            if (target is null)
            {
                return (false, "No se encontró el pedido seleccionado.");
            }

            if (!string.Equals(target.Status, "pendiente", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(target.PaymentMethod, "Efectivo", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Solo se puede registrar cobro en efectivo para pedidos pendientes.");
            }

            target.Status = "pagado";
            target.PaidAtUtc = DateTime.UtcNow;
            await _client.From<ClientPurchaseRow>().Update(target).ConfigureAwait(false);
            return (true, null);
        }
        catch
        {
            return (false, "No fue posible registrar el pago en efectivo.");
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

        await _initializer.Value.ConfigureAwait(false);

        try
        {
            var purchaseResponse = await _client.From<ClientPurchaseRow>()
                .Get()
                .ConfigureAwait(false);

            var target = purchaseResponse.Models.FirstOrDefault(x => x.Id == receiptId);
            if (target is null)
            {
                return (false, "No se encontró el pedido seleccionado.");
            }

            if (string.Equals(target.Status, "entregado", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "El pedido ya fue marcado como entregado.");
            }

            if (!string.Equals(target.Status, "pagado", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Solo se puede marcar como entregado un pedido pagado.");
            }

            target.Status = "entregado";
            target.DeliveredAtUtc = DateTime.UtcNow;
            await _client.From<ClientPurchaseRow>().Update(target).ConfigureAwait(false);
            return (true, null);
        }
        catch
        {
            return (false, "No fue posible actualizar el estado del pedido.");
        }
    }

    private static IReadOnlyList<ClientPurchaseReceipt> MapReceipts(
        IReadOnlyList<ClientPurchaseRow> purchases,
        IReadOnlyList<ClientPurchaseLineRow> lines)
    {
        var linesByPurchase = lines
            .GroupBy(x => x.PurchaseId)
            .ToDictionary(x => x.Key, x => x.ToList());

        return purchases.Select(purchase =>
        {
            linesByPurchase.TryGetValue(purchase.Id, out var currentLines);

            var mappedLines = (currentLines ?? new List<ClientPurchaseLineRow>())
                .OrderBy(x => x.CreatedAtUtc)
                .Select(line => new ClientPurchaseReceiptLine
                {
                    ProductId = line.ProductId,
                    ProductName = line.ProductName,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    LineTotal = line.LineTotal
                })
                .ToList();

            return new ClientPurchaseReceipt
            {
                Id = purchase.Id,
                UserEmail = purchase.UserEmail,
                ReceiptNumber = purchase.ReceiptNumber,
                CreatedAtUtc = purchase.CreatedAtUtc,
                PaymentMethod = purchase.PaymentMethod,
                Status = purchase.Status,
                PaidAtUtc = purchase.PaidAtUtc,
                DeliveredAtUtc = purchase.DeliveredAtUtc,
                Total = purchase.Total,
                Lines = mappedLines
            };
        }).ToList();
    }

    private static string NormalizeEmail(string? email)
    {
        return email?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static string ResolveRegisterError(PostgrestException exception)
    {
        var detail = exception.Message ?? string.Empty;
        if (detail.Contains("client_purchases", StringComparison.OrdinalIgnoreCase)
            && detail.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return "Falta crear la tabla client_purchases en Supabase. Ejecuta el script SQL de pedidos y soporte.";
        }

        if (detail.Contains("client_purchase_lines", StringComparison.OrdinalIgnoreCase)
            && detail.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return "Falta crear la tabla client_purchase_lines en Supabase. Ejecuta el script SQL de pedidos y soporte.";
        }

        if (detail.Contains("permission denied", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("row-level security", StringComparison.OrdinalIgnoreCase))
        {
            return "Supabase bloqueó la operación por permisos. Verifica grants y políticas RLS del script SQL.";
        }

        if (detail.Contains("receipt_number", StringComparison.OrdinalIgnoreCase)
            && detail.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
        {
            return "No se pudo generar un número de comprobante único. Intenta nuevamente.";
        }

        return "No fue posible registrar el comprobante de compra en Supabase.";
    }

    [Table("client_purchases")]
    private sealed class ClientPurchaseRow : BaseModel
    {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("user_email")]
        public string UserEmail { get; set; } = string.Empty;

        [Column("receipt_number")]
        public string ReceiptNumber { get; set; } = string.Empty;

        [Column("created_at_utc")]
        public DateTime CreatedAtUtc { get; set; }

        [Column("payment_method")]
        public string PaymentMethod { get; set; } = string.Empty;

        [Column("status")]
        public string Status { get; set; } = string.Empty;

        [Column("paid_at_utc")]
        public DateTime? PaidAtUtc { get; set; }

        [Column("delivered_at_utc")]
        public DateTime? DeliveredAtUtc { get; set; }

        [Column("total")]
        public decimal Total { get; set; }
    }

    [Table("client_purchase_lines")]
    private sealed class ClientPurchaseLineRow : BaseModel
    {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("purchase_id")]
        public Guid PurchaseId { get; set; }

        [Column("product_id")]
        public Guid ProductId { get; set; }

        [Column("product_name")]
        public string ProductName { get; set; } = string.Empty;

        [Column("quantity")]
        public int Quantity { get; set; }

        [Column("unit_price")]
        public decimal UnitPrice { get; set; }

        [Column("line_total")]
        public decimal LineTotal { get; set; }

        [Column("created_at_utc")]
        public DateTime CreatedAtUtc { get; set; }
    }
}
