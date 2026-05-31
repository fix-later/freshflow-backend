# Implementation Plan: FreshFlow Auth Module v1

**Branch**: `002-pricing-hub-admin-batch` (active) | **Date**: 2026-05-31 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/003-auth-module/spec.md`

---

## Summary

Implement the Auth module for FreshFlow v1: JWT login with 15-minute access tokens, 7-day refresh tokens with rotation and family-level reuse detection, Admin-managed user creation for all v1 roles (`market_agent`, `hub_staff`, `driver`, `restaurant`), restaurant approval workflow, idempotent Admin seed, and full RBAC enforcement. Auth is the critical-path dependency for every other module — all four CI gates (unit tests, integration tests on real PostgreSQL, format check, security scan) must pass before downstream modules begin.

---

## Technical Context

**Language/Version**: C# 14, .NET 10
**Primary Dependencies**: `Microsoft.AspNetCore.Authentication.JwtBearer` (built-in), `MediatR 12`, `FluentValidation 11`, `BCrypt.Net-Next`, `EF Core 10`, `Npgsql.EntityFrameworkCore.PostgreSQL`
**Storage**: PostgreSQL 16 via shared `AppDbContext` in `FreshFlow.Infrastructure.Persistence`
**Testing**: xUnit, `Testcontainers.PostgreSql`, `FluentAssertions`, `NSubstitute`
**Target Platform**: Linux container (Docker Compose for dev; GitHub Actions for CI)
**Project Type**: Module within ASP.NET Core 10 modular monolith
**Performance Goals**: Login p95 ≤ 200 ms; refresh p95 ≤ 100 ms
**Constraints**: 15-min access token TTL (non-negotiable for RBAC); 7-day refresh TTL; no database mocks in integration tests (Constitution III)
**Scale/Scope**: v1 target ~50–100 concurrent users; Auth is the first of 7 modules

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

### Principle I — Module Boundary Enforcement

| Check | Status | Notes |
|-------|--------|-------|
| Auth.Domain → SharedKernel only | ✅ PASS | No cross-module domain references |
| Auth.Application → Domain + SharedKernel + Contracts | ✅ PASS | |
| Auth.Infrastructure → Application + packages only | ✅ PASS | |
| No lateral references to Orders/Pricing/etc. | ✅ PASS | |
| `restaurants` row creation on user create | ✅ PASS | `IRestaurantProfileCreator` in Auth.Application, impl in Persistence — approved by Constitution I.2 (application-layer interface) |
| `driver_profiles` row creation | ✅ PASS | Same pattern via `IDriverProfileCreator` |
| `marketId` validation for market_agent | ✅ PASS | `IMarketValidator` in Auth.Application, impl in Persistence (reads `markets` table, no Pricing project ref) |

### Principle II — Clean Architecture Layers

| Layer | Allowed refs | Status |
|-------|-------------|--------|
| Auth.Domain | SharedKernel | ✅ |
| Auth.Application | Domain + SharedKernel + Contracts | ✅ |
| Auth.Infrastructure | Application + EF Core + BCrypt packages | ✅ |
| API (host) | Auth.Infrastructure (DI wiring) | ✅ |

### Principle III — Test-First CI Gates

| Gate | Plan |
|------|------|
| Unit tests (`Category=Unit`) | All command handlers, query handlers, validators |
| Integration tests (`Category=Integration`) | All endpoints against real PostgreSQL via Testcontainers |
| Format check | `dotnet format --verify-no-changes` |
| Security scan | OWASP dependency check in CI |
| No DB mocks | ✅ Integration tests use Testcontainers.PostgreSql |

### Principle IV — Result Pattern & Defensive API

| Check | Status |
|-------|--------|
| Handlers return `Result<T>` / `Result` | ✅ |
| Controllers use `result.Error.ToActionResult()` | ✅ |
| All DTOs use `record` types | ✅ |
| All request DTOs have co-located FluentValidation | ✅ |
| Every endpoint has `[Authorize]` or `[AllowAnonymous]` | ✅ |
| No raw SQL in Auth module | ✅ |

### Principle V — Microservice-Readiness

| Check | Status |
|-------|--------|
| Single `AddAuthModule()` extension method | ✅ |
| UUID PKs throughout | ✅ |
| Soft delete on `users` | ✅ |
| `refresh_tokens` append-only (no deleted_at) | ✅ |
| No in-process shared state | ✅ |

**All gates: PASS. No complexity tracking required.**

---

## Project Structure

### Documentation (this feature)

```text
specs/003-auth-module/
├── spec.md              ← feature specification
├── plan.md              ← this file
├── research.md          ← Phase 0: resolved decisions
├── data-model.md        ← Phase 1: entity design
├── quickstart.md        ← Phase 1: local dev guide
├── contracts/
│   └── auth-api-contracts.md   ← Phase 1: request/response shapes
└── tasks.md             ← Phase 2 (/speckit-tasks — not yet generated)
```

### Source Code

```text
src/Modules/Auth/
  FreshFlow.Auth.Domain/
    Aggregates/
      User.cs
    Entities/
      RefreshToken.cs
      UserMarketAssignment.cs
      DriverProfile.cs
    Enums/
      UserRole.cs
    Events/
      UserCreatedDomainEvent.cs

  FreshFlow.Auth.Application/
    Abstractions/
      IUserRepository.cs
      IRefreshTokenRepository.cs
      ITokenService.cs           ← generates/validates JWTs
      IPasswordHasher.cs         ← hash + verify
      IRestaurantProfileCreator.cs   ← cross-module (implemented in Persistence)
      IDriverProfileCreator.cs       ← cross-module (implemented in Persistence)
      IMarketValidator.cs            ← cross-module (implemented in Persistence)
      IAdminSeeder.cs
    Commands/
      Login/
        LoginCommand.cs
        LoginCommandHandler.cs
        LoginCommandValidator.cs
        LoginResponse.cs
      RefreshToken/
        RefreshTokenCommand.cs
        RefreshTokenCommandHandler.cs
        RefreshTokenCommandValidator.cs
        RefreshTokenResponse.cs
      Logout/
        LogoutCommand.cs
        LogoutCommandHandler.cs
        LogoutCommandValidator.cs
      Admin/CreateUser/
        CreateUserCommand.cs
        CreateUserCommandHandler.cs
        CreateUserCommandValidator.cs
        CreateUserResponse.cs
      Admin/ActivateUser/
        ActivateUserCommand.cs
        ActivateUserCommandHandler.cs
        ActivateUserCommandValidator.cs
        ActivateUserResponse.cs
      Admin/ApproveRestaurant/
        ApproveRestaurantCommand.cs
        ApproveRestaurantCommandHandler.cs
        ApproveRestaurantResponse.cs
    Queries/
      GetUsers/
        GetUsersQuery.cs
        GetUsersQueryHandler.cs
        GetUsersQueryValidator.cs
        GetUsersResponse.cs

  FreshFlow.Auth.Infrastructure/
    Persistence/
      Configurations/
        UserConfiguration.cs
        RefreshTokenConfiguration.cs
        UserMarketAssignmentConfiguration.cs
        DriverProfileConfiguration.cs
    Repositories/
      UserRepository.cs
      RefreshTokenRepository.cs
    Services/
      JwtTokenService.cs         ← implements ITokenService
      BCryptPasswordHasher.cs    ← implements IPasswordHasher
    Seed/
      AdminSeeder.cs             ← IHostedService, idempotent
    DependencyInjection.cs       ← AddAuthModule() extension method

