using FluentAssertions;
using FreshFlow.Hub.Application.Commands.CreateHub;
using FreshFlow.Hub.UnitTests.TestDoubles;

namespace FreshFlow.Hub.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CreateHubCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_CreatesActiveHubAsync()
    {
        var repository = new InMemoryHubRepository();
        var sut = new CreateHubCommandHandler(repository);
        var managedBy = Guid.NewGuid();

        var result = await sut.Handle(
            new CreateHubCommand(" Main Hub ", " 123 Road ", 10m, 106m, 1000, managedBy),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.HubId.Should().NotBeEmpty();
        result.Value.Name.Should().Be("Main Hub");
        result.Value.Address.Should().Be("123 Road");
        result.Value.CapacityKg.Should().Be(1000);
        result.Value.OccupiedCapacityKg.Should().Be(0);
        result.Value.AvailableCapacityKg.Should().Be(1000);
        result.Value.IsActive.Should().BeTrue();
        result.Value.ManagedBy.Should().Be(managedBy);
        repository.Hubs.Should().ContainSingle();
        repository.SaveChangesCount.Should().Be(1);
    }
}
