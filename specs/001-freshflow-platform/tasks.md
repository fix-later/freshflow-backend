---
description: "Task list for FreshFlow Platform — all 6 sprints"
---

# Tasks: FreshFlow Platform

**Input**: `specs/001-freshflow-platform/` (plan.md, spec.md, data-model.md, contracts/, research.md)
**Branch**: `001-freshflow-platform`

**Tests**: Per Constitution Principle III, test coverage is MANDATORY before merging to `main`.
Unit tests (per module) and integration tests (real PostgreSQL + Redis, no mocks) MUST be
included. The CI pipeline enforces these gates — omitting them will block the PR.

**Assignee legend**: BE/DevOps · FE1-Web (admin/restaurant Angular) · FE2-Web (kiosk Angular + shared lib) · FE3-Mobile (React Native)

**Sizing**: S = half day · M = 1 day · L = 2 days (split anything larger)

**Story points (Jira)**: S = 2 · M = 3–5 · L = 8

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the solution skeleton. Nothing else can start until T001 is done.

- [ ] T001 Scaffold .NET 8 solution with Clean Architecture (see detail below)
- [ ] T002 [P] Set up PostgreSQL + EF Core + Auth module schema migration
- [ ] T003 [P] Set up Redis connection and RedisCacheService
- [ ] T004 Set up Docker Compose (nginx + api + postgres + redis)
- [ ] T005 [P] Configure GitHub Actions CI/CD pipeline skeleton

---

### T001 — Scaffold .NET 8 solution with Clean Architecture
**Assignee**: BE/DevOps | **Estimated**: L (2 days) | **Story points**: 8

**Files to create**:
```
FreshFlow.sln
src/Shared/FreshFlow.SharedKernel/FreshFlow.SharedKernel.csproj
  BaseEntity.cs, AggregateRoot.cs, Result.cs, Error.cs, ICommand.cs, IQuery.cs, IDomainEvent.cs
src/Shared/FreshFlow.Contracts/FreshFlow.Contracts.csproj
  (empty — placeholder for integration event records)
src/Modules/Auth/{Domain,Application,Infrastructure}/FreshFlow.Auth.*.csproj  (×3)
src/Modules/Pricing/{Domain,Application,Infrastructure}/FreshFlow.Pricing.*.csproj  (×3)
src/Modules/Orders/{Domain,Application,Infrastructure}/FreshFlow.Orders.*.csproj  (×3)
src/Modules/Logistics/{Domain,Application,Infrastructure}/FreshFlow.Logistics.*.csproj  (×3)
src/Modules/Hub/{Domain,Application,Infrastructure}/FreshFlow.Hub.*.csproj  (×3)
src/Modules/Analytics/{Domain,Application,Infrastructure}/FreshFlow.Analytics.*.csproj  (×3)
src/Modules/Notifications/{Domain,Application,Infrastructure}/FreshFlow.Notifications.*.csproj  (×3)
src/FreshFlow.Infrastructure.Persistence/FreshFlow.Infrastructure.Persistence.csproj
  AppDbContext.cs (empty, ApplyConfigurationsFromAssembly stub)
src/FreshFlow.API/FreshFlow.API.csproj
  Program.cs, appsettings.json, appsettings.Development.json
tests/Unit/FreshFlow.Auth.UnitTests/
tests/Integration/FreshFlow.IntegrationTests/
.editorconfig, .gitignore, global.json
```

**Dependency rules enforced via .csproj references**:
- Domain → SharedKernel only
- Application → Domain + SharedKernel + Contracts
- Infrastructure → Application + packages
- API → all Infrastructure projects

**Depends on**: —

**Acceptance criteria**:
1. `dotnet build FreshFlow.sln` exits 0 with no warnings or errors.
2. Adding a reference from any Domain project to another module's project causes a build error (verify manually for one pair).
3. `GET /swagger` returns Swagger UI in `Development` environment.
4. `dotnet test FreshFlow.sln` exits 0 (empty test projects produce no failures).

---

### T002 — Set up PostgreSQL + EF Core + Auth module schema migration
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
src/FreshFlow.Infrastructure.Persistence/AppDbContext.cs
src/FreshFlow.Infrastructure.Persistence/Migrations/20260511_000001_AuthSchema.cs
src/Modules/Auth/FreshFlow.Auth.Infrastructure/Persistence/Configurations/
  UserConfiguration.cs, RefreshTokenConfiguration.cs, UserMarketAssignmentConfiguration.cs
src/Modules/Auth/FreshFlow.Auth.Domain/Entities/User.cs
src/Modules/Auth/FreshFlow.Auth.Domain/Entities/RefreshToken.cs
src/FreshFlow.API/appsettings.json  (ConnectionStrings__DefaultConnection)
src/FreshFlow.API/Program.cs  (MigrateAsync on startup)
```

**Depends on**: T001

**Acceptance criteria**:
1. Running `dotnet ef database update` against a blank PostgreSQL instance creates `users`, `refresh_tokens`, and `user_market_assignments` tables with all columns, FK constraints, and indexes exactly matching `docs/03-database-schema.md`.
2. `dotnet run` with a healthy PostgreSQL instance applies migrations automatically before `app.Run()`.
3. `users_email_unique` unique constraint exists; inserting a duplicate email raises a constraint violation.

---

### T003 — Set up Redis connection and RedisCacheService
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
src/Shared/FreshFlow.SharedKernel/Caching/ICacheService.cs
src/Shared/FreshFlow.SharedKernel/Caching/RedisKeys.cs  (all key pattern constants)
src/FreshFlow.Infrastructure.Persistence/Caching/RedisCacheService.cs
src/FreshFlow.API/Program.cs  (AddStackExchangeRedis + ICacheService DI)
src/FreshFlow.API/appsettings.json  (ConnectionStrings__Redis)
tests/Integration/FreshFlow.IntegrationTests/Caching/RedisCacheServiceTests.cs
```

**Depends on**: T001

**Acceptance criteria**:
1. `ICacheService.SetAsync("test", "value", TimeSpan.FromMinutes(1))` stores the value; `GetAsync<string>("test")` returns `"value"`.
2. When Redis is unreachable, `GetAsync` returns `null` and logs a Warning-level entry — no exception is thrown.
3. `RedisKeys` class defines constants for all key patterns: `price:{marketId}:{productId}`, `reservation:{marketId}:{productId}`, `route:{hash}`, `analytics:{type}:{date}`.

---

### T004 — Set up Docker Compose (nginx + api + postgres + redis)
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
docker-compose.yml
docker-compose.override.yml  (dev port overrides: 5432, 6379 exposed)
nginx/nginx.conf  (TLS termination, proxy to api:8080, WebSocket upgrade for /hubs/)
src/FreshFlow.API/Dockerfile  (multi-stage: restore → build → publish → runtime)
.env.example
```

**Depends on**: T001

**Acceptance criteria**:
1. `docker compose up -d` starts all 4 containers; `docker compose ps` shows all as `healthy` within 60 seconds.
2. `curl http://localhost/health` returns `{"status":"Healthy"}` via Nginx (port 80).
3. `api` container does not start until `postgres` and `redis` pass their healthchecks (`depends_on: condition: service_healthy`).
4. A new team member following `specs/001-freshflow-platform/quickstart.md` has the stack running in under 10 minutes.

---

### T005 — Configure GitHub Actions CI/CD pipeline skeleton
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
.github/workflows/ci.yml
.github/pull_request_template.md
```

**Pipeline stages**: restore → build → unit-tests (--filter Category=Unit) → integration-tests (--filter Category=Integration) → format-check (dotnet format --verify-no-changes) → Docker-build (main only) → push (main only)

**Depends on**: T001

**Acceptance criteria**:
1. A PR to `main` triggers the CI workflow; all stages pass on a clean repo.
2. A deliberate unit test failure causes the pipeline to fail and blocks the PR merge.
3. `dotnet format --verify-no-changes` fails if any file has formatting violations.
4. Docker build stage runs only on pushes to `main`, not on PRs.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: JWT auth infrastructure. MUST complete before any auth endpoint work.

**⚠ CRITICAL**: Sprint 1 user story work is blocked until this phase is complete.

- [ ] T006 Configure JWT authentication middleware and BCrypt password service
- [ ] T007 [P] Configure SignalR with Redis backplane (stub hubs only)
- [ ] T008 [P] Configure Serilog structured logging + correlation ID middleware + /health endpoint

**Checkpoint**: Foundation ready — auth endpoint work (Phase 3) can begin.

---

### T006 — Configure JWT authentication middleware and BCrypt password service
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
src/Modules/Auth/FreshFlow.Auth.Application/Abstractions/ITokenService.cs
src/Modules/Auth/FreshFlow.Auth.Application/Abstractions/IPasswordService.cs
src/Modules/Auth/FreshFlow.Auth.Infrastructure/Services/JwtTokenService.cs
src/Modules/Auth/FreshFlow.Auth.Infrastructure/Services/BcryptPasswordService.cs
src/FreshFlow.API/Program.cs  (AddAuthentication + AddJwtBearer)
src/FreshFlow.API/appsettings.json  (Jwt__SecretKey, Jwt__AccessTokenTTL, Jwt__RefreshTokenTTL)
tests/Unit/FreshFlow.Auth.UnitTests/Services/JwtTokenServiceTests.cs
```

**Depends on**: T001

**Acceptance criteria**:
1. `JwtTokenService.GenerateAccessToken(user)` returns a signed JWT whose decoded payload contains `sub` (user UUID), `email`, `role`, and `exp - iat = 900` seconds.
2. A request to any `[Authorize]` endpoint with a valid JWT returns the handler's response; a missing JWT returns HTTP 401.
3. `BcryptPasswordService.Hash("password")` produces a BCrypt hash (starts with `$2`) that `Verify("password", hash)` returns `true` for.
4. Work factor is set to ≥ 12 in `appsettings.json`.

---

