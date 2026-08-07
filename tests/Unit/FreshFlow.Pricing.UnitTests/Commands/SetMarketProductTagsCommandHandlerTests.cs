using FluentAssertions;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Commands.SetMarketProductTags;
using FreshFlow.Pricing.Domain.Entities;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Pricing.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class SetMarketProductTagsCommandHandlerTests
{
    private readonly IMarketProductRepository _mpRepo = Substitute.For<IMarketProductRepository>();
    private readonly ITagRepository _tagRepo = Substitute.For<ITagRepository>();
    private readonly SetMarketProductTagsCommandHandler _sut;

    private static readonly Guid MarketId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    public SetMarketProductTagsCommandHandlerTests()
    {
        _sut = new SetMarketProductTagsCommandHandler(_mpRepo, _tagRepo);
    }

    [Fact]
    public async Task Handle_MarketProductNotFound_ReturnsNotFoundErrorAsync()
    {
        // Arrange
        _mpRepo.FindTrackedWithTagsByMarketAndProductAsync(MarketId, ProductId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var result = await _sut.Handle(
            new SetMarketProductTagsCommand(MarketId, ProductId, [], ActorId), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MARKET_PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_UnknownTagId_ReturnsValidationErrorAsync()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 10, null);
        _mpRepo.FindTrackedWithTagsByMarketAndProductAsync(MarketId, ProductId, Arg.Any<CancellationToken>())
            .Returns(mp);
        var unknownId = Guid.NewGuid();
        _tagRepo.FindByIdsAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(unknownId)),
            Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var result = await _sut.Handle(
            new SetMarketProductTagsCommand(MarketId, ProductId, [unknownId], ActorId), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Handle_AllTagsExist_AssignsTagsAndSavesAsync()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 10, null);
        var tag = Tag.Create("fresh", false, null);
        _mpRepo.FindTrackedWithTagsByMarketAndProductAsync(MarketId, ProductId, Arg.Any<CancellationToken>())
            .Returns(mp);
        _tagRepo.FindByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([tag]);

        // Act
        var result = await _sut.Handle(
            new SetMarketProductTagsCommand(MarketId, ProductId, [tag.Id], ActorId), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        mp.Tags.Should().BeEquivalentTo([tag]);
        await _mpRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
