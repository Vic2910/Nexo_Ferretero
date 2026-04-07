using Ferre.Models.Ui;

namespace Ferre.Services.Notifications;

public interface INotificationService
{
    IReadOnlyList<AdminNotificationViewModel> GetAll();

    AdminNotificationViewModel Add(string message);

    int MarkAsRead(Guid notificationId);

    void Clear();
}