### T007 — Configure SignalR with Redis backplane (stub hubs)
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5
> [HIGH-RISK: SignalR + Redis pub/sub] First integration of SignalR with Redis backplane.
> Verify message delivery between two simulated API instances in integration tests.
> Common failure: token not passed correctly via `?access_token=` query param; test this early.

**Files to create/modify**:
```
src/FreshFlow.API/SignalR/PricingHub.cs  (stub: JWT auth on negotiate only)
src/FreshFlow.API/SignalR/OrderHub.cs   (stub)
src/FreshFlow.API/SignalR/DeliveryHub.cs (stub)
src/FreshFlow.API/Program.cs  (AddSignalR().AddStackExchangeRedis() + MapHub endpoints)
```

**Depends on**: T003, T006

**Acceptance criteria**:
1. A client can connect to `/hubs/pricing` using a valid JWT via `?access_token=` query param and the connection is accepted (HTTP 101 Switching Protocols).
2. A client connecting without a JWT receives HTTP 401 on the `/hubs/pricing/negotiate` endpoint.
3. With `SignalR__UseRedis=true`, the Redis backplane connects successfully at startup (visible in startup logs).
4. With `SignalR__UseRedis=false` (local dev override), SignalR runs without Redis.

---

### T008 — Configure Serilog + correlation ID middleware + /health endpoint
**Assignee**: BE/DevOps | **Estimated**: S (half day) | **Story points**: 2

**Files to create/modify**:
```
src/FreshFlow.API/Middleware/CorrelationIdMiddleware.cs
src/FreshFlow.API/Program.cs  (Serilog, correlation middleware, MapHealthChecks)
src/FreshFlow.API/appsettings.json  (Serilog config)
```

**Depends on**: T002, T003

**Acceptance criteria**:
1. `GET /health` returns `{"status":"Healthy","components":{"postgresql":{"status":"Healthy"},"redis":{"status":"Healthy"}}}` when both services are up.
2. When PostgreSQL is unreachable, `GET /health` returns HTTP 503.
3. Every log line contains `correlationId`, `timestamp`, and `level` fields in JSON format.
4. `X-Correlation-Id` header on a request is propagated to all log entries for that request.

---

## Phase 3: Sprint 1 User Stories — Authentication (Priority: P1-equivalent)

**Goal**: End-to-end auth flow: login → access protected endpoint → refresh → logout on all three clients.

**Independent Test**: `POST /auth/login` with seeded admin credentials returns a valid JWT; `GET /health` with the token passes the authorize check; `POST /auth/refresh` issues a new token pair; `POST /auth/logout` revokes the token.

- [ ] T009 [US1] Implement POST /auth/login endpoint
- [ ] T010 [US1] Implement POST /auth/refresh with token rotation
- [ ] T011 [P] [US1] Implement POST /auth/logout endpoint
- [ ] T012 [P] [US1] Implement POST /auth/register (Admin only) + market assignment endpoint
- [ ] T013 [P] [US1] Scaffold Angular app with routing, auth guard, and JWT HTTP interceptor
- [ ] T014 [US1] Implement Angular login page and auth service
- [ ] T015 [P] [US1] Set up Angular shared component library + kiosk feature module scaffold
- [ ] T016 [US1] Scaffold React Native app with navigation and auth state
- [ ] T017 [US1] Implement React Native login screen + expo-secure-store token storage
- [ ] T018 [P] [US1] Implement React Native api.service.ts + auto token refresh

**Checkpoint**: Sprint 1 demo — all three clients can log in, token refreshes automatically, logout revokes the token.

---

### T009 — Implement POST /auth/login endpoint
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
src/FreshFlow.API/Controllers/AuthController.cs
src/Modules/Auth/FreshFlow.Auth.Application/Commands/Login/LoginCommand.cs
src/Modules/Auth/FreshFlow.Auth.Application/Commands/Login/LoginCommandHandler.cs
src/Modules/Auth/FreshFlow.Auth.Application/Commands/Login/LoginCommandValidator.cs
src/Modules/Auth/FreshFlow.Auth.Application/DTOs/LoginRequestDto.cs
src/Modules/Auth/FreshFlow.Auth.Application/DTOs/LoginResponseDto.cs
src/Modules/Auth/FreshFlow.Auth.Infrastructure/Persistence/Repositories/UserRepository.cs
src/Modules/Auth/FreshFlow.Auth.Infrastructure/Persistence/Repositories/RefreshTokenRepository.cs
src/Modules/Auth/FreshFlow.Auth.Domain/Repositories/IUserRepository.cs
src/Modules/Auth/FreshFlow.Auth.Domain/Repositories/IRefreshTokenRepository.cs
src/Modules/Auth/FreshFlow.Auth.Infrastructure/DependencyInjection.cs  (AddAuthModule)
tests/Unit/FreshFlow.Auth.UnitTests/Commands/LoginCommandHandlerTests.cs
tests/Integration/FreshFlow.IntegrationTests/Auth/LoginEndpointTests.cs
```

**Depends on**: T006, T002

**Acceptance criteria**:
1. `POST /api/v1/auth/login` with valid credentials returns HTTP 200 with `accessToken`, `refreshToken`, `expiresIn: 900`, and `user.role`.
2. Wrong password returns HTTP 401 with `error.code: "INVALID_CREDENTIALS"` — no token in body.
3. Non-existent email returns HTTP 401 with same `INVALID_CREDENTIALS` code (no user-enumeration leak).
4. Decoded JWT payload contains `sub`, `email`, `role`, `iat`, `exp`; `exp - iat = 900`.
5. Missing email field returns HTTP 400 with FluentValidation field-level error.

---

### T010 — Implement POST /auth/refresh with token rotation
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
src/Modules/Auth/FreshFlow.Auth.Application/Commands/RefreshToken/RefreshTokenCommand.cs
src/Modules/Auth/FreshFlow.Auth.Application/Commands/RefreshToken/RefreshTokenCommandHandler.cs
src/Modules/Auth/FreshFlow.Auth.Application/Commands/RefreshToken/RefreshTokenCommandValidator.cs
src/FreshFlow.API/Controllers/AuthController.cs  (add refresh action)
tests/Unit/FreshFlow.Auth.UnitTests/Commands/RefreshTokenCommandHandlerTests.cs
tests/Integration/FreshFlow.IntegrationTests/Auth/RefreshTokenEndpointTests.cs
```

**Depends on**: T009

**Acceptance criteria**:
1. `POST /api/v1/auth/refresh` with a valid token returns HTTP 200 with a new `accessToken` and `refreshToken`; the old refresh token is revoked in `refresh_tokens` (`revoked_at` is set).
2. Reusing the same token a second time returns HTTP 401 `REFRESH_TOKEN_REUSE`; ALL tokens in the same `family_id` are revoked (verify in DB).
3. An expired token returns HTTP 401 `REFRESH_TOKEN_EXPIRED`.
4. A token that was never issued returns HTTP 401.

---

### T011 — Implement POST /auth/logout endpoint
**Assignee**: BE/DevOps | **Estimated**: S (half day) | **Story points**: 2

**Files to create/modify**:
```
src/Modules/Auth/FreshFlow.Auth.Application/Commands/Logout/LogoutCommand.cs
src/Modules/Auth/FreshFlow.Auth.Application/Commands/Logout/LogoutCommandHandler.cs
src/FreshFlow.API/Controllers/AuthController.cs  (add logout action)
tests/Integration/FreshFlow.IntegrationTests/Auth/LogoutEndpointTests.cs
```

**Depends on**: T010

**Acceptance criteria**:
1. `POST /api/v1/auth/logout` with valid Bearer + valid refreshToken body returns HTTP 204; refresh token is revoked in DB.
2. After logout, `POST /api/v1/auth/refresh` with the revoked token returns HTTP 401.
3. Calling logout without `Authorization` header returns HTTP 401 (endpoint is protected).
4. Logout does not affect other active sessions for the same user (device-level isolation).

---

### T012 — Implement POST /auth/register (Admin only) + user market assignment
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
src/Modules/Auth/FreshFlow.Auth.Application/Commands/Register/RegisterUserCommand.cs
src/Modules/Auth/FreshFlow.Auth.Application/Commands/Register/RegisterUserCommandHandler.cs
src/Modules/Auth/FreshFlow.Auth.Application/Commands/Register/RegisterUserCommandValidator.cs
src/FreshFlow.API/Controllers/AdminController.cs  (POST /admin/users, PATCH /admin/users/{id}/approve)
src/Modules/Auth/FreshFlow.Auth.Application/Commands/AssignMarket/AssignMarketCommand.cs
tests/Integration/FreshFlow.IntegrationTests/Auth/RegisterEndpointTests.cs
```

**Depends on**: T009

**Acceptance criteria**:
1. `POST /api/v1/auth/register` by an Admin with `role: kiosk_staff` creates the account and returns HTTP 201 with `userId`.
2. A non-Admin token returns HTTP 403.
3. Duplicate email returns HTTP 409 `EMAIL_ALREADY_EXISTS`.
4. `PATCH /api/v1/admin/users/{id}/approve` sets `is_active: true` for PENDING accounts.

---

### T013 — Scaffold Angular app with routing, auth guard, and JWT HTTP interceptor
**Assignee**: FE1-Web | **Estimated**: L (2 days) | **Story points**: 8

**Files to create/modify**:
```
freshflow-web/
  angular.json, package.json, tsconfig.json, tsconfig.app.json
  src/app/app.routes.ts  (lazy-loaded: auth, pricing, orders, logistics, analytics, admin)
  src/app/core/guards/auth.guard.ts
  src/app/core/guards/role.guard.ts
  src/app/core/interceptors/auth.interceptor.ts  (attaches Bearer; proactive refresh at exp-60s)
  src/app/core/services/auth.service.ts  (login, logout, refresh, token storage)
  src/app/core/services/signalr.service.ts  (stub — connect/disconnect lifecycle only)
  src/environments/environment.ts
  src/environments/environment.prod.ts
