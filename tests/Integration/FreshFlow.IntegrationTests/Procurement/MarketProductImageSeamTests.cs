using FluentAssertions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Pricing.Domain.Entities;
using FreshFlow.Procurement.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Procurement;

// The image seam is a keyless ToSqlQuery join (market_products -> products); ToSqlQuery does not
// run on the EF InMemory provider, so it is only actually proven against real Postgres here.
[Trait("Category", "Integration")]
public sealed class MarketProductImageSeamTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    [Fact]
    public async Task ReadImages_ResolvesProductImageUrl_AndOmitsNullImageAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var unit = new UnitOfMeasurement($"kg-{Guid.NewGuid():N}", "kg");
        db.Set<UnitOfMeasurement>().Add(unit);
        await db.SaveChangesAsync();

        var withImage = new Product($"Imaged {Guid.NewGuid():N}", unit.Id, null, null, null);
        withImage.Update("Imaged", null, unit.Id, null, imageUrl: "https://img/tomato.jpg");
        var withoutImage = new Product($"Plain {Guid.NewGuid():N}", unit.Id, null, null, null);
        db.Set<Product>().AddRange(withImage, withoutImage);
        await db.SaveChangesAsync();

        var mpWithImage = new MarketProduct(Guid.NewGuid(), withImage.Id, 10_000m, 100, null);
        var mpWithoutImage = new MarketProduct(Guid.NewGuid(), withoutImage.Id, 10_000m, 100, null);
        db.Set<MarketProduct>().AddRange(mpWithImage, mpWithoutImage);
        await db.SaveChangesAsync();

        var reader = scope.ServiceProvider.GetRequiredService<IMarketProductImageReader>();

        var images = await reader.ReadImagesAsync(
            [mpWithImage.Id, mpWithoutImage.Id],
            default);

        images.Should().ContainKey(mpWithImage.Id)
            .WhoseValue.Should().Be("https://img/tomato.jpg");
        images.Should().NotContainKey(mpWithoutImage.Id);
    }
}
