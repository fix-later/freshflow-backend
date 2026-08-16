using FluentAssertions;
using FreshFlow.Pricing.Application.Queries.SearchMarketProducts;

namespace FreshFlow.Pricing.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class SearchMarketProductsValidatorTests
{
    private readonly SearchMarketProductsQueryValidator _sut = new();

    [Fact]
    public async Task Validate_ValidQuery_PassesValidationAsync()
    {
        // Arrange
        var query = new SearchMarketProductsQuery(Guid.NewGuid(), SearchText: "tôm");

        // Act
        var result = await _sut.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyMarketId_FailsValidationAsync()
    {
        // Arrange
        var query = new SearchMarketProductsQuery(Guid.Empty, SearchText: "tôm");

        // Act
        var result = await _sut.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(SearchMarketProductsQuery.MarketId));
    }

    [Fact]
    public async Task Validate_EmptySearchText_FailsValidationAsync()
    {
        // Arrange
        var query = new SearchMarketProductsQuery(Guid.NewGuid(), SearchText: "");

        // Act
        var result = await _sut.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(SearchMarketProductsQuery.SearchText));
    }

    [Fact]
    public async Task Validate_SearchTextExceedsMaxLength_FailsValidationAsync()
    {
        // Arrange
        var query = new SearchMarketProductsQuery(Guid.NewGuid(), SearchText: new string('a', 201));

        // Act
        var result = await _sut.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(SearchMarketProductsQuery.SearchText));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(200)]
    public async Task Validate_PageSizeOutOfRange_FailsValidationAsync(int pageSize)
    {
        // Arrange
        var query = new SearchMarketProductsQuery(Guid.NewGuid(), SearchText: "tôm", PageSize: pageSize);

        // Act
        var result = await _sut.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(SearchMarketProductsQuery.PageSize));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task Validate_PageSizeInRange_PassesValidationAsync(int pageSize)
    {
        // Arrange
        var query = new SearchMarketProductsQuery(Guid.NewGuid(), SearchText: "tôm", PageSize: pageSize);

        // Act
        var result = await _sut.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithOptionalCategoryInStockOnlyAndCursor_PassesValidationAsync()
    {
        // Arrange
        var query = new SearchMarketProductsQuery(
            Guid.NewGuid(),
            SearchText: "tôm",
            Category: "thủy hải sản",
            InStockOnly: true,
            Cursor: "some-cursor-value",
            PageSize: 20);

        // Act
        var result = await _sut.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