```

**Depends on**: T001

**Acceptance criteria**:
1. `ng build --configuration production` completes without TypeScript errors or warnings.
2. Navigating to `/pricing` without a token redirects to `/login`; after login, redirects back to `/pricing`.
3. `AuthInterceptor` attaches `Authorization: Bearer <token>` to all API requests.
4. When a 401 is received, the interceptor attempts one token refresh and retries the original request transparently; a second 401 logs the user out.
5. All components use `OnPush` change detection; no `any` types.

---

### T014 — Implement Angular login page and auth service
**Assignee**: FE1-Web | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
freshflow-web/src/app/features/auth/
  auth.routes.ts
  login/login.component.ts
  login/login.component.html
  login/login.component.scss
freshflow-web/src/app/core/services/auth.service.ts  (complete implementation)
```

**Depends on**: T013, T009

**Acceptance criteria**:
1. Submitting valid credentials navigates to `/dashboard`; invalid credentials shows an inline error message (no page reload).
2. Loading spinner is shown during the login API call.
3. Tokens are stored in `localStorage`; after browser refresh the user remains logged in.
4. "Logout" button in the nav calls `POST /api/v1/auth/logout` and redirects to `/login`.

---

### T015 — Set up Angular shared component library + kiosk feature module scaffold
**Assignee**: FE2-Web | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
freshflow-web/src/app/shared/components/
  button/ff-button.component.ts
  input/ff-input.component.ts
  badge/ff-badge.component.ts
  spinner/ff-spinner.component.ts
freshflow-web/src/app/shared/pipes/
  vnd-currency.pipe.ts
freshflow-web/src/app/features/kiosk/
  kiosk.routes.ts
  kiosk-layout.component.ts  (stub)
```

**Depends on**: T013

**Acceptance criteria**:
1. `FfButtonComponent` accepts `[variant]` (`primary`/`secondary`), `[loading]`, and `[disabled]` inputs; renders appropriately.
2. All shared components use `OnPush` and are standalone.
3. Kiosk route lazy-loads from `/kiosk` without errors.
4. `VndCurrencyPipe` formats `150000` as `₫150,000`.

---

### T016 — Scaffold React Native app with navigation and auth state
**Assignee**: FE3-Mobile | **Estimated**: L (2 days) | **Story points**: 8

**Files to create/modify**:
```
freshflow-mobile/
  package.json (Expo SDK 50+, TypeScript, React Navigation, Zustand, expo-secure-store)
  tsconfig.json
  app.json / app.config.ts
  src/navigation/AppNavigator.tsx   (root: Auth stack OR App tabs based on auth state)
  src/navigation/KioskNavigator.tsx (Kiosk tab navigator stub)
  src/navigation/RestaurantNavigator.tsx (Restaurant tab navigator stub)
  src/store/authStore.ts  (Zustand: token, user, login(), logout(), restoreToken())
  src/services/api.service.ts  (Axios instance, base URL from env, interceptor hooks)
```

**Depends on**: T001

**Acceptance criteria**:
1. `tsc --noEmit` exits 0 with strict mode enabled.
2. App launches without errors on Android emulator (Expo Go or bare).
3. On cold launch with a stored valid token, app navigates directly to the appropriate navigator (Kiosk or Restaurant) without showing the login screen.
4. On cold launch with no token, app shows the login screen.

---

### T017 — Implement React Native login screen + expo-secure-store token storage
**Assignee**: FE3-Mobile | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
freshflow-mobile/src/screens/auth/LoginScreen.tsx
freshflow-mobile/src/services/auth.service.ts
  (login, logout, refreshToken — wraps api.service.ts; stores tokens via expo-secure-store)
```

**Depends on**: T016, T009

**Acceptance criteria**:
1. Submitting valid credentials navigates to the role-appropriate navigator (kiosk_staff → KioskNavigator; restaurant → RestaurantNavigator).
2. Invalid credentials show an inline error toast — no crash.
3. Tokens are stored in `expo-secure-store`; app restart does not force re-login if token is still valid.
4. "Login" button is disabled while the API request is in-flight.

---

### T018 — Implement React Native api.service.ts + auto token refresh
**Assignee**: FE3-Mobile | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
freshflow-mobile/src/services/api.service.ts  (complete Axios instance)
freshflow-mobile/src/services/auth.service.ts  (complete refreshToken logic)
```

**Pattern**: Axios request interceptor checks if `accessToken` is within 60 seconds of expiry before each request; if so, calls `POST /auth/refresh` first (with in-flight deduplication via `refreshPromise` guard); if refresh fails, logs user out.

**Depends on**: T017, T010

**Acceptance criteria**:
1. An API request made with a token expiring in < 60 seconds triggers a refresh transparently; the original request is retried with the new token.
2. If two API calls fire simultaneously while the token is near expiry, only ONE refresh request is sent (in-flight deduplication).
3. A `401` response to the refresh call logs the user out and navigates to `LoginScreen`.
4. All API calls go through `api.service.ts`; no direct `fetch` or `axios` calls in screen components.

---

## Phase 4: Sprint 2 — Real-Time Pricing (Priority: P1 from spec)

**Goal**: Kiosk Staff updates a price → all connected Restaurant users see the update within 2 seconds.

**Independent Test**: Open Angular dashboard for Market A. Update price via kiosk mobile (or cURL). Verify Angular row updates within 2 seconds without any page interaction.

- [ ] T019 [P] [US1] DB migration for Pricing module tables
- [ ] T020 [P] [US1] Implement product catalog endpoints (GET products, POST/PATCH admin/products, GET markets)
- [ ] T021 [US1] Implement market products listing with Redis cache
- [ ] T022 [US1] Implement price + quantity update endpoint (PATCH) with PostgreSQL write and Redis cache update
- [ ] T023 [US1] [HIGH-RISK: SignalR + Redis pub/sub] Implement PricingHub group management + broadcast on price update
- [ ] T024 [P] [US1] Implement price history endpoint with cursor pagination
- [ ] T025 [P] [US1] Implement significant price change detection + SignalR alert
- [ ] T026 [US1] Implement Angular pricing dashboard with live SignalR feed
- [ ] T027 [P] [US1] Implement Angular kiosk product list + price update screens
- [ ] T028 [US1] Implement React Native kiosk product list + price update screen

**Checkpoint**: Live demo — kiosk updates a price, Angular dashboard updates without refresh, significant alert highlights the row.

---

### T019 — DB migration for Pricing module tables
**Assignee**: BE/DevOps | **Estimated**: S (half day) | **Story points**: 2

**Files to create/modify**:
```
src/FreshFlow.Infrastructure.Persistence/Migrations/20260511_000002_PricingSchema.cs
src/Modules/Pricing/FreshFlow.Pricing.Infrastructure/Persistence/Configurations/
  MarketConfiguration.cs, ProductConfiguration.cs, MarketProductConfiguration.cs,
  PriceSnapshotConfiguration.cs, SystemConfigConfiguration.cs
src/Modules/Pricing/FreshFlow.Pricing.Domain/Entities/
  Market.cs, Product.cs, MarketProduct.cs, PriceSnapshot.cs, SystemConfig.cs
```

**price_snapshots note**: Use raw SQL in the migration for `PARTITION BY RANGE (recorded_at)` and create `price_snapshots_default` + current/next month partitions. EF Core `ToTable()` maps to the parent table.

**Depends on**: T002

**Acceptance criteria**:
1. Migration creates `markets`, `products`, `market_products`, `price_snapshots` (partitioned), `system_config` tables matching `docs/03-database-schema.md` DDL.
2. `INSERT INTO price_snapshots` with `recorded_at = NOW()` lands in the current month's partition, not the default partition.
3. `idx_price_snapshots_market_product_recorded_at` index exists on the parent table.
4. Seeded data: 3 markets (Hoc Mon, Binh Dien, Thu Duc) with coordinates; `significant_price_threshold_percent = 5.00` in `system_config`.

---

### T020 — Implement product catalog endpoints
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
src/FreshFlow.API/Controllers/PricingController.cs
src/Modules/Pricing/FreshFlow.Pricing.Application/
  Queries/GetProductsQuery.cs, GetMarketsQuery.cs
  Commands/CreateProduct/CreateProductCommand.cs
  Commands/CreateProduct/CreateProductCommandValidator.cs
  DTOs/ProductDto.cs, MarketDto.cs, CreateProductRequestDto.cs
  Services/ProductService.cs
src/Modules/Pricing/FreshFlow.Pricing.Infrastructure/Persistence/Repositories/
  ProductRepository.cs, MarketRepository.cs
src/Modules/Pricing/FreshFlow.Pricing.Infrastructure/DependencyInjection.cs  (AddPricingModule)
tests/Integration/FreshFlow.IntegrationTests/Pricing/ProductEndpointTests.cs
```

**Depends on**: T019, T006

**Acceptance criteria**:
1. `GET /api/v1/products` returns only active products (no `deleted_at`); admin token + `?includeInactive=true` returns all.
2. `POST /api/v1/admin/products` by Admin returns 201 with `productId`; Kiosk Staff token returns 403.
3. `GET /api/v1/markets` returns the 3 seeded markets with `id`, `name`, `latitude`, `longitude`.
4. `PATCH /api/v1/admin/products/{id}` updates name/unit/category; soft-delete via `PATCH` with `status: inactive` sets `deleted_at`.

---

