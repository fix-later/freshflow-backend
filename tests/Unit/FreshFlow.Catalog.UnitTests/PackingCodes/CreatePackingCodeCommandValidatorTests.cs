using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.PackingCodes.Create;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.PackingCodes;

[Trait("Category", "Unit")]
public sealed class CreatePackingCodeCommandValidatorTests
{
    private readonly IPackingCodeRepository _packingCodes = Substitute.For<IPackingCodeRepository>();
    private readonly CreatePackingCodeCommandValidator _sut;

    public CreatePackingCodeCommandValidatorTests()
    {
        var config = Substitute.For<IConfiguration>();
        config["Logistics:Box:MaxLoadKg"].Returns("25");
        _sut = new CreatePackingCodeCommandValidator(_packingCodes, config);
    }

    [Fact]
    public async Task Validate_ValidCommand_Passes()
    {
        var result = await _sut.ValidateAsync(new CreatePackingCodeCommand("BOX-15", null, 15m));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_DuplicateCode_Fails()
    {
        _packingCodes.CodeExistsAsync("BOX-15", null, default).Returns(true);

        var result = await _sut.ValidateAsync(new CreatePackingCodeCommand("BOX-15", null, 15m));

        result.Errors.Should().Contain(e => e.PropertyName == "Code");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2.5)]
    [InlineData(26)]
    public async Task Validate_CapacityOutsideAllowedRange_Fails(decimal capacityKg)
    {
        var result = await _sut.ValidateAsync(
            new CreatePackingCodeCommand("BOX-15", null, capacityKg));

        result.Errors.Should().Contain(e => e.PropertyName == "CapacityKg");
    }
}
