using FluentAssertions;
using FreshFlow.Catalog.Infrastructure.Repositories;

namespace FreshFlow.Catalog.UnitTests.Products;

/// <summary>
/// Unit tests for <see cref="ProductRepository.EscapeILikePattern"/>.
/// Verifies that %, _, and \ are all escaped so they are treated as literal
/// characters in a PostgreSQL ILIKE pattern (escape char = \).
/// </summary>
[Trait("Category", "Unit")]
public sealed class ProductRepositoryEscapeTests
{
    [Theory]
    [InlineData("50%", @"50\%")]            // percent → \%
    [InlineData("_test_", @"\_test\_")]     // underscore → \_
    [InlineData(@"path\", @"path\\")]       // backslash → \\
    [InlineData("hello", "hello")]          // plain string — no changes
    [InlineData("50%_path\\", @"50\%\_path\\")] // all three in one string
    public void EscapeILikePattern_EscapesWildcardsAndBackslash(
        string input, string expected)
    {
        // Act
        var result = ProductRepository.EscapeILikePattern(input);

        // Assert
        result.Should().Be(expected,
            because: $"input '{input}' should have wildcards/backslash escaped for ILIKE");
    }

    [Fact]
    public void EscapeILikePattern_EmptyString_ReturnsEmpty()
    {
        ProductRepository.EscapeILikePattern(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void EscapeILikePattern_BackslashEscapedBeforePercent_NoDoubleEscape()
    {
        // Input: \% (backslash then percent)
        // The backslash is escaped first → \\, then percent → \%
        // Expected result: \\\% (i.e. C# literal @"\\%")
        // In the ILIKE pattern this renders as the literal two-char sequence \%
        const string input = @"\%";
        const string expected = @"\\\%";

        ProductRepository.EscapeILikePattern(input).Should().Be(expected,
            because: "backslash must be escaped before % to avoid double-escaping");
    }
}
