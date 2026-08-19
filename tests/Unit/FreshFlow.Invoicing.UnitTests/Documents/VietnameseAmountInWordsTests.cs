using FluentAssertions;
using FreshFlow.Invoicing.Infrastructure.Documents;

namespace FreshFlow.Invoicing.UnitTests.Documents;

[Trait("Category", "Unit")]
public sealed class VietnameseAmountInWordsTests
{
    [Theory]
    [InlineData(0, "Không đồng")]
    [InlineData(1_473_260, "Một triệu bốn trăm bảy mươi ba nghìn hai trăm sáu mươi đồng")]
    [InlineData(15_000, "Mười lăm nghìn đồng")]
    [InlineData(21, "Hai mươi mốt đồng")]
    [InlineData(1_000_050, "Một triệu không trăm năm mươi đồng")]
    [InlineData(105, "Một trăm lẻ năm đồng")]
    [InlineData(2_000_000_000, "Hai tỷ đồng")]
    public void Convert_Amount_ReadsItOutInVietnamese(long amount, string expected) =>
        VietnameseAmountInWords.Convert(amount).Should().Be(expected);

    [Fact]
    public void Convert_RoundsToWholeDong() =>
        VietnameseAmountInWords.Convert(332_100.6m).Should()
            .Be("Ba trăm ba mươi hai nghìn một trăm lẻ một đồng");
}
