using FluentAssertions;
using FreshFlow.Infrastructure.Persistence.Audit;

namespace FreshFlow.Infrastructure.Persistence.UnitTests.Audit;

[Trait("Category", "Unit")]
public sealed class GetAuditLogsQueryValidatorTests
{
    private readonly GetAuditLogsQueryValidator _sut = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Validate_InvalidPage_Fails(int page)
    {
        var result = await _sut.ValidateAsync(new GetAuditLogsQuery(null, null, null, null, null, page, 20));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetAuditLogsQuery.Page));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-1)]
    public async Task Validate_InvalidPageSize_Fails(int pageSize)
    {
        var result = await _sut.ValidateAsync(new GetAuditLogsQuery(null, null, null, null, null, 1, pageSize));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetAuditLogsQuery.PageSize));
    }

    [Fact]
    public async Task Validate_ValidQuery_Passes()
    {
        var result = await _sut.ValidateAsync(new GetAuditLogsQuery(null, null, null, null, null, 1, 20));
        result.IsValid.Should().BeTrue();
    }
}