### T021 — Implement market products listing with Redis cache
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
src/Modules/Pricing/FreshFlow.Pricing.Application/Queries/GetMarketProductsQuery.cs
src/Modules/Pricing/FreshFlow.Pricing.Application/DTOs/MarketProductDto.cs
src/Modules/Pricing/FreshFlow.Pricing.Application/Services/PricingService.cs
src/Modules/Pricing/FreshFlow.Pricing.Infrastructure/Persistence/Repositories/MarketProductRepository.cs
src/Modules/Pricing/FreshFlow.Pricing.Infrastructure/Caching/PricingCacheReader.cs
tests/Integration/FreshFlow.IntegrationTests/Pricing/MarketProductsEndpointTests.cs
```

**Cache logic**: Check `price:{marketId}:{productId}` Hash in Redis first. On miss, query `market_products` and populate cache. On Redis failure, fall back to PostgreSQL silently.
`availableQuantity = current_quantity - reserved_quantity` (computed in service layer).

**Depends on**: T019, T003

**Acceptance criteria**:
1. `GET /api/v1/markets/{marketId}/products` returns array with `productId`, `name`, `unit`, `currentPrice`, `currentQuantity`, `availableQuantity`, `updatedAt`.
2. Second request within TTL window is served from Redis cache (verifiable via Warn log on Redis hit vs miss).
3. When Redis is down, endpoint still returns correct data from PostgreSQL.
4. `availableQuantity` correctly reflects soft-reservations.

---

### T022 — Implement price + quantity update endpoints + Redis cache write
**Assignee**: BE/DevOps | **Estimated**: L (2 days) | **Story points**: 8

**Files to create/modify**:
```
src/Modules/Pricing/FreshFlow.Pricing.Application/Commands/UpdatePrice/
  UpdatePriceCommand.cs, UpdatePriceCommandHandler.cs, UpdatePriceCommandValidator.cs
src/Modules/Pricing/FreshFlow.Pricing.Application/Commands/UpdateQuantity/
  UpdateQuantityCommand.cs, UpdateQuantityCommandHandler.cs, UpdateQuantityCommandValidator.cs
src/Modules/Pricing/FreshFlow.Pricing.Application/DTOs/PriceUpdateRequestDto.cs, PriceUpdateResponseDto.cs
src/Modules/Pricing/FreshFlow.Pricing.Infrastructure/Caching/PricingCacheWriter.cs
src/Modules/Pricing/FreshFlow.Pricing.Infrastructure/Persistence/Repositories/PriceSnapshotRepository.cs
src/FreshFlow.API/Controllers/PricingController.cs  (add PATCH endpoints)
tests/Integration/FreshFlow.IntegrationTests/Pricing/PriceUpdateEndpointTests.cs
```

**Transaction**: `BEGIN → INSERT price_snapshots → UPDATE market_products.current_price → COMMIT → HSET Redis (fire-and-forget)`

**Depends on**: T021, T006

**Acceptance criteria**:
1. `PATCH /api/v1/markets/{marketId}/products/{productId}/price` with `{"price": 25000}` returns 200 with `updatedPrice`, `updatedAt`.
2. A Kiosk Staff user assigned to Market A calling this for Market B returns 403.
3. `price <= 0` returns 422 with field error `price: "Must be greater than 0"`.
4. After PATCH, `GET /markets/{id}/products/{id}` immediately reflects the new price (Redis + PG both updated).
5. A Redis failure does NOT roll back the PostgreSQL transaction — the write succeeds and a Warning is logged.

---

### T023 — Implement PricingHub group management + broadcast on price update
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5
> [HIGH-RISK: SignalR + Redis pub/sub] Real-time message delivery with Redis backplane.
> MITIGATION: Write an integration test that starts two `WebApplicationFactory` instances sharing
> the same Redis container, sends a price update to instance A, and verifies instance B's
> connected client receives `PriceUpdated` within 500 ms. Also test kiosk market-assignment
> enforcement (kiosk staff can only join `kiosk:{marketId}` for their assigned market).

**Files to create/modify**:
```
src/FreshFlow.API/SignalR/PricingHub.cs  (flesh out: JoinMarketGroup, LeaveMarketGroup, OnConnectedAsync for kiosk)
src/Modules/Pricing/FreshFlow.Pricing.Application/Abstractions/IPricingBroadcastService.cs
src/Modules/Notifications/FreshFlow.Notifications.Infrastructure/Realtime/PricingBroadcastService.cs
src/Modules/Pricing/FreshFlow.Pricing.Application/Commands/UpdatePrice/UpdatePriceCommandHandler.cs
  (add broadcast call — fire-and-forget)
tests/Integration/FreshFlow.IntegrationTests/Pricing/PricingHubTests.cs
```

**Depends on**: T022, T007

**Acceptance criteria**:
1. After `PATCH /price`, a SignalR client subscribed to `market:{marketId}` receives `PriceUpdated` event within 500 ms; payload contains `productId`, `marketId`, `newPrice`, `newQuantity`, `updatedAt`.
2. A client NOT subscribed to the market group does NOT receive the event.
3. Kiosk Staff attempting to join `market:{marketId}` for a market they're not assigned to receives a hub error.
4. Broadcast failure (Redis down) does NOT cause the PATCH endpoint to return an error (fire-and-forget).

---

### T024 — Implement price history endpoint with cursor pagination
**Assignee**: BE/DevOps | **Estimated**: S (half day) | **Story points**: 2

**Files to create/modify**:
```
src/Modules/Pricing/FreshFlow.Pricing.Application/Queries/GetPriceHistoryQuery.cs
src/Modules/Pricing/FreshFlow.Pricing.Application/DTOs/PriceSnapshotDto.cs
src/Modules/Pricing/FreshFlow.Pricing.Infrastructure/Persistence/Repositories/PriceSnapshotRepository.cs
src/FreshFlow.API/Controllers/PricingController.cs  (add price-history action)
```

**Cursor**: base64-encoded `{"id":"<uuid>","recordedAt":"<iso>"}` pointing to the last item returned.

**Depends on**: T022

**Acceptance criteria**:
1. `GET /api/v1/markets/{id}/products/{id}/price-history` returns records sorted descending by `recordedAt` with `nextCursor` in `meta`.
2. After 10 consecutive price updates, the endpoint returns ≥ 10 records with distinct `recordedAt` values.
3. No `DELETE` endpoint exists for price history; `PriceSnapshotRepository` exposes only append and read operations.

---

### T025 — Implement significant price change detection + SignalR alert
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
src/Modules/Pricing/FreshFlow.Pricing.Application/Services/SignificantChangeDetector.cs
src/Modules/Pricing/FreshFlow.Pricing.Application/Commands/UpdatePrice/UpdatePriceCommandHandler.cs
  (call SignificantChangeDetector after snapshot insert)
src/Modules/Pricing/FreshFlow.Pricing.Application/DTOs/SignificantPriceAlertDto.cs
src/FreshFlow.API/Controllers/PricingController.cs  (PATCH /admin/config/significant-price-threshold)
```

**Severity rules**: change ≥ 5% and < 15% → `MEDIUM`; ≥ 15% → `HIGH`.

**Depends on**: T023

**Acceptance criteria**:
1. A price change of ≥ 5% triggers a `SignificantPriceAlert` SignalR event to `market:{marketId}` within 500 ms; payload includes `changePercent`, `previousPrice`, `newPrice`, `severity`.
2. A change of exactly 5% is treated as significant (inclusive boundary).
3. A quantity-only update (price unchanged) does NOT trigger `SignificantPriceAlert`.
4. `PATCH /api/v1/admin/config/significant-price-threshold` by Admin updates the threshold; a non-Admin returns 403.

---

### T026 — Implement Angular pricing dashboard with live SignalR feed
**Assignee**: FE1-Web | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
freshflow-web/src/app/features/pricing/
  pricing.routes.ts
  pricing-dashboard/pricing-dashboard.component.ts  (OnPush, async pipe, signal-based state)
  pricing-dashboard/pricing-dashboard.component.html
  pricing-dashboard/pricing-dashboard.component.scss
  pricing.service.ts  (GET markets/{id}/products, subscribe to PricingHub)
```

**Pattern**: `PricingService` connects to `/hubs/pricing`, joins `market:{marketId}` on market select, listens for `PriceUpdated` and `SignificantPriceAlert`. Uses `WritableSignal<MarketProductDto[]>` to update the table reactively.

**Depends on**: T021, T023, T013

**Acceptance criteria**:
1. On market select, the price table loads via `GET /markets/{id}/products`.
2. When a kiosk staff updates a price, the relevant table row updates within 2 seconds — no refresh.
3. A `SignificantPriceAlert` event highlights the row with a yellow/red badge and the change percent.
4. All components use `OnPush`; no manual `.subscribe()` calls in component code; `async` pipe used in templates.

---

### T027 — Implement Angular kiosk product list + price update screens
**Assignee**: FE2-Web | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
freshflow-web/src/app/features/kiosk/
  product-list/kiosk-product-list.component.ts
  product-list/kiosk-product-list.component.html
  price-update/kiosk-price-update.component.ts
  price-update/kiosk-price-update.component.html
  kiosk.service.ts  (PATCH price, PATCH quantity)
```

**UX constraint**: ≤ 4 taps from product list to price update confirmation (large touch targets, high contrast for poor lighting).

**Depends on**: T022, T015

**Acceptance criteria**:
1. Kiosk Staff can update a product price in ≤ 4 taps from the product list.
2. After a successful PATCH, the product list row immediately reflects the new price (optimistic update).
3. A 403 response (wrong market) shows a clear "Not authorized for this market" message.

---

