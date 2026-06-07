# Frontend Enum Values

This file lists enum-like API values that the frontend can safely centralize.

## C# Enums

There are currently no active C# enums exposed through the API.

Note: `UserRole` was replaced by the `roles` lookup table. Frontend code should use the role string values below instead of the legacy enum.

## RoleName

Backend source: `FreshFlow.Auth.Domain.Enums.RoleNames` and the `roles` table.

| Value | Meaning |
|---|---|
| `admin` | System administrator |
| `market_agent` | Market agent |
| `restaurant` | Restaurant user |
| `hub_staff` | Hub staff |
| `driver` | Driver |
| `operations_manager` | Operations manager |

Suggested TypeScript:

```ts
export const RoleName = {
  Admin: 'admin',
  MarketAgent: 'market_agent',
  Restaurant: 'restaurant',
  HubStaff: 'hub_staff',
  Driver: 'driver',
  OperationsManager: 'operations_manager',
} as const;

export type RoleName = (typeof RoleName)[keyof typeof RoleName];
```

## AdminCreateUserRole

`POST /api/v1/admin/users` currently accepts these role values.

| Request value | Notes |
|---|---|
| `market_agent` | Creates a Market Agent user |
| `kiosk_staff` | Legacy alias; backend stores and returns `market_agent` |
| `hub_staff` | Creates a Hub Staff user |
| `driver` | Creates a Driver user |
| `restaurant` | Creates a Restaurant user |

Suggested TypeScript:

```ts
export const AdminCreateUserRole = {
  MarketAgent: 'market_agent',
  KioskStaff: 'kiosk_staff',
  HubStaff: 'hub_staff',
  Driver: 'driver',
  Restaurant: 'restaurant',
} as const;

export type AdminCreateUserRole =
  (typeof AdminCreateUserRole)[keyof typeof AdminCreateUserRole];
```

## AuthErrorCode

Auth/Admin Auth error codes currently emitted by the backend.

| Value | Common case |
|---|---|
| `UNAUTHORIZED` | Missing/invalid access token, or user no longer active |
| `FORBIDDEN` | Valid token but insufficient role |
| `VALIDATION_ERROR` | Invalid request body or query parameters |
| `INVALID_CREDENTIALS` | Login identifier/password is incorrect |
| `INVALID_CURRENT_PASSWORD` | Change password current password is incorrect |
| `ACCOUNT_INACTIVE` | Account is deactivated |
| `TOKEN_EXPIRED` | Access token has expired |
| `REFRESH_TOKEN_EXPIRED` | Refresh token has expired |
| `REFRESH_TOKEN_REVOKED` | Refresh token is missing or revoked |
| `REFRESH_TOKEN_REUSE` | Rotated refresh token was reused |
| `EMAIL_ALREADY_EXISTS` | Admin create user duplicate email |
| `PHONE_ALREADY_EXISTS` | Admin create user duplicate phone |
| `ALREADY_APPROVED` | Restaurant is already approved |
| `INVALID_MARKET` | Market is invalid for Market Agent creation |
| `CANNOT_DEACTIVATE_SELF` | Admin attempted to deactivate their own account |
| `USER_NOT_FOUND` | User was not found |
| `RESTAURANT_NOT_FOUND` | Restaurant was not found |
| `INTERNAL_ERROR` | Unexpected server error |

Suggested TypeScript:

```ts
export type AuthErrorCode =
  | 'UNAUTHORIZED'
  | 'FORBIDDEN'
  | 'VALIDATION_ERROR'
  | 'INVALID_CREDENTIALS'
  | 'INVALID_CURRENT_PASSWORD'
  | 'ACCOUNT_INACTIVE'
  | 'TOKEN_EXPIRED'
  | 'REFRESH_TOKEN_EXPIRED'
  | 'REFRESH_TOKEN_REVOKED'
  | 'REFRESH_TOKEN_REUSE'
  | 'EMAIL_ALREADY_EXISTS'
  | 'PHONE_ALREADY_EXISTS'
  | 'ALREADY_APPROVED'
  | 'INVALID_MARKET'
  | 'CANNOT_DEACTIVATE_SELF'
  | 'USER_NOT_FOUND'
  | 'RESTAURANT_NOT_FOUND'
  | 'INTERNAL_ERROR';
```
