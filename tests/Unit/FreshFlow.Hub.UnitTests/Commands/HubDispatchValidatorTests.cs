using FluentAssertions;
using FreshFlow.Hub.Application.Commands.CreateCrossDock;
using FreshFlow.Hub.Application.Commands.RecordOutbound;
using FreshFlow.Hub.Application.Queries.ListCrossDock;
using FreshFlow.Hub.Application.Queries.ListOutbound;

namespace FreshFlow.Hub.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class HubDispatchValidatorTests
{
    [Fact]
    public void CreateCrossDockCommandValidator_ValidCommand_Passes()
    {
        var sut = new CreateCrossDockCommandValidator();

        var result = sut.Validate(new CreateCrossDockCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateCrossDockCommandValidator_EmptyInbound_Fails()
    {
        var sut = new CreateCrossDockCommandValidator();

        var result = sut.Validate(new CreateCrossDockCommand(Guid.NewGuid(), Guid.Empty, Guid.NewGuid()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCrossDockCommand.InboundEventId));
    }

    [Fact]
    public void RecordOutboundCommandValidator_ValidCommand_Passes()
    {
        var sut = new RecordOutboundCommandValidator();

        var result = sut.Validate(new RecordOutboundCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new HubOutboundItemCommand(Guid.NewGuid(), null, 1m)],
            DateTime.UtcNow));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RecordOutboundCommandValidator_ZeroQuantity_Fails()
    {
        var sut = new RecordOutboundCommandValidator();

        var result = sut.Validate(new RecordOutboundCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new HubOutboundItemCommand(Guid.NewGuid(), null, 0m)],
            DateTime.UtcNow));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.EndsWith(nameof(HubOutboundItemCommand.QuantityKg)));
    }

    [Fact]
    public void ListCrossDockQueryValidator_InvalidStatus_Fails()
    {
        var sut = new ListCrossDockQueryValidator();

        var result = sut.Validate(new ListCrossDockQuery(Guid.NewGuid(), "bad"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListCrossDockQuery.Status));
    }

    [Fact]
    public void ListOutboundQueryValidator_InvalidPageSize_Fails()
    {
        var sut = new ListOutboundQueryValidator();

        var result = sut.Validate(new ListOutboundQuery(Guid.NewGuid(), PageSize: 0));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListOutboundQuery.PageSize));
    }
}
