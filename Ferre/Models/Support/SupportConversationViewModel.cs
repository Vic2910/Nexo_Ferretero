namespace Ferre.Models.Support;

public sealed class SupportConversationViewModel
{
    public Guid ConversationId { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string ClientName { get; set; } = string.Empty;

    public string ClientEmail { get; set; } = string.Empty;

    public string Status { get; set; } = "pendiente";

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public IReadOnlyList<SupportConversationMessageViewModel> Messages { get; set; } = Array.Empty<SupportConversationMessageViewModel>();
}

public sealed class SupportConversationMessageViewModel
{
    public Guid Id { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string SenderRole { get; set; } = "cliente";

    public string SenderName { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool IsSystemEvent { get; set; }
}
