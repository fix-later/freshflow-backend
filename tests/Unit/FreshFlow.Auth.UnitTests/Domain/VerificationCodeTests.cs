using FluentAssertions;
using FreshFlow.Auth.Domain.Entities;

namespace FreshFlow.Auth.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class VerificationCodeTests
{
    [Fact]
    public void Create_SetsPropertiesCorrectly()
    {
        var userId = Guid.NewGuid();

        var code = VerificationCode.Create(userId, "EMAIL", "hashedcode");

        code.UserId.Should().Be(userId);
        code.Channel.Should().Be("EMAIL");
        code.CodeHash.Should().Be("hashedcode");
        code.UsedAt.Should().BeNull();
        code.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(10), TimeSpan.FromSeconds(5));
        code.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void IsExpired_ReturnsFalse_WhenNew()
    {
        var code = VerificationCode.Create(Guid.NewGuid(), "EMAIL", "hash");

        code.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsUsed_ReturnsFalse_BeforeMarkUsed()
    {
        var code = VerificationCode.Create(Guid.NewGuid(), "EMAIL", "hash");

        code.IsUsed.Should().BeFalse();
    }

    [Fact]
    public void IsValid_ReturnsTrue_WhenNotExpiredAndNotUsed()
    {
        var code = VerificationCode.Create(Guid.NewGuid(), "EMAIL", "hash");

        code.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MarkUsed_SetsUsedAt_AndIsUsedBecomesTrue()
    {
        var code = VerificationCode.Create(Guid.NewGuid(), "EMAIL", "hash");

        code.MarkUsed();

        code.IsUsed.Should().BeTrue();
        code.UsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void IsValid_ReturnsFalse_AfterMarkUsed()
    {
        var code = VerificationCode.Create(Guid.NewGuid(), "EMAIL", "hash");

        code.MarkUsed();

        code.IsValid.Should().BeFalse();
    }

    [Fact]
    public void MarkUsed_IsIdempotent()
    {
        var code = VerificationCode.Create(Guid.NewGuid(), "EMAIL", "hash");
        code.MarkUsed();
        var firstUsedAt = code.UsedAt;

        code.MarkUsed();

        code.UsedAt.Should().Be(firstUsedAt);
    }
}
