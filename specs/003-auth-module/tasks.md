# Tasks: FreshFlow Auth Module v1

**Input**: Design documents from `specs/003-auth-module/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Tests**: Per Constitution Principle III, test coverage is MANDATORY before merging to `main`.
Unit tests (per module) and integration tests (real PostgreSQL, no mocks) MUST be included.
The CI pipeline enforces these gates — omitting them will block the PR.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Each task targets exactly one file (or two tightly-coupled interface + implementation files)
- **[Trait]**: ALL unit test classes MUST carry `[Trait("Category", "Unit")]`; ALL integration test classes MUST carry `[Trait("Category", "Integration")]`. Required for CI filter commands `dotnet test --filter Category=Unit` / `--filter Category=Integration` (Constitution III). Missing traits cause gates to silently pass with 0 tests run.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add packages and create the cross-module glue before any domain code.

- [x] T001 Add `BCrypt.Net-Next` to `src/Modules/Auth/FreshFlow.Auth.Infrastructure/FreshFlow.Auth.Infrastructure.csproj`
- [x] T002 [P] Add `Testcontainers.PostgreSql`, `NSubstitute`, `FluentAssertions` to `tests/Unit/FreshFlow.Auth.UnitTests/` and `tests/Integration/FreshFlow.IntegrationTests/` project files
- [x] T003 [P] Create cross-module interfaces in `src/Modules/Auth/FreshFlow.Auth.Application/Abstractions/`: `IRestaurantProfileCreator.cs`, `IDriverProfileCreator.cs`, `IMarketValidator.cs`
- [x] T004 [P] Create cross-module implementations in `src/FreshFlow.Infrastructure.Persistence/CrossModule/`: `RestaurantProfileCreator.cs`, `DriverProfileCreator.cs`, `MarketValidator.cs`
- [x] T059 Create `Error.ToActionResult()` HTTP extension in `src/FreshFlow.API/Extensions/ErrorExtensions.cs` — maps `Error.Code` to `IActionResult`: `*_NOT_FOUND` → 404, `EMAIL_ALREADY_EXISTS`/`REFRESH_TOKEN_REUSE`/`ALREADY_APPROVED` → 409, `UNAUTHORIZED`/`INVALID_CREDENTIALS`/`REFRESH_TOKEN_*` → 401, `FORBIDDEN` → 403, `VALIDATION_ERROR` → 400, `CANNOT_DEACTIVATE_SELF`/`INVALID_MARKET`/`ACCOUNT_*` → 422 (required by every controller action per Constitution IV)
- [x] T060 [P] Create integration test infrastructure in `tests/Integration/FreshFlow.IntegrationTests/Infrastructure/`: `AuthWebAppFactory.cs` (WebApplicationFactory\<Program\> subclass that overrides DB connection string to Testcontainers PostgreSQL container) and `DatabaseResetFixture.cs` (truncates Auth tables between tests; seeds seeded Admin via env vars) — required by ALL integration test tasks (T022–T024, T041–T042, T049–T050)

**Checkpoint**: Packages restored, cross-module wiring in place, `ToActionResult()` extension ready, integration test scaffolding ready.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain model, EF mappings, crypto services, and Admin seed — must be complete before any story can be implemented.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T005 Create `UserRole` enum in `src/Modules/Auth/FreshFlow.Auth.Domain/Enums/UserRole.cs`
- [x] T006 [P] Create `User` aggregate root in `src/Modules/Auth/FreshFlow.Auth.Domain/Aggregates/User.cs` (fields: Id, Email, PasswordHash, Role, IsActive, CreatedAt, UpdatedAt, DeletedAt)
- [x] T007 [P] Create `UserCreatedDomainEvent` in `src/Modules/Auth/FreshFlow.Auth.Domain/Events/UserCreatedDomainEvent.cs`
- [x] T008 [P] Create `DriverProfile` entity in `src/Modules/Auth/FreshFlow.Auth.Domain/Entities/DriverProfile.cs`
- [x] T010 [P] Create `UserConfiguration` (IEntityTypeConfiguration) in `src/Modules/Auth/FreshFlow.Auth.Infrastructure/Persistence/Configurations/UserConfiguration.cs`
- [x] T011 [P] Create `DriverProfileConfiguration` in `src/Modules/Auth/FreshFlow.Auth.Infrastructure/Persistence/Configurations/DriverProfileConfiguration.cs`
- [x] T012 Create `IPasswordHasher` interface in `src/Modules/Auth/FreshFlow.Auth.Application/Abstractions/IPasswordHasher.cs` and `BCryptPasswordHasher` in `src/Modules/Auth/FreshFlow.Auth.Infrastructure/Services/BCryptPasswordHasher.cs`
- [x] T013 Create `ITokenService` interface in `src/Modules/Auth/FreshFlow.Auth.Application/Abstractions/ITokenService.cs` and `JwtTokenService` in `src/Modules/Auth/FreshFlow.Auth.Infrastructure/Services/JwtTokenService.cs` (generate access token + raw refresh token; validate JWT)
- [x] T014 [P] Write unit tests for `BCryptPasswordHasher` (hash, verify, wrong password returns false) in `tests/Unit/FreshFlow.Auth.UnitTests/Services/BCryptPasswordHasherTests.cs` — run RED, then GREEN
- [x] T015 [P] Write unit tests for `JwtTokenService` (claims present, expiry = 900s, tampered token invalid) in `tests/Unit/FreshFlow.Auth.UnitTests/Services/JwtTokenServiceTests.cs` — run RED, then GREEN
- [x] T016 Create `AdminSeeder` (`IHostedService`) in `src/Modules/Auth/FreshFlow.Auth.Infrastructure/Seed/AdminSeeder.cs` — idempotent, reads `ADMIN_SEED_EMAIL` / `ADMIN_SEED_PASSWORD` env vars
- [x] T017 Create `AddAuthModule()` DI extension in `src/Modules/Auth/FreshFlow.Auth.Infrastructure/DependencyInjection.cs` (registers services, repositories, seeder; wires JWT Bearer auth with role claim mapping)

**Checkpoint**: `dotnet build` passes; unit tests T014–T015 GREEN. Migration is generated later (T009) once all EF configs exist.

---

## Phase 3: User Story 1 — Authenticated User Logs In And Maintains Session (Priority: P1) 🎯 MVP

**Goal**: Any registered user can log in, get a JWT + refresh token, rotate the refresh token, detect reuse (family revoke), and log out a single session.

**Independent Test**: Seed an Admin, POST /auth/login → get tokens, call a protected endpoint with the access token, POST /auth/refresh → get new tokens (old ones rejected), POST /auth/logout, verify old refresh token rejected.

### Tests for User Story 1 (write FIRST — must FAIL before implementation)

- [x] T018 [P] [US1] Write `LoginCommandHandlerTests` (valid creds → tokens; wrong password → Failure; inactive account → Failure) in `tests/Unit/FreshFlow.Auth.UnitTests/Commands/LoginCommandHandlerTests.cs`
- [x] T019 [P] [US1] Write `LoginCommandValidatorTests` (empty email, invalid format, empty password) in `tests/Unit/FreshFlow.Auth.UnitTests/Validators/LoginCommandValidatorTests.cs`
- [x] T020 [P] [US1] Write `RefreshTokenCommandHandlerTests` (valid rotation; expired token; reuse detected → family revoke) in `tests/Unit/FreshFlow.Auth.UnitTests/Commands/RefreshTokenCommandHandlerTests.cs`
- [x] T021 [P] [US1] Write `LogoutCommandHandlerTests` (valid logout revokes only submitted token; missing token returns success) in `tests/Unit/FreshFlow.Auth.UnitTests/Commands/LogoutCommandHandlerTests.cs`
- [x] T022 [P] [US1] Write `LoginEndpointTests` (200 with tokens; 401 wrong creds; 400 missing fields; 422 inactive) in `tests/Integration/FreshFlow.IntegrationTests/Auth/LoginEndpointTests.cs`
- [x] T023 [P] [US1] Write `RefreshTokenEndpointTests` (200 new pair; 401 expired; 401 revoked; 409 reuse) in `tests/Integration/FreshFlow.IntegrationTests/Auth/RefreshTokenEndpointTests.cs`
- [x] T024 [P] [US1] Write `LogoutEndpointTests` (204 on valid logout; 401 missing bearer; 204 even if token already revoked) in `tests/Integration/FreshFlow.IntegrationTests/Auth/LogoutEndpointTests.cs`

### Implementation for User Story 1

- [x] T025 [P] [US1] Create `RefreshToken` entity in `src/Modules/Auth/FreshFlow.Auth.Domain/Entities/RefreshToken.cs` (Id, UserId, TokenHash, FamilyId, ExpiresAt, RevokedAt, ReplacedByTokenId, CreatedAt)
- [x] T026 [P] [US1] Create `UserMarketAssignment` entity in `src/Modules/Auth/FreshFlow.Auth.Domain/Entities/UserMarketAssignment.cs`
- [x] T027 [P] [US1] Create `RefreshTokenConfiguration` in `src/Modules/Auth/FreshFlow.Auth.Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs`
- [x] T028 [P] [US1] Create `UserMarketAssignmentConfiguration` in `src/Modules/Auth/FreshFlow.Auth.Infrastructure/Persistence/Configurations/UserMarketAssignmentConfiguration.cs`
- [x] T009 [US1] Run `dotnet ef migrations add InitAuth --project src/FreshFlow.Infrastructure.Persistence --startup-project src/FreshFlow.API` — all four EF configs now exist (UserConfiguration T010, DriverProfileConfiguration T011, RefreshTokenConfiguration T027, UserMarketAssignmentConfiguration T028); migration must include all four tables: `users`, `refresh_tokens`, `user_market_assignments`, `driver_profiles` *(moved from Phase 2 — C1 fix: migration requires all configs to exist first)*
- [x] T029 [US1] Create `IUserRepository` in `src/Modules/Auth/FreshFlow.Auth.Application/Abstractions/IUserRepository.cs` + `UserRepository` in `src/Modules/Auth/FreshFlow.Auth.Infrastructure/Repositories/UserRepository.cs` (FindByEmailAsync, FindByIdAsync, ExistsAsync)
- [x] T030 [US1] Create `IRefreshTokenRepository` in `src/Modules/Auth/FreshFlow.Auth.Application/Abstractions/IRefreshTokenRepository.cs` + `RefreshTokenRepository` in `src/Modules/Auth/FreshFlow.Auth.Infrastructure/Repositories/RefreshTokenRepository.cs` (FindByFamilyAsync, AddAsync, RevokeByFamilyAsync)
- [x] T031 [US1] Implement `LoginCommand` + `LoginCommandHandler` + `LoginCommandValidator` + `LoginResponse` in `src/Modules/Auth/FreshFlow.Auth.Application/Commands/Login/`
- [x] T032 [US1] Implement `RefreshTokenCommand` + `RefreshTokenCommandHandler` + `RefreshTokenCommandValidator` + `RefreshTokenResponse` in `src/Modules/Auth/FreshFlow.Auth.Application/Commands/RefreshToken/`
- [x] T033 [US1] Implement `LogoutCommand` + `LogoutCommandHandler` + `LogoutCommandValidator` in `src/Modules/Auth/FreshFlow.Auth.Application/Commands/Logout/`
- [x] T034 [US1] Create `AuthController` with `POST /api/v1/auth/login`, `POST /api/v1/auth/refresh` ([AllowAnonymous]), `POST /api/v1/auth/logout` ([Authorize]) in `src/FreshFlow.API/Controllers/AuthController.cs`
- [x] T035 [US1] Run tests T018–T024; fix implementation until all GREEN

**Checkpoint**: Login → refresh → logout flow fully working. Admin seed account can log in. Unit + integration tests GREEN.

---

## Phase 4: User Story 2 — Admin Creates And Approves Users (Priority: P1)

**Goal**: Seeded Admin can create accounts for all v1 roles (market_agent, hub_staff, driver, restaurant), list users, activate/deactivate accounts, and approve restaurants. Non-Admin users receive 403.

**Independent Test**: Log in as seeded Admin, create one user of each role via POST /admin/users, approve the restaurant, list all users, deactivate one account.

### Tests for User Story 2 (write FIRST — must FAIL before implementation)

- [x] T036 [P] [US2] Write `CreateUserCommandHandlerTests` (all 4 roles; email conflict → 409; invalid marketId → 422; restaurant starts unapproved) in `tests/Unit/FreshFlow.Auth.UnitTests/Commands/CreateUserCommandHandlerTests.cs`
- [x] T037 [P] [US2] Write `CreateUserCommandValidatorTests` (password strength; marketId required for market_agent; restaurantName required for restaurant; kiosk_staff alias) in `tests/Unit/FreshFlow.Auth.UnitTests/Validators/CreateUserCommandValidatorTests.cs`
- [x] T038 [P] [US2] Write `ActivateUserCommandHandlerTests` (deactivate user; reactivate; cannot deactivate self → 422; user not found → 404) in `tests/Unit/FreshFlow.Auth.UnitTests/Commands/ActivateUserCommandHandlerTests.cs`
- [x] T039 [P] [US2] Write `ApproveRestaurantCommandHandlerTests` (approve; already approved → 422; not found → 404) in `tests/Unit/FreshFlow.Auth.UnitTests/Commands/ApproveRestaurantCommandHandlerTests.cs`
- [x] T040 [P] [US2] Write `GetUsersQueryHandlerTests` (paginated list; filter by role; filter by isActive; search by email) in `tests/Unit/FreshFlow.Auth.UnitTests/Queries/GetUsersQueryHandlerTests.cs`
- [x] T041 [P] [US2] Write `AdminUsersEndpointTests` (201 create; 403 non-admin; 409 duplicate email; 422 invalid market; pagination on GET) in `tests/Integration/FreshFlow.IntegrationTests/Auth/AdminUsersEndpointTests.cs`
- [x] T042 [P] [US2] Write `ApproveRestaurantEndpointTests` (200 approve; 422 already approved; 403 non-admin; 404 not found) in `tests/Integration/FreshFlow.IntegrationTests/Auth/ApproveRestaurantEndpointTests.cs`

### Implementation for User Story 2

- [x] T043 [P] [US2] Implement `CreateUserCommand` + `CreateUserCommandHandler` + `CreateUserCommandValidator` + `CreateUserResponse` in `src/Modules/Auth/FreshFlow.Auth.Application/Commands/Admin/CreateUser/` (normalise kiosk_staff → market_agent; call IRestaurantProfileCreator / IDriverProfileCreator as needed)
- [x] T044 [P] [US2] Implement `ActivateUserCommand` + `ActivateUserCommandHandler` + `ActivateUserCommandValidator` + `ActivateUserResponse` in `src/Modules/Auth/FreshFlow.Auth.Application/Commands/Admin/ActivateUser/`
- [x] T045 [P] [US2] Implement `ApproveRestaurantCommand` + `ApproveRestaurantCommandHandler` + `ApproveRestaurantResponse` in `src/Modules/Auth/FreshFlow.Auth.Application/Commands/Admin/ApproveRestaurant/`
- [x] T046 [US2] Implement `GetUsersQuery` + `GetUsersQueryHandler` + `GetUsersQueryValidator` + `GetUsersResponse` (with marketAssignments for market_agent; isApproved for restaurant) in `src/Modules/Auth/FreshFlow.Auth.Application/Queries/GetUsers/`
- [x] T047 [US2] Create `AdminController` with `POST /api/v1/admin/users`, `GET /api/v1/admin/users`, `PATCH /api/v1/admin/users/{userId}/activate`, `PATCH /api/v1/admin/restaurants/{restaurantId}/approve` — all `[Authorize(Roles = "admin")]` — in `src/FreshFlow.API/Controllers/AdminController.cs`
- [x] T048 [US2] Run tests T036–T042; fix implementation until all GREEN

**Checkpoint**: Admin can create, list, activate, and approve users. All four v1 roles creatable. Unit + integration tests GREEN.

---

## Phase 5: User Story 3 — Role-Based Access Is Enforced (Priority: P1)

**Goal**: Every protected endpoint enforces authentication and role. 401 for missing/invalid tokens, 403 for wrong role. Seeded Admin exists on every fresh start.

**Independent Test**: Call every Auth and Admin endpoint without a token (expect 401), with a non-Admin token (expect 403 on Admin endpoints), and with the correct role (expect success). Verify Admin seed runs exactly once on repeated restarts.

### Tests for User Story 3 (write FIRST — must FAIL before implementation)

- [x] T049 [P] [US3] Write `RbacEnforcementTests` — assert 401 on every endpoint when unauthenticated; assert 403 on Admin endpoints with non-Admin JWT — in `tests/Integration/FreshFlow.IntegrationTests/Auth/RbacEnforcementTests.cs`
- [x] T050 [P] [US3] Write `AdminSeedTests` — assert Admin account exists after startup; assert second startup does not duplicate it — in `tests/Integration/FreshFlow.IntegrationTests/Auth/AdminSeedTests.cs`

### Implementation for User Story 3

- [x] T051 [US3] Verify all controller actions in `AuthController.cs` and `AdminController.cs` have explicit `[Authorize]`, `[Authorize(Roles = "admin")]`, or `[AllowAnonymous]` attributes; add any missing ones
- [x] T052 [US3] Add startup CI lint test that asserts no controller action is missing an auth attribute — in `tests/Unit/FreshFlow.Auth.UnitTests/Rbac/AllEndpointsDecoratedTests.cs`
- [x] T053 [US3] Run tests T049–T052; fix until all GREEN

**Checkpoint**: Complete Auth module: all 12 FRs covered; all 3 user stories independently testable; all CI gates passing.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [x] T054 [P] Run `dotnet format FreshFlow.sln --verify-no-changes`; fix any formatting violations
- [x] T055 [P] Run `dotnet build FreshFlow.sln`; fix any build warnings treated as errors
- [x] T056 Run `dotnet test FreshFlow.sln --filter Category=Unit` — confirm all unit tests GREEN
- [x] T057 Run `dotnet test FreshFlow.sln --filter Category=Integration` — confirm all integration tests GREEN against real PostgreSQL
- [x] T058 Validate `quickstart.md` smoke test end-to-end: seed login → create restaurant user → approve → refresh token → logout — all steps pass
- [x] T061 Run OWASP dependency check — `dotnet list package --vulnerable --include-transitive`; resolve any CRITICAL CVEs before merging to main (Constitution III Gate 4; if CI uses `dotnet-ossindex`, run `dotnet ossindex` instead and fail on severity ≥ HIGH)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — **BLOCKS all user stories**
- **US1 (Phase 3)**: Depends on Phase 2 only
- **US2 (Phase 4)**: Depends on Phase 2; functionally depends on US1 (needs login to get Admin token for integration tests)
- **US3 (Phase 5)**: Depends on US1 and US2 being complete (needs all endpoints to exist for RBAC tests)
- **Polish (Phase 6)**: Depends on all story phases complete

### Within Each User Story

1. Write ALL tests for the story (RED)
2. Create entities / EF configs (models)
3. Create repositories
4. Create command/query handlers
5. Create controller
6. Run tests → GREEN

### Parallel Opportunities

Within Phase 2: T005–T008, T010–T011 in parallel; T014–T015 in parallel after T012–T013.

Within Phase 3 tests: T018–T024 all in parallel (different files, no dependencies).
Within Phase 3 models: T025–T028 all in parallel.
Then T029–T030 (repositories) in parallel, then T031–T033 (handlers) sequentially within each command, then T034 (controller).

Within Phase 4 tests: T036–T042 all in parallel.
Within Phase 4 handlers: T043–T045 in parallel; T046 after.

---

## Parallel Example: Phase 3 Tests (launch together)

```text
Task: "Write LoginCommandHandlerTests in tests/Unit/.../Commands/LoginCommandHandlerTests.cs"
Task: "Write LoginCommandValidatorTests in tests/Unit/.../Validators/LoginCommandValidatorTests.cs"
Task: "Write RefreshTokenCommandHandlerTests in tests/Unit/.../Commands/RefreshTokenCommandHandlerTests.cs"
Task: "Write LogoutCommandHandlerTests in tests/Unit/.../Commands/LogoutCommandHandlerTests.cs"
Task: "Write LoginEndpointTests in tests/Integration/.../Auth/LoginEndpointTests.cs"
Task: "Write RefreshTokenEndpointTests in tests/Integration/.../Auth/RefreshTokenEndpointTests.cs"
Task: "Write LogoutEndpointTests in tests/Integration/.../Auth/LogoutEndpointTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL)
3. Complete Phase 3: User Story 1 — login, refresh, logout
4. **STOP and VALIDATE**: `dotnet test --filter Category=Unit`, `dotnet test --filter Category=Integration`
5. All other modules can now use JWT auth

### Incremental Delivery

1. Setup + Foundational → crypto services + Admin seed ready
2. US1 → JWT session management working → **any downstream module can integrate auth**
3. US2 → Admin can create all v1 users → **onboarding flow complete**
4. US3 → RBAC verified end-to-end → **safe to begin Pricing module**

---

## Notes

- [P] tasks target different files — safe to parallelize
- [Story] label maps each task to its user story for traceability
- Constitution III: write tests FIRST, ensure RED, then implement
- No database mocks — integration tests use `Testcontainers.PostgreSql`
- `kiosk_staff` is a legacy alias — normalize to `market_agent` in `CreateUserCommandValidator`
- `IRestaurantProfileCreator` and `IDriverProfileCreator` are in `Auth.Application/Abstractions/`; implementations live in `FreshFlow.Infrastructure.Persistence/CrossModule/`
- Access token revocation is NOT implemented — tokens expire naturally within 15 min (by design)
