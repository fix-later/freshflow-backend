namespace FreshFlow.Hub.Application.Abstractions;

public interface IHubAccessRequest
{
    public Guid HubId { get; }
    public Guid ActorUserId { get; }
    public bool BypassHubAssignment { get; }
}

public interface IHubManagementRequest
{
    public Guid ActorUserId { get; }
}