### T028 — Implement React Native kiosk product list + price update screen
**Assignee**: FE3-Mobile | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
freshflow-mobile/src/screens/kiosk/ProductListScreen.tsx
freshflow-mobile/src/screens/kiosk/PriceUpdateScreen.tsx
freshflow-mobile/src/services/pricing.service.ts
```

**Reconnection**: Connect to `/hubs/pricing` on login; exponential backoff (initial 1s, max 30s) on disconnect.

**Depends on**: T017, T022, T023

**Acceptance criteria**:
1. Product list loads from `GET /markets/{id}/products` on mount; pull-to-refresh works.
2. Kiosk Staff can update price in ≤ 4 taps from product list screen.
3. After successful update, the product list row updates immediately (optimistic update or SignalR echo).
4. SignalR reconnects automatically with exponential backoff after a network drop; no manual app restart needed.

---

## Phase 5: Sprint 3 — Order Management (Priority: P2, P3, P5 from spec)

**Goal**: Restaurant places order → Admin transitions status → restaurant sees update live.

**Independent Test**: Restaurant places a single-item order. Admin calls PATCH /admin/orders/{id}/status (confirmed → in_transit). Restaurant's Angular dashboard shows IN_TRANSIT without refresh.

- [ ] T029 [P] [US2] DB migration for Orders module tables
- [ ] T030 [P] [US2] Implement restaurant profile endpoints
- [ ] T031 [US2] Implement bulk order creation with stock validation and soft-reservation
- [ ] T032 [P] [US2] Implement order listing and detail endpoints
- [ ] T033 [P] [US2] Implement order cancellation with soft-reservation release
- [ ] T034 [US5] Implement order status transitions + OrderHub SignalR broadcast
- [ ] T035 [P] [US6] Implement order grouping endpoints
- [ ] T036 [US3] Implement scheduled recurring orders + background job
- [ ] T037 [US2] [US5] Implement Angular order management UI (list, create, detail + live status)
- [ ] T038 [P] [US2] [US5] Implement React Native order list + detail with real-time status

**Checkpoint**: Restaurant places order, Admin advances status, restaurant sees live update. Scheduled order fires automatically within 60s.

---

### T029 — DB migration for Orders module tables
**Assignee**: BE/DevOps | **Estimated**: S (half day) | **Story points**: 2

**Files to create/modify**:
```
src/FreshFlow.Infrastructure.Persistence/Migrations/20260511_000003_OrdersSchema.cs
src/Modules/Orders/FreshFlow.Orders.Infrastructure/Persistence/Configurations/
  RestaurantConfiguration.cs, OrderConfiguration.cs, OrderItemConfiguration.cs,
  OrderGroupConfiguration.cs, ScheduledOrderConfiguration.cs
src/Modules/Orders/FreshFlow.Orders.Domain/Entities/
  Restaurant.cs, Order.cs, OrderItem.cs, OrderGroup.cs, ScheduledOrder.cs
```

**Note**: `order_items.subtotal` is a `GENERATED ALWAYS AS (quantity * unit_price) STORED` computed column — use raw SQL in migration for this column.

**Depends on**: T019

**Acceptance criteria**:
1. Migration creates all 5 tables with correct FK constraints, enum types (`order_status`, `order_group_status`), and indexes per `docs/03-database-schema.md`.
2. `order_items.subtotal` is a generated stored column (verify via `\d order_items` in psql).
3. `orders.restaurant_id` FK is `ON DELETE RESTRICT`.

---

### T030 — Implement restaurant profile endpoints
**Assignee**: BE/DevOps | **Estimated**: S (half day) | **Story points**: 2

**Files to create/modify**:
```
src/FreshFlow.API/Controllers/OrdersController.cs
src/Modules/Orders/FreshFlow.Orders.Application/Commands/CreateRestaurant/CreateRestaurantCommand.cs
src/Modules/Orders/FreshFlow.Orders.Application/DTOs/RestaurantDto.cs
src/Modules/Orders/FreshFlow.Orders.Infrastructure/Persistence/Repositories/RestaurantRepository.cs
src/Modules/Orders/FreshFlow.Orders.Infrastructure/DependencyInjection.cs  (AddOrdersModule)
```

**Depends on**: T029, T009

**Acceptance criteria**:
1. `POST /api/v1/restaurants` by a Restaurant user creates the profile and returns 201 with `restaurantId`.
2. Duplicate call (user already has profile) returns 409.
3. `GET /api/v1/restaurants/me` returns the restaurant profile with `isApproved` field.

---

### T031 — Implement bulk order creation with stock validation and soft-reservation
**Assignee**: BE/DevOps | **Estimated**: L (2 days) | **Story points**: 8

**Files to create/modify**:
```
src/Modules/Orders/FreshFlow.Orders.Application/Commands/CreateOrder/
  CreateOrderCommand.cs, CreateOrderCommandHandler.cs, CreateOrderCommandValidator.cs
src/Modules/Orders/FreshFlow.Orders.Application/Services/SoftReservationService.cs
src/Modules/Orders/FreshFlow.Orders.Application/DTOs/CreateOrderRequestDto.cs, OrderResponseDto.cs
src/Modules/Orders/FreshFlow.Orders.Infrastructure/Persistence/Repositories/OrderRepository.cs
src/Modules/Pricing/FreshFlow.Pricing.Application/Abstractions/IProductCatalogReader.cs
  (interface in Pricing.Application; impl in Pricing.Infrastructure; injected into Orders)
tests/Integration/FreshFlow.IntegrationTests/Orders/CreateOrderEndpointTests.cs
```

**Soft-reservation**: `INCRBY reservation:{marketId}:{productId} <qty>` in Redis. On any failure after reservation, run `DECRBY` rollback.

**Depends on**: T030, T021

**Acceptance criteria**:
1. `POST /api/v1/orders` returns 201 with `orderId`, `status: pending`, `totalAmount`; Redis reservation counter incremented for each line item.
2. Line item requesting more than `availableQuantity` returns 422 `INSUFFICIENT_STOCK` with `productId`, `requestedQty`, `availableQty` per failing item.
3. Two concurrent requests for the last available unit: exactly one succeeds, the other gets 422 `INSUFFICIENT_STOCK`.
4. On any failure after reservation is applied, the reservation is rolled back (Redis counter decremented).
5. Unapproved restaurant (`is_approved: false`) returns 403 `RESTAURANT_NOT_APPROVED`.

---

### T032 — Implement order listing and detail endpoints
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
src/Modules/Orders/FreshFlow.Orders.Application/Queries/
  GetOrderQuery.cs, ListOrdersQuery.cs, ListAdminOrdersQuery.cs
src/Modules/Orders/FreshFlow.Orders.Application/DTOs/OrderListItemDto.cs
src/FreshFlow.API/Controllers/OrdersController.cs
src/FreshFlow.API/Controllers/AdminController.cs  (GET /admin/orders)
```

**Depends on**: T031

**Acceptance criteria**:
1. `GET /api/v1/orders/{orderId}` returns order with all line items for the owning restaurant; wrong restaurant returns 403.
2. `GET /api/v1/orders?status=pending&page=1&pageSize=20` returns paginated response with `totalCount`, `page`, `pageSize` in `meta`.
3. `GET /api/v1/admin/orders?restaurantId={id}` returns that restaurant's orders; non-Admin returns 403.

---

### T033 — Implement order cancellation with soft-reservation release
**Assignee**: BE/DevOps | **Estimated**: S (half day) | **Story points**: 2

**Files to create/modify**:
```
src/Modules/Orders/FreshFlow.Orders.Application/Commands/CancelOrder/
  CancelOrderCommand.cs, CancelOrderCommandHandler.cs
```

**Depends on**: T031

**Acceptance criteria**:
1. `PATCH /api/v1/orders/{orderId}/cancel` when status is `pending` or `confirmed` returns 200; status transitions to `cancelled`; Redis reservation counters for all line items decremented.
2. Cancelling `in_transit` order returns 409 `ORDER_NOT_CANCELLABLE`.
3. A different restaurant's order returns 403.

---

### T034 — Implement order status transitions + OrderHub SignalR broadcast
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
src/FreshFlow.API/SignalR/OrderHub.cs  (OnConnectedAsync: auto-join restaurant:{restaurantId} or admin:all)
src/Modules/Orders/FreshFlow.Orders.Application/Commands/UpdateOrderStatus/
  UpdateOrderStatusCommand.cs, UpdateOrderStatusCommandHandler.cs
src/Modules/Orders/FreshFlow.Orders.Application/Abstractions/IOrderBroadcastService.cs
src/Modules/Notifications/FreshFlow.Notifications.Infrastructure/Realtime/OrderBroadcastService.cs
src/FreshFlow.API/Controllers/AdminController.cs  (PATCH /admin/orders/{id}/status)
tests/Integration/FreshFlow.IntegrationTests/Orders/OrderStatusBroadcastTests.cs
```

**Depends on**: T032, T007

**Acceptance criteria**:
1. `PATCH /api/v1/admin/orders/{id}/status` with `{"status":"confirmed"}` transitions the order; restaurant client on `restaurant:{restaurantId}` receives `OrderStatusChanged` within 500 ms.
2. `OrderStatusChanged` payload: `{ orderId, previousStatus, newStatus, changedAt, changedByUserId }`.
3. Invalid transition (e.g., `pending → delivered`) returns 422.
4. A non-Admin token returns 403.

---

### T035 — Implement order grouping endpoints
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
src/Modules/Orders/FreshFlow.Orders.Application/Commands/CreateOrderGroup/
  CreateOrderGroupCommand.cs, CreateOrderGroupCommandHandler.cs
src/Modules/Orders/FreshFlow.Orders.Application/DTOs/CreateOrderGroupRequestDto.cs, OrderGroupDto.cs
src/Modules/Orders/FreshFlow.Orders.Infrastructure/Persistence/Repositories/OrderGroupRepository.cs
src/FreshFlow.API/Controllers/AdminController.cs  (POST /admin/order-groups, GET /admin/order-groups/{id})
```

**Depends on**: T034

**Acceptance criteria**:
1. `POST /api/v1/admin/order-groups` with `confirmed` order IDs returns 201 with `orderGroupId`.
2. Including a non-`confirmed` order returns 422.
3. Adding an order already in an active group returns 409.

---

### T036 — Implement scheduled recurring orders + background job
**Assignee**: BE/DevOps | **Estimated**: L (2 days) | **Story points**: 8

**Files to create/modify**:
```
src/Modules/Orders/FreshFlow.Orders.Application/Commands/CreateScheduledOrder/
  CreateScheduledOrderCommand.cs, CreateScheduledOrderCommandHandler.cs
src/Modules/Orders/FreshFlow.Orders.Application/BackgroundJobs/ScheduledOrderJob.cs  (IHostedService, 60s tick)
src/Modules/Orders/FreshFlow.Orders.Infrastructure/Persistence/Repositories/ScheduledOrderRepository.cs
src/FreshFlow.API/Controllers/OrdersController.cs  (POST /orders/scheduled, GET /orders/scheduled/{id}/instances)
```

**Job idempotency**: Before creating an instance, check `last_executed_at` + recurrence pattern to confirm the window has not already been processed. Use `MISSED_EXECUTION` log entry for catch-up runs.

