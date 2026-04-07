namespace Ferre.Services.Common;

public sealed record OperationResult(bool Succeeded, string? ErrorMessage = null)
{
    public static OperationResult Success() => new(true);

    public static OperationResult Failure(string message) => new(false, message);
}
