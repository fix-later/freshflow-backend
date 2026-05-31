# Data Model: FreshFlow Auth Module v1

**Phase**: 1 — Design
**Date**: 2026-05-31
**Source**: `docs/03-database-schema.md` v1.0, `specs/003-auth-module/spec.md`

---

## Aggregate Root: User

Owned exclusively by the Auth module. The central entity for all authentication and access control.

| Field | Type | Constraint | Notes |
|-------|------|------------|-------|
| `Id` | `Guid` | PK, `gen_random_uuid()` | UUID v4 |
| `Email` | `string` | NOT NULL, UNIQUE, max 255 | Lowercase normalized |
| `PasswordHash` | `string` | NOT NULL | bcrypt hash; never exposed in API |
| `Role` | `UserRole` | NOT NULL, enum | See roles below |
| `IsActive` | `bool` | NOT NULL, default `true` | Fast active-status check |
| `CreatedAt` | `DateTimeOffset` | NOT NULL, default NOW() | UTC stored |
| `UpdatedAt` | `DateTimeOffset` | NOT NULL, default NOW() | Updated on every mutation |
| `DeletedAt` | `DateTimeOffset?` | NULL = active | Soft delete; `NULL = active` |

**Roles** (`UserRole` enum):
```
admin, market_agent, hub_staff, driver, restaurant
```
`kiosk_staff` is a legacy alias for `market_agent` accepted in input only — always stored and returned as `market_agent`.

**State rules**:
- Login is blocked when `IsActive = false`, `DeletedAt IS NOT NULL`, or (role = restaurant AND `restaurant.IsApproved = false`)
- `IsActive = false` deactivates future logins but does not invalidate existing access tokens (TTL max 15 min)
- Soft delete (`DeletedAt IS NOT NULL`) is treated as non-existent to the API

**Domain events raised by User aggregate**:
- `UserCreatedDomainEvent` — raised when a new user is created; triggers downstream profile creation

---

## Entity: RefreshToken (append-only)

Owned by Auth. One-to-many with `User`. No `UpdatedAt` or `DeletedAt` — append-only by design.

| Field | Type | Constraint | Notes |
|-------|------|------------|-------|
| `Id` | `Guid` | PK | Per-token unique ID |
| `UserId` | `Guid` | FK → users, ON DELETE CASCADE | |
| `TokenHash` | `string` | NOT NULL, UNIQUE | bcrypt hash of the 64-char hex raw token |
| `FamilyId` | `Guid` | NOT NULL | Groups all rotated tokens from one login session |
| `ExpiresAt` | `DateTimeOffset` | NOT NULL | NOW() + 7 days |
| `RevokedAt` | `DateTimeOffset?` | NULL = still valid | Set on rotation or logout |
| `ReplacedByTokenId` | `Guid?` | FK → refresh_tokens self-ref, ON DELETE SET NULL | Audit trail for rotation chain |
| `CreatedAt` | `DateTimeOffset` | NOT NULL, default NOW() | |

**Token lifecycle**:
```
ISSUED (RevokedAt = NULL, ExpiresAt > NOW())
  → ROTATED (RevokedAt = NOW(), ReplacedByTokenId = new token ID)
  → REVOKED (RevokedAt = NOW() via logout or family-wide revoke)
  → EXPIRED (ExpiresAt ≤ NOW(), still accepted as "expired" not "valid")
```

**Family revoke on reuse**:
When a token with `RevokedAt IS NOT NULL` is submitted to `/refresh`:
```sql
UPDATE refresh_tokens
SET revoked_at = NOW()
WHERE family_id = :familyId AND revoked_at IS NULL
```

---

## Entity: UserMarketAssignment

Owned by Auth. Many-to-many join between `market_agent` users and markets (which are owned by Pricing module). Auth stores the assignment; Pricing module validates it for price-update authorization.

| Field | Type | Constraint | Notes |
|-------|------|------------|-------|
| `Id` | `Guid` | PK | |
| `UserId` | `Guid` | FK → users, ON DELETE CASCADE | Must be `market_agent` role |
| `MarketId` | `Guid` | FK → markets, ON DELETE CASCADE | Cross-module FK — markets owned by Pricing |
| `AssignedBy` | `Guid?` | FK → users, ON DELETE SET NULL | Admin who assigned |
| `CreatedAt` | `DateTimeOffset` | NOT NULL | |
| `UpdatedAt` | `DateTimeOffset` | NOT NULL | |

