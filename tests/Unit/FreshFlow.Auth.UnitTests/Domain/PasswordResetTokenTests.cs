using FluentAssertions;
using FreshFlow.Auth.Domain.Entities;

namespace FreshFlow.Auth.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class PasswordResetTokenTests
{
    [Fact]
    public void Create_SetsPropertiesCorrectly()
    {
        var userId = Guid.NewGuid();
        const string hash = "abc123hash";

        var token = PasswordResetToken.Create(userId, hash);

        token.UserId.Should().Be(userId);
        token.TokenHash.Should().Be(hash);
        token.UsedAt.Should().BeNull();
        token.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
        token.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void IsExpired_ReturnsFalse_WhenTokenIsNew()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash");

        token.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsUsed_ReturnsFalse_BeforeMarkUsed()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash");

        token.IsUsed.Should().BeFalse();
    }

    [Fact]
    public void IsValid_ReturnsTrue_WhenNotExpiredAndNotUsed()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash");

        token.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MarkUsed_SetsUsedAt_AndIsUsedBecomesTrue()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash");

        token.MarkUsed();

        token.IsUsed.Should().BeTrue();
        token.UsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void IsValid_ReturnsFalse_AfterMarkUsed()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash");

        token.MarkUsed();

        token.IsValid.Should().BeFalse();
    }

    [Fact]
    public void MarkUsed_IsIdempotent()
    {
        var token = PasswordResetToken.Create(Guid.NewGuid(), "hash");
        token.MarkUsed();
        var firstUsedAt = token.UsedAt;

        token.MarkUsed();

        token.UsedAt.Should().Be(firstUsedAt);
    }
}
