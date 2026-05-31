using FluentAssertions;
using FreshFlow.Auth.Infrastructure.Services;

namespace FreshFlow.Auth.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _sut = new();

    [Fact]
    public void Hash_ReturnsNonEmptyString()
    {
        var hash = _sut.Hash("MyP@ssword1");
        hash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Hash_TwoCalls_ProduceDifferentHashes()
    {
        var hash1 = _sut.Hash("MyP@ssword1");
        var hash2 = _sut.Hash("MyP@ssword1");
        hash1.Should().NotBe(hash2, "bcrypt uses a random salt per call");
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        const string password = "C0rrectP@ss!";
        var hash = _sut.Hash(password);
        _sut.Verify(password, hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _sut.Hash("OriginalP@ss1");
        _sut.Verify("WrongP@ss1", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_EmptyPassword_ReturnsFalse()
    {
        var hash = _sut.Hash("RealP@ss1");
        _sut.Verify(string.Empty, hash).Should().BeFalse();
    }
}
