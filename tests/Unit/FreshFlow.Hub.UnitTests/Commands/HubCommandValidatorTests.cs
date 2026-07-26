using FluentAssertions;
using FreshFlow.Hub.Application.Commands.CreateHub;
using FreshFlow.Hub.Application.Commands.DeactivateHub;
using FreshFlow.Hub.Application.Commands.RecordInbound;
using FreshFlow.Hub.Application.Commands.ScanInbound;
using FreshFlow.Hub.Application.Commands.UpdateHub;
using FreshFlow.Hub.Application.Queries.GetPendingInbound;
using FreshFlow.Hub.Application.Queries.ListHubs;
using FreshFlow.Hub.Application.Queries.ListInbound;

namespace FreshFlow.Hub.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class HubCommandValidatorTests
{
    [Fact]
    public void CreateHubCommandValidator_ValidCommand_Passes()
    {
        var sut = new CreateHubCommandValidator();

        var result = sut.Validate(new CreateHubCommand(
            Guid.NewGuid(), "Main Hub", null, 10m, 106m, 1000, null));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void CreateHubCommandValidator_BlankName_Fails(string name)
    {
        var sut = new CreateHubCommandValidator();

        var result = sut.Validate(new CreateHubCommand(
            Guid.NewGuid(), name, null, null, null, 1000, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateHubCommand.Name));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateHubCommandValidator_InvalidCapacity_Fails(decimal capacityKg)
    {
        var sut = new CreateHubCommandValidator();

        var result = sut.Validate(new CreateHubCommand(
            Guid.NewGuid(), "Main Hub", null, null, null, capacityKg, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateHubCommand.CapacityKg));
    }

    [Theory]
    [InlineData(-90.1)]
    [InlineData(90.1)]
    public void CreateHubCommandValidator_InvalidLatitude_Fails(decimal latitude)
    {
        var sut = new CreateHubCommandValidator();

        var result = sut.Validate(new CreateHubCommand(
            Guid.NewGuid(), "Main Hub", null, latitude, null, 1000, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateHubCommand.Latitude));
    }

    [Theory]
    [InlineData(-180.1)]
    [InlineData(180.1)]
    public void UpdateHubCommandValidator_InvalidLongitude_Fails(decimal longitude)
    {
        var sut = new UpdateHubCommandValidator();

        var result = sut.Validate(new UpdateHubCommand(Guid.NewGuid(), "Main Hub", null, null, longitude, 1000, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateHubCommand.Longitude));
    }

    [Fact]
    public void UpdateHubCommandValidator_EmptyHubId_Fails()
    {
        var sut = new UpdateHubCommandValidator();

        var result = sut.Validate(new UpdateHubCommand(Guid.Empty, "Main Hub", null, null, null, 1000, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateHubCommand.HubId));
    }

    [Fact]
    public void UpdateHubCommandValidator_EmptyManagedBy_Fails()
    {
        var sut = new UpdateHubCommandValidator();

        var result = sut.Validate(new UpdateHubCommand(Guid.NewGuid(), "Main Hub", null, null, null, 1000, Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateHubCommand.ManagedBy));
    }

    [Fact]
    public void DeactivateHubCommandValidator_EmptyHubId_Fails()
    {
        var sut = new DeactivateHubCommandValidator();

        var result = sut.Validate(new DeactivateHubCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(DeactivateHubCommand.HubId));
    }

    [Fact]
    public void RecordInboundCommandValidator_ValidCommand_Passes()
    {
        var sut = new RecordInboundCommandValidator();

        var result = sut.Validate(new RecordInboundCommand(
            Guid.NewGuid(),
            null,
            null,
            [new HubInboundItemCommand(Guid.NewGuid(), Guid.NewGuid(), 10m)],
            DateTime.UtcNow));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RecordInboundCommandValidator_EmptyItems_Fails()
    {
        var sut = new RecordInboundCommandValidator();

        var result = sut.Validate(new RecordInboundCommand(Guid.NewGuid(), null, null, [], DateTime.UtcNow));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RecordInboundCommand.Items));
    }

    [Fact]
    public void RecordInboundCommandValidator_InvalidItemQuantity_Fails()
    {
        var sut = new RecordInboundCommandValidator();

        var result = sut.Validate(new RecordInboundCommand(
            Guid.NewGuid(),
            null,
            null,
            [new HubInboundItemCommand(Guid.NewGuid(), null, 0)],
            DateTime.UtcNow));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.EndsWith(nameof(HubInboundItemCommand.QuantityKg)));
    }

    [Fact]
    public void ScanInboundCommandValidator_EmptyCode_Fails()
    {
        var sut = new ScanInboundCommandValidator();

        var result = sut.Validate(new ScanInboundCommand(""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ScanInboundCommand.Code));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public void ListHubsQueryValidator_InvalidPageSize_Fails(int pageSize)
    {
        var sut = new ListHubsQueryValidator();

        var result = sut.Validate(new ListHubsQuery(PageSize: pageSize));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListHubsQuery.PageSize));
    }

    [Fact]
    public void ListHubsQueryValidator_ValidPageSize_Passes()
    {
        var sut = new ListHubsQueryValidator();

        var result = sut.Validate(new ListHubsQuery(PageSize: 50));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public void GetPendingInboundQueryValidator_InvalidPageSize_Fails(int pageSize)
    {
        var sut = new GetPendingInboundQueryValidator();

        var result = sut.Validate(new GetPendingInboundQuery(Guid.NewGuid(), PageSize: pageSize));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetPendingInboundQuery.PageSize));
    }

    [Fact]
    public void GetPendingInboundQueryValidator_ValidPageSize_Passes()
    {
        var sut = new GetPendingInboundQueryValidator();

        var result = sut.Validate(new GetPendingInboundQuery(Guid.NewGuid(), PageSize: 50));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public void ListInboundQueryValidator_InvalidPageSize_Fails(int pageSize)
    {
        var sut = new ListInboundQueryValidator();

        var result = sut.Validate(new ListInboundQuery(Guid.NewGuid(), PageSize: pageSize));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ListInboundQuery.PageSize));
    }

    [Fact]
    public void ListInboundQueryValidator_ValidPageSize_Passes()
    {
        var sut = new ListInboundQueryValidator();

        var result = sut.Validate(new ListInboundQuery(Guid.NewGuid(), PageSize: 50));

        result.IsValid.Should().BeTrue();
    }
}
