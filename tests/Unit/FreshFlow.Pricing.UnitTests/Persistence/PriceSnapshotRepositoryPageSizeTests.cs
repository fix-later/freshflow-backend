using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Pricing.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Pricing.UnitTests.Persistence;

/// <summary>
/// Unit tests for <see cref="PriceSnapshotRepository.GetPageAsync"/> pageSize guard (Issue #13).
///
/// The guard must fire before any DB I/O, so an in-memory context is sufficient.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PriceSnapshotRepositoryPageSizeTests
{
    private static PriceSnapshotRepository BuildSut(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var ctx = new AppDbContext(options);
        return new PriceSnapshotRepository(ctx);
    }

    [Fact]
    public async Task GetPageAsync_ZeroPageSize_ThrowsArgumentException()
    {
        // Arrange
        var sut = BuildSut($"db-{Guid.NewGuid()}");

        // Act
        var act = async () => await sut.GetPageAsync(
            Guid.NewGuid(), null, 0, null, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("pageSize");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GetPageAsync_NegativePageSize_ThrowsArgumentException(int pageSize)
    {
        // Arrange
        var sut = BuildSut($"db-{Guid.NewGuid()}");

        // Act
        var act = async () => await sut.GetPageAsync(
            Guid.NewGuid(), null, pageSize, null, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("pageSize");
    }

    [Fact]
    public async Task GetPageAsync_PositivePageSize_DoesNotThrow()
    {
        // Arrange — empty DB is fine; we only care no guard exception fires
        var sut = BuildSut($"db-{Guid.NewGuid()}");

        // Act
        var act = async () => await sut.GetPageAsync(
            Guid.NewGuid(), null, 1, null, null, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync<ArgumentException>();
    }
}
