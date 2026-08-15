namespace FreshFlow.Auth.Domain.Entities;

/// <summary>
/// Single-use OTP credential issued by the forgot-password flow.
/// Append-only: created once, marked used on consumption, never deleted.
/// Expires after 15 minutes; issuing a new one must invalidate any pending prior credential.
/// </summary>
public sealed class PasswordResetToken
{
    private static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(15);

    private PasswordResetToken() { }  // EF Core materialisation

    private PasswordResetToken(Guid userId, string tokenHash)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = DateTime.UtcNow.Add(TokenTtl);
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>BCrypt hash of the OTP sent to the user.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }

    /// <summary>Set when the token is consumed by the reset-password flow.</summary>
    public DateTime? UsedAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public bool IsExpired => ExpiresAt <= DateTime.UtcNow;
    public bool IsUsed => UsedAt.HasValue;
    public bool IsValid => !IsExpired && !IsUsed;

    public static PasswordResetToken Create(Guid userId, string tokenHash) =>
        new(userId, tokenHash);

    public void MarkUsed()
    {
        if (UsedAt.HasValue) return;
        UsedAt = DateTime.UtcNow;
    }
}
