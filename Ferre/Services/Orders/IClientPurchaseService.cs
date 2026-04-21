using Ferre.Models.Catalog;
using Ferre.Models.Orders;

namespace Ferre.Services.Orders;

public interface IClientPurchaseService
{
    Task<(bool Succeeded, ClientPurchaseReceipt? Receipt, string? ErrorMessage)> RegisterPurchaseAsync(
        string userEmail,
        string paymentMethod,
        IReadOnlyCollection<(Product Product, int Quantity)> lines,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClientPurchaseReceipt>> GetPurchaseHistoryAsync(
        string userEmail,
        CancellationToken cancellationToken = default);

    Task<ClientPurchaseReceipt?> GetPurchaseByIdAsync(
        string userEmail,
        Guid receiptId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClientPurchaseReceipt>> GetAllPurchasesAsync(
        DateOnly? date = null,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> CancelPurchaseAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> RegisterCashPaymentAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> MarkPurchaseAsDeliveredAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default);
}
