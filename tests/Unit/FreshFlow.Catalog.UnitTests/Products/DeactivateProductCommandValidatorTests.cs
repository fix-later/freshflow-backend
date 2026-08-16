using FluentAssertions;
using FreshFlow.Catalog.Application.Commands.Products.Deactivate;

namespace FreshFlow.Catalog.UnitTests.Products;

[Trait("Category", "Unit")]
public sealed class DeactivateProductCommandValidatorTests
{
    private readonly DeactivateProductCommandValidator _sut = new();

    [Fact]
    public async Task Validate_ValidId_Passes()
    {
        var cmd = new DeactivateProductCommand(Guid.NewGuid());
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyId_Fails()
    {
        var cmd = new DeactivateProductCommand(Guid.Empty);
        var result = await _sut.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Id");
    }
}
