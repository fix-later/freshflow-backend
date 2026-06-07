using FreshFlow.Auth.Domain.Aggregates;

namespace FreshFlow.Auth.Application.Abstractions;

public interface IUserRepository
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken ct);
    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Looks up a user by email address OR phone number (whichever matches the given identifier).
    /// The identifier is normalised (trimmed, lowercased) before comparison.
    /// </summary>
    public Task<User?> FindByIdentifierAsync(string identifier, CancellationToken ct);

    public Task<bool> ExistsAsync(string email, CancellationToken ct);

    /// <summary>Returns true if any non-deleted user already has this phone number.</summary>
    public Task<bool> ExistsByPhoneAsync(string phone, CancellationToken ct);

    public Task AddAsync(User user, CancellationToken ct);
    public Task SaveChangesAsync(CancellationToken ct);
    public Task<(IReadOnlyList<User> Data, int Total)> GetPagedAsync(
        string? role, bool? isActive, string? search,
        int page, int pageSize, CancellationToken ct);
}
