# API Contracts: FreshFlow Auth Module v1

**Phase**: 1 — Design
**Date**: 2026-05-31
**Source**: `docs/04-api-design.md` v1.0

All contracts use the standard response envelope defined in `docs/04-api-design.md §1.3`.
All C# types use `record` (immutable, value equality) per Constitution IV.

---

## Auth Endpoints (`/api/v1/auth`)

### POST /api/v1/auth/login

**Role**: Public (AllowAnonymous)

#### Request

```csharp
// Commands/Login/LoginCommand.cs
public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResponse>;
```

#### Validator

```csharp
// Commands/Login/LoginCommandValidator.cs
RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
RuleFor(x => x.Password).NotEmpty();
```

#### Response (200 OK)

```csharp
// Commands/Login/LoginResponse.cs
public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,       // always 900
    LoginUserDto User
);

public sealed record LoginUserDto(Guid Id, string Email, string Role);
```

#### Error codes

| Status | Code |
|--------|------|
| 400 | `VALIDATION_ERROR` |
| 401 | `INVALID_CREDENTIALS` |
| 422 | `ACCOUNT_INACTIVE` |
| 422 | `ACCOUNT_PENDING_APPROVAL` |

---

### POST /api/v1/auth/refresh

**Role**: Public (AllowAnonymous)

#### Request

```csharp
// Commands/RefreshToken/RefreshTokenCommand.cs
public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<RefreshTokenResponse>;
```

#### Validator

```csharp
RuleFor(x => x.RefreshToken).NotEmpty();
```

#### Response (200 OK)

```csharp
// Commands/RefreshToken/RefreshTokenResponse.cs
public sealed record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn       // always 900
);
```

#### Error codes

| Status | Code |
|--------|------|
| 400 | `VALIDATION_ERROR` |
| 401 | `REFRESH_TOKEN_EXPIRED` |
| 401 | `REFRESH_TOKEN_REVOKED` |
| 409 | `REFRESH_TOKEN_REUSE` |

---

### POST /api/v1/auth/logout

**Role**: Any authenticated user

#### Request

```csharp
// Commands/Logout/LogoutCommand.cs
public sealed record LogoutCommand(Guid UserId, string RefreshToken) : ICommand;
// UserId populated from JWT claim in controller — not from request body
```

**HTTP body** (only `refreshToken` field from client):
```json
{ "refreshToken": "<token>" }
```

#### Validator

```csharp
RuleFor(x => x.RefreshToken).NotEmpty();
```

#### Response

- **204 No Content** on success (no response body)
- **401** `UNAUTHORIZED` if access token missing/invalid

**Note**: If the submitted `refreshToken` is already revoked or does not belong to the authenticated user, the server still returns 204 — prevents oracle attacks on token existence.

---

## Admin User Endpoints (`/api/v1/admin`)

All endpoints require `[Authorize(Roles = "admin")]`.

---

### POST /api/v1/admin/users

#### Request

```csharp
// Commands/Admin/CreateUser/CreateUserCommand.cs
public sealed record CreateUserCommand(
    string Email,
    string Password,
    string Role,
    Guid? MarketId,
    string? RestaurantName
) : ICommand<CreateUserResponse>;
```

#### Validator

```csharp
RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
    .Matches("[A-Z]").Matches("[0-9]").Matches("[^a-zA-Z0-9]");
RuleFor(x => x.Role).NotEmpty()
    .Must(r => ValidRoles.Contains(r.ToLowerInvariant()))
    .WithMessage("Role must be one of: market_agent, hub_staff, driver, restaurant");
RuleFor(x => x.MarketId).NotEmpty()
    .When(x => x.Role is "market_agent" or "kiosk_staff");
RuleFor(x => x.RestaurantName).NotEmpty().MaximumLength(200)
    .When(x => x.Role is "restaurant");

private static readonly HashSet<string> ValidRoles =
    ["market_agent", "kiosk_staff", "hub_staff", "driver", "restaurant"];
```

#### Response (201 Created)

```csharp
// Commands/Admin/CreateUser/CreateUserResponse.cs
public sealed record CreateUserResponse(
    Guid Id,
    string Email,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt
);
```

#### Error codes

