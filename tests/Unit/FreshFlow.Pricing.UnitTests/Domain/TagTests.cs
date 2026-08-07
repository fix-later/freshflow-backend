using FluentAssertions;
using FreshFlow.Pricing.Domain.Entities;

namespace FreshFlow.Pricing.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class TagTests
{
    private static readonly Guid ActorId = Guid.NewGuid();

    // ── Create ───────────────────────────────────────────────────────────────

    [Fact]
    public void Create_TrimsAndLowercasesName()
    {
        // Act
        var tag = Tag.Create("  Khuyến Mãi ", false, ActorId);

        // Assert
        tag.Name.Should().Be("khuyến mãi");
        tag.PinsToTop.Should().BeFalse();
        tag.UpdatedBy.Should().Be(ActorId);
    }

    [Fact]
    public void Create_PinsToTopTrue_SetsPinsToTop()
    {
        // Act
        var tag = Tag.Create("nổi bật", true, ActorId);

        // Assert
        tag.PinsToTop.Should().BeTrue();
    }

    [Fact]
    public void Create_NameLongerThanThirtyChars_ThrowsArgumentException()
    {
        // Arrange
        var tooLong = new string('a', 31);

        // Act
        var act = () => Tag.Create(tooLong, false, ActorId);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyOrWhitespaceName_ThrowsArgumentException(string name)
    {
        // Act
        var act = () => Tag.Create(name, false, ActorId);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    // ── Rename ───────────────────────────────────────────────────────────────

    [Fact]
    public void Rename_NewName_UpdatesNameAndActor()
    {
        // Arrange
        var tag = Tag.Create("fresh", false, null);
        var newActor = Guid.NewGuid();

        // Act
        tag.Rename("Organic", newActor);

        // Assert
        tag.Name.Should().Be("organic");
        tag.UpdatedBy.Should().Be(newActor);
    }

    [Fact]
    public void Rename_SameNormalizedName_IsNoOp()
    {
        // Arrange
        var tag = Tag.Create("fresh", false, ActorId);
        var before = tag.UpdatedAt;

        // Act
        tag.Rename("FRESH", Guid.NewGuid());

        // Assert
        tag.UpdatedBy.Should().Be(ActorId);
        tag.UpdatedAt.Should().Be(before);
    }

    // ── SetPinsToTop ─────────────────────────────────────────────────────────

    [Fact]
    public void SetPinsToTop_Toggle_UpdatesFlagAndActor()
    {
        // Arrange
        var tag = Tag.Create("fresh", false, null);
        var newActor = Guid.NewGuid();

        // Act
        tag.SetPinsToTop(true, newActor);

        // Assert
        tag.PinsToTop.Should().BeTrue();
        tag.UpdatedBy.Should().Be(newActor);
    }

    [Fact]
    public void SetPinsToTop_SameValue_IsNoOp()
    {
        // Arrange
        var tag = Tag.Create("fresh", false, ActorId);
        var before = tag.UpdatedAt;

        // Act
        tag.SetPinsToTop(false, Guid.NewGuid());

        // Assert
        tag.UpdatedBy.Should().Be(ActorId);
        tag.UpdatedAt.Should().Be(before);
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    [Fact]
    public void Delete_SoftDeletesTag()
    {
        // Arrange
        var tag = Tag.Create("fresh", false, ActorId);

        // Act
        tag.Delete();

        // Assert
        tag.IsDeleted.Should().BeTrue();
        tag.DeletedAt.Should().NotBeNull();
    }
}
