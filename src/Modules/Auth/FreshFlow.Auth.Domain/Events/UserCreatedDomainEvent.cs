using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Auth.Domain.Events;

public sealed record UserCreatedDomainEvent(Guid UserId, string Email, string RoleName) : IDomainEvent;
