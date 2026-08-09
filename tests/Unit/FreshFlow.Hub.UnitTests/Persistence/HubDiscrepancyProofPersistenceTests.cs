using FluentAssertions;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Hub.Infrastructure.Repositories;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Hub.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class HubDiscrepancyProofPersistenceTests
{
    [Fact]
    public async Task ProofImageUrl_SaveAndReadBackAsync()
    {
        _ = typeof(FreshFlow.Hub.Infrastructure.DependencyInjection).Assembly;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"hub-discrepancy-proof-{Guid.NewGuid()}")
            .Options;
        const string proofUrl = "https://res.cloudinary.com/demo/image/upload/hub-proof.jpg";
        var discrepancy = HubDiscrepancy.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1m,
            HubDiscrepancy.ConditionMissing,
            null,
            proofUrl);

        await using (var writeContext = new AppDbContext(options))
        {
            var repository = new HubDiscrepancyRepository(writeContext);
            await repository.AddAsync(discrepancy, default);
            await repository.SaveChangesAsync(default);
        }

        await using var readContext = new AppDbContext(options);
        var persisted = await new HubDiscrepancyRepository(readContext)
            .FindByIdForHubAsync(discrepancy.HubId, discrepancy.Id, default);

        persisted!.ProofImageUrl.Should().Be(proofUrl);
    }
}
