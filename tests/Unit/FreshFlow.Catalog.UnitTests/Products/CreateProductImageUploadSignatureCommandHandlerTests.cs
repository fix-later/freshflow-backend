using FluentAssertions;
using FreshFlow.Catalog.Application.Commands.Products.CreateImageUploadSignature;
using FreshFlow.SharedKernel.Application;
using NSubstitute;

namespace FreshFlow.Catalog.UnitTests.Products;

[Trait("Category", "Unit")]
public sealed class CreateProductImageUploadSignatureCommandHandlerTests
{
    private readonly ICloudinarySignatureService _signer = Substitute.For<ICloudinarySignatureService>();
    private readonly CreateProductImageUploadSignatureCommandHandler _sut;

    public CreateProductImageUploadSignatureCommandHandlerTests() =>
        _sut = new CreateProductImageUploadSignatureCommandHandler(_signer);

    [Fact]
    public async Task Handle_AlwaysSignsServerConstantProductFolderAsync()
    {
        _signer.Sign(Arg.Any<CloudinarySignatureRequest>())
            .Returns(new CloudinarySignatureResult("sig", 1_700_000_000, "key", "cloud", "freshflow/products"));

        await _sut.Handle(new CreateProductImageUploadSignatureCommand(), default);

        _signer.Received(1).Sign(Arg.Is<CloudinarySignatureRequest>(
            r => r.Folder == "freshflow/products" && r.Extra == null));
    }

    [Fact]
    public async Task Handle_ReturnsSignatureResultMappedToResponseAsync()
    {
        _signer.Sign(Arg.Any<CloudinarySignatureRequest>())
            .Returns(new CloudinarySignatureResult(
                "sig-abc", 1_700_000_000, "key-123", "cloud-x", "freshflow/products"));

        var result = await _sut.Handle(new CreateProductImageUploadSignatureCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Signature.Should().Be("sig-abc");
        result.Value.Timestamp.Should().Be(1_700_000_000);
        result.Value.ApiKey.Should().Be("key-123");
        result.Value.CloudName.Should().Be("cloud-x");
        result.Value.Folder.Should().Be("freshflow/products");
    }
}
