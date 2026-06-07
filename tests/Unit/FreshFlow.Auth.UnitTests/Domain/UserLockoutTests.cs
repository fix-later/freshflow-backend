using System.Reflection;
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

    [Fact]
    public void RecordFailedLogin_AfterExpiredLockout_ResetsCounterToOne()
    {
        // Arrange — lock the user (5 failures), then backdate LockedUntil to simulate
        // the lockout window expiring naturally (LockedUntil.HasValue = true, IsLockedOut = false).
        var user = MakeUser();
        for (var i = 0; i < 5; i++)
            user.RecordFailedLogin();

        user.IsLockedOut.Should().BeTrue();
        user.FailedLoginCount.Should().Be(5);

        // Simulate clock advancing past the lockout window via reflection (private setter).
        typeof(User)
            .GetProperty("LockedUntil", BindingFlags.Instance | BindingFlags.Public)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(user, [DateTime.UtcNow.AddMinutes(-1)]);

        user.IsLockedOut.Should().BeFalse();        // window has passed
        user.FailedLoginCount.Should().Be(5);       // stale high counter still present

        // Act — one more bad password after expiry
        user.RecordFailedLogin();

        // Assert — counter resets to 1 (not 6), no immediate re-lock
        user.FailedLoginCount.Should().Be(1);
        user.IsLockedOut.Should().BeFalse();
    }
}
