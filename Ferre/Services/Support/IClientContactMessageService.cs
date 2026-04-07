using Ferre.Models.Support;

namespace Ferre.Services.Support;

public interface IClientContactMessageService
{
    Task SaveAsync(ClientContactMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClientContactMessage>> GetAllAsync(DateOnly? date = null, CancellationToken cancellationToken = default);
}
