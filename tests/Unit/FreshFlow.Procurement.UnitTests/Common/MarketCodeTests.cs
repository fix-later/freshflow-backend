using FluentAssertions;
using FreshFlow.Procurement.Application.Common;

namespace FreshFlow.Procurement.UnitTests.Common;

[Trait("Category", "Unit")]
public sealed class MarketCodeTests
{
    [Theory]
    [InlineData("Chợ Thủ Đức", "TD")]
    [InlineData("Cho Thu Duc", "TD")]
    [InlineData("Bình Điền", "BD")]
    [InlineData("Chợ Đầu Mối Nông Sản Thủ Đức", "DMNS")]
    [InlineData("Hóc-Môn", "HM")]
    public void DeriveMarketCode_DerivesExpectedInitials(string name, string expected)
    {
        MarketCode.DeriveMarketCode(name).Should().Be(expected);
    }
}
