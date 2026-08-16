using FluentAssertions;
using FreshFlow.Catalog.Domain.Entities;

namespace FreshFlow.Catalog.UnitTests.PackingCodes;

[Trait("Category", "Unit")]
public sealed class PackingCodeTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyCode_Throws(string code)
    {
        var act = () => new PackingCode(code, null, 15m);
        act.Should().Throw<ArgumentException>().WithParameterName("code");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveCapacity_Throws(decimal capacityKg)
    {
        var act = () => new PackingCode("BOX-15", null, capacityKg);
        act.Should().Throw<ArgumentException>().WithParameterName("capacityKg");
    }

    [Fact]
    public void Constructor_FractionalCapacity_Throws()
    {
        var act = () => new PackingCode("BOX-2.5", null, 2.5m);
        act.Should().Throw<ArgumentException>().WithParameterName("capacityKg");
    }

    [Fact]
    public void Constructor_ValidValues_TrimsAndActivates()
    {
        var code = new PackingCode(" BOX-15 ", " Standard box ", 15m);

        code.Code.Should().Be("BOX-15");
        code.Description.Should().Be("Standard box");
        code.CapacityKg.Should().Be(15m);
        code.IsActive.Should().BeTrue();
    }
}
