using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Orders.UnitTests.Persistence;

/// <summary>
/// Unit tests for <see cref="CreditRepository.GetTransactionsPageAsync"/> pageSize guard
/// and cursor resilience, mirroring PriceSnapshotRepositoryPageSizeTests (Pricing module).
///
/// The guard must fire before any DB I/O, so an in-memory context is sufficient.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CreditRepositoryPageSizeTests
{
    private static CreditRepository BuildSut(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var ctx = new AppDbContext(options);
        return new CreditRepository(ctx);
    }

    [Fact]
    public async Task GetTransactionsPageAsync_ZeroPageSize_ThrowsArgumentException()
    {
        // Arrange
        var sut = BuildSut($"db-{Guid.NewGuid()}");

        // Act
        var act = async () => await sut.GetTransactionsPageAsync(
            Guid.NewGuid(), null, 0, null, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("pageSize");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GetTransactionsPageAsync_NegativePageSize_ThrowsArgumentException(int pageSize)
    {
        // Arrange
        var sut = BuildSut($"db-{Guid.NewGuid()}");

        // Act
        var act = async () => await sut.GetTransactionsPageAsync(
            Guid.NewGuid(), null, pageSize, null, null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("pageSize");
    }

    [Fact]
    public async Task GetTransactionsPageAsync_PositivePageSize_DoesNotThrow()
    {
        // Arrange — empty DB is fine; we only care no guard exception fires
        var sut = BuildSut($"db-{Guid.NewGuid()}");

        // Act
        var act = async () => await sut.GetTransactionsPageAsync(
            Guid.NewGuid(), null, 1, null, null, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync<ArgumentException>();
    }

    // ── Cursor resilience (regression) ───────────────────────────────────────

    [Theory]
    [InlineData("not-valid-base64!@#$")]
    [InlineData("aGVsbG8=")] // valid base64 but not a valid TransactionCursor JSON
    [InlineData("")]
    public async Task GetTransactionsPageAsync_InvalidOrTamperedCursor_TreatedAsStartOfListAsync(string cursor)
    {
        // Arrange — a corrupt/tampered cursor must never throw; it should silently
        // fall back to the start of the list (same behaviour as cursor=null).
        var sut = BuildSut($"db-{Guid.NewGuid()}");

        // Act — empty DB, so first page = empty list regardless of cursor
        var act = async () => await sut.GetTransactionsPageAsync(
            Guid.NewGuid(), cursor, 10, null, null, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
