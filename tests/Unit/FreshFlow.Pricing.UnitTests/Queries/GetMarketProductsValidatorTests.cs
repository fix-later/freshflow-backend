using FluentAssertions;
using FreshFlow.Pricing.Application.Queries.GetMarketProducts;

namespace FreshFlow.Pricing.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetMarketProductsValidatorTests
{
    private readonly GetMarketProductsQueryValidator _sut = new();

    [Fact]
    public async Task Validate_ValidQuery_PassesValidation()
    {
        // Arrange
        var query = new GetMarketProductsQuery(Guid.NewGuid(), PageSize: 20);

        // Act
        var result = await _sut.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyMarketId_FailsValidation()
    {
        // Arrange
        var query = new GetMarketProductsQuery(Guid.Empty);

        // Act
        var result = await _sut.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(GetMarketProductsQuery.MarketId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(200)]
    public async Task Validate_PageSizeOutOfRange_FailsValidation(int pageSize)
    {
        // Arrange
        var query = new GetMarketProductsQuery(Guid.NewGuid(), PageSize: pageSize);

        // Act
        var result = await _sut.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(GetMarketProductsQuery.PageSize));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task Validate_PageSizeInRange_PassesValidation(int pageSize)
    {
        // Arrange
        var query = new GetMarketProductsQuery(Guid.NewGuid(), PageSize: pageSize);

        // Act
        var result = await _sut.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithOptionalCursorAndCategory_PassesValidation()
    {
        // Arrange
        var query = new GetMarketProductsQuery(
            Guid.NewGuid(), Category: "thủy hải sản", Cursor: "some-cursor-value", PageSize: 20);

        // Act
        var result = await _sut.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
