using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Orders.UnitTests.Persistence;

/// <summary>
/// Unit tests verifying EF model configuration for the Orders module's Persistence layer.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PersistenceConfigurationTests
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
    public void Model_RegistersOrderEntity()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();

        // Act
        var entity = ctx.Model.FindEntityType(typeof(Order));

        // Assert
        entity.Should().NotBeNull("Order should be registered in the EF model");
    }

    [Fact]
    public void Model_RegistersOrderItemEntity()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();

        // Act
        var entity = ctx.Model.FindEntityType(typeof(OrderItem));

        // Assert
        entity.Should().NotBeNull("OrderItem should be registered in the EF model");
    }

    [Fact]
    public void Model_RegistersScheduledOrderEntity()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();

        // Act
        var entity = ctx.Model.FindEntityType(typeof(ScheduledOrder));

        // Assert
        entity.Should().NotBeNull("ScheduledOrder should be registered in the EF model");
    }

    [Fact]
    public void OrderItemConfiguration_HasForeignKeyToOrder()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();

        // Act
        var itemEntity = ctx.Model.FindEntityType(typeof(OrderItem));
        var fk = itemEntity!.GetForeignKeys()
            .FirstOrDefault(fk => fk.Properties.Any(p => p.Name == nameof(OrderItem.OrderId)));

        // Assert
        fk.Should().NotBeNull("OrderItem.OrderId should have a FK constraint to Order");
        fk!.PrincipalEntityType.ClrType.Should().Be(typeof(Order));
        fk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public void OrderItemConfiguration_DoesNotMapDeletedAt()
    {
        // Arrange — order_items has no soft delete column per DDL design decision
        using var ctx = CreateInMemoryContext();

        // Act
        var itemEntity = ctx.Model.FindEntityType(typeof(OrderItem));
        var prop = itemEntity!.FindProperty(nameof(OrderItem.DeletedAt));

        // Assert
        prop.Should().BeNull("order_items table has no deleted_at column");
    }

    [Fact]
    public void OrderConfiguration_RestaurantIdIsRequired()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();

        // Act
        var orderEntity = ctx.Model.FindEntityType(typeof(Order));
        var prop = orderEntity!.FindProperty(nameof(Order.RestaurantId));

        // Assert
        prop.Should().NotBeNull();
        prop!.IsNullable.Should().BeFalse();
    }
}
