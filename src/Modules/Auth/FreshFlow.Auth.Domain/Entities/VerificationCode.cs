namespace FreshFlow.Auth.Domain.Entities;

public sealed class VerificationCode
{
    private static readonly TimeSpan CodeTtl = TimeSpan.FromMinutes(10);

    private VerificationCode() { }

    private VerificationCode(Guid userId, string channel, string codeHash)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Channel = channel;
        CodeHash = codeHash;
        ExpiresAt = DateTime.UtcNow.Add(CodeTtl);
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Channel { get; private set; } = string.Empty;
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public bool IsExpired => ExpiresAt <= DateTime.UtcNow;
    public bool IsUsed => UsedAt.HasValue;
    public bool IsValid => !IsExpired && !IsUsed;

    public static VerificationCode Create(Guid userId, string channel, string codeHash) =>
        new(userId, channel, codeHash);

    public void MarkUsed()
    {
        if (UsedAt.HasValue) return;
        UsedAt = DateTime.UtcNow;
    }
}