**Depends on**: T031

**Acceptance criteria**:
1. `POST /api/v1/orders/scheduled` returns 201 with `scheduledOrderId` and `nextRunAt` in `Asia/Ho_Chi_Minh` timezone.
2. The background job creates a concrete order instance within 60 seconds of the scheduled time.
3. Running the job twice within the same 60-second window does NOT create duplicate instances (idempotent).
4. If the job was skipped (app was down), the missed instance is created on the next job tick and a `MISSED_EXECUTION` log entry is written.

---

### T037 — Implement Angular order management UI (list, create, detail + live status)
**Assignee**: FE1-Web | **Estimated**: L (2 days) | **Story points**: 8

**Files to create/modify**:
```
freshflow-web/src/app/features/orders/
  orders.routes.ts
  order-list/order-list.component.ts + .html  (paginated, status filter, live status updates)
  create-order/create-order.component.ts + .html  (market selector → product picker → submit)
  order-detail/order-detail.component.ts + .html  (status timeline)
  orders.service.ts  (POST /orders, GET /orders, GET /orders/{id}, PATCH cancel)
```

**Depends on**: T032, T034, T013

**Acceptance criteria**:
1. Restaurant user can browse products by market, add line items, and submit a new order in ≤ 3 interactions.
2. When Admin advances order status, `OrderListComponent` updates the row in real time via SignalR — no refresh.
3. `CreateOrderComponent` shows per-item `INSUFFICIENT_STOCK` errors inline on submit failure.
4. All components use `OnPush` and `async` pipe.

---

### T038 — Implement React Native order list + detail with real-time status
**Assignee**: FE3-Mobile | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
freshflow-mobile/src/screens/restaurant/OrderListScreen.tsx
freshflow-mobile/src/screens/restaurant/OrderDetailScreen.tsx
freshflow-mobile/src/services/orders.service.ts
```

**Depends on**: T017, T034

**Acceptance criteria**:
1. `OrderDetailScreen` updates displayed status in real time when Admin transitions it — no manual refresh.
2. Order list is paginated and filterable by status.
3. All API calls go through `orders.service.ts` wrapping `api.service.ts`.

---

## Phase 6: Sprint 4 — Logistics + Hub (Priority: P4, P6 from spec)

**Goal**: Admin calculates route, assigns vehicle, schedules delivery, marks delivered; restaurants receive live delivery events.

- [ ] T039 [P] [US4] DB migration for Logistics module tables
- [ ] T040 [P] [US4] Implement vehicle CRUD endpoints
- [ ] T041 [US4] [HIGH-RISK: VRP routing] Implement VRP route calculation engine (nearest-neighbor + 2-opt)
- [ ] T042 [US4] Implement route creation + vehicle assignment + Redis route cache
- [ ] T043 [US4] Implement delivery status tracking + DeliveryHub SignalR broadcast
- [ ] T044 [P] DB migration for Hub module tables
- [ ] T045 [P] Implement hub CRUD endpoints
- [ ] T046 Implement hub inbound + outbound recording with transactional inventory update
- [ ] T047 [P] Implement hub inventory view + redistribution suggestions
- [ ] T048 [US4] Implement Angular logistics dashboard (route calc, vehicle assign, delivery tracking)

---

### T039 — DB migration for Logistics module tables
**Assignee**: BE/DevOps | **Estimated**: S (half day) | **Story points**: 2

**Files**: Logistics EF configs, Domain entities, migration for `vehicles`, `delivery_routes` (with `route_metadata JSONB`), `deliveries`.
**Depends on**: T029 | **AC**: Migration applies cleanly; `route_metadata` column is `jsonb`; `deliveries_order_id_unique` constraint exists.

---

### T040 — Implement vehicle CRUD endpoints
**Assignee**: BE/DevOps | **Estimated**: S (half day) | **Story points**: 2

**Files**: VehicleService, VehicleRepository, DTOs, validators, Logistics controller.
**Depends on**: T039 | **AC**: POST creates vehicle; duplicate plate returns 409; `GET ?available=true` filters correctly; PATCH supports status change to inactive.

---

### T041 — Implement VRP route calculation engine (nearest-neighbor + 2-opt)
**Assignee**: BE/DevOps | **Estimated**: L (2 days) | **Story points**: 8
> [HIGH-RISK: VRP routing] Algorithmic task. The nearest-neighbor + 2-opt heuristic must be
> correct enough that it doesn't produce nonsensical routes for the 3-market geography.
> MITIGATION: Write unit tests with known-optimal small cases (3-stop triangle, 4-stop square)
> before implementing. Test that the 2-opt improvement never produces a worse result than
> the initial nearest-neighbor tour. Cap input at 20 stops and test the boundary.

**Files to create/modify**:
```
src/Modules/Logistics/FreshFlow.Logistics.Application/Services/VrpSolver.cs
  (pure in-memory: no DB, no Redis, no constructor injection dependencies)
src/Modules/Logistics/FreshFlow.Logistics.Application/Services/RouteCalculationService.cs
src/Modules/Logistics/FreshFlow.Logistics.Application/DTOs/
  RouteCalculationRequestDto.cs, RouteCalculationResponseDto.cs, RouteStopDto.cs
src/Modules/Logistics/FreshFlow.Logistics.Application/Abstractions/IRouteCalculationService.cs
src/Modules/Logistics/FreshFlow.Logistics.Infrastructure/DependencyInjection.cs  (AddLogisticsModule)
tests/Unit/FreshFlow.Logistics.UnitTests/Algorithms/VrpSolverTests.cs
```

**Algorithm**:
1. Nearest-neighbor: start from depot, repeatedly pick the nearest unvisited stop.
2. 2-opt improvement: iterate all (i,j) pairs; swap if reversing the segment between i and j reduces total cost. Repeat until no improvement.
3. Cost function: `DISTANCE` = Haversine km; `TIME` = distance / avg speed (30 km/h default); `COST` = distance × cost-per-km constant.

**Depends on**: T039

**Acceptance criteria**:
1. `VrpSolver.Solve(3 stops, DISTANCE)` returns a correct ordered route and completes in < 100 ms.
2. `VrpSolver.Solve(20 stops, COST)` completes in < 100 ms; 2-opt result is ≤ initial nearest-neighbor result (never worse).
3. Passing 21 stops throws `StopLimitExceededException` (mapped to 422 `STOP_LIMIT_EXCEEDED` by controller).
4. `VrpSolver` has NO constructor parameters referencing `IRepository`, `ICacheService`, or any infrastructure type — it must be `new VrpSolver()` testable.

---

### T042 — Implement route creation + vehicle assignment + Redis route cache
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
src/Modules/Logistics/FreshFlow.Logistics.Infrastructure/Caching/RedisRouteCache.cs
  cache key: route:{SHA256(sorted stop IDs + "|" + criterion)}, TTL 1 hour
src/Modules/Logistics/FreshFlow.Logistics.Infrastructure/Persistence/Repositories/DeliveryRouteRepository.cs
src/FreshFlow.API/Controllers/LogisticsController.cs  (POST /logistics/routes/calculate, POST /logistics/routes/{id}/assign-vehicle)
```

**Depends on**: T041, T040

**Acceptance criteria**:
1. `POST /logistics/routes/calculate` returns route within 3 seconds; route is persisted and cached.
2. A second identical request within 1 hour returns the cached result (one VRP computation, verifiable via log).
3. `POST /logistics/routes/{id}/assign-vehicle` with an already-scheduled vehicle returns 409 `VEHICLE_NOT_AVAILABLE`.

---

### T043 — Implement delivery status tracking + DeliveryHub SignalR broadcast
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files to create/modify**:
```
src/FreshFlow.API/SignalR/DeliveryHub.cs  (auto-join restaurant:{restaurantId} on connect)
src/Modules/Logistics/FreshFlow.Logistics.Application/Commands/UpdateDeliveryStatus/
  UpdateDeliveryStatusCommand.cs, UpdateDeliveryStatusCommandHandler.cs
src/Modules/Logistics/FreshFlow.Logistics.Application/Abstractions/IDeliveryBroadcastService.cs
src/Modules/Notifications/FreshFlow.Notifications.Infrastructure/Realtime/DeliveryBroadcastService.cs
src/FreshFlow.API/Controllers/LogisticsController.cs  (PATCH /admin/deliveries/{id}/status, POST /logistics/schedules, GET /logistics/schedules)
```

**Depends on**: T042, T035, T007

**Acceptance criteria**:
1. `PATCH /admin/deliveries/{id}/status` with `in_transit` emits `DeliveryStarted` to `restaurant:{restaurantId}` within 500 ms; payload includes `scheduleId`, `routeId`, `estimatedArrivalAt`, `orderIds[]`.
2. Each restaurant receives ONLY events for its own orders, even in a multi-drop delivery.
3. `POST /logistics/schedules` validates vehicle capacity; exceeding it returns 422 `VEHICLE_CAPACITY_EXCEEDED`.

---

### T044 — DB migration for Hub module tables
**Assignee**: BE/DevOps | **Estimated**: S (half day) | **Story points**: 2

**Files**: Hub EF configs, Domain entities, migration for `hubs`, `hub_inventory`, `hub_inbound_events`, `hub_outbound_events`, `cross_dock_transfers`.
**Note**: `hub_inventory.quantity_available` is `GENERATED ALWAYS AS (quantity_in - quantity_out) STORED`; `items` columns are `jsonb`.
**Depends on**: T039 | **AC**: Migration applies; computed column and jsonb columns verified; `UNIQUE (hub_id, market_product_id)` on hub_inventory.

---

### T045 — Implement hub CRUD endpoints
**Assignee**: BE/DevOps | **Estimated**: S (half day) | **Story points**: 2

**Files**: HubService, DistributionHubRepository, DTOs, HubController.
**Depends on**: T044 | **AC**: POST creates hub; PATCH updates; deactivating hub with pending deliveries returns 409 `HUB_HAS_PENDING_DELIVERIES`; GET returns `occupiedCapacityKg` and `availableCapacityKg`.

