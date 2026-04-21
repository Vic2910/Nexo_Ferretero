using Ferre.Models.Catalog;
using Ferre.Models.Orders;

namespace Ferre.Models.Ui;

public sealed class VendedorDashboardViewModel
{
    public IReadOnlyList<Product> InventoryProducts { get; init; } = Array.Empty<Product>();

    public IReadOnlyList<Category> CategoryOptions { get; init; } = Array.Empty<Category>();

    public IReadOnlyList<AdminNotificationViewModel> Notifications { get; init; } = Array.Empty<AdminNotificationViewModel>();

    public IReadOnlyList<ClientPurchaseReceipt> Orders { get; init; } = Array.Empty<ClientPurchaseReceipt>();

    public string StatusFilter { get; init; } = string.Empty;

    public string ReceiptSearchFilter { get; init; } = string.Empty;

    public int UnreadNotificationsCount => Notifications.Count(x => !x.IsRead);
}
