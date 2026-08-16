using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Pricing.Domain.Entities;
using FreshFlow.Pricing.Infrastructure.CrossModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Pricing.UnitTests.Persistence;

/// <summary>
/// Unit tests verifying EF model configuration and DI registration behaviour
/// for the Persistence layer.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PersistenceConfigurationTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an AppDbContext using the InMemory provider so the EF conceptual
    /// model (entity relationships, FK constraints) can be inspected without
    /// a real database connection.
    ///
    /// NOTE: Relational metadata such as column names and filter strings require
    /// a relational provider (Npgsql); those are covered by migration output
    /// verification rather than this unit test.
    /// </summary>
    private static AppDbContext CreateInMemoryContext()
    {
        // Force-load Pricing.Infrastructure so ApplyConfigurationsFromAssembly discovers it.
        _ = typeof(FreshFlow.Pricing.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    // ── Fix #2 — Connection string null validation ─────────────────────────────

    [Fact]
    public void AddPersistence_WhenConnectionStringMissing_ThrowsInvalidOperationExceptionOnResolve()
    {
        // Arrange — configuration with no ConnectionStrings section at all
        var config = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddPersistence(config);

        // Act — resolving AppDbContext triggers the deferred lambda that validates the string
        var provider = services.BuildServiceProvider();
        var act = () => provider.GetRequiredService<AppDbContext>();

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*DefaultConnection*");
    }

    [Fact]
    public void AddPersistence_WhenConnectionStringEmpty_ThrowsInvalidOperationExceptionOnResolve()
    {
        // Arrange — config with an empty string for the connection string key
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ""
            })
            .Build();

        var services = new ServiceCollection();
        services.AddPersistence(config);

        var provider = services.BuildServiceProvider();
        var act = () => provider.GetRequiredService<AppDbContext>();

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*DefaultConnection*");
    }

    // ── Fix #11 — PriceSnapshot → MarketProduct FK in EF conceptual model ────

    [Fact]
    public void PriceSnapshotConfiguration_HasForeignKeyToMarketProduct()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();

        // Act
        var snapshotEntity = ctx.Model.FindEntityType(typeof(PriceSnapshot));

        // Assert — FK from PriceSnapshot.MarketProductId → MarketProduct.Id must exist
        snapshotEntity.Should().NotBeNull("PriceSnapshot entity should be registered in the model");

        var foreignKeys = snapshotEntity!.GetForeignKeys().ToList();
        var mpFk = foreignKeys.FirstOrDefault(fk =>
            fk.Properties.Any(p => p.Name == nameof(PriceSnapshot.MarketProductId)));

        mpFk.Should().NotBeNull(
            "PriceSnapshot.MarketProductId should have a FK constraint to MarketProduct");

        mpFk!.PrincipalEntityType.ClrType.Should().Be(
            typeof(MarketProduct),
            "FK principal should be MarketProduct");

        mpFk.DeleteBehavior.Should().Be(
            DeleteBehavior.Cascade,
            "price_snapshots ON DELETE CASCADE matches the schema DDL");
    }

    [Fact]
    public void PriceSnapshotConfiguration_MarketProductIdIsRequired()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();

        // Act
        var snapshotEntity = ctx.Model.FindEntityType(typeof(PriceSnapshot));
        var prop = snapshotEntity!.FindProperty(nameof(PriceSnapshot.MarketProductId));

        // Assert — FK column must be NOT NULL
        prop.Should().NotBeNull();
        prop!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ProductDetailRowConfiguration_ProjectsSellingUnitInOneQuery()
    {
        using var ctx = CreateInMemoryContext();

        var sql = ctx.Model.FindEntityType(typeof(ProductDetailRow))!.GetSqlQuery();

        sql.Should().Contain("packing_codes");
        sql.Should().Contain("CapacityKg");
    }
}