---

### T046 — Implement hub inbound + outbound recording with transactional inventory update
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files**: InboundTrackingService, OutboundTrackingService, HubInboundRepository, HubOutboundRepository, HubController (POST /hubs/{id}/inbound, POST /hubs/{id}/outbound).
**Critical**: `hub_inventory` update MUST be in the same DB transaction as the event insert.
**Depends on**: T045 | **AC**: Inbound creates event and increments `quantity_in` atomically; duplicate delivery returns 409 `ALREADY_RECEIVED`; capacity exceeded returns 422; outbound deducts from `quantity_out`; insufficient stock returns 422 `INSUFFICIENT_HUB_STOCK`.

---

### T047 — Implement hub inventory view + redistribution suggestions
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files**: RedistributionSuggestionService, GET /hubs/{id}/inventory, GET /hubs/{id}/redistribution-suggestions.
**Note**: `IOrderGroupReader` interface in Hub.Application; implemented via Orders.Infrastructure; injected at host level. Response must be < 2 seconds.
**Depends on**: T046, T035

**AC**: GET /inventory returns current stock per product; redistribution suggestions show `restaurantId`, `orderId`, `productId`, `suggestedQuantityKg`, `rationale` in < 2 s; no automatic dispatch is triggered.

---

### T048 — Implement Angular logistics dashboard
**Assignee**: FE1-Web | **Estimated**: L (2 days) | **Story points**: 8

**Files**:
```
freshflow-web/src/app/features/logistics/
  logistics.routes.ts
  route-calculator/route-calculator.component.ts + .html  (market + hub + restaurant pickers, criterion selector)
  vehicle-list/vehicle-list.component.ts + .html
  schedule-list/schedule-list.component.ts + .html
  delivery-tracker/delivery-tracker.component.ts + .html  (real-time status via DeliveryHub)
  logistics.service.ts
```

**Depends on**: T042, T043, T013 | **AC**: Admin can calculate a route, assign a vehicle, create schedule in one screen flow; delivery status updates live via `DeliveryStatusChanged` SignalR event.

---

## Phase 7: Sprint 5 — Analytics + Notifications (Priority: P7 from spec)

**Goal**: Price trend chart, delivery KPIs, demand heatmap, async export, notification persistence.

- [ ] T049 [P] [US7] Implement price trend analytics endpoint + Redis cache
- [ ] T050 [P] Implement demand heatmap endpoint (Admin only)
- [ ] T051 [P] Implement delivery performance KPI endpoints
- [ ] T052 [US7] [HIGH-RISK: AI price prediction — DEFERRED] See note below
- [ ] T053 Implement async CSV export with background job + polling endpoints
- [ ] T054 [P] Implement analytics aggregation background job + analytics aggregations table migration
- [ ] T055 [P] Implement notification persistence + read/mark-read endpoints
- [ ] T056 [US7] Implement Angular analytics dashboard (price trends + KPIs + heatmap)

> **T052 — AI price prediction** [HIGH-RISK: OUT OF SCOPE FOR V1]
> Per spec.md Assumptions and research.md Decision 5, AI recommendations are deferred.
> If this is revisited: implement a 7-day or 30-day rolling average over `price_snapshots`
> in `AnalyticsAggregationJob`. No ML model or external service. Story points: 5.

---

### T049 — Implement price trend analytics endpoint + Redis cache
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files**: PriceTrendQueryService, AnalyticsController (GET /analytics/price-trends), RedisAnalyticsCache.
**Cache**: `analytics:price_trend:{mpId}:{from}:{to}` TTL 15 min.
**Depends on**: T024 | **AC**: Returns `minPrice`, `maxPrice`, `avgPrice`, `priceVolatility` + time-series array < 800 ms; second request within 15 min is cached; range > 12 months returns daily-aggregated points.

---

### T050 — Implement demand heatmap endpoint
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files**: DemandHeatmapQueryService, AnalyticsController (GET /analytics/demand-heatmap, GET /analytics/demand-heatmap/time-distribution).
**Note**: Queries must target the read replica connection. Non-Admin token returns 403.
**Depends on**: T032, T054 | **AC**: Returns restaurant list with `totalOrderCount`, `totalOrderValueVND`, `dominantProductCategory`, `latitude`, `longitude`; time-distribution returns 7×24 matrix.

---

### T051 — Implement delivery performance KPI endpoints
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files**: DeliveryPerformanceQueryService, AnalyticsController (GET /analytics/delivery-performance, GET /analytics/delivery-performance/by-route).
**Late threshold**: `actualArrivalAt > estimatedArrivalAt + 15 minutes`.
**Depends on**: T043 | **AC**: Returns `onTimeRatePercent`, `avgDeliveryDurationMinutes`, `avgVehicleUtilizationPercent` < 800 ms; `by-route` breaks down per route ID.

---

### T053 — Implement async CSV export with background job + polling endpoints
**Assignee**: BE/DevOps | **Estimated**: L (2 days) | **Story points**: 8

**Files**: ExportJobService (IHostedService), ExportJobRepository, migration for `export_jobs`, AnalyticsController (POST /analytics/export, GET /analytics/export/{id}/status, GET /analytics/export/{id}/download).
**Depends on**: T049 | **AC**: POST returns 202 with `jobId`; GET /status returns `ready` once processed; GET /download returns CSV with correct Content-Type; file 404 after 24-hour expiry.

---

