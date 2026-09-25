using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Caching;
using FreshFlow.Catalog.Application.Queries.Categories.GetCategories;
using FreshFlow.Catalog.Application.Queries.Units.GetUnits;
using FreshFlow.Catalog.Domain.Entities;
using Microsoft.Extensions.Caching.Hybrid;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Caching;

[Trait("Category", "Unit")]
public sealed class CatalogReferenceDataCachingTests
{
    private readonly IUnitOfMeasurementRepository _units = Substitute.For<IUnitOfMeasurementRepository>();
    private readonly IProductCategoryRepository _categories = Substitute.For<IProductCategoryRepository>();
    private readonly HybridCache _cache = TestCache.Create();

    private GetUnitsQueryHandler Units => new(_units, _cache);
    private GetCategoriesQueryHandler Categories => new(_categories, _cache);

    [Fact]
    public async Task reads_the_repository_once_when_the_same_query_runs_twice()
    {
        // Arrange
        _units.GetAllAsync(true, default).Returns([new UnitOfMeasurement("kg", "kg")]);

        // Act
        await Units.Handle(new GetUnitsQuery(ActiveOnly: true), default);
        var second = await Units.Handle(new GetUnitsQuery(ActiveOnly: true), default);

        // Assert
        await _units.Received(1).GetAllAsync(true, default);
        second.Value.Should().ContainSingle(u => u.Name == "kg");
    }

    [Fact]
    public async Task reads_the_repository_again_after_the_tag_is_evicted()
    {
        // Arrange — an admin edit evicts the tag between the two reads
        _units.GetAllAsync(true, default).Returns([new UnitOfMeasurement("kg", "kg")]);
        await Units.Handle(new GetUnitsQuery(ActiveOnly: true), default);

        _units.GetAllAsync(true, default).Returns([new UnitOfMeasurement("thùng", null)]);
        await TestCache.Evictor(_cache).EvictAsync(CatalogCache.UnitsTag);

        // Act
        var result = await Units.Handle(new GetUnitsQuery(ActiveOnly: true), default);

        // Assert — the reader sees the new value immediately, without waiting out the TTL
        result.Value.Should().ContainSingle(u => u.Name == "thùng");
        await _units.Received(2).GetAllAsync(true, default);
    }

    [Fact]
    public async Task caches_each_activeOnly_variant_under_its_own_key()
    {
        // Arrange
        _units.GetAllAsync(true, default).Returns([new UnitOfMeasurement("kg", "kg")]);
        _units.GetAllAsync(false, default).Returns([new UnitOfMeasurement("kg", "kg"), new UnitOfMeasurement("bó", null)]);

        // Act
        var activeOnly = await Units.Handle(new GetUnitsQuery(ActiveOnly: true), default);
        var all = await Units.Handle(new GetUnitsQuery(ActiveOnly: false), default);

        // Assert — a cached ActiveOnly=true entry must never be served for ActiveOnly=false
        activeOnly.Value.Should().HaveCount(1);
        all.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task evicting_one_family_leaves_another_family_cached()
    {
        // Arrange
        _units.GetAllAsync(true, default).Returns([new UnitOfMeasurement("kg", "kg")]);
        _categories.GetAllAsync(true, default).Returns([new ProductCategory("Rau", null, null)]);
        await Units.Handle(new GetUnitsQuery(ActiveOnly: true), default);
        await Categories.Handle(new GetCategoriesQuery(), default);

        // Act
        await TestCache.Evictor(_cache).EvictAsync(CatalogCache.UnitsTag);
        await Units.Handle(new GetUnitsQuery(ActiveOnly: true), default);
        await Categories.Handle(new GetCategoriesQuery(), default);

        // Assert
        await _units.Received(2).GetAllAsync(true, default);
        await _categories.Received(1).GetAllAsync(true, default);
    }

    [Fact]
    public async Task serves_reads_from_the_repository_when_the_cache_backend_is_down()
    {
        // Arrange — HybridCache swallows backing-store failures on the read path
        _units.GetAllAsync(true, default).Returns([new UnitOfMeasurement("kg", "kg")]);
        var handler = new GetUnitsQueryHandler(_units, TestCache.CreateWithFailingBackend());

        // Act
        var result = await handler.Handle(new GetUnitsQuery(ActiveOnly: true), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(u => u.Name == "kg");
    }

    [Fact]
    public async Task does_not_throw_when_eviction_fails_so_a_committed_write_still_succeeds()
    {
        // Arrange — unlike reads, RemoveByTagAsync propagates the backing-store failure. If that
        // escaped a command handler the API would answer 500 for a write that already committed.
        var evictor = TestCache.Evictor(TestCache.CreateWithFailingBackend());

        // Act
        var evict = async () => await evictor.EvictAsync(CatalogCache.UnitsTag);

        // Assert
        await evict.Should().NotThrowAsync();
    }
}
