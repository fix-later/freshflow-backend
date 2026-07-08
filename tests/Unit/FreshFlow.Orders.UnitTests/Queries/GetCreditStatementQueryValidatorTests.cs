using FluentAssertions;
using FreshFlow.Orders.Application.Queries.GetCreditStatement;

namespace FreshFlow.Orders.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetCreditStatementQueryValidatorTests
{
    private readonly GetCreditStatementQueryValidator _sut = new();

    [Fact]
    public async Task Validate_ByStatementId_PassesAsync()
    {
        var result = await _sut.ValidateAsync(
            new GetCreditStatementQuery(Guid.NewGuid(), IsAdmin: false, Guid.NewGuid(), StatementId: Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ByPeriod_PassesAsync()
    {
        var result = await _sut.ValidateAsync(
            new GetCreditStatementQuery(Guid.NewGuid(), IsAdmin: false, Guid.NewGuid(), Year: 2026, Month: 6));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_NeitherStatementIdNorPeriod_FailsAsync()
    {
        var result = await _sut.ValidateAsync(
            new GetCreditStatementQuery(Guid.NewGuid(), IsAdmin: false, Guid.NewGuid()));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_BothStatementIdAndPeriod_FailsAsync()
    {
        var result = await _sut.ValidateAsync(
            new GetCreditStatementQuery(
                Guid.NewGuid(), IsAdmin: false, Guid.NewGuid(), StatementId: Guid.NewGuid(), Year: 2026, Month: 6));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_PeriodWithMonthOutOfRange_FailsAsync()
    {
        var result = await _sut.ValidateAsync(
            new GetCreditStatementQuery(Guid.NewGuid(), IsAdmin: false, Guid.NewGuid(), Year: 2026, Month: 13));

        result.Errors.Should().Contain(e => e.PropertyName == "Month");
    }
}
