namespace Ferre.Models.Support;

public sealed class ClientContactMessage
{
    public Guid Id { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
