# Research: FreshFlow Auth Module v1

**Phase**: 0 — Pre-design research
**Date**: 2026-05-31
**Status**: Complete — all NEEDS CLARIFICATION resolved

No open questions exist: the specification, constitution, database schema (v1.0), and API design (v1.0) are fully aligned. This document records the key technical decisions that are already ratified so implementers do not need to re-derive them.

---

## Decision 1 — Refresh Token Storage Strategy

**Decision**: Store refresh tokens as a **bcrypt hash** of the raw token value in `refresh_tokens.token_hash`.

**Rationale**: The database schema comment explicitly states `"stores bcrypt hash of the raw token"`. Bcrypt is the correct choice here because it is computationally expensive (slows brute-force even if the DB is compromised) and salt is embedded, avoiding rainbow table attacks. Raw tokens are never persisted.

**Implementation**:
- Generate 32 cryptographically-random bytes: `RandomNumberGenerator.GetBytes(32)` → `Convert.ToHexString()` → 64-char hex string returned to client
- Hash for storage: `BCrypt.Net.BCrypt.HashPassword(rawToken, workFactor: 12)`
- Lookup on refresh: fetch the token record by `family_id` + `revoked_at IS NULL`, then `BCrypt.Net.BCrypt.Verify(rawToken, tokenHash)`

**Library**: `BCrypt.Net-Next` (NuGet) — actively maintained, .NET 6+ support confirmed, last release 2024.

**Alternatives considered**: SHA-256 (faster but no salt → rainbow table risk), Argon2 (slower, more secure for passwords, but overkill for a 64-char random token with bcrypt work factor 12).

---

## Decision 2 — JWT Configuration

**Decision**: Use `Microsoft.AspNetCore.Authentication.JwtBearer` with **HS256** (HMAC-SHA256) signing.

**Rationale**: FreshFlow v1 is a single-instance API (not federated). HS256 with a strong symmetric key is simpler to operate than RS256 and sufficient for the threat model. Asymmetric keys are deferred to a future extraction where token verification moves to a dedicated auth service.

**Claims set**: `sub` (user UUID), `email`, `role`, `iat`, `exp`

**Lifetimes**: Access token TTL = 900 s (15 min); refresh token TTL = 7 days.

**Configuration keys** (environment variables; never committed):
- `JWT__Key` — ≥ 256-bit random secret
- `JWT__Issuer` — `https://api.freshflow.vn`
- `JWT__Audience` — `freshflow-api`

---

## Decision 3 — RBAC Implementation

**Decision**: Use ASP.NET Core's built-in `[Authorize(Roles = "admin")]` / `[Authorize(Roles = "...")]` attribute pattern driven by the `role` claim in the JWT.

**Rationale**: FreshFlow v1 has exactly 5 roles with fixed permissions. No dynamic RBAC engine is needed. The built-in middleware handles it with zero extra packages. A CI lint check (custom Roslyn analyzer or a startup test) will verify that every controller action has an explicit `[Authorize]` or `[AllowAnonymous]` attribute — no undecorated endpoints allowed per Constitution IV.

**kiosk_staff alias**: The `user_role` enum includes both `kiosk_staff` (legacy) and `market_agent`. For JWT claim output, always emit `market_agent`. For incoming role strings in requests, normalize `kiosk_staff` → `market_agent` in `CreateUserCommandValidator`.

---

## Decision 4 — Admin Seed

**Decision**: Implement `IHostedService` (`AdminSeeder`) that runs at application startup, idempotently creating one Admin account.

**Rationale**: Idempotent startup seed is the standard pattern for bootstrap data in ASP.NET Core. The seed must be safe to run on every restart — check `users.email = ADMIN_SEED_EMAIL` first, skip if found.

**Environment variables**:
- `ADMIN_SEED_EMAIL` — seed admin email
- `ADMIN_SEED_PASSWORD` — seed admin password (min 8 chars, 1 upper, 1 digit, 1 special)

**Failure behaviour**: If seed fails (e.g., DB unreachable), log error and throw to prevent the application from starting without a valid admin account on first run. On subsequent runs, if the account already exists, log info and continue.

---

## Decision 5 — Cross-Module Restaurant Profile Creation

**Decision**: `IRestaurantProfileCreator` interface in `FreshFlow.Auth.Application/Abstractions/`, implemented by `FreshFlow.Infrastructure.Persistence` (which has access to the shared `AppDbContext`), injected at host level.

**Rationale**: When Admin creates a `restaurant` user, both the `users` row (Auth) and the `restaurants` row (Orders) must be created in the same database transaction. Direct table access from Auth would violate the spirit of module ownership; a MediatR integration event would create an async gap in a synchronous HTTP request. The Constitution explicitly approves "application-layer interfaces defined in the calling module's Application project and injected at the host level" for exactly this scenario.

**Interface**:
```csharp
// FreshFlow.Auth.Application/Abstractions/IRestaurantProfileCreator.cs
public interface IRestaurantProfileCreator
{
    Task<Guid> CreateAsync(Guid userId, string restaurantName, CancellationToken ct);
}
```

**Implementation** lives in `FreshFlow.Infrastructure.Persistence/CrossModule/RestaurantProfileCreator.cs` — accesses `AppDbContext` directly, no Orders.Application reference.

**Driver profiles**: `driver_profiles` are created by the same pattern via `IDriverProfileCreator` (shared Auth/Logistics ownership per schema). However, a driver profile can be populated lazily after first login, so it can be deferred to a domain event + integration event if preferred. For v1 simplicity, use the same synchronous interface pattern.

---

## Decision 6 — Token Family Reuse Detection

**Decision**: On refresh token reuse, execute a single bulk revoke: `UPDATE refresh_tokens SET revoked_at = NOW() WHERE family_id = :familyId AND revoked_at IS NULL`.

**Rationale**: The `family_id` column on `refresh_tokens` groups all rotated tokens from one login session. Revoking the whole family on reuse detection prevents the attacker from using any token in the chain while still being one PostgreSQL query (no N+1 concern).

**Sequence for successful refresh**:
1. Receive raw refresh token
2. Find matching token record (fetch by user_id + latest in family, then bcrypt-verify)
3. Confirm `revoked_at IS NULL` and `expires_at > NOW()`
4. Mark old token as revoked (set `revoked_at = NOW()`, set `replaced_by_token_id` to new token's ID)
5. Issue new token, insert new `refresh_tokens` row with same `family_id`
6. Return new access + refresh token pair

**Sequence for reuse detection** (old token submitted again after it was already rotated):
1. Receive raw refresh token
2. Fetch record — `revoked_at IS NOT NULL` (already used)
3. Bulk-revoke entire family: `UPDATE refresh_tokens SET revoked_at = NOW() WHERE family_id = :familyId AND revoked_at IS NULL`
4. Return 409 `REFRESH_TOKEN_REUSE`

---

## Decision 7 — Integration Test Infrastructure

**Decision**: Use `Testcontainers.PostgreSql` to spin up a real PostgreSQL 16 container per test run. No mocked database — per Constitution III.

**Test isolation**: Each integration test class uses a dedicated schema or truncates tables between tests via a helper. `WebApplicationFactory<Program>` overrides `AppDbContext` connection string to the container.

**Library**: `Testcontainers.PostgreSql` + `xUnit` + `FluentAssertions`.

---

## No Remaining NEEDS CLARIFICATION

All implementation questions are resolved. Proceed to Phase 1: design and contracts.
