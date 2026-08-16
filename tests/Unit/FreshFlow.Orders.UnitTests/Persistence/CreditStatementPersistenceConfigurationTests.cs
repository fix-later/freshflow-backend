using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Orders.UnitTests.Persistence;

/// <summary>
/// Unit tests verifying EF model configuration for <see cref="CreditStatement"/> and
/// <see cref="CreditStatementLine"/> (SCRUM-261).
/// </summary>
[Trait("Category", "Unit")]
public sealed class CreditStatementPersistenceConfigurationTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        // Force-load Orders.Infrastructure so ApplyConfigurationsFromAssembly discovers it.
        _ = typeof(FreshFlow.Orders.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public void Model_RegistersCreditStatementEntity()
    {
        using var ctx = CreateInMemoryContext();

        var entity = ctx.Model.FindEntityType(typeof(CreditStatement));

        entity.Should().NotBeNull("CreditStatement should be registered in the EF model");
    }

    [Fact]
    public void Model_RegistersCreditStatementLineEntity()
    {
        using var ctx = CreateInMemoryContext();

        var entity = ctx.Model.FindEntityType(typeof(CreditStatementLine));

        entity.Should().NotBeNull("CreditStatementLine should be registered in the EF model");
    }

    [Fact]
    public void CreditStatementConfiguration_HasUniqueIndexOnRestaurantIdAndPeriodStart()
    {
        using var ctx = CreateInMemoryContext();

        var entity = ctx.Model.FindEntityType(typeof(CreditStatement));
        var index = entity!.GetIndexes()
            .FirstOrDefault(i => i.Properties.Select(p => p.Name)
                .SequenceEqual([nameof(CreditStatement.RestaurantId), nameof(CreditStatement.PeriodStart)]));

        index.Should().NotBeNull(
            "exactly one statement must exist per (restaurant, period) — append-only + idempotent");
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void CreditStatementLineConfiguration_HasCascadingForeignKeyToCreditStatement()
    {
        using var ctx = CreateInMemoryContext();

        var lineEntity = ctx.Model.FindEntityType(typeof(CreditStatementLine));
        var fk = lineEntity!.GetForeignKeys()
            .FirstOrDefault(fk => fk.Properties.Any(p => p.Name == nameof(CreditStatementLine.CreditStatementId)));

        fk.Should().NotBeNull("CreditStatementLine.CreditStatementId should have a FK constraint to CreditStatement");
        fk!.PrincipalEntityType.ClrType.Should().Be(typeof(CreditStatement));
        fk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public void CreditStatementConfiguration_RestaurantIdIsRequired()
    {
        using var ctx = CreateInMemoryContext();

        var entity = ctx.Model.FindEntityType(typeof(CreditStatement));
        var prop = entity!.FindProperty(nameof(CreditStatement.RestaurantId));

        prop.Should().NotBeNull();
        prop!.IsNullable.Should().BeFalse();
    }
}
