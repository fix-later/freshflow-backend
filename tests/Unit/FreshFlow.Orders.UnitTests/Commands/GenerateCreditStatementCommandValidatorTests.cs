using FluentAssertions;
using FreshFlow.Orders.Application.Commands.GenerateCreditStatement;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class GenerateCreditStatementCommandValidatorTests
{
    private readonly GenerateCreditStatementCommandValidator _sut = new();

    [Fact]
    public async Task Validate_ValidCommand_PassesAsync()
    {
        var result = await _sut.ValidateAsync(
            new GenerateCreditStatementCommand(Guid.NewGuid(), IsAdmin: false, Guid.NewGuid(), 2026, 6));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyUserId_FailsAsync()
    {
        var result = await _sut.ValidateAsync(
            new GenerateCreditStatementCommand(Guid.Empty, IsAdmin: false, Guid.NewGuid(), 2026, 6));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(GenerateCreditStatementCommand.UserId));
    }

    [Fact]
    public async Task Validate_EmptyRestaurantId_FailsAsync()
    {
        var result = await _sut.ValidateAsync(
            new GenerateCreditStatementCommand(Guid.NewGuid(), IsAdmin: false, Guid.Empty, 2026, 6));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(GenerateCreditStatementCommand.RestaurantId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public async Task Validate_MonthOutOfRange_FailsAsync(int month)
    {
        var result = await _sut.ValidateAsync(
            new GenerateCreditStatementCommand(Guid.NewGuid(), IsAdmin: false, Guid.NewGuid(), 2026, month));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(GenerateCreditStatementCommand.Month));
    }

    [Theory]
    [InlineData(2019)]
    [InlineData(2101)]
    public async Task Validate_YearOutOfRange_FailsAsync(int year)
    {
        var result = await _sut.ValidateAsync(
            new GenerateCreditStatementCommand(Guid.NewGuid(), IsAdmin: false, Guid.NewGuid(), year, 6));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(GenerateCreditStatementCommand.Year));
    }
}
