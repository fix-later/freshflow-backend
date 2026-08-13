using FluentAssertions;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;

namespace FreshFlow.Procurement.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class MarketSessionTests
{
    [Fact]
    public void Lifecycle_DraftOpenClose_CannotReopen()
    {
        var now = new DateTime(2026, 8, 13, 3, 0, 0, DateTimeKind.Utc);
        var session = MarketSession.Create(
            Guid.NewGuid(), null, new DateOnly(2026, 8, 15), now.AddDays(1),
            MarketSessionCreatedSource.Auto, false).Value;
        var hubId = Guid.NewGuid();

        session.Open(hubId, now).IsSuccess.Should().BeTrue();
        session.HubId.Should().Be(hubId);
        session.Status.Should().Be(MarketSessionStatus.Open);
        session.Close(Guid.NewGuid(), "capacity reached", now.AddHours(1)).IsSuccess.Should().BeTrue();
        session.Close(null, null, now.AddHours(2)).IsSuccess.Should().BeTrue();
        session.Open(hubId, now.AddHours(2)).Error.Code.Should().Be("MARKET_SESSION_CLOSED");
    }
}
