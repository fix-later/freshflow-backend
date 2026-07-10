using FluentAssertions;
using FreshFlow.Hub.Application.Commands.CreateHandover;
using FreshFlow.Hub.Application.Commands.DriverCheckout;
using FreshFlow.Hub.Application.Queries.ListHandovers;

namespace FreshFlow.Hub.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class HubHandoverValidatorTests
{
    [Fact]
    public void CreateHandoverCommandValidator_ValidCommand_Passes()
    {
        var sut = new CreateHandoverCommandValidator();

        var result = sut.Validate(new CreateHandoverCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateHandoverCommandValidator_EmptyDriver_Fails()
    {
        var sut = new CreateHandoverCommandValidator();

        var result = sut.Validate(new CreateHandoverCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.Empty,
            null,
            Guid.NewGuid()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateHandoverCommand.DriverUserId));
    }

    [Fact]
    public void DriverCheckoutCommandValidator_EmptyHandover_Fails()
    {
        var sut = new DriverCheckoutCommandValidator();

        var result = sut.Validate(new DriverCheckoutCommand(Guid.NewGuid(), Guid.Empty, Guid.NewGuid()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(DriverCheckoutCommand.HandoverId));
    }

    [Fact]
    public void ListHandoversQueryValidator_InvalidPageSize_Fails()
    {
        var sut = new ListHandoversQueryValidator();

        var result = sut.Validate(new ListHandoversQuery(Guid.NewGuid(), PageSize: 0));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListHandoversQuery.PageSize));
    }
}
