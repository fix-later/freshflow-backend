using FluentAssertions;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Commands.CreateExceptionProofUploadSignature;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CreateExceptionProofUploadSignatureCommandTests
{
    [Fact]
    public async Task Handler_Owner_ReturnsSignatureForExceptionFolderAsync()
    {
        var agentUserId = Guid.NewGuid();
        var batch = BuildAssignedBatch(agentUserId);
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        var signer = Substitute.For<ICloudinarySignatureService>();
        signer.Sign(Arg.Any<CloudinarySignatureRequest>())
            .Returns(new CloudinarySignatureResult(
                "sig",
                123,
                "key",
                "cloud",
                "freshflow/procurement-exceptions"));
        var handler = new CreateExceptionProofUploadSignatureCommandHandler(
            repository,
            signer);

        var result = await handler.Handle(
            new CreateExceptionProofUploadSignatureCommand(batch.Id, agentUserId),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Signature.Should().Be("sig");
        result.Value.Folder.Should().Be("freshflow/procurement-exceptions");
        signer.Received(1).Sign(Arg.Is<CloudinarySignatureRequest>(
            request => request.Folder == "freshflow/procurement-exceptions"));
    }

    [Fact]
    public async Task Handler_DifferentAgent_ReturnsNotFoundWithoutSigningAsync()
    {
        var batch = BuildAssignedBatch(Guid.NewGuid());
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        var signer = Substitute.For<ICloudinarySignatureService>();
        var handler = new CreateExceptionProofUploadSignatureCommandHandler(
            repository,
            signer);

        var result = await handler.Handle(
            new CreateExceptionProofUploadSignatureCommand(batch.Id, Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PROCUREMENT_BATCH_NOT_FOUND");
        signer.DidNotReceive().Sign(Arg.Any<CloudinarySignatureRequest>());
    }

    private static ProcurementBatch BuildAssignedBatch(Guid agentUserId)
    {
        var productId = Guid.NewGuid();
        var batch = ProcurementBatch.Build(
            new DateOnly(2026, 7, 15),
            Guid.NewGuid(),
            [(productId, "Tomato", 2, Guid.NewGuid())])
            .Value;
        batch.Manifest(
            new Dictionary<Guid, decimal> { [productId] = 10_000m },
            DateTime.UtcNow.AddHours(-2));
        batch.AssignAgent(agentUserId, DateTime.UtcNow.AddHours(-1));
        batch.ClearDomainEvents();
        return batch;
    }
}
