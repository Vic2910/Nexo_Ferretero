using Ferre.Models.Catalog;

namespace Ferre.Models.Ui;

public sealed class VendedorDashboardViewModel
{
    public IReadOnlyList<Product> InventoryProducts { get; init; } = Array.Empty<Product>();

    public IReadOnlyList<Category> CategoryOptions { get; init; } = Array.Empty<Category>();

    public IReadOnlyList<AdminNotificationViewModel> Notifications { get; init; } = Array.Empty<AdminNotificationViewModel>();

    public int UnreadNotificationsCount => Notifications.Count(x => !x.IsRead);
}
