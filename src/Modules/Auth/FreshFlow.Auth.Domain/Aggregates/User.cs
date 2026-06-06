using FreshFlow.Auth.Domain.Entities;
using FreshFlow.Auth.Domain.Events;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Auth.Domain.Aggregates;

public sealed class User : AggregateRoot
{
    private User() { }  // EF Core materialisation

    private User(string email, string passwordHash, Guid roleId, string? phone)
    {
        Email = email.ToLowerInvariant();
        PasswordHash = passwordHash;
        RoleId = roleId;
        Phone = phone?.Trim().ToLowerInvariant();
        IsActive = true;
    }

    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// Optional phone number stored in normalised (lowercased/trimmed) form.
    /// Used as a second login identifier alongside email.
    /// </summary>
    public string? Phone { get; private set; }

    public string PasswordHash { get; private set; } = string.Empty;
    public Guid RoleId { get; private set; }

    /// <summary>
    /// Navigation property populated by EF Core (Include) or by the <see cref="Create"/> factory.
    /// Always set when the user is loaded from the repository.
    /// </summary>
    public Role Role { get; private set; } = null!;

    public bool IsActive { get; private set; }

    /// <summary>
    /// Factory — sets both <see cref="RoleId"/> (FK) and the <see cref="Role"/> navigation so that
    /// the entity is fully usable in memory (e.g. in unit tests) without an EF context.
    /// </summary>
    public static User Create(string email, string passwordHash, Role role, string? phone = null)
    {
        var user = new User(email, passwordHash, role.Id, phone) { Role = role };
        user.RaiseDomainEvent(new UserCreatedDomainEvent(user.Id, user.Email, role.Name));
        return user;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool CanLogin() => IsActive && !IsDeleted;
}
