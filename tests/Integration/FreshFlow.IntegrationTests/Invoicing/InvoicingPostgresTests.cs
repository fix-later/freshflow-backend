using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Xml.Linq;
using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Invoicing.Application.Abstractions;
using FreshFlow.Invoicing.Domain.Entities;
using FreshFlow.Invoicing.Domain.Enums;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Invoicing;

[Trait("Category", "Integration")]
public sealed class InvoicingPostgresTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private const string FivePercentProduct = "Five-percent greens";
    private const string KctProduct = "KCT fish";
    private const string UnknownVatProduct = "Unknown-VAT herbs";
    private const string DeletedProduct = "Deleted product";
    private const string DeletedMarketProduct = "Deleted market product";
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task OrderInvoiceReader_ExecutesPostgresProjectionAsync()
    {
        var seed = await SeedAsync();
        using var scope = factory.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IOrderInvoiceReader>();

        var snapshot = await reader.GetByOrderIdAsync(seed.OrderId, default);

        snapshot.Should().NotBeNull();
        snapshot!.RestaurantId.Should().Be(seed.RestaurantId);
        snapshot.Lines.Should().HaveCount(5);

        var fivePercent = snapshot.Lines.Should().ContainSingle(line =>
            line.ProductName == FivePercentProduct).Which;
        fivePercent.Quantity.Should().Be(2.5m);
        fivePercent.UnitPrice.Should().Be(11_000m);
        fivePercent.VatRateCode.Should().Be("5");
        fivePercent.Unit.Should().StartWith("kg-");

        var kct = snapshot.Lines.Should().ContainSingle(line => line.ProductName == KctProduct).Which;
        kct.Quantity.Should().Be(2m);
        kct.UnitPrice.Should().Be(20_000m);
        kct.VatRateCode.Should().Be("KCT");

        snapshot.Lines.Should().ContainSingle(line => line.ProductName == UnknownVatProduct)
            .Which.VatRateCode.Should().Be("KCT");

        var deletedProduct = snapshot.Lines.Should().ContainSingle(line =>
            line.ProductName == DeletedProduct).Which;
        deletedProduct.Quantity.Should().Be(1);
        deletedProduct.UnitPrice.Should().Be(1_000m);
        deletedProduct.VatRateCode.Should().Be("8");

        var deletedMarketProduct = snapshot.Lines.Should().ContainSingle(line =>
            line.ProductName == DeletedMarketProduct).Which;
        deletedMarketProduct.Quantity.Should().Be(1);
        deletedMarketProduct.UnitPrice.Should().Be(1_000m);
        deletedMarketProduct.VatRateCode.Should().Be("10");

        (await reader.GetByOrderIdAsync(seed.SoftDeletedOrderId, default)).Should().BeNull();
    }

    [Fact]
    public async Task RestaurantReader_ExecutesPostgresProjectionAsync()
    {
        var seed = await SeedAsync();
        using var scope = factory.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IRestaurantReader>();

        var profile = await reader.GetTaxProfileAsync(seed.RestaurantId, default);

        profile.Should().NotBeNull();
        profile!.TaxCode.Should().Be("0312345678");
        profile.LegalName.Should().Be("FreshFlow Integration Co.");
        profile.Address.Should().Be("123 Nguyễn Huệ, Quận 1, TP.HCM");
        profile.Email.Should().Be("invoice@integration.freshflow");
        (await reader.FindRestaurantIdByUserIdAsync(seed.UserId, default))
            .Should().Be(seed.RestaurantId);
        (await reader.FindRestaurantIdByUserIdAsync(seed.MissingProfileUserId, default))
            .Should().Be(seed.MissingProfileRestaurantId);
    }

    [Fact]
    public async Task IssueForDeliveredOrderAsync_PersistsOneIssuedInvoiceAsync()
    {
        var seed = await SeedAsync();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IInvoiceIssuanceService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await service.IssueForDeliveredOrderAsync(seed.OrderId, default);
        await service.IssueForDeliveredOrderAsync(seed.OrderId, default);

        (await db.Set<Invoice>().CountAsync(invoice => invoice.OrderId == seed.OrderId))
            .Should().Be(1);
        var invoice = await db.Set<Invoice>()
            .AsNoTracking()
            .Include(value => value.Lines)
            .SingleAsync(value => value.OrderId == seed.OrderId);
        invoice.Status.Should().Be(InvoiceStatus.Issued);
        invoice.TaxAuthorityCode.Should().StartWith("DEV-MCQT-");
        invoice.SubTotal.Should().Be(84_500m);
        invoice.VatAmount.Should().Be(1_555m);
        invoice.Total.Should().Be(86_055m);
        invoice.Lines.Should().HaveCount(5);

        var fivePercent = invoice.Lines.Should().ContainSingle(line =>
            line.ProductName == FivePercentProduct).Which;
        fivePercent.Quantity.Should().Be(2.5m);
        fivePercent.UnitPrice.Should().Be(11_000m);
        fivePercent.VatRateCode.Should().Be("5");
        fivePercent.Unit.Should().StartWith("kg-");
        fivePercent.LineSubtotal.Should().Be(27_500m);
        fivePercent.LineVatAmount.Should().Be(1_375m);
        fivePercent.LineTotal.Should().Be(28_875m);
        invoice.Lines.Should().ContainSingle(line =>
            line.ProductName == KctProduct && line.VatRateCode == "KCT");
        invoice.Lines.Should().ContainSingle(line =>
            line.ProductName == UnknownVatProduct && line.VatRateCode == "KCT");
        invoice.Lines.Should().ContainSingle(line =>
            line.ProductName == DeletedProduct &&
            line.VatRateCode == "8" &&
            line.LineSubtotal == 1_000m &&
            line.LineVatAmount == 80m &&
            line.LineTotal == 1_080m);
        invoice.Lines.Should().ContainSingle(line =>
            line.ProductName == DeletedMarketProduct &&
            line.VatRateCode == "10" &&
            line.LineSubtotal == 1_000m &&
            line.LineVatAmount == 100m &&
            line.LineTotal == 1_100m);
    }

    [Fact]
    public async Task IssueForDeliveredOrderAsync_InvalidMstPersistsPendingInvoiceAsync()
    {
        var seed = await SeedAsync();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IInvoiceIssuanceService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await service.IssueForDeliveredOrderAsync(seed.MissingProfileOrderId, default);

        var invoice = await db.Set<Invoice>()
            .AsNoTracking()
            .SingleAsync(value => value.OrderId == seed.MissingProfileOrderId);
        invoice.Status.Should().Be(InvoiceStatus.PendingIssuance);
        invoice.RetryCount.Should().Be(0);
        invoice.TaxAuthorityCode.Should().BeNull();
        invoice.ErrorReason.Should().Be("BUYER_TAX_CODE_INVALID");
    }

    [Fact]
    public async Task ExportIssuedInvoice_ReturnsPersistedStructuredDocumentAsync()
    {
        var seed = await SeedAsync();
        Guid invoiceId;
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IInvoiceIssuanceService>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await service.IssueForDeliveredOrderAsync(seed.OrderId, default);
            invoiceId = await db.Set<Invoice>()
                .Where(value => value.OrderId == seed.OrderId)
                .Select(value => value.Id)
                .SingleAsync();
        }

        var token = await LoginAsAdminAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.GetAsync($"/api/v1/invoices/{invoiceId}/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/xml");
        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
        document.Root!.Attribute("environment")!.Value.Should().Be("development");
        document.Root.Attribute("legalValue")!.Value.Should().Be("false");
        document.Root.Element("Header")!.Element("TaxAuthorityCode")!.Value.Should().StartWith("DEV-MCQT-");
        document.Root.Element("Buyer")!.Element("TaxCode")!.Value.Should().Be("0312345678");
        document.Root.Element("Lines")!.Elements("Line").Should().OnlyContain(line =>
            line.Element("Unit")!.Value.StartsWith("kg-", StringComparison.Ordinal));
    }

    private async Task<SeedData> SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var restaurants = scope.ServiceProvider.GetRequiredService<IRestaurantRepository>();
        var role = await db.Set<Role>().SingleAsync(value => value.Name == "restaurant");
        var suffix = Guid.NewGuid().ToString("N");
        var user = User.Create($"invoice-{suffix}@test.freshflow", "test-hash", role);
        var missingProfileUser = User.Create($"invoice-missing-{suffix}@test.freshflow", "test-hash", role);
        user.ClearDomainEvents();
        missingProfileUser.ClearDomainEvents();
        db.Set<User>().AddRange(user, missingProfileUser);
        await db.SaveChangesAsync();

        var restaurantId = await restaurants.CreateAsync(user.Id, "Invoice Restaurant", taxCode: null, invoiceLegalName: null, invoiceAddress: null, default);
        var missingProfileRestaurantId = await restaurants.CreateAsync(
            missingProfileUser.Id, "Missing Profile Restaurant", taxCode: null, invoiceLegalName: null, invoiceAddress: null, default);
        await restaurants.UpdateTaxProfileAsync(
            restaurantId,
            "0312345678",
            "FreshFlow Integration Co.",
            "123 Nguyễn Huệ, Quận 1, TP.HCM",
            "invoice@integration.freshflow",
            default);
        await restaurants.UpdateTaxProfileAsync(
            missingProfileRestaurantId,
            "031234567A",
            "Invalid MST Co.",
            "456 Lê Lợi, Quận 1, TP.HCM",
            null,
            default);

        var unit = new UnitOfMeasurement($"kg-{suffix}", "kg");
        var market = new Market($"Invoice Market {suffix}", "HCMC", "1 Test Street", null, null);
        db.Set<UnitOfMeasurement>().Add(unit);
        db.Set<Market>().Add(market);
        await db.SaveChangesAsync();

        var fivePercentProduct = new Product(
            FivePercentProduct, unit.Id, null, null, null, vatRate: "5");
        var kctProduct = new Product(KctProduct, unit.Id, null, null, null, vatRate: "KCT");
        var unknownVatProduct = new Product(UnknownVatProduct, unit.Id, null, null, null);
        var deletedProduct = new Product(DeletedProduct, unit.Id, null, null, null, vatRate: "8");
        var deletedMarketProductSource = new Product(
            DeletedMarketProduct, unit.Id, null, null, null, vatRate: "10");
        deletedProduct.Delete();
        db.Set<Product>().AddRange(
            fivePercentProduct,
            kctProduct,
            unknownVatProduct,
            deletedProduct,
            deletedMarketProductSource);
        await db.SaveChangesAsync();

        var fivePercentMarketProduct = new MarketProduct(
            market.Id, fivePercentProduct.Id, 10_000m, 100, null);
        var kctMarketProduct = new MarketProduct(market.Id, kctProduct.Id, 20_000m, 100, null);
        var unknownVatMarketProduct = new MarketProduct(
            market.Id, unknownVatProduct.Id, 15_000m, 100, null);
        var deletedProductMarketProduct = new MarketProduct(
            market.Id, deletedProduct.Id, 1_000m, 100, null);
        var deletedMarketProduct = new MarketProduct(
            market.Id, deletedMarketProductSource.Id, 1_000m, 100, null);
        db.Set<MarketProduct>().AddRange(
            fivePercentMarketProduct,
            kctMarketProduct,
            unknownVatMarketProduct,
            deletedProductMarketProduct,
            deletedMarketProduct);
        db.Entry(deletedMarketProduct).Property(nameof(MarketProduct.DeletedAt)).CurrentValue = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var order = CreateDeliveredOrder(
            restaurantId,
            fivePercentMarketProduct.Id,
            kctMarketProduct.Id,
            unknownVatMarketProduct.Id,
            deletedProductMarketProduct.Id,
            deletedMarketProduct.Id);
        var missingProfileOrder = CreateDeliveredOrder(
            missingProfileRestaurantId,
            fivePercentMarketProduct.Id,
            kctMarketProduct.Id,
            unknownVatMarketProduct.Id,
            deletedProductMarketProduct.Id,
            deletedMarketProduct.Id);
        var softDeletedOrder = CreateDeliveredOrder(
            restaurantId,
            fivePercentMarketProduct.Id,
            kctMarketProduct.Id,
            unknownVatMarketProduct.Id,
            deletedProductMarketProduct.Id,
            deletedMarketProduct.Id);
        db.Set<Order>().AddRange(order, missingProfileOrder, softDeletedOrder);
        db.Entry(softDeletedOrder).Property(nameof(Order.DeletedAt)).CurrentValue = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new SeedData(
            user.Id,
            restaurantId,
            missingProfileUser.Id,
            missingProfileRestaurantId,
            order.Id,
            missingProfileOrder.Id,
            softDeletedOrder.Id);
    }

    private static Order CreateDeliveredOrder(
        Guid restaurantId,
        Guid fivePercentMarketProductId,
        Guid kctMarketProductId,
        Guid unknownVatMarketProductId,
        Guid deletedProductMarketProductId,
        Guid deletedMarketProductId)
    {
        var order = new Order(restaurantId, null, null);
        order.AddItem(fivePercentMarketProductId, FivePercentProduct, 3, 10_000m)
            .IsSuccess.Should().BeTrue();
        order.AddItem(kctMarketProductId, KctProduct, 2, 20_000m)
            .IsSuccess.Should().BeTrue();
        order.AddItem(unknownVatMarketProductId, UnknownVatProduct, 1, 15_000m)
            .IsSuccess.Should().BeTrue();
        order.AddItem(deletedProductMarketProductId, DeletedProduct, 1, 1_000m)
            .IsSuccess.Should().BeTrue();
        order.AddItem(deletedMarketProductId, DeletedMarketProduct, 1, 1_000m)
            .IsSuccess.Should().BeTrue();
        order.ApplyConfirmationPricing(
            new Dictionary<Guid, OrderItemTaxSnapshot>
            {
                [fivePercentMarketProductId] = new("5", 5m),
                [kctMarketProductId] = new("KCT", 0m),
                [unknownVatMarketProductId] = new("KCT", 0m),
                [deletedProductMarketProductId] = new("8", 8m),
                [deletedMarketProductId] = new("10", 10m)
            },
            deliveryDistanceKm: 0m,
            deliveryFee: 0m).IsSuccess.Should().BeTrue();
        order.Confirm().IsSuccess.Should().BeTrue();

        var fivePercentLine = order.Items.Single(item => item.ProductNameSnapshot == FivePercentProduct);
        fivePercentLine.LockPricing(12_000m, "5", 5m);
        order.AdvanceStatus(OrderStatus.Batched).IsSuccess.Should().BeTrue();
        order.ApplyProcurementActuals(
            new Dictionary<Guid, OrderItemProcurementActual>
            {
                [fivePercentLine.Id] = new(2.5m, 11_000m)
            }).IsSuccess.Should().BeTrue();
        foreach (var status in new[]
                 {
                     OrderStatus.PickedUp,
                     OrderStatus.AtHub,
                     OrderStatus.Delivering,
                     OrderStatus.Delivered
                 })
            order.AdvanceStatus(status).IsSuccess.Should().BeTrue();

        order.ClearDomainEvents();
        return order;
    }

    private async Task<string> LoginAsAdminAsync()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { identifier = "admin@test.freshflow", password = "AdminP@ss1" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>())!.Data!.AccessToken;
    }

    private sealed record SeedData(
        Guid UserId,
        Guid RestaurantId,
        Guid MissingProfileUserId,
        Guid MissingProfileRestaurantId,
        Guid OrderId,
        Guid MissingProfileOrderId,
        Guid SoftDeletedOrderId);
}
