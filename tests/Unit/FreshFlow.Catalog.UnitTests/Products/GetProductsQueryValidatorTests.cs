using FluentAssertions;
using FreshFlow.Catalog.Application.Queries.Products.GetProducts;

namespace FreshFlow.Catalog.UnitTests.Products;

[Trait("Category", "Unit")]
public sealed class GetProductsQueryValidatorTests
{
    private readonly GetProductsQueryValidator _sut = new();

    [Fact]
    public void Validate_ValidDefaults_Passes()
    {
        var result = _sut.Validate(new GetProductsQuery(null, null, false, 1, 20));
        result.IsValid.Should().BeTrue();
    }

    // ── Null page / pageSize are optional (docs: default 1 / default 20) ─────

    [Fact]
    public void Validate_NullPage_Passes()
    {
        var result = _sut.Validate(new GetProductsQuery(null, null, false, null, 20));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NullPageSize_Passes()
    {
        var result = _sut.Validate(new GetProductsQuery(null, null, false, 1, null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_BothNull_Passes()
    {
        var result = _sut.Validate(new GetProductsQuery(null, null, false, null, null));
        result.IsValid.Should().BeTrue();
    }

    // ── Explicit invalid values are still rejected ────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidPage_Fails(int page)
    {
        var result = _sut.Validate(new GetProductsQuery(null, null, false, page, 20));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Page");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidPageSize_Fails(int pageSize)
    {
        var result = _sut.Validate(new GetProductsQuery(null, null, false, 1, pageSize));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void Validate_PageSizeExceedsMax_Fails()
    {
        // Max was raised to 200 (SCRUM-126); 201 must still be rejected.
        var result = _sut.Validate(new GetProductsQuery(null, null, false, 1, 201));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void Validate_MaxPageSize_Passes()
    {
        // Max is 200 (SCRUM-126).
        var result = _sut.Validate(new GetProductsQuery(null, null, false, 1, 200));
        result.IsValid.Should().BeTrue();
    }
}
