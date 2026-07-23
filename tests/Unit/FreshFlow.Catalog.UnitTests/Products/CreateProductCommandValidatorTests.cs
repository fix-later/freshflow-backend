using FluentAssertions;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.Products.Create;
using FreshFlow.Catalog.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Products;

[Trait("Category", "Unit")]
public sealed class CreateProductCommandValidatorTests
{
    private readonly IPackingCodeRepository _packingCodes = Substitute.For<IPackingCodeRepository>();
    private readonly CreateProductCommandValidator _sut;

    public CreateProductCommandValidatorTests()
    {
        _sut = new CreateProductCommandValidator(_packingCodes);
    }

    [Fact]
    public async Task Validate_ValidCommand_Passes()
    {
        var cmd = new CreateProductCommand("Cà rốt", Guid.NewGuid(), null, null, null);
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_EmptyName_Fails(string name)
    {
        var cmd = new CreateProductCommand(name, Guid.NewGuid(), null, null, null);
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_NameExceedsMaxLength_Fails()
    {
        var longName = new string('x', 201);
        var cmd = new CreateProductCommand(longName, Guid.NewGuid(), null, null, null);
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_EmptyUnitId_Fails()
    {
        var cmd = new CreateProductCommand("Cà rốt", Guid.Empty, null, null, null);
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UnitId");
    }

    [Fact]
    public async Task Validate_EmptyGuidCategoryId_Fails()
    {
        // Non-null but empty guid is invalid
        var cmd = new CreateProductCommand("Cà rốt", Guid.NewGuid(), Guid.Empty, null, null);
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CategoryId");
    }

    [Fact]
    public async Task Validate_NullCategoryId_Passes()
    {
        // Null is allowed — product can be uncategorised
        var cmd = new CreateProductCommand("Cà rốt", Guid.NewGuid(), null, null, null);
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ActivePackingCode_Passes()
    {
        _packingCodes.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new PackingCode("BOX-15", null, 15m));
        var cmd = new CreateProductCommand("Cà rốt", Guid.NewGuid(), null, null, null, Guid.NewGuid());

        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyGuidPackingCode_Fails()
    {
        var cmd = new CreateProductCommand("Cà rốt", Guid.NewGuid(), null, null, null, Guid.Empty);

        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PackingCodeId");
    }

    [Fact]
    public async Task Validate_MissingOrInactivePackingCode_Fails()
    {
        _packingCodes.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((PackingCode?)null);
        var cmd = new CreateProductCommand("Cà rốt", Guid.NewGuid(), null, null, null, Guid.NewGuid());

        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PackingCodeId");
    }
}