src/FreshFlow.Infrastructure.Persistence/
  CrossModule/
    RestaurantProfileCreator.cs  ← implements IRestaurantProfileCreator
    DriverProfileCreator.cs      ← implements IDriverProfileCreator
    MarketValidator.cs           ← implements IMarketValidator

src/FreshFlow.API/
  Controllers/
    AuthController.cs            ← login, refresh, logout
    AdminController.cs           ← admin/users, admin/restaurants (Auth scope only)
```

### Tests

```text
tests/Unit/FreshFlow.Auth.UnitTests/
  Commands/
    LoginCommandHandlerTests.cs
    RefreshTokenCommandHandlerTests.cs
    LogoutCommandHandlerTests.cs
    CreateUserCommandHandlerTests.cs
    ActivateUserCommandHandlerTests.cs
    ApproveRestaurantCommandHandlerTests.cs
  Queries/
    GetUsersQueryHandlerTests.cs
  Validators/
    LoginCommandValidatorTests.cs
    CreateUserCommandValidatorTests.cs
    ActivateUserCommandValidatorTests.cs

tests/Integration/FreshFlow.IntegrationTests/
  Auth/
    LoginEndpointTests.cs
    RefreshTokenEndpointTests.cs
    LogoutEndpointTests.cs
    AdminUsersEndpointTests.cs
    ApproveRestaurantEndpointTests.cs
    AdminSeedTests.cs
    RbacEnforcementTests.cs
```

**Structure Decision**: Standard three-layer module (`Domain` / `Application` / `Infrastructure`) within the existing modular monolith scaffold. Cross-module creation interfaces implemented in the shared `Persistence` project, which holds the shared `AppDbContext` and therefore has access to all tables without introducing project-level circular references.

---

## Implementation Order (TDD)

Follow **RED → GREEN → REFACTOR** for each item. Do not proceed to the next item until all gates pass for the current one.

1. **EF Core migration** — `users`, `refresh_tokens`, `user_market_assignments`, `driver_profiles` tables
2. **User aggregate** — domain entity, `UserRole` enum, `UserCreatedDomainEvent`
3. **Password hashing** — `BCryptPasswordHasher` + unit tests
4. **JWT service** — `JwtTokenService` (generate + validate) + unit tests
5. **Login flow** — `LoginCommandHandler` → unit tests → integration test (`LoginEndpointTests`)
6. **Refresh flow** — `RefreshTokenCommandHandler` (rotation + reuse detection) → unit tests → integration test
7. **Logout flow** — `LogoutCommandHandler` → unit tests → integration test
8. **Admin seed** — `AdminSeeder` (IHostedService) + `AdminSeedTests`
9. **Admin user creation** — `CreateUserCommandHandler` (all 4 roles, cross-module interfaces) → unit tests → integration test
10. **Admin user list** — `GetUsersQueryHandler` → unit tests → integration test
11. **Admin activate user** — `ActivateUserCommandHandler` → unit tests → integration test
12. **Admin restaurant approval** — `ApproveRestaurantCommandHandler` → unit tests → integration test
13. **RBAC gate test** — `RbacEnforcementTests` (one test class verifying 401/403 across all endpoints)
14. **Format + security scan gates** — run and fix before marking module complete

---

## Key Reuse: SharedKernel Already Exists

These files are already implemented — do not recreate:

| File | Location |
|------|----------|
| `Result<T>` / `Result` | `src/Shared/FreshFlow.SharedKernel/Application/Result.cs` |
| `Error` | `src/Shared/FreshFlow.SharedKernel/Application/Error.cs` |
| `ICommand<T>` / `IQuery<T>` | `src/Shared/FreshFlow.SharedKernel/Application/ICommand.cs` / `IQuery.cs` |
| `BaseEntity` / `AggregateRoot` | `src/Shared/FreshFlow.SharedKernel/Domain/` |
| `ICacheService` / `RedisKeys` | `src/Shared/FreshFlow.SharedKernel/Caching/` |
