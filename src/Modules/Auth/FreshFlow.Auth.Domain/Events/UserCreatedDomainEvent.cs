using FreshFlow.Auth.Domain.Enums;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Auth.Domain.Events;

public sealed record UserCreatedDomainEvent(Guid UserId, string Email, UserRole Role) : IDomainEvent;
