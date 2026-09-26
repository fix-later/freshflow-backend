using FluentAssertions;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Caching;
using FreshFlow.Pricing.Application.Commands.DeleteTag;
using FreshFlow.Pricing.Application.Queries.GetTags;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FreshFlow.Pricing.UnitTests.Caching;

[Trait("Category", "Unit")]
public sealed class TagListCachingTests
{
    private readonly ITagRepository _tags = Substitute.For<ITagRepository>();
    private readonly PricingCache _cache = CreateCache();

    [Fact]
    public async Task reads_the_tag_list_once_when_the_query_runs_twice()
    {
        // Arrange
        _tags.GetAllAsync(default).Returns([Tag.Create("tươi", true, null)]);
        var sut = new GetTagsQueryHandler(_tags, _cache);

        // Act
        await sut.Handle(new GetTagsQuery(), default);
        var second = await sut.Handle(new GetTagsQuery(), default);

        // Assert
        await _tags.Received(1).GetAllAsync(default);
        second.Value.Should().ContainSingle(t => t.Name == "tươi");
    }

    [Fact]
    public async Task shows_a_deleted_tag_as_gone_on_the_next_read()
    {
        // Arrange — the list is warm before the delete, so a stale entry exists to invalidate
        _tags.GetAllAsync(default).Returns([Tag.Create("tươi", true, null)]);
        var read = new GetTagsQueryHandler(_tags, _cache);
        await read.Handle(new GetTagsQuery(), default);

        var doomed = Tag.Create("tươi", true, null);
        _tags.FindByIdAsync(doomed.Id, default).Returns(doomed);
        _tags.GetAllAsync(default).Returns([]);

        // Act
        var deleted = await new DeleteTagCommandHandler(_tags, _cache)
            .Handle(new DeleteTagCommand(doomed.Id), default);

        // Assert
        deleted.IsSuccess.Should().BeTrue();
        var after = await read.Handle(new GetTagsQuery(), default);
        after.Value.Should().BeEmpty("deleting a tag must evict the cached list");
    }

    [Fact]
    public async Task does_not_throw_when_eviction_fails_so_a_committed_delete_still_succeeds()
    {
        // Arrange — RemoveByTagAsync propagates a dead backing store, unlike reads. If that
        // escaped, the API would answer 500 for a delete that had already committed.
        var cache = new PricingCache(
            new ServiceCollection()
                .AddSingleton<IDistributedCache, FailingDistributedCache>()
                .AddHybridCache().Services
                .BuildServiceProvider()
                .GetRequiredService<HybridCache>(),
            NullLogger<PricingCache>.Instance);

        // Act
        var evict = async () => await cache.EvictTagsAsync();

        // Assert
        await evict.Should().NotThrowAsync();
    }

    private static PricingCache CreateCache() =>
        new(new ServiceCollection().AddHybridCache().Services
                .BuildServiceProvider().GetRequiredService<HybridCache>(),
            NullLogger<PricingCache>.Instance);

    private sealed class FailingDistributedCache : IDistributedCache
    {
        private static InvalidOperationException Down() => new("cache backend is down");

        public byte[]? Get(string key) => throw Down();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Down();
        public void Refresh(string key) => throw Down();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Down();
        public void Remove(string key) => throw Down();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Down();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Down();
        public Task SetAsync(
            string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default) => throw Down();
    }
}
