using Ferre.Models.Support;
using Postgrest.Attributes;
using Postgrest.Models;
using SupabaseClient = Supabase.Client;

namespace Ferre.Services.Support;

public sealed class SupabaseClientContactMessageService : IClientContactMessageService
{
    private readonly SupabaseClient _client;
    private readonly Lazy<Task> _initializer;

    public SupabaseClientContactMessageService(SupabaseClient client)
    {
        _client = client;
        _initializer = new Lazy<Task>(() => _client.InitializeAsync());
    }

    public async Task SaveAsync(ClientContactMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        await _initializer.Value.ConfigureAwait(false);

        var row = new ClientContactMessageRow
        {
            Id = message.Id == Guid.Empty ? Guid.NewGuid() : message.Id,
            CreatedAtUtc = message.CreatedAtUtc == default ? DateTime.UtcNow : message.CreatedAtUtc,
            Name = message.Name,
            Email = message.Email,
            Subject = message.Subject,
            Message = message.Message
        };

        await _client.From<ClientContactMessageRow>().Insert(row).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClientContactMessage>> GetAllAsync(DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        await _initializer.Value.ConfigureAwait(false);

        try
        {
            var response = await _client.From<ClientContactMessageRow>()
                .Get()
                .ConfigureAwait(false);

            var query = response.Models.AsEnumerable();
            if (date.HasValue)
            {
                query = query.Where(x => DateOnly.FromDateTime(x.CreatedAtUtc) == date.Value);
            }

            return query
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new ClientContactMessage
                {
                    Id = x.Id,
                    CreatedAtUtc = x.CreatedAtUtc,
                    Name = x.Name,
                    Email = x.Email,
                    Subject = x.Subject,
                    Message = x.Message
                })
                .ToList();
        }
        catch
        {
            return Array.Empty<ClientContactMessage>();
        }
    }

    [Table("client_contact_messages")]
    private sealed class ClientContactMessageRow : BaseModel
    {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("created_at_utc")]
        public DateTime CreatedAtUtc { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("subject")]
        public string Subject { get; set; } = string.Empty;

        [Column("message")]
        public string Message { get; set; } = string.Empty;
    }
}