### T054 — Analytics aggregation background job + migration
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files**: AnalyticsAggregationJob (IHostedService), migration for `analytics_aggregations`, `PartitionMaintenanceJob` (create next month's price_snapshots partition on 25th).
**Depends on**: T049 | **AC**: Job pre-computes `price_trend` and `demand_heatmap` rows; next month's partition created on the 25th; running the job twice on the same day is idempotent.

---

### T055 — Implement notification persistence + read/mark-read endpoints
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files**: NotificationService, NotificationRepository, migration for `notifications`, Notifications DI, NotificationsController (GET /notifications cursor-paginated, PATCH /notifications/{id}/read, PATCH /notifications/read-all).
**Note**: `NotificationService.PersistAsync` called after broadcast events (fire-and-forget — DB write failure must never block broadcast path).
**Depends on**: T023, T034, T043 | **AC**: Notification row inserted after each price update, order status change, delivery start; GET /notifications returns newest-first with cursor pagination; mark-read sets `is_read = true` and `read_at`; user can only read own notifications (403 otherwise).

---

### T056 — Implement Angular analytics dashboard
**Assignee**: FE1-Web | **Estimated**: L (2 days) | **Story points**: 8

**Files**:
```
freshflow-web/src/app/features/analytics/
  analytics.routes.ts
  price-trend/price-trend.component.ts + .html  (chart, product/market selector, date range)
  demand-heatmap/demand-heatmap.component.ts + .html
  delivery-kpis/delivery-kpis.component.ts + .html
  analytics.service.ts
```

**Depends on**: T049, T050, T051, T013 | **AC**: Price trend chart shows last 30 days by default; date range extendable to 12 months; delivery KPIs display on-time rate, avg duration, utilization; Admin-only routes guarded by RoleGuard.

---

## Phase 8: Sprint 6 — Deployment + Testing Polish

**Purpose**: Production readiness, coverage gates, integration test completeness.

- [ ] T057 [P] Write integration test suite (auth + pricing + orders critical paths)
- [ ] T058 [P] Write unit tests for VRP solver, pricing service, token service
- [ ] T059 [P] Production Nginx TLS config + docker-compose.prod.yml
- [ ] T060 [P] CI/CD pipeline polish (coverage gate ≥ 70%, Docker push, staging deploy stage)

---

### T057 — Integration test suite (Testcontainers + WebApplicationFactory)
**Assignee**: BE/DevOps | **Estimated**: L (2 days) | **Story points**: 8

**Files**:
```
tests/Integration/FreshFlow.IntegrationTests/
  Infrastructure/IntegrationTestBase.cs  (WebApplicationFactory + Testcontainers PostgreSQL + Redis)
  Auth/LoginEndpointTests.cs, RefreshTokenTests.cs, LogoutTests.cs
  Pricing/PriceUpdateEndpointTests.cs, PricingHubTests.cs
  Orders/CreateOrderEndpointTests.cs, OrderStatusBroadcastTests.cs
  Logistics/RouteCalculationEndpointTests.cs
```

**Pattern**: Each test class derives from `IntegrationTestBase` which spins up a PostgreSQL container + Redis container via Testcontainers, creates a `WebApplicationFactory<Program>`, and tears down after the test class.

**Depends on**: T010, T022, T031, T042 | **AC**: All critical paths (login → price update → order create → status transition) covered; integration tests use real DB/Redis; `dotnet test --filter Category=Integration` passes with exit code 0.

---

### T058 — Unit tests for VRP solver, pricing service, token service
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files**:
```
tests/Unit/FreshFlow.Logistics.UnitTests/Algorithms/VrpSolverTests.cs
  (3-stop optimal, 20-stop boundary, 21-stop exception, DISTANCE vs TIME vs COST)
tests/Unit/FreshFlow.Pricing.UnitTests/Services/SignificantChangeDetectorTests.cs
  (5% threshold, exactly 5%, < 5%, HIGH severity at 15%)
tests/Unit/FreshFlow.Auth.UnitTests/Services/JwtTokenServiceTests.cs
  (claim presence, TTL, signature validation)
```

**Depends on**: T041, T025, T006 | **AC**: 100% branch coverage on VrpSolver; significant change detector tests cover exact boundary values; all tests pass with `dotnet test --filter Category=Unit`.

---

### T059 — Production Nginx TLS config + docker-compose.prod.yml
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files**:
```
nginx/nginx.prod.conf  (HTTPS only, HTTP → HTTPS redirect, HSTS, WebSocket upgrade)
docker-compose.prod.yml  (no exposed dev ports 5432/6379, IMAGE_SHA variable, health checks)
.env.prod.example
```

**Depends on**: T004 | **AC**: `docker compose -f docker-compose.prod.yml up` starts all 4 services with no dev ports exposed; HTTPS redirect works; `curl -k https://localhost/health` returns Healthy.

---

### T060 — CI/CD pipeline polish (coverage, Docker push, staging deploy)
**Assignee**: BE/DevOps | **Estimated**: M (1 day) | **Story points**: 5

**Files**: `.github/workflows/ci.yml` (add coverage gate ≥ 70%, Docker push to registry, staging deploy step with rolling update).

**Depends on**: T005, T057, T058 | **AC**: PR to `main` with < 70% coverage fails the pipeline; `main` push builds and pushes Docker image tagged with git SHA; staging deploy step runs `docker compose pull && docker compose up -d --no-deps api` with health-check gating.

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup):    No deps — start immediately
Phase 2 (Found.):   Depends on T001 completion
Phase 3 (Sprint 1): Depends on Phase 2 (T006 especially)
Phase 4 (Sprint 2): Depends on T009 (login working)
Phase 5 (Sprint 3): Depends on T021 (market products — needed for order stock check)
Phase 6 (Sprint 4): Depends on T035 (order groups — needed for logistics scheduling)
Phase 7 (Sprint 5): Depends on T043, T032 (delivery + order data for analytics)
Phase 8 (Sprint 6): Depends on all implementation phases
```

### Critical Path

```
T001 → T002 → T006 → T009 → T010 → T019 → T021 → T022 → T023 → T031 → T034 → T041 → T042 → T043
```

### Parallel Opportunities

Within Sprint 1 (after T001):
- T002 (DB) ∥ T003 (Redis) ∥ T004 (Docker)
- T006 (JWT) ∥ T007 (SignalR) ∥ T008 (Logging)
- T013 (Angular scaffold) ∥ T016 (RN scaffold) — independent of backend
- T011 (logout) ∥ T012 (register) — both depend only on T009/T010

Within Sprint 2 (after T019):
- T020 (catalog endpoints) ∥ T024 (price history) — independent
- T026 (Angular dashboard) ∥ T027 (Angular kiosk) ∥ T028 (RN kiosk) — frontend parallel

Within Sprint 3 (after T029):
- T030 (restaurant profile) ∥ T036 setup work — both depend on T029 only
- T032 (order list) ∥ T033 (cancel) — both depend on T031

---

## Task Summary

| Sprint | Tasks | Team |
|--------|-------|------|
| Sprint 1 — Foundation & Auth | T001–T018 (18 tasks) | All 4 |
| Sprint 2 — Real-time Pricing | T019–T028 (10 tasks) | All 4 |
| Sprint 3 — Order Management | T029–T038 (10 tasks) | BE + FE1 + FE3 |
| Sprint 4 — Logistics + Hub | T039–T048 (10 tasks) | BE + FE1 |
| Sprint 5 — Analytics + Notifications | T049–T056 (8 tasks, T052 deferred) | BE + FE1 |
| Sprint 6 — Deployment + Testing | T057–T060 (4 tasks) | BE/DevOps |
| **Total** | **60 tasks** | |

**MVP scope** (demo-ready after Sprint 2): T001–T028 — full live price feed with auth.
**Full demo scope** (after Sprint 3): T001–T038 — price feed + orders + live status tracking.

---

## HIGH-RISK Task Summary

| Task | Risk | Mitigation |
|------|------|-----------|
| T007 | SignalR + Redis backplane integration | Integration test with 2 WebApplicationFactory instances sharing one Redis container |
| T023 | SignalR broadcast under load | Test `?access_token=` JWT flow early; test market-assignment enforcement |
| T041 | VRP algorithm correctness | Unit tests with known-optimal 3-stop and 4-stop cases before implementation; 2-opt must never produce worse result than nearest-neighbor |
| T052 | AI price prediction | **DEFERRED** — out of scope for v1. Simple rolling average is the v2 path; no ML model needed |
| React Native httpOnly cookies | **RESOLVED** — Bearer tokens used instead | See research.md Decision 3. `expo-secure-store` for token storage |

---

## Jira Stories

Copy the following into Jira. Story points: S=2, M=3–5, L=8.

### Sprint 1 Stories

| # | Title | Assignee | Points |
|---|-------|----------|--------|
| T001 | Scaffold .NET 8 solution with Clean Architecture | BE/DevOps | 8 |
| T002 | Set up PostgreSQL + EF Core + Auth schema migration | BE/DevOps | 5 |
| T003 | Set up Redis connection and RedisCacheService | BE/DevOps | 5 |
| T004 | Set up Docker Compose (nginx, api, postgres, redis) | BE/DevOps | 5 |
| T005 | Configure GitHub Actions CI/CD pipeline skeleton | BE/DevOps | 5 |
| T006 | Configure JWT auth middleware and BCrypt password service | BE/DevOps | 5 |
| T007 | Configure SignalR with Redis backplane (stub hubs) ⚠ HIGH-RISK | BE/DevOps | 5 |
| T008 | Configure Serilog + correlation ID middleware + /health endpoint | BE/DevOps | 2 |
| T009 | Implement POST /auth/login endpoint | BE/DevOps | 5 |
| T010 | Implement POST /auth/refresh with token rotation | BE/DevOps | 5 |
| T011 | Implement POST /auth/logout endpoint | BE/DevOps | 2 |
| T012 | Implement POST /auth/register (Admin only) | BE/DevOps | 5 |
| T013 | Scaffold Angular app with routing, auth guard, HTTP interceptor | FE1-Web | 8 |
| T014 | Implement Angular login page and auth service | FE1-Web | 5 |
| T015 | Set up Angular shared component library + kiosk module scaffold | FE2-Web | 5 |
| T016 | Scaffold React Native app with navigation and auth state | FE3-Mobile | 8 |
| T017 | Implement React Native login screen + secure token storage | FE3-Mobile | 5 |
| T018 | Implement React Native api.service.ts + auto token refresh | FE3-Mobile | 5 |

### Sprint 2 Stories

| # | Title | Assignee | Points |
|---|-------|----------|--------|
| T019 | DB migration for Pricing module tables | BE/DevOps | 2 |
| T020 | Implement product catalog endpoints | BE/DevOps | 5 |
| T021 | Implement market products listing with Redis cache | BE/DevOps | 5 |
| T022 | Implement price + quantity update endpoints + Redis write | BE/DevOps | 8 |
| T023 | Implement PricingHub group management + broadcast ⚠ HIGH-RISK | BE/DevOps | 5 |
| T024 | Implement price history endpoint (cursor pagination) | BE/DevOps | 2 |
| T025 | Implement significant price change detection + alert | BE/DevOps | 5 |
| T026 | Implement Angular pricing dashboard with live SignalR feed | FE1-Web | 5 |
| T027 | Implement Angular kiosk product list + price update screens | FE2-Web | 5 |
| T028 | Implement React Native kiosk price update screen | FE3-Mobile | 5 |

### Sprint 3 Stories

| # | Title | Assignee | Points |
|---|-------|----------|--------|
| T029 | DB migration for Orders module tables | BE/DevOps | 2 |
| T030 | Implement restaurant profile endpoints | BE/DevOps | 2 |
| T031 | Implement bulk order creation + stock validation + soft-reservation | BE/DevOps | 8 |
| T032 | Implement order listing and detail endpoints | BE/DevOps | 5 |
| T033 | Implement order cancellation with reservation release | BE/DevOps | 2 |
| T034 | Implement order status transitions + OrderHub broadcast | BE/DevOps | 5 |
| T035 | Implement order grouping endpoints | BE/DevOps | 5 |
| T036 | Implement scheduled recurring orders + background job | BE/DevOps | 8 |
| T037 | Implement Angular order management UI | FE1-Web | 8 |
| T038 | Implement React Native order list + detail screens | FE3-Mobile | 5 |

### Sprint 4 Stories

| # | Title | Assignee | Points |
|---|-------|----------|--------|
| T039 | DB migration for Logistics module tables | BE/DevOps | 2 |
| T040 | Implement vehicle CRUD endpoints | BE/DevOps | 2 |
| T041 | Implement VRP route calculation engine ⚠ HIGH-RISK | BE/DevOps | 8 |
| T042 | Implement route creation + vehicle assignment + cache | BE/DevOps | 5 |
| T043 | Implement delivery status tracking + DeliveryHub broadcast | BE/DevOps | 5 |
| T044 | DB migration for Hub module tables | BE/DevOps | 2 |
| T045 | Implement hub CRUD endpoints | BE/DevOps | 2 |
| T046 | Implement hub inbound + outbound recording | BE/DevOps | 5 |
| T047 | Implement hub inventory + redistribution suggestions | BE/DevOps | 5 |
| T048 | Implement Angular logistics dashboard | FE1-Web | 8 |

### Sprint 5 Stories

| # | Title | Assignee | Points |
|---|-------|----------|--------|
| T049 | Implement price trend analytics endpoint + cache | BE/DevOps | 5 |
| T050 | Implement demand heatmap endpoint | BE/DevOps | 5 |
| T051 | Implement delivery performance KPI endpoints | BE/DevOps | 5 |
| T052 | AI price prediction ⚠ HIGH-RISK — DEFERRED to v2 | — | — |
| T053 | Implement async CSV export with polling endpoints | BE/DevOps | 8 |
| T054 | Analytics aggregation background job + migration | BE/DevOps | 5 |
| T055 | Implement notification persistence + read endpoints | BE/DevOps | 5 |
| T056 | Implement Angular analytics dashboard | FE1-Web | 8 |

### Sprint 6 Stories

| # | Title | Assignee | Points |
|---|-------|----------|--------|
| T057 | Integration test suite (Testcontainers + WebApplicationFactory) | BE/DevOps | 8 |
| T058 | Unit tests for VRP solver, pricing service, token service | BE/DevOps | 5 |
| T059 | Production Nginx TLS config + docker-compose.prod.yml | BE/DevOps | 5 |
| T060 | CI/CD pipeline polish (coverage gate, Docker push, staging deploy) | BE/DevOps | 5 |
