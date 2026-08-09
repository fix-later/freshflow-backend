using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using FreshFlow.API.Controllers;
using FreshFlow.Hub.Application.Commands.CreateDiscrepancyProofUploadSignature;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace FreshFlow.Hub.UnitTests.Controllers;

[Trait("Category", "Unit")]
public sealed class HubDiscrepancyProofControllerTests
{
    [Fact]
    public void UploadSignature_RequiresHubStaff()
    {
        var authorize = typeof(HubInboundController)
            .GetMethod(nameof(HubInboundController.CreateDiscrepancyProofUploadSignatureAsync))!
            .GetCustomAttribute<AuthorizeAttribute>();

        authorize!.Roles.Should().Be("hub_staff");
    }

    [Fact]
    public async Task UploadSignature_SendsScopedCommandAsync()
    {
        var sender = Substitute.For<ISender>();
        var hubId = Guid.NewGuid();
        var inboundId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        sender.Send(
                Arg.Any<CreateDiscrepancyProofUploadSignatureCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<UploadSignatureResponse>.Success(
                new UploadSignatureResponse("sig", 123, "key", "cloud", "freshflow/hub-discrepancies")));
        var controller = new HubInboundController(sender)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, actorId.ToString())]))
                }
            }
        };

        var result = await controller.CreateDiscrepancyProofUploadSignatureAsync(
            hubId,
            inboundId,
            default);

        result.Should().BeOfType<OkObjectResult>();
        await sender.Received(1).Send(
            Arg.Is<CreateDiscrepancyProofUploadSignatureCommand>(command =>
                command.HubId == hubId &&
                command.InboundEventId == inboundId &&
                command.ActorUserId == actorId &&
                !command.BypassHubAssignment),
            Arg.Any<CancellationToken>());
    }
}
