using FluentAssertions;
using FreshFlow.Hub.Application.Commands.RecordDiscrepancy;
using FreshFlow.Hub.Application.Queries.ListDiscrepancies;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class HubDiscrepancyValidatorTests
{
    [Fact]
    public void RecordDiscrepancyCommandValidator_ValidCommand_Passes()
    {
        var sut = new RecordDiscrepancyCommandValidator();

        var result = sut.Validate(new RecordDiscrepancyCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1m,
            HubDiscrepancy.ConditionDamaged,
            null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RecordDiscrepancyCommandValidator_InvalidConditionStatus_Fails()
    {
        var sut = new RecordDiscrepancyCommandValidator();

        var result = sut.Validate(new RecordDiscrepancyCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1m,
            "OK",
            null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RecordDiscrepancyCommand.ConditionStatus));
    }

    [Fact]
    public void RecordDiscrepancyCommandValidator_InvalidAffectedQuantity_Fails()
    {
        var sut = new RecordDiscrepancyCommandValidator();

        var result = sut.Validate(new RecordDiscrepancyCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            0m,
            HubDiscrepancy.ConditionDamaged,
            null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RecordDiscrepancyCommand.AffectedQuantity));
    }

    [Fact]
    public void ListDiscrepanciesQueryValidator_InvalidStatus_Fails()
    {
        var sut = new ListDiscrepanciesQueryValidator();

        var result = sut.Validate(new ListDiscrepanciesQuery(Guid.NewGuid(), "BAD"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListDiscrepanciesQuery.Status));
    }
}