| Status | Code |
|--------|------|
| 400 | `VALIDATION_ERROR` |
| 401 | `UNAUTHORIZED` |
| 403 | `FORBIDDEN` |
| 409 | `EMAIL_ALREADY_EXISTS` |
| 422 | `INVALID_MARKET` |

---

### GET /api/v1/admin/users

#### Request (query parameters)

```csharp
// Queries/GetUsers/GetUsersQuery.cs
public sealed record GetUsersQuery(
    string? Role,
    bool? IsActive,
    string? Search,
    int Page = 1,
    int PageSize = 20
) : IQuery<GetUsersResponse>;
```

#### Response (200 OK)

```csharp
// Queries/GetUsers/GetUsersResponse.cs
public sealed record GetUsersResponse(
    IReadOnlyList<UserSummaryDto> Data,
    PaginationMeta Meta
);

public sealed record UserSummaryDto(
    Guid Id,
    string Email,
    string Role,
    bool IsActive,
    bool? IsApproved,                         // non-null only for restaurant role
    DateTimeOffset CreatedAt,
    IReadOnlyList<MarketAssignmentDto> MarketAssignments   // non-empty only for market_agent
);

public sealed record MarketAssignmentDto(Guid MarketId, string MarketName);

public sealed record PaginationMeta(int Page, int PageSize, int Total);
```

#### Validator

```csharp
RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
RuleFor(x => x.Page).GreaterThan(0);
```

---

### PATCH /api/v1/admin/users/{userId}/activate

#### Request

```csharp
// Commands/Admin/ActivateUser/ActivateUserCommand.cs
public sealed record ActivateUserCommand(Guid UserId, bool IsActive) : ICommand<ActivateUserResponse>;
```

#### Validator

```csharp
RuleFor(x => x.UserId).NotEmpty();
// IsActive: bool (always valid)
```

#### Response (200 OK)

```csharp
// Commands/Admin/ActivateUser/ActivateUserResponse.cs
public sealed record ActivateUserResponse(
    Guid Id,
    string Email,
    string Role,
    bool IsActive,
    DateTimeOffset UpdatedAt
);
```

#### Error codes

| Status | Code |
|--------|------|
| 400 | `VALIDATION_ERROR` |
| 401 | `UNAUTHORIZED` |
| 403 | `FORBIDDEN` |
| 404 | `USER_NOT_FOUND` |
| 422 | `CANNOT_DEACTIVATE_SELF` |

---

### PATCH /api/v1/admin/restaurants/{restaurantId}/approve

#### Request

No request body — approval is a single action on the restaurant ID.

```csharp
// Commands/Admin/ApproveRestaurant/ApproveRestaurantCommand.cs
public sealed record ApproveRestaurantCommand(Guid RestaurantId) : ICommand<ApproveRestaurantResponse>;
```

#### Response (200 OK)

```csharp
// Commands/Admin/ApproveRestaurant/ApproveRestaurantResponse.cs
public sealed record ApproveRestaurantResponse(
    Guid RestaurantId,
    string RestaurantName,
    bool IsApproved,
    DateTimeOffset UpdatedAt
);
```

#### Error codes

| Status | Code |
|--------|------|
| 401 | `UNAUTHORIZED` |
| 403 | `FORBIDDEN` |
| 404 | `RESTAURANT_NOT_FOUND` |
| 422 | `ALREADY_APPROVED` |

---

## Cross-Module Application Interfaces

Defined in `Auth.Application`, implemented in `FreshFlow.Infrastructure.Persistence`.

```csharp
// Abstractions/IRestaurantProfileCreator.cs
public interface IRestaurantProfileCreator
{
    Task<Guid> CreateAsync(Guid userId, string restaurantName, CancellationToken ct);
}

// Abstractions/IDriverProfileCreator.cs
public interface IDriverProfileCreator
{
    Task<Guid> CreateAsync(Guid userId, CancellationToken ct);
}

// Abstractions/IMarketValidator.cs — validates marketId for market_agent creation
public interface IMarketValidator
{
    Task<bool> IsActiveMarketAsync(Guid marketId, CancellationToken ct);
}
```

`IMarketValidator` is also defined in `Auth.Application`, implemented by `FreshFlow.Infrastructure.Persistence` querying the `markets` table (read-only). This avoids any Auth → Pricing project reference.
