using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Pricing.Infrastructure.CrossModule;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Pricing.UnitTests.Persistence;

/// <summary>
/// Unit tests for <see cref="MarketProductReader.GetPageAsync"/> pageSize guard (Issue #7).
///
/// The guard must fire synchronously before any DB I/O, so an in-memory context is sufficient.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MarketProductReaderPageSizeTests
{
    private static MarketProductReader BuildSut(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var ctx = new AppDbContext(options);
        return new MarketProductReader(ctx);
    }

    [Fact]
    public async Task GetPageAsync_ZeroPageSize_ThrowsArgumentException()
    {
        // Arrange
        var sut = BuildSut($"db-{Guid.NewGuid()}");

        // Act
        var act = async () => await sut.GetPageAsync(
            Guid.NewGuid(), null, null, pageSize: 0, CancellationToken.None);

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
            Guid.NewGuid(), null, null, pageSize, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("pageSize");
    }

    [Fact]
    public async Task GetPageAsync_PositivePageSize_DoesNotThrow()
    {
        // Arrange — empty DB is fine; we only care that no exception is thrown
        var sut = BuildSut($"db-{Guid.NewGuid()}");

        // Act
        var act = async () => await sut.GetPageAsync(
            Guid.NewGuid(), null, null, pageSize: 1, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync<ArgumentException>();
    }
}
