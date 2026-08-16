using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.PackingCodes.Update;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.PackingCodes;

[Trait("Category", "Unit")]
public sealed class UpdatePackingCodeCommandValidatorTests
{
    private readonly IPackingCodeRepository _packingCodes = Substitute.For<IPackingCodeRepository>();
    private readonly UpdatePackingCodeCommandValidator _sut;

    public UpdatePackingCodeCommandValidatorTests()
    {
        var config = Substitute.For<IConfiguration>();
        config["Logistics:Box:MaxLoadKg"].Returns("25");
        _sut = new UpdatePackingCodeCommandValidator(_packingCodes, config);
    }

    [Fact]
    public async Task Validate_DuplicateCodeExcludingCurrentId_Fails()
    {
        var id = Guid.NewGuid();
        _packingCodes.CodeExistsAsync("BOX-15", id, default).Returns(true);

        var result = await _sut.ValidateAsync(
            new UpdatePackingCodeCommand(id, "BOX-15", null, 15m));

        result.Errors.Should().Contain(e => e.PropertyName == "Code");
    }

    [Fact]
    public async Task Validate_CapacityAboveConfiguredMaximum_Fails()
    {
        var result = await _sut.ValidateAsync(
            new UpdatePackingCodeCommand(Guid.NewGuid(), "BOX-30", null, 30m));

        result.Errors.Should().Contain(e => e.PropertyName == "CapacityKg");
    }
}
