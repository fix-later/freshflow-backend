using System.Security.Claims;
using FluentAssertions;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace FreshFlow.Pricing.UnitTests.Realtime;

[Trait("Category", "Unit")]
public sealed class PricingHubTests
{
    private const string AdminRole = "admin";
    private const string MarketAgentRole = "market_agent";

    private readonly IAssignedMarketReader _reader = Substitute.For<IAssignedMarketReader>();
    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly HubCallerContext _hubContext = Substitute.For<HubCallerContext>();

    private static readonly Guid MarketId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private const string ConnectionId = "test-connection-id";

    // ── Helpers ───────────────────────────────────────────────────────────────

    private PricingHub BuildHub(string role) => BuildHubWithSub(role, UserId.ToString());

    /// <summary>
    /// Builds a PricingHub whose "sub" claim is set to the supplied <paramref name="sub"/> string.
    /// Use this overload to test identity-parsing failure paths (e.g. non-GUID sub values).
    /// </summary>
    private PricingHub BuildHubWithSub(string role, string sub)
    {
        // Match the JWT configuration in Auth.Infrastructure.DependencyInjection:
        //   NameClaimType = "sub", RoleClaimType = "role"
        // Without the roleType parameter, IsInRole() would default to the long
        // ClaimTypes.Role URI and the "admin" check would always return false.
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            claims:
            [
                new Claim("sub", sub),
                new Claim("role", role),
            ],
            authenticationType: "test",
            nameType: "sub",
            roleType: "role"));

        _hubContext.User.Returns(user);
        _hubContext.ConnectionId.Returns(ConnectionId);
        _hubContext.ConnectionAborted.Returns(CancellationToken.None);

        var hub = new PricingHub(_reader)
        {
            Context = _hubContext,
            Groups = _groups,
        };
        return hub;
    }

    // ── JoinMarketAsync — Admin bypass ────────────────────────────────────────

    [Fact]
    public async Task JoinMarketAsync_AdminRole_JoinsGroupWithoutCheckingAssignment()
    {
        // Arrange
        var hub = BuildHub(AdminRole);

        // Act
        await hub.JoinMarketAsync(MarketId.ToString());

        // Assert — reader must NOT be consulted for admins
        await _reader.DidNotReceive().HasAssignmentAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        await _groups.Received(1).AddToGroupAsync(
            ConnectionId, $"market:{MarketId}", Arg.Any<CancellationToken>());
    }

    // ── JoinMarketAsync — authorized market agent ─────────────────────────────

    [Fact]
    public async Task JoinMarketAsync_AgentWithValidAssignment_JoinsGroup()
    {
        // Arrange
        var hub = BuildHub(MarketAgentRole);
        _reader.HasAssignmentAsync(UserId, MarketId, Arg.Any<CancellationToken>())
               .Returns(true);

        // Act
        await hub.JoinMarketAsync(MarketId.ToString());

        // Assert
        await _groups.Received(1).AddToGroupAsync(
            ConnectionId, $"market:{MarketId}", Arg.Any<CancellationToken>());
    }

    // ── JoinMarketAsync — unauthorized (no assignment) ────────────────────────

    [Fact]
    public async Task JoinMarketAsync_AgentWithoutAssignment_ThrowsHubException()
    {
        // Arrange
        var hub = BuildHub(MarketAgentRole);
        _reader.HasAssignmentAsync(UserId, MarketId, Arg.Any<CancellationToken>())
               .Returns(false);

        // Act
        var act = async () => await hub.JoinMarketAsync(MarketId.ToString());

        // Assert
        await act.Should().ThrowAsync<HubException>();
        await _groups.DidNotReceive().AddToGroupAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── JoinMarketAsync — revoked (soft-deleted) assignment excluded ───────────

    [Fact]
    public async Task JoinMarketAsync_RevokedAssignment_ThrowsHubException()
    {
        // Arrange — reader returns false for a revoked (soft-deleted) assignment,
        // mirroring the SQL fix: WHERE deleted_at IS NULL filters out revoked rows.
        var hub = BuildHub(MarketAgentRole);
        _reader.HasAssignmentAsync(UserId, MarketId, Arg.Any<CancellationToken>())
               .Returns(false);

        // Act
        var act = async () => await hub.JoinMarketAsync(MarketId.ToString());

        // Assert — revoked assignment must NOT grant hub access
        await act.Should().ThrowAsync<HubException>();
    }

    // ── LeaveMarketAsync — Admin bypass ──────────────────────────────────────

    [Fact]
    public async Task LeaveMarketAsync_AdminRole_LeavesGroupWithoutCheckingAssignment()
    {
        // Arrange
        var hub = BuildHub(AdminRole);

        // Act
        await hub.LeaveMarketAsync(MarketId.ToString());

        // Assert — reader must NOT be consulted for admins
        await _reader.DidNotReceive().HasAssignmentAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        await _groups.Received(1).RemoveFromGroupAsync(
            ConnectionId, $"market:{MarketId}", Arg.Any<CancellationToken>());
    }

    // ── LeaveMarketAsync — authorized market agent ────────────────────────────

    [Fact]
    public async Task LeaveMarketAsync_AgentWithValidAssignment_LeavesGroup()
    {
        // Arrange
        var hub = BuildHub(MarketAgentRole);
        _reader.HasAssignmentAsync(UserId, MarketId, Arg.Any<CancellationToken>())
               .Returns(true);

        // Act
        await hub.LeaveMarketAsync(MarketId.ToString());

        // Assert
        await _groups.Received(1).RemoveFromGroupAsync(
            ConnectionId, $"market:{MarketId}", Arg.Any<CancellationToken>());
    }

    // ── LeaveMarketAsync — unauthorized (no assignment) ───────────────────────

    [Fact]
    public async Task LeaveMarketAsync_AgentWithoutAssignment_ThrowsHubException()
    {
        // Arrange
        var hub = BuildHub(MarketAgentRole);
        _reader.HasAssignmentAsync(UserId, MarketId, Arg.Any<CancellationToken>())
               .Returns(false);

        // Act
        var act = async () => await hub.LeaveMarketAsync(MarketId.ToString());

        // Assert
        await act.Should().ThrowAsync<HubException>();
        await _groups.DidNotReceive().RemoveFromGroupAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Input validation ──────────────────────────────────────────────────────

    // ── Identity validation — malformed sub claim ─────────────────────────────

    [Fact]
    public async Task JoinMarketAsync_InvalidSubClaim_ThrowsHubExceptionAsync()
    {
        // Arrange — "sub" claim present but not a valid GUID (simulates a misconfigured
        // token issuer or a tampered token). Admin bypass does NOT apply here (non-admin role),
        // so AuthorizeMarketAccessAsync reaches the Guid.TryParse("sub") check and must reject.
        var hub = BuildHubWithSub(MarketAgentRole, "not-a-valid-guid");

        // Act
        var act = async () => await hub.JoinMarketAsync(MarketId.ToString());

        // Assert — hub must throw before touching the assignment reader
        await act.Should().ThrowAsync<HubException>()
            .WithMessage("*Unable to determine caller identity*");
        await _reader.DidNotReceive().HasAssignmentAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinMarketAsync_InvalidMarketIdFormat_ThrowsHubException()
    {
        // Arrange
        var hub = BuildHub(MarketAgentRole);

        // Act
        var act = async () => await hub.JoinMarketAsync("not-a-guid");

        // Assert — invalid GUID format must be rejected before any DB call
        await act.Should().ThrowAsync<HubException>();
        await _reader.DidNotReceive().HasAssignmentAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeaveMarketAsync_InvalidMarketIdFormat_ThrowsHubException()
    {
        // Arrange
        var hub = BuildHub(MarketAgentRole);

        // Act
        var act = async () => await hub.LeaveMarketAsync("not-a-guid");

        // Assert
        await act.Should().ThrowAsync<HubException>();
        await _reader.DidNotReceive().HasAssignmentAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
