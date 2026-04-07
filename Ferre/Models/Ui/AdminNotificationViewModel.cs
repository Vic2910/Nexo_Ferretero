namespace Ferre.Models.Ui;

public sealed class AdminNotificationViewModel
{
    public Guid Id { get; set; }

    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; }
}
