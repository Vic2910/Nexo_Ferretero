using System.Text.Json;
using Ferre.Models.Ui;

namespace Ferre.Services.Notifications;

public sealed class SessionNotificationService : INotificationService
{
    private const string StoreFileName = "admin-notifications.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly object SyncRoot = new();
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _storeFilePath;

    private sealed class NotificationStore
    {
        public Dictionary<string, List<AdminNotificationViewModel>> ItemsByOwner { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    public SessionNotificationService(IHttpContextAccessor httpContextAccessor, IWebHostEnvironment webHostEnvironment)
    {
        _httpContextAccessor = httpContextAccessor;
        _storeFilePath = Path.Combine(webHostEnvironment.ContentRootPath, "App_Data", StoreFileName);
    }

    public IReadOnlyList<AdminNotificationViewModel> GetAll()
    {
        return LoadNotifications()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToList();
    }

    public AdminNotificationViewModel Add(string message)
    {
        var notifications = LoadNotifications();
        var notification = new AdminNotificationViewModel
        {
            Id = Guid.NewGuid(),
            Message = message,
            CreatedAtUtc = DateTime.UtcNow,
            IsRead = false
        };

        notifications.Add(notification);
        SaveNotifications(notifications);
        return notification;
    }

    public int MarkAsRead(Guid notificationId)
    {
        var notifications = LoadNotifications();
        var notification = notifications.FirstOrDefault(x => x.Id == notificationId);
        if (notification is not null)
        {
            notification.IsRead = true;
            SaveNotifications(notifications);
        }

        return notifications.Count(x => !x.IsRead);
    }

    public void Clear()
    {
        SaveNotifications(new List<AdminNotificationViewModel>());
    }

    private List<AdminNotificationViewModel> LoadNotifications()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return new List<AdminNotificationViewModel>();
        }

        var currentUserEmail = NormalizeEmail(context.Session.GetString("UserEmail"));
        if (string.IsNullOrWhiteSpace(currentUserEmail))
        {
            return new List<AdminNotificationViewModel>();
        }

        lock (SyncRoot)
        {
            var store = ReadStoreUnsafe();
            if (!store.ItemsByOwner.TryGetValue(currentUserEmail, out var items) || items is null)
            {
                return new List<AdminNotificationViewModel>();
            }

            return items;
        }
    }

    private void SaveNotifications(List<AdminNotificationViewModel> notifications)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return;
        }

        var currentUserEmail = NormalizeEmail(context.Session.GetString("UserEmail"));
        if (string.IsNullOrWhiteSpace(currentUserEmail))
        {
            return;
        }

        lock (SyncRoot)
        {
            var store = ReadStoreUnsafe();

            if (notifications.Count == 0)
            {
                store.ItemsByOwner.Remove(currentUserEmail);
            }
            else
            {
                store.ItemsByOwner[currentUserEmail] = notifications;
            }

            WriteStoreUnsafe(store);
        }
    }

    private NotificationStore ReadStoreUnsafe()
    {
        try
        {
            if (!File.Exists(_storeFilePath))
            {
                return new NotificationStore();
            }

            var raw = File.ReadAllText(_storeFilePath);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new NotificationStore();
            }

            return JsonSerializer.Deserialize<NotificationStore>(raw, JsonOptions) ?? new NotificationStore();
        }
        catch
        {
            return new NotificationStore();
        }
    }

    private void WriteStoreUnsafe(NotificationStore store)
    {
        var directory = Path.GetDirectoryName(_storeFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_storeFilePath, JsonSerializer.Serialize(store, JsonOptions));
    }

    private static string NormalizeEmail(string? email)
    {
        return email?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}
