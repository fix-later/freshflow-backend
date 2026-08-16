using FluentAssertions;
using FreshFlow.Notifications.Application.Queries.ListNotifications;

namespace FreshFlow.Notifications.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class ListNotificationsQueryValidatorTests
{
    private readonly ListNotificationsQueryValidator _sut = new();

    [Fact]
    public async Task Validate_ValidCommand_PassesAsync()
    {
        var result = await _sut.ValidateAsync(new ListNotificationsQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyUserId_FailsAsync()
    {
        var result = await _sut.ValidateAsync(new ListNotificationsQuery(Guid.Empty));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListNotificationsQuery.UserId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(201)]
    public async Task Validate_InvalidPageSize_FailsAsync(int pageSize)
    {
        var result = await _sut.ValidateAsync(
            new ListNotificationsQuery(Guid.NewGuid(), PageSize: pageSize));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListNotificationsQuery.PageSize));
    }

    [Fact]
    public async Task Validate_MalformedCursor_DoesNotFailAsync()
    {
        var result = await _sut.ValidateAsync(
            new ListNotificationsQuery(Guid.NewGuid(), Cursor: "not-valid-base64-or-json"));

        result.IsValid.Should().BeTrue();
    }
}
