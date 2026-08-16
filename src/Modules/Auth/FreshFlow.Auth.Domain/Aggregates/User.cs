using FreshFlow.Auth.Domain.Entities;
using FreshFlow.Auth.Domain.Events;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Auth.Domain.Aggregates;

public sealed class User : AggregateRoot
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    private User() { }  // EF Core materialisation

    private User(string email, string passwordHash, Guid roleId, string? phone, string? fullName)
    {
        Email = email.ToLowerInvariant();
        PasswordHash = passwordHash;
        RoleId = roleId;
        Phone = phone?.Trim().ToLowerInvariant();
        FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim();
        IsActive = true;
    }

    public string Email { get; private set; } = string.Empty;

    /// <summary>Optional display name.</summary>
    public string? FullName { get; private set; }

    /// <summary>URL of the user's profile picture — set by the user via UpdateProfile.</summary>
    public string? AvatarUrl { get; private set; }

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

    /// <summary>Consecutive failed login attempts since the last successful login.</summary>
    public int FailedLoginCount { get; private set; }

    /// <summary>When set, the account is locked until this UTC time.</summary>
    public DateTime? LockedUntil { get; private set; }

    /// <summary>True while the lockout window has not yet expired.</summary>
    public bool IsLockedOut => LockedUntil.HasValue && LockedUntil > DateTime.UtcNow;

    /// <summary>
    /// Factory — sets both <see cref="RoleId"/> (FK) and the <see cref="Role"/> navigation so that
    /// the entity is fully usable in memory (e.g. in unit tests) without an EF context.
    /// </summary>
    public static User Create(
        string email,
        string passwordHash,
        Role role,
        string? phone = null,
        string? fullName = null)
    {
        var user = new User(email, passwordHash, role.Id, phone, fullName) { Role = role };
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

    /// <summary>
    /// Records a failed login attempt and locks the account when <see cref="MaxFailedAttempts"/> is reached.
    /// If a previous lockout window has already expired, the counter is reset to zero first so that
    /// stale failed-attempt counts don't cause an immediate re-lock on the very next bad password.
    /// </summary>
    public void RecordFailedLogin()
    {
        if (LockedUntil.HasValue && !IsLockedOut)   // lockout window has passed
        {
            FailedLoginCount = 0;
            LockedUntil = null;
        }

        FailedLoginCount++;
        if (FailedLoginCount >= MaxFailedAttempts)
            LockedUntil = DateTime.UtcNow.Add(LockDuration);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Resets the failed-attempt counter and clears the lockout timestamp on a successful
    /// user-initiated authentication. Called by the login flow after password verification passes.
    /// </summary>
    public void RecordSuccessfulLogin()
    {
        FailedLoginCount = 0;
        LockedUntil = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Admin-initiated unlock: clears both the counter and the lockout timestamp.
    /// Semantically distinct from <see cref="RecordSuccessfulLogin"/> — this is an explicit
    /// override by an administrator, not a consequence of a successful authentication attempt.
    /// </summary>
    public void Unlock()
    {
        FailedLoginCount = 0;
        LockedUntil = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Assigns a new global role. Caller must revoke all active refresh tokens after this call
    /// so that subsequent JWT claims reflect the new role.
    /// </summary>
    public void AssignRole(Role newRole)
    {
        RoleId = newRole.Id;
        Role = newRole;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates mutable profile fields. Null arguments clear the corresponding field.
    /// Phone is normalised (trimmed + lowercased) the same way as the constructor.
    /// Email and Role are intentionally excluded — use dedicated operations for those.
    /// </summary>
    public void UpdateProfile(string? fullName, string? phone, string? avatarUrl)
    {
        FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim().ToLowerInvariant();
        AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public DateTime? EmailVerifiedAt { get; private set; }

    public void MarkEmailVerified()
    {
        if (EmailVerifiedAt.HasValue) return;
        EmailVerifiedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns true when the account is in a state that allows session operations (refresh, etc.).
    /// Lockout is intentionally excluded: the <c>LoginCommandHandler</c> already checks
    /// <see cref="IsLockedOut"/> explicitly before password verification, and removing it here
    /// prevents an attacker from DoS-ing a victim's refresh sessions by repeatedly hammering the
    /// login endpoint to keep the lockout window active.
    /// </summary>
    public bool CanLogin() => IsActive && !IsDeleted;
}