**Uniqueness**: `(user_id, market_id)` unique constraint prevents duplicate assignments.

**Validation**: When Admin creates a `market_agent` user, `marketId` is required and must reference an active market. Auth.Application does not query the markets table directly — it delegates to the Pricing module's read-only catalog via `IMarketValidator` interface (cross-module application-layer interface, approved by Constitution I.2).

---

## Entity: DriverProfile

Shared Auth/Logistics ownership. Extended profile for `driver` role users. Created synchronously when Admin creates a driver user (same session, same transaction).

| Field | Type | Constraint | Notes |
|-------|------|------------|-------|
| `Id` | `Guid` | PK | |
| `UserId` | `Guid` | FK → users, UNIQUE | One-to-one |
| `LicensePlate` | `string?` | | Optional at creation |
| `PhoneNumber` | `string?` | | Optional at creation |
| `CreatedAt` | `DateTimeOffset` | NOT NULL | |
| `UpdatedAt` | `DateTimeOffset` | NOT NULL | |

**Cross-module creation**: Handled via `IDriverProfileCreator` interface in `Auth.Application/Abstractions/`, implemented in `FreshFlow.Infrastructure.Persistence/CrossModule/`. Same pattern as `IRestaurantProfileCreator`.

---

## Cross-Module Reference: Restaurant Profile

`restaurants` table is owned by the Orders module. Auth module does not define a `RestaurantProfile` domain entity. Instead:

- Auth.Application defines `IRestaurantProfileCreator` (interface only)
- Implemented by `FreshFlow.Infrastructure.Persistence/CrossModule/RestaurantProfileCreator.cs`
- Called synchronously in `CreateUserCommandHandler` when `role = restaurant`

**What Auth creates via this interface**:
```
restaurants.id           = gen_random_uuid()
restaurants.user_id      = new user's ID
restaurants.name         = restaurantName from request
restaurants.is_approved  = false (always starts unapproved)
restaurants.created_at   = NOW()
restaurants.updated_at   = NOW()
```

The `is_approved` flag is set to `true` via `PATCH /api/v1/admin/restaurants/{restaurantId}/approve`, which maps to `ApproveRestaurantCommand`. Auth writes to the `restaurants` table only through the `IRestaurantProfileCreator` interface — no direct EF entity reference in Domain or Application.

---

## State Transitions

### User Account States

```
[Created: is_active=true, deleted_at=null]
       ↓ Admin deactivates
[Deactivated: is_active=false]
       ↓ Admin activates
[Created: is_active=true]
       ↓ Soft delete (out of scope for v1 API; possible future operation)
[Deleted: deleted_at IS NOT NULL]
```

### Restaurant Approval States

```
[Pending: is_approved=false]   ← created here by Admin
       ↓ PATCH /approve
[Approved: is_approved=true]   ← cannot revert via this endpoint
```
To block an approved restaurant, use `PATCH /{userId}/activate` → `isActive: false`.

### Access Token States (stateless, validated via JWT TTL)

```
[Valid: exp > now]  →  [Expired: exp ≤ now]
```
No server-side access token revocation. Sessions end naturally within 15 minutes of logout.

---

## Validation Rules

| Rule | Scope | Detail |
|------|-------|--------|
| Email format | All user creation | RFC 5321, max 255 chars |
| Email uniqueness | All user creation | 409 `EMAIL_ALREADY_EXISTS` if exists |
| Password strength | Login + Admin create | Min 8 chars, ≥1 upper, ≥1 digit, ≥1 special |
| Role values | Admin create | `market_agent`, `hub_staff`, `driver`, `restaurant`; `kiosk_staff` normalized |
| marketId required | market_agent creation | Required and must reference an active market |
| restaurantName required | restaurant creation | Required, max 200 chars |
| targetDate (future) | Admin create | Validated by FluentValidation, not Auth domain |
| Refresh token expiry | Refresh endpoint | `ExpiresAt ≤ NOW()` → 401 `REFRESH_TOKEN_EXPIRED` |
| Refresh token revoked | Refresh endpoint | `RevokedAt IS NOT NULL` + not reused → 401 `REFRESH_TOKEN_REVOKED` |
| Refresh token reuse | Refresh endpoint | `RevokedAt IS NOT NULL` and was replaced → 409 `REFRESH_TOKEN_REUSE` + family revoke |
