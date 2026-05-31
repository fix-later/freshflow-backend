using FreshFlow.Auth.Domain.Enums;
using FreshFlow.Auth.Domain.Events;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Auth.Domain.Aggregates;

public sealed class User : AggregateRoot
{
    private User() { }  // EF Core materialisation

    private User(string email, string passwordHash, UserRole role)
    {
        Email = email.ToLowerInvariant();
        PasswordHash = passwordHash;
        Role = role;
        IsActive = true;
    }

    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }

    public static User Create(string email, string passwordHash, UserRole role)
    {
        var user = new User(email, passwordHash, role);
        user.RaiseDomainEvent(new UserCreatedDomainEvent(user.Id, user.Email, user.Role));
        return user;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool CanLogin() => IsActive && !IsDeleted;
}
