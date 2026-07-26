using FluentAssertions;
using FreshFlow.Invoicing.Application.Common;

namespace FreshFlow.Invoicing.UnitTests.Common;

[Trait("Category", "Unit")]
public sealed class VatRateResolverTests
{
    [Theory]
    [InlineData("KCT", 0)]
    [InlineData("0", 0)]
    [InlineData("5", 5)]
    [InlineData("8", 8)]
    [InlineData("10", 10)]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("weird", 0)]
    public void ToPercent_MapsCodes(string? code, int expected) =>
        VatRateResolver.ToPercent(code).Should().Be(expected);

    [Fact]
    public void Normalize_NullBecomesKct() =>
        VatRateResolver.Normalize(null).Should().Be("KCT");

    [Fact]
    public void Normalize_TrimsAndUppercases() =>
        VatRateResolver.Normalize(" kct ").Should().Be("KCT");
}
