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
        var result = _sut.Validate(new GetProductsQuery(null, null, false, 1, 101));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void Validate_MaxPageSize_Passes()
    {
        var result = _sut.Validate(new GetProductsQuery(null, null, false, 1, 100));
        result.IsValid.Should().BeTrue();
    }
}
