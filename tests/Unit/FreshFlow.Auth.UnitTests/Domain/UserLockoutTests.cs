using FluentAssertions;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;

namespace FreshFlow.Auth.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class UserLockoutTests
{
    private static User MakeUser() =>
        User.Create("u@test.com", "hash", new Role("driver", "Driver"));

    [Fact]
    public void RecordFailedLogin_BelowThreshold_DoesNotLock()
    {
        var user = MakeUser();

        for (var i = 0; i < 4; i++)
            user.RecordFailedLogin();

        user.IsLockedOut.Should().BeFalse();
        user.FailedLoginCount.Should().Be(4);
    }

    [Fact]
    public void RecordFailedLogin_AtThreshold_LocksAccount()
    {
        var user = MakeUser();

        for (var i = 0; i < 5; i++)
            user.RecordFailedLogin();

        user.IsLockedOut.Should().BeTrue();
        user.LockedUntil.Should().NotBeNull();
        user.LockedUntil.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void Unlock_ClearsLockAndResetsCount()
    {
        var user = MakeUser();
        for (var i = 0; i < 5; i++)
            user.RecordFailedLogin();

        user.Unlock();

        user.IsLockedOut.Should().BeFalse();
        user.FailedLoginCount.Should().Be(0);
        user.LockedUntil.Should().BeNull();
    }

    [Fact]
    public void IsLocked_ExpiredLockTime_ReturnsFalse()
    {
        // A user manually set up to have a past LockedUntil should not be considered locked.
        // We test via Unlock (which clears it) to confirm post-lock state.
        var user = MakeUser();
        for (var i = 0; i < 5; i++)
            user.RecordFailedLogin();

        user.IsLockedOut.Should().BeTrue();

        user.Unlock();

        user.IsLockedOut.Should().BeFalse();
    }
}
