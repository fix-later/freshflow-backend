using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Queries.Products.GetProducts;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Products;

[Trait("Category", "Unit")]
public sealed class GetProductsQueryHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly GetProductsQueryHandler _sut;

    public GetProductsQueryHandlerTests()
    {
        _sut = new GetProductsQueryHandler(_products);
    }

    [Fact]
    public async Task Handle_ReturnsPagedProducts()
    {
        // Arrange
        var unit = new UnitOfMeasurement("kg", "kg");
        var product = new Product("Cá lóc", unit.Id, null, null, null);
        var data = (IReadOnlyList<Product>)[product];
        _products.GetPagedAsync(null, null, false, 1, 20, default).Returns((data, 1));

        var query = new GetProductsQuery(null, null, false, 1, 20);

        // Act
        var result = await _sut.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].Name.Should().Be("Cá lóc");
        result.Value.Meta.Total.Should().Be(1);
        result.Value.Meta.Page.Should().Be(1);
        result.Value.Meta.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Handle_EmptyResult_ReturnsEmptyPage()
    {
        // Arrange
        _products.GetPagedAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Product>)[], 0));

        // Act
        var result = await _sut.Handle(new GetProductsQuery(null, null, false, 1, 20), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Data.Should().BeEmpty();
        result.Value.Meta.Total.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PassesFiltersToRepository()
    {
        // Arrange
        _products.GetPagedAsync("cá", "Hải sản", true, 2, 10, default)
            .Returns(((IReadOnlyList<Product>)[], 0));

        // Act
        var result = await _sut.Handle(
            new GetProductsQuery("cá", "Hải sản", true, 2, 10), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _products.Received(1).GetPagedAsync("cá", "Hải sản", true, 2, 10, default);
    }

    [Fact]
    public async Task Handle_NullPageAndPageSize_UsesDefaultsForRepoCallAndMeta()
    {
        // Arrange — page=1, pageSize=20 are the documented defaults
        _products.GetPagedAsync(null, null, false, 1, 20, default)
            .Returns(((IReadOnlyList<Product>)[], 0));

        var query = new GetProductsQuery(null, null, false, null, null);

        // Act
        var result = await _sut.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Meta.Page.Should().Be(1);
        result.Value.Meta.PageSize.Should().Be(20);
        await _products.Received(1).GetPagedAsync(null, null, false, 1, 20, default);
    }

    [Fact]
    public async Task Handle_NullPageOnly_UsesDefaultPage()
    {
        // Arrange
        _products.GetPagedAsync(null, null, false, 1, 50, default)
            .Returns(((IReadOnlyList<Product>)[], 0));

        var query = new GetProductsQuery(null, null, false, null, 50);

        // Act
        var result = await _sut.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Meta.Page.Should().Be(1);
        result.Value.Meta.PageSize.Should().Be(50);
        await _products.Received(1).GetPagedAsync(null, null, false, 1, 50, default);
    }

    [Fact]
    public async Task Handle_NullPageSizeOnly_UsesDefaultPageSize()
    {
        // Arrange
        _products.GetPagedAsync(null, null, false, 3, 20, default)
            .Returns(((IReadOnlyList<Product>)[], 0));

        var query = new GetProductsQuery(null, null, false, 3, null);

        // Act
        var result = await _sut.Handle(query, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Meta.Page.Should().Be(3);
        result.Value.Meta.PageSize.Should().Be(20);
        await _products.Received(1).GetPagedAsync(null, null, false, 3, 20, default);
    }
}
