using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Orders.Infrastructure;
using FreshFlow.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
    public void Model_RegistersRestaurantCreditEntity()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();

        // Act
        var entity = ctx.Model.FindEntityType(typeof(RestaurantCredit));

        // Assert
        entity.Should().NotBeNull("RestaurantCredit should be registered in the EF model");
    }

    [Fact]
    public void Model_RegistersCreditTransactionEntity()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();

        // Act
        var entity = ctx.Model.FindEntityType(typeof(CreditTransaction));

        // Assert
        entity.Should().NotBeNull("CreditTransaction should be registered in the EF model");
    }

    [Fact]
    public void Model_RegistersOrderIssueEntity()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();

        // Act
        var entity = ctx.Model.FindEntityType(typeof(OrderIssue));

        // Assert
        entity.Should().NotBeNull("OrderIssue should be registered in the EF model");
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

    [Fact]
    public void OrderConfiguration_ConfirmedReceiptAtUsesSnakeCaseColumn()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();

        // Act
        var orderEntity = ctx.Model.FindEntityType(typeof(Order));
        var prop = orderEntity!.FindProperty(nameof(Order.ConfirmedReceiptAt));

        // Assert
        prop.Should().NotBeNull();
        prop!.GetColumnName(StoreObjectIdentifier.Table("orders", null))
            .Should().Be("confirmed_receipt_at");
    }

    [Fact]
    public void OrderConfiguration_DeliverySnapshotUsesSnakeCaseColumns()
    {
        using var ctx = CreateInMemoryContext();
        var orderEntity = ctx.Model.FindEntityType(typeof(Order))!;
        var table = StoreObjectIdentifier.Table("orders", null);

        orderEntity.FindProperty(nameof(Order.DeliveryAddressId))!
            .GetColumnName(table).Should().Be("delivery_address_id");
        orderEntity.FindProperty(nameof(Order.DeliveryAddressLine))!
            .GetColumnName(table).Should().Be("delivery_address_line");
        orderEntity.FindProperty(nameof(Order.DeliveryLatitude))!
            .GetColumnName(table).Should().Be("delivery_latitude");
        orderEntity.FindProperty(nameof(Order.DeliveryLongitude))!
            .GetColumnName(table).Should().Be("delivery_longitude");
    }

    [Fact]
    public void OrderIssueConfiguration_UsesSnakeCaseTableAndColumns()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();

        // Act
        var issueEntity = ctx.Model.FindEntityType(typeof(OrderIssue));

        // Assert
        issueEntity.Should().NotBeNull();
        issueEntity!.GetTableName().Should().Be("order_issues");
        issueEntity.FindProperty(nameof(OrderIssue.OrderId))!
            .GetColumnName(StoreObjectIdentifier.Table("order_issues", null))
            .Should().Be("order_id");
        issueEntity.FindProperty(nameof(OrderIssue.OrderItemId))!
            .GetColumnName(StoreObjectIdentifier.Table("order_issues", null))
            .Should().Be("order_item_id");
        issueEntity.FindProperty(nameof(OrderIssue.ReportedBy))!
            .GetColumnName(StoreObjectIdentifier.Table("order_issues", null))
            .Should().Be("reported_by");
        issueEntity.FindProperty(nameof(OrderIssue.AffectedQuantity))!
            .GetColumnName(StoreObjectIdentifier.Table("order_issues", null))
            .Should().Be("affected_quantity");
    }

    [Fact]
    public void AddOrdersModule_RegistersCrossModuleReaders()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"test-{Guid.NewGuid()}"));

        // Act
        services.AddOrdersModule(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<IMarketProductReader>().Should().NotBeNull();
        provider.GetRequiredService<IRestaurantReader>().Should().NotBeNull();
    }

    [Fact]
    public void AddOrdersModule_RegistersCreditServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"test-{Guid.NewGuid()}"));

        // Act
        services.AddOrdersModule(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<ICreditRepository>().Should().NotBeNull();
        provider.GetRequiredService<ICreditService>().Should().NotBeNull();
    }

    [Fact]
    public void AddOrdersModule_RegistersOrderIssueRepository()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"test-{Guid.NewGuid()}"));

        // Act
        services.AddOrdersModule(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<IOrderIssueRepository>().Should().NotBeNull();
    }

    [Fact]
    public void AddOrdersModule_RegistersOrderBroadcastService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSignalR();

        // Act
        services.AddOrdersModule(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<IOrderBroadcastService>().Should().NotBeNull();
    }

    [Fact]
    public void AddOrdersModule_RegistersScheduledOrderGenerationServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"test-{Guid.NewGuid()}"));
        services.AddLogging();

        // Act
        services.AddOrdersModule(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<IScheduledOrderGenerationService>().Should().NotBeNull();
        provider.GetRequiredService<ICreditStatementGenerationService>().Should().NotBeNull();

        // ScheduledOrderGenerationHostedService (recurring orders) + MonthlyCreditStatementHostedService.
        provider.GetServices<IHostedService>().Should().HaveCount(2);
    }

    [Fact]
    public void RestaurantCreditConfiguration_UsesRestaurantIdPrimaryKey()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();

        // Act
        var entity = ctx.Model.FindEntityType(typeof(RestaurantCredit));
        var key = entity!.FindPrimaryKey();

        // Assert
        key.Should().NotBeNull();
        key!.Properties.Should().ContainSingle(p => p.Name == nameof(RestaurantCredit.RestaurantId));
        key.Properties.Single().ValueGenerated.Should().Be(ValueGenerated.Never);
    }

    [Fact]
    public void CreditTransactionConfiguration_DoesNotMapDeletedAt()
    {
        // Arrange
        using var ctx = CreateInMemoryContext();

        // Act
        var entity = ctx.Model.FindEntityType(typeof(CreditTransaction));
        var prop = entity!.FindProperty("DeletedAt");

        // Assert
        prop.Should().BeNull("credit_transactions is append-only and has no deleted_at column");
    }

    // Regression (SCRUM-264 review finding): a plain ToString().ToLowerInvariant() conversion
    // would silently persist "banktransfer" instead of "bank_transfer" — Enum.Parse(ignoreCase:
    // true) still round-trips it, so a round-trip-only test wouldn't have caught this. These
    // assert the literal provider-side (DB) string via the model's ValueConverter directly.
    [Theory]
    [InlineData(PaymentMethod.BankTransfer, "bank_transfer")]
    [InlineData(PaymentMethod.Manual, "manual")]
    public void CreditTransactionConfiguration_PaymentMethod_ConvertsToSnakeCaseProviderValue(
        PaymentMethod value, string expectedStored)
    {
        // Arrange
        using var ctx = CreateInMemoryContext();
        var entity = ctx.Model.FindEntityType(typeof(CreditTransaction));
        var converter = entity!.FindProperty(nameof(CreditTransaction.PaymentMethod))!.GetValueConverter();

        // Act
        var stored = converter!.ConvertToProvider(value);

        // Assert
        stored.Should().Be(expectedStored);
    }

    [Theory]
    [InlineData("bank_transfer", PaymentMethod.BankTransfer)]
    [InlineData("manual", PaymentMethod.Manual)]
    public void CreditTransactionConfiguration_PaymentMethod_ConvertsFromSnakeCaseProviderValue(
        string stored, PaymentMethod expected)
    {
        // Arrange
        using var ctx = CreateInMemoryContext();
        var entity = ctx.Model.FindEntityType(typeof(CreditTransaction));
        var converter = entity!.FindProperty(nameof(CreditTransaction.PaymentMethod))!.GetValueConverter();

        // Act
        var value = converter!.ConvertFromProvider(stored);

        // Assert
        value.Should().Be(expected);
    }

    [Fact]
    public async Task OrderRepository_AddItemToExistingOrder_PersistsNewOrderItemAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"test-{Guid.NewGuid()}"));
        services.AddOrdersModule(new ConfigurationBuilder().Build());

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var order = new Order(Guid.NewGuid(), scheduledFor: null, notes: null);

        await repository.AddAsync(order, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        // Act
        var loaded = await repository.FindByIdAsync(order.Id, CancellationToken.None);
        loaded.Should().NotBeNull();
        loaded!.AddItem(Guid.NewGuid(), "Cà chua", 3, 20_000m);
        var newItem = loaded.Items.Single();

        repository.TrackNewItem(newItem);
        repository.Track(loaded);
        await repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        var saved = await repository.FindByIdAsync(order.Id, CancellationToken.None);
        saved!.Items.Should().ContainSingle();
        saved.Items.Single().Quantity.Should().Be(3);
        saved.TotalAmount.Should().Be(60_000m);
    }

    [Fact]
    public async Task OrderRepository_SearchAsync_FiltersSortsAndPaginatesAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"test-{Guid.NewGuid()}"));
        services.AddOrdersModule(new ConfigurationBuilder().Build());

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var restaurantId = Guid.NewGuid();
        var otherRestaurantId = Guid.NewGuid();

        var excludedOld = NewConfirmedOrder(restaurantId, new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var firstMatch = NewConfirmedOrder(restaurantId, new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc));
        var secondMatch = NewConfirmedOrder(restaurantId, new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));
        var excludedDraft = new Order(restaurantId, scheduledFor: null, notes: null);
        SetCreatedAt(excludedDraft, new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc));
        var excludedOtherRestaurant =
            NewConfirmedOrder(otherRestaurantId, new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc));

        foreach (var order in new[] { excludedOld, firstMatch, secondMatch, excludedDraft, excludedOtherRestaurant })
            await repository.AddAsync(order, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        var criteria = new OrderSearchCriteria(
            restaurantId,
            OrderStatus.Confirmed,
            new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 13, 23, 59, 59, DateTimeKind.Utc),
            SortAscending: true,
            Page: 1,
            PageSize: 1);

        // Act
        var (orders, total) = await repository.SearchAsync(criteria, CancellationToken.None);

        // Assert
        total.Should().Be(2);
        orders.Should().ContainSingle();
        orders.Single().Id.Should().Be(firstMatch.Id);
        orders.Single().Items.Should().ContainSingle();
    }

    [Fact]
    public async Task OrderRepository_GetByScheduledOrderIdAsync_FiltersAndPaginatesAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"test-{Guid.NewGuid()}"));
        services.AddOrdersModule(new ConfigurationBuilder().Build());

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var restaurantId = Guid.NewGuid();
        var scheduledOrderId = Guid.NewGuid();
        var otherScheduledOrderId = Guid.NewGuid();

        var firstMatch = NewScheduledOrderInstance(
            restaurantId, scheduledOrderId, new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc));
        var secondMatch = NewScheduledOrderInstance(
            restaurantId, scheduledOrderId, new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));
        var excludedOtherSchedule = NewScheduledOrderInstance(
            restaurantId, otherScheduledOrderId, new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc));

        foreach (var order in new[] { firstMatch, secondMatch, excludedOtherSchedule })
            await repository.AddAsync(order, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        // Act
        var (orders, total) = await repository.GetByScheduledOrderIdAsync(
            scheduledOrderId, page: 1, pageSize: 1, CancellationToken.None);

        // Assert
        total.Should().Be(2);
        orders.Should().ContainSingle();
        orders.Single().Id.Should().Be(secondMatch.Id);
    }

    [Fact]
    public async Task ScheduledOrderRepository_SearchAsync_FiltersActiveAndPaginatesAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"test-{Guid.NewGuid()}"));
        services.AddOrdersModule(new ConfigurationBuilder().Build());

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IScheduledOrderRepository>();
        var restaurantId = Guid.NewGuid();
        var otherRestaurantId = Guid.NewGuid();

        var activeOld = NewScheduledOrder(restaurantId, new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var activeNew = NewScheduledOrder(restaurantId, new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc));
        var cancelled = NewScheduledOrder(restaurantId, new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc));
        cancelled.Cancel();
        var otherRestaurant = NewScheduledOrder(
            otherRestaurantId, new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

        foreach (var scheduledOrder in new[] { activeOld, activeNew, cancelled, otherRestaurant })
            await repository.AddAsync(scheduledOrder, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        var criteria = new ScheduledOrderSearchCriteria(
            restaurantId,
            IncludeCancelled: false,
            Page: 1,
            PageSize: 1);

        // Act
        var (scheduledOrders, total) = await repository.SearchAsync(criteria, CancellationToken.None);

        // Assert
        total.Should().Be(2);
        scheduledOrders.Should().ContainSingle();
        scheduledOrders.Single().Id.Should().Be(activeNew.Id);
    }

    [Fact]
    public async Task OrderIssueRepository_PersistsIssueAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"test-{Guid.NewGuid()}"));
        services.AddOrdersModule(new ConfigurationBuilder().Build());

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrderIssueRepository>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var issue = new OrderIssue(
            Guid.NewGuid(),
            orderItemId: null,
            reportedBy: Guid.NewGuid(),
            OrderIssueType.Missing,
            affectedQuantity: 1m,
            description: "missing item");

        // Act
        await repository.AddAsync(issue, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        var saved = await db.Set<OrderIssue>().SingleAsync();
        saved.Id.Should().Be(issue.Id);
        saved.IssueType.Should().Be(OrderIssueType.Missing);
        saved.Status.Should().Be(OrderIssueStatus.Open);
    }

    [Fact]
    public async Task CreditRepository_PersistsAccountAndTransactionsAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"test-{Guid.NewGuid()}"));
        services.AddOrdersModule(new ConfigurationBuilder().Build());

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICreditRepository>();
        var restaurantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var account = new RestaurantCredit(restaurantId, creditLimit: 1_000m);
        account.Charge(300m);

        // Act
        await repository.AddAccountAsync(account, CancellationToken.None);
        repository.AddTransaction(new CreditTransaction(
            restaurantId,
            orderId,
            CreditTransactionType.Charge,
            amount: 300m,
            balanceAfter: 300m,
            note: "Order confirmed"));
        await repository.SaveChangesAsync(CancellationToken.None);

        // Assert
        var savedAccount = await repository.FindAccountAsync(restaurantId, CancellationToken.None);
        var (savedTransactions, nextCursor) = await repository.GetTransactionsPageAsync(
            restaurantId, cursor: null, pageSize: 50, from: null, to: null, CancellationToken.None);
        savedAccount.Should().NotBeNull();
        savedAccount!.OutstandingBalance.Should().Be(300m);
        savedTransactions.Should().ContainSingle();
        savedTransactions.Single().Type.Should().Be(CreditTransactionType.Charge);
        nextCursor.Should().BeNull();
    }

    private static Order NewConfirmedOrder(Guid restaurantId, DateTime createdAt)
    {
        var order = new Order(restaurantId, scheduledFor: null, notes: null);
        order.AddItem(Guid.NewGuid(), "Cà chua", 3, 20_000m);
        order.Confirm();
        SetCreatedAt(order, createdAt);
        return order;
    }

    private static void SetCreatedAt(BaseEntity entity, DateTime createdAt)
    {
        typeof(BaseEntity)
            .GetField($"<{nameof(BaseEntity.CreatedAt)}>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(entity, createdAt);
    }

    private static Order NewScheduledOrderInstance(Guid restaurantId, Guid scheduledOrderId, DateTime createdAt)
    {
        var order = new Order(restaurantId, scheduledFor: null, notes: null, scheduledOrderId: scheduledOrderId);
        SetCreatedAt(order, createdAt);
        return order;
    }

    private static ScheduledOrder NewScheduledOrder(Guid restaurantId, DateTime createdAt)
    {
        var scheduledOrder = new ScheduledOrder(
            restaurantId,
            RecurrenceType.Daily,
            firstRunAt: createdAt.AddDays(1),
            notes: null);
        SetCreatedAt(scheduledOrder, createdAt);
        return scheduledOrder;
    }
}
