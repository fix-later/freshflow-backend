# FreshFlow (FFX) — Implementation Plan

**Version:** 1.0
**Date:** 2026-05-09
**Project:** FreshFlow – Intermediary Platform for Food Procurement and Logistics Optimization
**Status:** Ready for Implementation
**Based on:** Requirements Spec v1.0 · System Architecture v1.0 · Database Schema v1.0 · API Design v1.0

---

## Table of Contents

1. [Task Breakdown](#1-task-breakdown)
2. [Implementation Order (Critical Path)](#2-implementation-order-critical-path)
3. [MVP Scope](#3-mvp-scope)
4. [Folder Structure](#4-folder-structure)
5. [Coding Standards](#5-coding-standards)
6. [Testing Strategy](#6-testing-strategy)

---

## 1. Task Breakdown

### Group A — Infrastructure (T001–T008)

| Task ID | Title | Domain/Module | Description | Files to Create/Modify | Dependencies | Complexity | Acceptance Criteria |
|---------|-------|---------------|-------------|------------------------|--------------|------------|---------------------|
| T001 | Initialize ASP.NET Core solution structure (Clean Architecture per module) | Infrastructure | Create `FreshFlow.sln` with the full per-module project layout: `src/Shared/` (SharedKernel + Contracts), `src/Modules/{Auth,Pricing,Orders,Logistics,Hub,Analytics,Notifications}/{Domain,Application,Infrastructure}`, `src/FreshFlow.Infrastructure.Persistence/` (shared AppDbContext + Migrations), `src/FreshFlow.API/` (host). Wire `.csproj` references: Domain→SharedKernel; Application→Domain+SharedKernel+Contracts; Infrastructure→Application; API→all Infrastructure projects. Add MediatR, FluentValidation, Swashbuckle. Add `.editorconfig` and `.gitignore`. | `FreshFlow.sln`, `src/Shared/FreshFlow.SharedKernel/FreshFlow.SharedKernel.csproj`, `src/Shared/FreshFlow.Contracts/FreshFlow.Contracts.csproj`, `src/Modules/Auth/FreshFlow.Auth.Domain/FreshFlow.Auth.Domain.csproj`, `src/Modules/Auth/FreshFlow.Auth.Application/FreshFlow.Auth.Application.csproj`, `src/Modules/Auth/FreshFlow.Auth.Infrastructure/FreshFlow.Auth.Infrastructure.csproj` (+ same pattern ×6 modules), `src/FreshFlow.Infrastructure.Persistence/FreshFlow.Infrastructure.Persistence.csproj`, `src/FreshFlow.API/FreshFlow.API.csproj`, `src/FreshFlow.API/Program.cs`, `src/FreshFlow.API/appsettings.json`, `.editorconfig`, `.gitignore`, `tests/Unit/FreshFlow.Auth.UnitTests/`, `tests/Integration/FreshFlow.IntegrationTests/` | None | 1. `dotnet build FreshFlow.sln` exits with code 0 and no warnings. 2. Project reference rules enforced: no module Domain project has a reference to another module's project; any attempt causes a build error. 3. `GET /swagger` returns the OpenAPI UI in Development environment. |
| T002 | Set up PostgreSQL + EF Core + initial migrations | Infrastructure | Install EF Core 10 with Npgsql provider in `FreshFlow.Infrastructure.Persistence`. Configure `AppDbContext` to auto-discover `IEntityTypeConfiguration<T>` via `ApplyConfigurationsFromAssembly()`, add connection string to `appsettings.json`, and generate the initial migration covering `users`, `refresh_tokens`, `user_market_assignments`, `markets`, `products`, `market_products`, and `system_config` tables. Run `MigrateAsync()` in `Program.cs` before `app.Run()`. | `src/FreshFlow.Infrastructure.Persistence/AppDbContext.cs`, `src/FreshFlow.Infrastructure.Persistence/Migrations/20260101_000001_InitialSchema.cs`, `src/FreshFlow.API/Program.cs`, `src/FreshFlow.API/appsettings.json` | T001 | M | 1. `dotnet ef migrations list` shows at least one migration. 2. Running the application against a blank PostgreSQL instance creates all tables and applies the migration automatically. 3. All six PostgreSQL enum types (`user_role`, `order_status`, etc.) are created in the correct migration. |
| T003 | Set up Redis connection and caching infrastructure | Infrastructure | Add `StackExchange.Redis` to `src/Shared/FreshFlow.SharedInfrastructure/` (shared infrastructure project). Implement a `RedisCacheService` wrapping `IDistributedCache` with typed helpers for `GetAsync<T>`, `SetAsync<T>`, and `DeleteAsync`. Implement graceful degradation: Redis failure logs a warning and returns `null` (cache miss) without throwing. Register the service in DI. | `src/Shared/FreshFlow.SharedInfrastructure/Caching/RedisCacheService.cs`, `src/Shared/FreshFlow.SharedKernel/ICacheService.cs`, `src/FreshFlow.API/Program.cs`, `src/FreshFlow.API/appsettings.json` | T001 | S | 1. `ICacheService.SetAsync("test", "value", TimeSpan.FromMinutes(1))` stores the value in Redis and `GetAsync<string>("test")` returns it. 2. When Redis is unreachable, `GetAsync` returns `null` instead of throwing an exception, and a `Warning`-level log entry is written. 3. All Redis key patterns from the database schema (Section 6) are documented as constants in a `RedisKeys` static class. |
| T004 | Configure JWT authentication middleware | Infrastructure | Implement JWT token generation (`TokenService`) and validation middleware in `FreshFlow.Infrastructure`. Token generation produces access tokens (15-min TTL) with `sub`, `email`, `role`, `iat`, and `exp` claims signed with HMAC-SHA256. Configure `AddAuthentication` + `AddJwtBearer` in `Program.cs`. Implement `PasswordService` wrapping BCrypt with work factor ≥ 12. | `src/Modules/Auth/FreshFlow.Auth.Infrastructure/Services/JwtTokenService.cs`, `src/Modules/Auth/FreshFlow.Auth.Application/Abstractions/ITokenService.cs`, `src/Modules/Auth/FreshFlow.Auth.Infrastructure/Services/BcryptPasswordService.cs`, `src/Modules/Auth/FreshFlow.Auth.Application/Abstractions/IPasswordService.cs`, `src/FreshFlow.API/Program.cs` | T001 | M | 1. `TokenService.GenerateAccessToken(user)` returns a signed JWT whose decoded payload contains `sub`, `email`, `role`, and an `exp` exactly 900 seconds after `iat`. 2. A request to any `[Authorize]` endpoint with a valid JWT returns 200; a missing or expired JWT returns 401. 3. `PasswordService.Hash("password")` produces a BCrypt hash verifiable by `PasswordService.Verify("password", hash)`. |
| T005 | Set up Docker Compose | Infrastructure | Create `docker-compose.yml` with four services: `nginx`, `api`, `postgres`, `redis`. Configure healthchecks on `postgres` and `redis`. Create `nginx/nginx.conf` with TLS termination, reverse proxy to `api:8080`, WebSocket upgrade for SignalR, and static file serving for the Angular build output. Create `Dockerfile` for the API project using multi-stage build. | `docker-compose.yml`, `nginx/nginx.conf`, `src/FreshFlow.API/Dockerfile`, `.env.example` | T001 | M | 1. `docker compose up` starts all four containers without errors and the `api` container passes its healthcheck. 2. `curl http://localhost/health` returns `{"status":"Healthy"}` after all containers are running. 3. The `api` container does not start until `postgres` and `redis` healthchecks pass (`depends_on: condition: service_healthy`). |
| T006 | Set up CI/CD pipeline | Infrastructure | Create GitHub Actions workflow `.github/workflows/ci.yml` with stages: restore → build → unit tests → integration tests → format check → Docker build (on `main` only) → push to registry (on `main` only). Enforce minimum 70% code coverage in the unit test stage. Add a PR template with the review checklist. | `.github/workflows/ci.yml`, `.github/pull_request_template.md` | T001, T005 | M | 1. A pull request to `main` triggers the CI workflow and all stages complete successfully on a clean repository. 2. A unit test failure causes the pipeline to fail and block the PR merge. 3. The Docker build stage runs only on pushes to `main`, not on PRs. |
| T007 | Configure SignalR with Redis backplane | Infrastructure | Add SignalR to `FreshFlow.API` with `AddSignalR().AddStackExchangeRedis(...)`. Create the three hub classes (`PricingHub`, `OrderHub`, `DeliveryHub`) as empty stubs with JWT authentication on the negotiate endpoint (token from `access_token` query string). Register hub endpoints in `Program.cs`. Implement `SignalRNotificationService` in infrastructure. | `src/FreshFlow.API/SignalR/PricingHub.cs`, `src/FreshFlow.API/SignalR/OrderHub.cs`, `src/FreshFlow.API/SignalR/DeliveryHub.cs`, `src/Modules/Notifications/FreshFlow.Notifications.Infrastructure/Realtime/SignalRNotificationPushService.cs`, `src/FreshFlow.API/Program.cs` | T001, T003, T004 | M | 1. A client can connect to `/hubs/pricing` with a valid JWT (via `?access_token=`) and the connection is accepted. 2. A client connecting without a JWT receives HTTP 401 on the negotiate endpoint. 3. With `SignalR__UseRedis=true`, `services.AddSignalR().AddStackExchangeRedis(...)` is called and the backplane connects to Redis successfully at startup. |
| T008 | Set up logging and health check endpoints | Infrastructure | Configure Serilog with structured JSON output and mandatory fields: `correlationId`, `userId`, `timestamp`, `level`, `message`. Add a correlation-ID middleware that reads/generates the `X-Correlation-Id` header and adds it to the log context. Implement `GET /health` using ASP.NET Core health checks for PostgreSQL and Redis. Register all health checks in `Program.cs`. | `src/FreshFlow.API/Middleware/ExceptionHandlingMiddleware.cs`, `src/FreshFlow.API/Program.cs`, `src/FreshFlow.API/appsettings.json` | T001, T002, T003 | S | 1. `GET /health` returns `{"status":"Healthy","components":{"postgresql":{"status":"Healthy"},"redis":{"status":"Healthy"}}}` when both services are reachable. 2. Every log line written via `ILogger` contains `correlationId`, `timestamp`, and `level` fields in JSON. 3. When PostgreSQL is unreachable, `GET /health` returns HTTP 503 with `status: Unhealthy`. |

---

### Group B — Auth Module (T009–T012)

| Task ID | Title | Domain/Module | Description | Files to Create/Modify | Dependencies | Complexity | Acceptance Criteria |
|---------|-------|---------------|-------------|------------------------|--------------|------------|---------------------|
| T009 | Implement login endpoint | Auth | Implement `POST /api/v1/auth/login`. Create `AuthController`, `AuthService`, `UserRepository`, and `RefreshTokenRepository`. `AuthService.LoginAsync` validates credentials (BCrypt), issues a JWT access token and a cryptographically random refresh token (stored as BCrypt hash), and creates a token family ID for rotation tracking. Return `accessToken`, `refreshToken`, `expiresIn`, and user object. Implement FluentValidation for the login DTO. | `src/FreshFlow.API/Controllers/AuthController.cs`, `src/Modules/Auth/FreshFlow.Auth.Application/Commands/LoginCommand.cs`, `src/Modules/Auth/FreshFlow.Auth.Application/DTOs/LoginRequestDto.cs`, `src/Modules/Auth/FreshFlow.Auth.Application/DTOs/LoginResponseDto.cs`, `src/Modules/Auth/FreshFlow.Auth.Application/Validators/LoginRequestValidator.cs`, `src/Modules/Auth/FreshFlow.Auth.Application/Services/AuthService.cs`, `src/Modules/Auth/FreshFlow.Auth.Infrastructure/Persistence/Repositories/UserRepository.cs`, `src/Modules/Auth/FreshFlow.Auth.Infrastructure/Persistence/Repositories/RefreshTokenRepository.cs`, `src/Modules/Auth/FreshFlow.Auth.Domain/Entities/User.cs`, `src/Modules/Auth/FreshFlow.Auth.Domain/Entities/RefreshToken.cs`, `src/Modules/Auth/FreshFlow.Auth.Domain/Repositories/IUserRepository.cs`, `src/Modules/Auth/FreshFlow.Auth.Domain/Repositories/IRefreshTokenRepository.cs` | T002, T004 | M | 1. `POST /auth/login` with valid credentials returns HTTP 200 with `accessToken` and `refreshToken` fields; decoded JWT payload contains `sub`, `email`, `role`, and `exp - iat = 900`. 2. `POST /auth/login` with wrong password returns HTTP 401 with error code `INVALID_CREDENTIALS` and no token in the body. 3. `POST /auth/login` with a non-existent email returns HTTP 401 with the same `INVALID_CREDENTIALS` code (no user enumeration). |
| T010 | Implement refresh token endpoint with rotation | Auth | Implement `POST /api/v1/auth/refresh`. `AuthService.RefreshAsync` finds the token by its hash, validates expiry, marks it revoked (`revoked_at = NOW()`), creates a new token in the same family, and returns the new pair. Implement token-reuse detection: if the presented token is already revoked, invalidate the entire `family_id` (set `revoked_at` on all tokens in the family). | `src/FreshFlow.API/Controllers/AuthController.cs`, `src/Modules/Auth/FreshFlow.Auth.Application/Commands/RefreshTokenCommand.cs`, `src/Modules/Auth/FreshFlow.Auth.Application/DTOs/RefreshRequestDto.cs`, `src/Modules/Auth/FreshFlow.Auth.Application/Services/AuthService.cs`, `src/Modules/Auth/FreshFlow.Auth.Infrastructure/Persistence/Repositories/RefreshTokenRepository.cs` | T009 | M | 1. `POST /auth/refresh` with a valid token returns HTTP 200 with a new `accessToken` and a new `refreshToken`; the old refresh token is now revoked in the database. 2. Reusing the same refresh token a second time returns HTTP 409 with error code `REFRESH_TOKEN_REUSE` and all tokens in the same family are revoked. 3. An expired refresh token returns HTTP 401 with error code `REFRESH_TOKEN_EXPIRED`. |
| T011 | Implement logout endpoint | Auth | Implement `POST /api/v1/auth/logout` (protected — requires Bearer token). `AuthService.LogoutAsync` looks up the refresh token by hash and sets `revoked_at`. If the token is already revoked or belongs to a different user, return 204 silently (no oracle attack). Returns HTTP 204 on success. | `src/FreshFlow.API/Controllers/AuthController.cs`, `src/Modules/Auth/FreshFlow.Auth.Application/Commands/LogoutCommand.cs`, `src/Modules/Auth/FreshFlow.Auth.Application/DTOs/LogoutRequestDto.cs`, `src/Modules/Auth/FreshFlow.Auth.Application/Services/AuthService.cs` | T009, T010 | S | 1. `POST /auth/logout` with a valid Bearer token and a valid refresh token body returns HTTP 204. 2. After logout, `POST /auth/refresh` with the invalidated token returns HTTP 401. 3. Logging out without an Authorization header returns HTTP 401; the refresh token is not touched. |
| T012 | Implement admin user registration | Auth | Implement `POST /api/v1/auth/register` (Admin role required). `AuthService.RegisterAsync` validates the email uniqueness, hashes the password, creates the `users` row with the specified role, and (for `restaurant` role) creates a `restaurants` stub row with `is_approved = false`. Return HTTP 201 with the new `userId`. Implement `PATCH /api/v1/admin/users/{id}/approve` for Admin approval flow. | `src/FreshFlow.API/Controllers/AuthController.cs`, `src/FreshFlow.API/Controllers/AdminController.cs`, `src/Modules/Auth/FreshFlow.Auth.Application/Commands/RegisterUserCommand.cs`, `src/Modules/Auth/FreshFlow.Auth.Application/DTOs/RegisterRequestDto.cs`, `src/Modules/Auth/FreshFlow.Auth.Application/Validators/RegisterRequestValidator.cs`, `src/Modules/Auth/FreshFlow.Auth.Application/Services/AuthService.cs`, `src/Modules/Auth/FreshFlow.Auth.Domain/Entities/User.cs` | T009 | M | 1. `POST /auth/register` by an Admin with role `KIOSK_STAFF` or `RESTAURANT` creates the account and returns HTTP 201 with `userId`. 2. A non-Admin calling `POST /auth/register` receives HTTP 403. 3. Creating a user with a duplicate email returns HTTP 409 with error code `EMAIL_ALREADY_EXISTS`. |

---

### Group C — Pricing Module (T013–T019)

| Task ID | Title | Domain/Module | Description | Files to Create/Modify | Dependencies | Complexity | Acceptance Criteria |
|---------|-------|---------------|-------------|------------------------|--------------|------------|---------------------|
| T013 | Create remaining DB migrations (market_products, price_snapshots with partitioning) | Pricing | Generate EF Core migrations for `market_products`, `price_snapshots` (with monthly RANGE partitioning), `markets` (if not in T002), and `system_config`. Configure the `IEntityTypeConfiguration<PriceSnapshot>` to use `PARTITION BY RANGE`. Add the `price_snapshots_default` partition and the first two monthly partition tables. Add the `PartitionMaintenanceJob` stub as an `IHostedService`. | `src/FreshFlow.Infrastructure.Persistence/Migrations/20260101_000003_AddPriceSnapshotsPartitioning.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Infrastructure/Persistence/Configurations/`, `src/Modules/Pricing/FreshFlow.Pricing.Application/BackgroundJobs/PartitionMaintenanceJob.cs` | T002 | M | 1. Running `MigrateAsync()` creates `price_snapshots` as a partitioned table with `price_snapshots_default` and at least the current month's partition. 2. An insert to `price_snapshots` with a `recorded_at` in the current month lands in the correct monthly partition, not the default partition. 3. `idx_price_snapshots_market_product_recorded_at` index exists on the parent table and propagates to all partitions. |
| T014 | Implement product catalog endpoints | Pricing | Implement `GET /api/v1/products` (returns active products, supports `?includeInactive=true` for Admin), `POST /api/v1/admin/products` (Admin only — creates product), `PATCH /api/v1/admin/products/{id}` (Admin only — update), and `GET /api/v1/markets` (returns active markets). Create `ProductsController`, `PricingService`, `ProductRepository`, and `MarketRepository`. Use FluentValidation on all request DTOs. | `src/FreshFlow.API/Controllers/PricingController.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Application/Services/PricingService.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Application/DTOs/ProductDto.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Application/DTOs/CreateProductRequestDto.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Application/Validators/CreateProductValidator.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Infrastructure/Persistence/Repositories/ProductRepository.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Infrastructure/Persistence/Repositories/MarketRepository.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Domain/Entities/Product.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Domain/Entities/Market.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Domain/Repositories/IProductRepository.cs` | T013 | M | 1. `GET /api/v1/products` without admin token returns only products where `deleted_at IS NULL`. 2. `POST /api/v1/admin/products` by Admin returns HTTP 201 with the new `productId`; a Kiosk Staff token returns HTTP 403. 3. `GET /api/v1/markets` returns the three seeded markets (Hoc Mon, Binh Dien, Thu Duc) with their coordinates. |
| T015 | Implement market products listing with current price | Pricing | Implement `GET /api/v1/markets/{marketId}/products` and `GET /api/v1/markets/{marketId}/products/{productId}`. The service reads from the Redis cache (`price:{marketId}:{productId}`) first; on cache miss, falls back to `market_products` PostgreSQL table and repopulates the cache. Available quantity = `current_quantity - reserved_quantity`. | `src/FreshFlow.API/Controllers/PricingController.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Application/Services/PricingService.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Application/DTOs/MarketProductDto.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Infrastructure/Persistence/Repositories/MarketProductRepository.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Domain/Entities/MarketProduct.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Domain/Repositories/IMarketProductRepository.cs` | T013, T003 | M | 1. `GET /api/v1/markets/{marketId}/products` returns all active products for the market with `currentPrice`, `currentQuantity`, and `availableQuantity` fields. 2. If a Redis cache entry exists for a product, it is served from Redis without hitting PostgreSQL (verifiable via log output). 3. When Redis is unavailable, the endpoint still returns correct data from PostgreSQL without error. |
| T016 | Implement kiosk price update endpoint + Redis cache update | Pricing | Implement `PATCH /api/v1/markets/{marketId}/products/{productId}/price` and `PATCH /api/v1/markets/{marketId}/products/{productId}/quantity`. `PricingService.UpdatePriceAsync` validates market assignment (Kiosk Staff can only update their assigned market), applies optimistic concurrency via `updatedAt` check, writes a `price_snapshots` row and updates `market_products.current_price` in the same transaction, then calls `PricingCacheWriter` to update `price:{marketId}:{productId}` in Redis. Return `Result<PriceUpdateResponseDto>`. | `src/FreshFlow.API/Controllers/PricingController.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Application/Commands/UpdatePriceCommand.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Application/DTOs/PriceUpdateRequestDto.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Application/DTOs/PriceUpdateResponseDto.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Application/Validators/UpdatePriceValidator.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Application/Services/PricingService.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Infrastructure/Caching/RedisPricingCacheWriter.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Infrastructure/Persistence/Repositories/PriceSnapshotRepository.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Domain/Entities/PriceSnapshot.cs` | T013, T015, T004 | L | 1. `PATCH /markets/{id}/products/{id}/price` with a valid price returns HTTP 200 with `updatedPrice` and `updatedAt`; the new price is immediately readable from `GET /markets/{id}/products/{id}`. 2. A Kiosk Staff assigned to Market A calling the endpoint for Market B returns HTTP 403. 3. A zero or negative price returns HTTP 422 with a field-level validation error on `price`. |
| T017 | Implement PricingHub (SignalR) + broadcast on price update | Pricing/Notifications | Flesh out `PricingHub.cs` with `JoinMarketGroup(marketId)` and `LeaveMarketGroup(marketId)` hub methods. Implement `IPricingBroadcastService` and `PricingBroadcastService` which calls `IHubContext<PricingHub>.Clients.Group($"market:{marketId}").SendAsync("PriceUpdated", payload)`. Wire up `PricingService` to call `IPricingBroadcastService.BroadcastPriceUpdateAsync` after a successful database commit. Broadcast is fire-and-forget (no `await` in the hot path). | `src/FreshFlow.API/SignalR/PricingHub.cs`, `src/Modules/Notifications/FreshFlow.Notifications.Infrastructure/Realtime/SignalRNotificationPushService.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Application/Abstractions/IPricingBroadcastService.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Application/Services/PricingService.cs` | T007, T016 | M | 1. A SignalR client subscribed to `market:{marketId}` receives a `PriceUpdated` event within 500 ms of the `PATCH /price` response timestamp. 2. The event payload contains `productId`, `marketId`, `newPrice`, `newQuantity`, and `updatedAt`. 3. Clients not subscribed to the relevant market group do not receive the event. |
| T018 | Implement price history endpoint with cursor pagination | Pricing | Implement `GET /api/v1/markets/{marketId}/products/{productId}/price-history` with cursor-based pagination (cursor = base64-encoded `{id, recordedAt}`). `PriceSnapshotRepository.GetHistoryAsync` queries `price_snapshots` ordered by `recorded_at DESC`, scoped to the given `market_product_id`. The endpoint is accessible to Admin and Restaurant roles. | `src/FreshFlow.API/Controllers/PricingController.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Application/Queries/GetPriceHistoryQuery.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Application/DTOs/PriceSnapshotDto.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Infrastructure/Persistence/Repositories/PriceSnapshotRepository.cs` | T016 | S | 1. `GET /markets/{id}/products/{id}/price-history` returns a paginated list of snapshots sorted descending by `recordedAt` with `nextCursor` in the meta envelope. 2. After 10 consecutive price updates, the history endpoint returns at least 10 distinct records with monotonically distinct `recordedAt` values. 3. Records cannot be deleted — there is no `DELETE` endpoint for this resource, and the repository exposes only append and read operations. |
| T019 | Implement significant price change detection + SignalR alert | Pricing | Implement `SignificantChangeDetector` inside `PricingService`. After writing a price snapshot, compute percentage change relative to the previous snapshot. If change ≥ configured threshold (read from `system_config` via Redis or PostgreSQL), emit a `SignificantPriceAlert` SignalR event with `changePercent`, `previousPrice`, `newPrice`, and `severity` (`MEDIUM` for 5–15%, `HIGH` for > 15%). Implement `PATCH /api/v1/admin/config/significant-price-threshold` for Admin to update the threshold. | `src/Modules/Pricing/FreshFlow.Pricing.Application/Services/PricingService.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Application/Services/SignificantChangeDetector.cs`, `src/FreshFlow.API/Controllers/PricingController.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Application/DTOs/SignificantPriceAlertDto.cs` | T017 | M | 1. A price change of ≥ 5% triggers a `SignificantPriceAlert` SignalR event to `market:{marketId}` within 500 ms of the PATCH response. 2. The alert payload includes `changePercent`, `previousPrice`, `newPrice`, and `severity` (`MEDIUM` or `HIGH`). 3. A quantity-only update (price unchanged) does not trigger a `SignificantPriceAlert`. |

---

### Group D — Orders Module (T020–T027)

| Task ID | Title | Domain/Module | Description | Files to Create/Modify | Dependencies | Complexity | Acceptance Criteria |
|---------|-------|---------------|-------------|------------------------|--------------|------------|---------------------|
| T020 | Create DB migrations for Orders module | Orders | Generate EF Core migrations for `restaurants`, `order_groups`, `scheduled_orders`, `orders`, and `order_items` tables with all constraints, indexes, and the `order_status` / `order_group_status` enum types. Configure EF Core entity type configurations for each entity. | `src/FreshFlow.Infrastructure.Persistence/Migrations/20260101_000004_AddOrdersTables.cs`, `src/Modules/Orders/FreshFlow.Orders.Infrastructure/Persistence/Configurations/`, `src/Modules/Orders/FreshFlow.Orders.Domain/Entities/Order.cs`, `src/Modules/Orders/FreshFlow.Orders.Domain/Entities/OrderItem.cs`, `src/Modules/Orders/FreshFlow.Orders.Domain/Entities/OrderGroup.cs`, `src/Modules/Orders/FreshFlow.Orders.Domain/Entities/Restaurant.cs`, `src/Modules/Orders/FreshFlow.Orders.Domain/Entities/ScheduledOrder.cs` | T002 | S | 1. Migrations apply cleanly to a blank database after `MigrateAsync()`. 2. All FK constraints (e.g., `orders.restaurant_id → restaurants.id ON DELETE RESTRICT`) are present in the schema. 3. `order_items.subtotal` is a `GENERATED ALWAYS AS (quantity * unit_price) STORED` computed column. |
| T021 | Implement restaurant profile creation | Orders | Implement `POST /api/v1/restaurants` (Restaurant role — creates the restaurant profile linked to the authenticated user's `user_id`) and `GET /api/v1/restaurants/me` (returns the requesting restaurant's profile). Create `RestaurantService` and `RestaurantRepository`. A restaurant account with `is_approved = false` cannot place orders (validated in T022). | `src/FreshFlow.API/Controllers/OrdersController.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/Services/RestaurantService.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/DTOs/RestaurantDto.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/DTOs/CreateRestaurantRequestDto.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/Validators/CreateRestaurantValidator.cs`, `src/Modules/Orders/FreshFlow.Orders.Infrastructure/Persistence/Repositories/RestaurantRepository.cs`, `src/Modules/Orders/FreshFlow.Orders.Domain/Repositories/IRestaurantRepository.cs` | T020, T009 | S | 1. `POST /restaurants` by an authenticated Restaurant user returns HTTP 201 with the new restaurant profile. 2. A duplicate call (user already has a restaurant profile) returns HTTP 409. 3. `GET /restaurants/me` returns the requesting restaurant's profile with `isApproved` field. |
| T022 | Implement bulk order creation with stock validation and soft-reservation | Orders | Implement `POST /api/v1/orders`. `OrderService.CreateOrderAsync` validates each line item references an active product (via `IProductCatalogReader`), checks available stock (Redis quantity minus existing reservation), applies soft-reservation in Redis (`order:reservation:{marketProductId}` INCRBY), then writes the `orders` + `order_items` rows in a single PostgreSQL transaction. If Redis write fails, roll back the PostgreSQL transaction and return HTTP 500. | `src/FreshFlow.API/Controllers/OrdersController.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/Commands/CreateOrderCommand.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/DTOs/CreateOrderRequestDto.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/DTOs/OrderResponseDto.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/Validators/CreateOrderValidator.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/Services/OrderService.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/Services/SoftReservationService.cs`, `src/Modules/Orders/FreshFlow.Orders.Infrastructure/Persistence/Repositories/OrderRepository.cs`, `src/Modules/Orders/FreshFlow.Orders.Domain/Entities/Order.cs`, `src/Modules/Pricing/FreshFlow.Pricing.Application/Abstractions/IProductCatalogReader.cs` | T020, T015, T003 | L | 1. `POST /orders` with a valid body returns HTTP 201 with `orderId` and `status: pending`; the soft-reservation counter in Redis is incremented for each line item. 2. A line item requesting more than available stock returns HTTP 422 with `INSUFFICIENT_STOCK`, including `productId`, `requestedQty`, and `availableQty` for each failing item. 3. An order with zero line items returns HTTP 422; a line item with a non-existent product returns HTTP 422 with `INVALID_PRODUCT`. |
| T023 | Implement order listing and detail endpoints | Orders | Implement `GET /api/v1/orders/{orderId}` (returns order with line items; Restaurant sees only their own orders, Admin sees all), `GET /api/v1/orders` (paginated, filterable by `?status=` and `?sort=createdAt:asc`, offset-based), and `GET /api/v1/admin/orders` (Admin only, supports `?restaurantId=`). | `src/FreshFlow.API/Controllers/OrdersController.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/Queries/GetOrderQuery.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/Queries/ListOrdersQuery.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/DTOs/OrderListItemDto.cs`, `src/Modules/Orders/FreshFlow.Orders.Infrastructure/Persistence/Repositories/OrderRepository.cs` | T022 | S | 1. `GET /orders/{orderId}` for the owning restaurant returns the order with all line items. 2. A restaurant requesting another restaurant's order returns HTTP 403. 3. `GET /orders?status=pending&page=1&pageSize=20` returns a paginated response with `totalCount`, `page`, and `pageSize` in the `meta` envelope. |
| T024 | Implement order cancellation with reservation release | Orders | Implement `PATCH /api/v1/orders/{orderId}/cancel`. `OrderService.CancelOrderAsync` validates the order status is `pending` or `confirmed`; transitions to `cancelled`; calls `SoftReservationService.ReleaseAsync` to decrement Redis reservation counters for all line items; sets `cancelled_at`. Cancellation of `in_transit` or `delivered` orders returns HTTP 409 with `ORDER_NOT_CANCELLABLE`. | `src/FreshFlow.API/Controllers/OrdersController.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/Commands/CancelOrderCommand.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/Services/OrderService.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/Services/SoftReservationService.cs` | T022, T023 | S | 1. `PATCH /orders/{orderId}/cancel` when status is `pending` returns HTTP 200 and transitions status to `cancelled`; Redis reservation counters for all line items are decremented. 2. Cancelling an `in_transit` order returns HTTP 409 with `ORDER_NOT_CANCELLABLE`. 3. Only the owning restaurant or an Admin can cancel an order; a different restaurant returns HTTP 403. |
| T025 | Implement order status updates + OrderHub SignalR broadcast | Orders | Implement `PATCH /api/v1/admin/orders/{orderId}/status` (Admin only — transitions order through the status machine: `pending → confirmed → in_transit → delivered`). `OrderStatusService.TransitionAsync` validates the transition is allowed, writes the new status to PostgreSQL, then calls `IOrderBroadcastService.BroadcastStatusChangeAsync`. Flesh out `OrderHub` with automatic group join (`restaurant:{restaurantId}`) on connect based on JWT claims. | `src/FreshFlow.API/Controllers/AdminController.cs`, `src/FreshFlow.API/SignalR/OrderHub.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/Commands/UpdateOrderStatusCommand.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/Services/OrderStatusService.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/Abstractions/IOrderBroadcastService.cs`, `src/Modules/Notifications/FreshFlow.Notifications.Infrastructure/Realtime/SignalRNotificationPushService.cs` | T023, T007 | M | 1. `PATCH /admin/orders/{id}/status` by Admin transitions the order status and returns HTTP 200. 2. The restaurant client subscribed to `restaurant:{restaurantId}` receives an `OrderStatusChanged` event within 500 ms of the status update; payload includes `orderId`, `newStatus`, `previousStatus`, and `changedAt`. 3. An invalid status transition (e.g., `pending → delivered`) returns HTTP 422. |
| T026 | Implement order grouping | Orders | Implement `POST /api/v1/admin/order-groups` (Admin only — creates an `order_groups` row and links the specified `orderIds`) and `GET /api/v1/admin/order-groups/{id}`. `OrderGroupService.CreateGroupAsync` validates all included orders have status `confirmed`, checks no order already belongs to an active group, and sets `order_group_id` on each included order. | `src/FreshFlow.API/Controllers/AdminController.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/Commands/CreateOrderGroupCommand.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/DTOs/CreateOrderGroupRequestDto.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/DTOs/OrderGroupDto.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/Services/OrderGroupService.cs`, `src/Modules/Orders/FreshFlow.Orders.Infrastructure/Persistence/Repositories/OrderGroupRepository.cs`, `src/Modules/Orders/FreshFlow.Orders.Domain/Repositories/IOrderGroupRepository.cs` | T025 | M | 1. `POST /admin/order-groups` with a list of confirmed `orderIds` returns HTTP 201 with `orderGroupId`. 2. Including an order not in `confirmed` status returns HTTP 422. 3. Adding an order already in an active group returns HTTP 409. |
| T027 | Implement scheduled orders | Orders | Implement `POST /api/v1/orders/scheduled` (Restaurant role — creates a `scheduled_orders` row) and `GET /api/v1/orders/scheduled/{id}/instances`. Implement `ScheduledOrderService` with a background `IHostedService` that runs every 60 seconds, checks `scheduled_orders` for due instances (based on `last_executed_at` and `recurrence_type`), and creates order instances idempotently. Implement missed-execution recovery and `MISSED_EXECUTION` logging. | `src/FreshFlow.API/Controllers/OrdersController.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/Commands/CreateScheduledOrderCommand.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/DTOs/ScheduledOrderDto.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/Services/ScheduledOrderService.cs`, `src/Modules/Orders/FreshFlow.Orders.Application/BackgroundJobs/ScheduledOrderJob.cs`, `src/Modules/Orders/FreshFlow.Orders.Infrastructure/Persistence/Repositories/ScheduledOrderRepository.cs`, `src/Modules/Orders/FreshFlow.Orders.Domain/Entities/ScheduledOrder.cs` | T022 | L | 1. `POST /orders/scheduled` with `recurrence: daily` returns HTTP 201 with `scheduledOrderId` and the next run time in `Asia/Ho_Chi_Minh`. 2. The background job generates a concrete order instance within 60 seconds of the scheduled time. 3. The job is idempotent: running it twice in the same 60-second window does not create duplicate order instances. |

---

### Group E — Logistics Module (T028–T033)

| Task ID | Title | Domain/Module | Description | Files to Create/Modify | Dependencies | Complexity | Acceptance Criteria |
|---------|-------|---------------|-------------|------------------------|--------------|------------|---------------------|
| T028 | Create DB migrations for Logistics module | Logistics | Generate EF Core migrations for `vehicles`, `delivery_routes`, and `deliveries` tables with all constraints, the `route_status` and `delivery_status` enum types, and the JSONB `route_metadata` column. Configure `IEntityTypeConfiguration` for each entity. | `src/FreshFlow.Infrastructure.Persistence/Migrations/20260101_000005_AddLogisticsTables.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Infrastructure/Persistence/Configurations/`, `src/Modules/Logistics/FreshFlow.Logistics.Domain/Entities/Vehicle.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Domain/Entities/DeliveryRoute.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Domain/Entities/Delivery.cs` | T002 | S | 1. Migrations apply cleanly; `delivery_routes.route_metadata` column is type `jsonb`. 2. All FK constraints are present (e.g., `deliveries.order_id → orders.id ON DELETE RESTRICT`). 3. `deliveries_order_id_unique` unique constraint exists on `deliveries.order_id`. |
| T029 | Implement vehicle CRUD endpoints | Logistics | Implement `POST /api/v1/admin/vehicles` (Admin only — creates vehicle), `GET /api/v1/admin/vehicles` (lists vehicles, supports `?available=true`), and `PATCH /api/v1/admin/vehicles/{id}` (Admin only — update plate, capacity, or status). Create `VehicleRepository`. Enforce `plate_number` uniqueness returning HTTP 409 on duplicate. | `src/FreshFlow.API/Controllers/LogisticsController.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Application/Services/VehicleService.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Application/DTOs/VehicleDto.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Application/DTOs/CreateVehicleRequestDto.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Application/Validators/CreateVehicleValidator.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Infrastructure/Persistence/Repositories/VehicleRepository.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Domain/Entities/Vehicle.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Domain/Repositories/IVehicleRepository.cs` | T028 | S | 1. `POST /admin/vehicles` returns HTTP 201 with `vehicleId`; a duplicate `plateNumber` returns HTTP 409. 2. `GET /admin/vehicles?available=true` returns only vehicles where `is_available = true` and `deleted_at IS NULL`. 3. Assigning `status: INACTIVE` to a vehicle via PATCH sets it inactive and it no longer appears in available-vehicle queries. |
| T030 | Implement route calculation engine | Logistics | Implement `VrpSolver` (nearest-neighbor heuristic with 2-opt local improvement) as a pure in-memory class with no database or Redis dependencies. Accepts a list of stops (markets, hubs, restaurants with coordinates) and an optimization criterion (`DISTANCE`, `TIME`, `COST`). Returns an ordered stop sequence with estimated arrival/departure times, total distance, duration, and cost. Enforce the 20-stop limit. | `src/Modules/Logistics/FreshFlow.Logistics.Application/Services/VrpSolver.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Application/Services/RouteCalculationService.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Application/DTOs/RouteCalculationRequestDto.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Application/DTOs/RouteCalculationResponseDto.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Application/Abstractions/IRouteCalculationService.cs`, `tests/Unit/FreshFlow.Logistics.UnitTests/Algorithms/NearestNeighborVrpSolverTests.cs` | T001 | L | 1. `VrpSolver.Solve(stops, criterion: DISTANCE)` for 3 stops returns an ordered stop list with estimated distances and durations; the method completes in < 100 ms. 2. Passing more than 20 stops throws a `StopLimitExceededException` (mapped to HTTP 422 with `STOP_LIMIT_EXCEEDED` by the controller). 3. `VrpSolver` has no constructor dependencies on `IRepository` or `ICacheService` — it is unit-testable in complete isolation. |
| T031 | Implement route creation and assignment endpoints | Logistics | Implement `POST /api/v1/logistics/routes/calculate` (calls `RouteCalculationService`, caches result in Redis at `route:{sha256}`, persists to `delivery_routes`), and `POST /api/v1/logistics/routes/{routeId}/assign-vehicle` (Admin only — links a vehicle to a route, validates vehicle availability and active status). Implement `IHubAvailabilityReader` for hub capacity checks during route calculation. | `src/FreshFlow.API/Controllers/LogisticsController.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Application/Services/RouteCalculationService.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Infrastructure/Persistence/Repositories/DeliveryRouteRepository.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Infrastructure/Caching/RedisRouteCache.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Application/Abstractions/IHubAvailabilityReader.cs` | T030, T029, T003 | M | 1. `POST /logistics/routes/calculate` with valid inputs returns a route plan within 3 seconds; the route is persisted to `delivery_routes` and cached in Redis. 2. A second identical request within 1 hour returns the cached route without re-running the VRP solver. 3. `POST /logistics/routes/{id}/assign-vehicle` with an already-assigned vehicle returns HTTP 409 with `VEHICLE_NOT_AVAILABLE`. |
| T032 | Implement delivery status tracking + DeliveryHub SignalR broadcast | Logistics | Implement `PATCH /api/v1/admin/deliveries/{deliveryId}/status` (Admin only — transitions delivery status: `pending → picked_up → in_transit → delivered`). `DeliveryStatusService` updates PostgreSQL and calls `IDeliveryBroadcastService` to emit `DeliveryStarted` or `DeliveryCompleted` events per-restaurant to `restaurant:{restaurantId}` groups. Flesh out `DeliveryHub` with automatic group join. | `src/FreshFlow.API/Controllers/LogisticsController.cs`, `src/FreshFlow.API/SignalR/DeliveryHub.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Application/Services/DeliveryStatusService.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Application/Abstractions/IDeliveryBroadcastService.cs`, `src/Modules/Notifications/FreshFlow.Notifications.Infrastructure/Realtime/SignalRNotificationPushService.cs` | T031, T025, T007 | M | 1. `PATCH /admin/deliveries/{id}/status` with `status: in_transit` emits a `DeliveryStarted` SignalR event to each affected restaurant within 500 ms. 2. The `DeliveryStarted` payload includes `scheduleId`, `routeId`, `estimatedArrivalAt`, and `orderIds`. 3. Each restaurant only receives events for its own orders, even in a multi-drop delivery. |
| T033 | Implement route caching in Redis | Logistics | Implement `RouteCache` in `FreshFlow.Infrastructure/Caching/RouteCache.cs`. The cache key is `route:{SHA256(sorted_stop_ids + "|" + criterion)}`. TTL is 1 hour (absolute). On vehicle re-assignment (`DeliveryScheduleService`), explicitly `DEL` the cached route key. Ensure the SHA256 hash is computed deterministically (stop IDs sorted ascending, criterion appended). | `src/Modules/Logistics/FreshFlow.Logistics.Infrastructure/Caching/RedisRouteCache.cs`, `src/Modules/Logistics/FreshFlow.Logistics.Application/Services/RouteCalculationService.cs` | T031 | S | 1. Two identical route calculation requests within 1 hour return the same result; only one VRP computation occurs (verifiable via log output). 2. After vehicle re-assignment, the route cache key is deleted, and the next calculation re-runs the VRP solver. 3. Different optimization criteria for the same stop list produce different cache keys. |

---

### Group F — Hub Module (T034–T038)

| Task ID | Title | Domain/Module | Description | Files to Create/Modify | Dependencies | Complexity | Acceptance Criteria |
|---------|-------|---------------|-------------|------------------------|--------------|------------|---------------------|
| T034 | Create DB migrations for Hub module | Hub | Generate EF Core migrations for `hubs`, `hub_inventory`, `hub_inbound_events`, `hub_outbound_events`, and `cross_dock_transfers` tables. Configure EF Core entity type configurations, including the JSONB `items` column on `hub_inbound_events` and `hub_outbound_events`. | `src/FreshFlow.Infrastructure.Persistence/Migrations/20260101_000006_AddHubTables.cs`, `src/Modules/Hub/FreshFlow.Hub.Infrastructure/Persistence/Configurations/`, `src/Modules/Hub/FreshFlow.Hub.Domain/Entities/Hub.cs`, `src/Modules/Hub/FreshFlow.Hub.Domain/Entities/HubInventory.cs`, `src/Modules/Hub/FreshFlow.Hub.Domain/Entities/HubInboundEvent.cs`, `src/Modules/Hub/FreshFlow.Hub.Domain/Entities/HubOutboundEvent.cs`, `src/Modules/Hub/FreshFlow.Hub.Domain/Entities/CrossDockTransfer.cs` | T028 | S | 1. Migrations apply cleanly; `hub_inbound_events.items` and `hub_outbound_events.items` columns are type `jsonb`. 2. `hub_inventory.quantity_available` is a `GENERATED ALWAYS AS (quantity_in - quantity_out) STORED` computed column. 3. `hub_inventory_unique` constraint on `(hub_id, market_product_id)` is present. |
| T035 | Implement hub CRUD endpoints | Hub | Implement `POST /api/v1/admin/hubs` (Admin only — create hub with name, address, lat/lng, capacity), `GET /api/v1/admin/hubs` (returns all hubs with `occupiedCapacityKg` and `availableCapacityKg`), and `PATCH /api/v1/admin/hubs/{id}` (update any field). Block deactivation of a hub with pending inbound deliveries (check `hub_inbound_events` for unresolved entries). | `src/FreshFlow.API/Controllers/HubController.cs`, `src/Modules/Hub/FreshFlow.Hub.Application/Services/HubService.cs`, `src/Modules/Hub/FreshFlow.Hub.Application/DTOs/HubDto.cs`, `src/Modules/Hub/FreshFlow.Hub.Application/DTOs/CreateHubRequestDto.cs`, `src/Modules/Hub/FreshFlow.Hub.Application/Validators/CreateHubValidator.cs`, `src/Modules/Hub/FreshFlow.Hub.Infrastructure/Persistence/Repositories/DistributionHubRepository.cs`, `src/Modules/Hub/FreshFlow.Hub.Domain/Entities/Hub.cs`, `src/Modules/Hub/FreshFlow.Hub.Domain/Repositories/IDistributionHubRepository.cs` | T034 | S | 1. `POST /admin/hubs` returns HTTP 201 with `hubId`; `GET /admin/hubs` returns hubs with `occupiedCapacityKg` and `availableCapacityKg` computed from `hub_inventory`. 2. Deactivating a hub with pending inbound deliveries returns HTTP 409 with `HUB_HAS_PENDING_DELIVERIES`. 3. `PATCH /admin/hubs/{id}` allows updating all fields except `id`; returns HTTP 200. |
| T036 | Implement inbound goods recording | Hub | Implement `POST /api/v1/hubs/{hubId}/inbound` (records an inbound event with `sourceMarketId`, `deliveryRouteId`, items JSONB, `arrivedAt`). `InboundTrackingService.RecordAsync` validates hub capacity, prevents duplicate recording for the same delivery schedule (`ALREADY_RECEIVED` check), updates `hub_inventory.quantity_in` transactionally with the event insert. Implement `GET /api/v1/hubs/{hubId}/inbound?date=`. | `src/FreshFlow.API/Controllers/HubController.cs`, `src/Modules/Hub/FreshFlow.Hub.Application/Commands/RecordInboundCommand.cs`, `src/Modules/Hub/FreshFlow.Hub.Application/DTOs/InboundEventDto.cs`, `src/Modules/Hub/FreshFlow.Hub.Application/Services/InboundTrackingService.cs`, `src/Modules/Hub/FreshFlow.Hub.Infrastructure/Persistence/Repositories/HubInboundRepository.cs`, `src/Modules/Hub/FreshFlow.Hub.Domain/Repositories/IHubEventRepository.cs` | T035, T031 | M | 1. `POST /hubs/{hubId}/inbound` returns HTTP 201 with `inboundId`; `hub_inventory.quantity_in` for each item is incremented in the same transaction. 2. A duplicate inbound for the same delivery route returns HTTP 409 with `ALREADY_RECEIVED`. 3. An inbound that would exceed hub `capacity_kg` returns HTTP 422 with `HUB_CAPACITY_EXCEEDED`. |
| T037 | Implement outbound goods recording | Hub | Implement `POST /api/v1/hubs/{hubId}/outbound` (records an outbound event with `destinationRouteId`, items JSONB, `dispatchedAt`). `OutboundTrackingService.RecordAsync` validates available hub stock per product (cannot dispatch more than `quantity_available`), updates `hub_inventory.quantity_out` transactionally. Implement `GET /api/v1/hubs/{hubId}/outbound?date=`. | `src/FreshFlow.API/Controllers/HubController.cs`, `src/Modules/Hub/FreshFlow.Hub.Application/Commands/RecordOutboundCommand.cs`, `src/Modules/Hub/FreshFlow.Hub.Application/DTOs/OutboundEventDto.cs`, `src/Modules/Hub/FreshFlow.Hub.Application/Services/OutboundTrackingService.cs`, `src/Modules/Hub/FreshFlow.Hub.Infrastructure/Persistence/Repositories/HubOutboundRepository.cs` | T036 | M | 1. `POST /hubs/{hubId}/outbound` returns HTTP 201 with `outboundId`; `hub_inventory.quantity_out` is incremented transactionally. 2. Dispatching more than available stock for any item returns HTTP 422 with `INSUFFICIENT_HUB_STOCK`. 3. `GET /hubs/{hubId}/outbound?date=2026-05-09` returns all outbound events for that date with totals. |
| T038 | Implement hub inventory view + redistribution suggestion logic | Hub | Implement `GET /api/v1/hubs/{hubId}/inventory` (returns current stock per product at the hub). Implement `GET /api/v1/hubs/{hubId}/redistribution-suggestions` — `RedistributionSuggestionService.GetSuggestionsAsync` reads hub stock and pending orders (via `IOrderGroupReader`), applies priority rules (oldest pending order first), and returns advisory suggestions. Must complete within 2 seconds. | `src/FreshFlow.API/Controllers/HubController.cs`, `src/Modules/Hub/FreshFlow.Hub.Application/Queries/GetRedistributionSuggestionsQuery.cs`, `src/Modules/Hub/FreshFlow.Hub.Application/Services/RedistributionSuggestionService.cs`, `src/Modules/Hub/FreshFlow.Hub.Application/DTOs/RedistributionSuggestionDto.cs`, `src/Modules/Hub/FreshFlow.Hub.Application/Abstractions/IOrderGroupReader.cs` | T037, T026 | M | 1. `GET /hubs/{hubId}/redistribution-suggestions` returns suggestions with `restaurantId`, `orderId`, `productId`, `suggestedQuantityKg`, and `rationale` in < 2 seconds. 2. Suggestions only recommend quantities currently available in hub stock. 3. No automatic dispatch is triggered — the endpoint is read-only. |

---

### Group G — Analytics Module (T039–T042)

| Task ID | Title | Domain/Module | Description | Files to Create/Modify | Dependencies | Complexity | Acceptance Criteria |
|---------|-------|---------------|-------------|------------------------|--------------|------------|---------------------|
| T039 | Implement price trend analytics endpoint | Analytics | Implement `GET /api/v1/analytics/price-trends?productId=&marketId=&from=&to=`. `PriceTrendQueryService` queries `price_snapshots` (read replica preferred), aggregates by day for ranges > 12 months, computes `minPrice`, `maxPrice`, `avgPrice`, and `priceVolatility` (standard deviation). Cache result in Redis at `analytics:price_trend:{marketProductId}:{date}` with 15-min TTL. | `src/FreshFlow.API/Controllers/AnalyticsController.cs`, `src/Modules/Analytics/FreshFlow.Analytics.Application/Queries/GetPriceTrendQuery.cs`, `src/Modules/Analytics/FreshFlow.Analytics.Application/Services/PriceTrendQueryService.cs`, `src/Modules/Analytics/FreshFlow.Analytics.Application/DTOs/PriceTrendDto.cs`, `src/Modules/Analytics/FreshFlow.Analytics.Infrastructure/Caching/RedisAnalyticsCache.cs` | T018, T003 | M | 1. `GET /analytics/price-trends?productId=X&marketId=Y&from=2026-04-01&to=2026-05-01` returns a time-series array and summary statistics (`minPrice`, `maxPrice`, `avgPrice`, `priceVolatility`) in < 800 ms. 2. A second request within 15 minutes returns the cached response (verifiable via log). 3. A date range > 12 months returns daily-aggregated data points, not individual snapshot records. |
| T040 | Implement demand heatmap endpoint | Analytics | Implement `GET /api/v1/analytics/demand-heatmap` (Admin only) and `GET /api/v1/analytics/demand-heatmap/time-distribution`. `DemandHeatmapQueryService` aggregates order volume and value by restaurant location and by hour-of-day × day-of-week (7×24 matrix). Reads from `orders` and `restaurants` tables on the read replica. | `src/FreshFlow.API/Controllers/AnalyticsController.cs`, `src/Modules/Analytics/FreshFlow.Analytics.Application/Queries/GetDemandHeatmapQuery.cs`, `src/Modules/Analytics/FreshFlow.Analytics.Application/Services/DemandHeatmapQueryService.cs`, `src/Modules/Analytics/FreshFlow.Analytics.Application/DTOs/DemandHeatmapDto.cs` | T023, T039 | M | 1. `GET /analytics/demand-heatmap?from=2026-04-01&to=2026-05-01` returns a list with `restaurantId`, `latitude`, `longitude`, `totalOrderCount`, `totalOrderValueVND`, and `dominantProductCategory` for each restaurant. 2. A Restaurant token returns HTTP 403. 3. `GET /analytics/demand-heatmap/time-distribution` returns a 7×24 matrix of order counts. |
| T041 | Implement delivery performance KPI endpoint | Analytics | Implement `GET /api/v1/analytics/delivery-performance` and `GET /api/v1/analytics/delivery-performance/by-route`. `DeliveryPerformanceQueryService` computes `totalDeliveries`, `onTimeCount`, `lateCount`, `onTimeRatePercent`, `avgDeliveryDurationMinutes`, `avgVehicleUtilizationPercent`. A delivery is `LATE` if `actualArrivalAt > estimatedArrivalAt + 15 minutes`. Serve from pre-aggregated data or Redis cache; response must be < 800 ms. | `src/FreshFlow.API/Controllers/AnalyticsController.cs`, `src/Modules/Analytics/FreshFlow.Analytics.Application/Queries/GetDeliveryPerformanceQuery.cs`, `src/Modules/Analytics/FreshFlow.Analytics.Application/Services/DeliveryPerformanceQueryService.cs`, `src/Modules/Analytics/FreshFlow.Analytics.Application/DTOs/DeliveryPerformanceDto.cs` | T032, T039 | M | 1. `GET /analytics/delivery-performance?from=2026-04-01&to=2026-05-01` returns all KPI fields and responds in < 800 ms. 2. A delivery where `actualArrivalAt` exceeds `estimatedArrivalAt + 15 minutes` is counted as `LATE` in `lateCount`. 3. `GET /analytics/delivery-performance/by-route` returns the same metrics broken down per route ID. |
| T042 | Implement async CSV export with polling status | Analytics | Implement `POST /api/v1/analytics/export` (Admin only — accepts `exportType` and `parameters` JSONB, creates an `export_jobs` row with `status: pending`, returns HTTP 202 with `jobId`). Implement a background `IHostedService` (`ExportJobService`) that processes pending jobs, writes CSV to temp storage, marks job `ready`. Implement `GET /api/v1/analytics/export/{jobId}/status` and `GET /api/v1/analytics/export/{jobId}/download`. Purge files after 24 hours. | `src/FreshFlow.API/Controllers/AnalyticsController.cs`, `src/Modules/Analytics/FreshFlow.Analytics.Application/Commands/CreateExportJobCommand.cs`, `src/Modules/Analytics/FreshFlow.Analytics.Application/Services/ExportJobService.cs`, `src/Modules/Analytics/FreshFlow.Analytics.Application/BackgroundJobs/ExportProcessorJob.cs`, `src/Modules/Analytics/FreshFlow.Analytics.Infrastructure/Persistence/Repositories/ExportJobRepository.cs`, `src/Modules/Analytics/FreshFlow.Analytics.Application/ExportJob.cs` | T039 | L | 1. `POST /analytics/export` returns HTTP 202 with `jobId`; `GET /analytics/export/{jobId}/status` returns `status: ready` once processing completes. 2. `GET /analytics/export/{jobId}/download` returns a CSV file with `Content-Disposition: attachment` and `Content-Type: text/csv` headers. 3. After 24 hours, `GET /analytics/export/{jobId}/download` returns HTTP 404. |

---

### Group H — Notifications Module (T043–T044)

| Task ID | Title | Domain/Module | Description | Files to Create/Modify | Dependencies | Complexity | Acceptance Criteria |
|---------|-------|---------------|-------------|------------------------|--------------|------------|---------------------|
| T043 | Implement notification persistence | Notifications | Implement notification persistence: on `PriceUpdated`, `OrderStatusChanged`, and `DeliveryStarted`/`DeliveryCompleted` events, insert a row into the `notifications` table via `NotificationService.PersistAsync`. Create the EF Core migration for the `notifications` table. Call `PersistAsync` from the broadcast services after SignalR dispatch (fire-and-forget). | `src/FreshFlow.Infrastructure.Persistence/Migrations/20260101_000007_AddNotifications.cs`, `src/Modules/Notifications/FreshFlow.Notifications.Infrastructure/Persistence/Repositories/NotificationRepository.cs`, `src/Modules/Notifications/FreshFlow.Notifications.Application/Services/NotificationService.cs`, `src/Modules/Notifications/FreshFlow.Notifications.Domain/Entities/Notification.cs`, `src/Modules/Notifications/FreshFlow.Notifications.Domain/Repositories/INotificationRepository.cs` | T017, T025, T032 | M | 1. After a price update, a `notifications` row of type `price_change` is inserted for each affected restaurant user. 2. After an order status change, a `notifications` row of type `order_status` is inserted for the restaurant owner. 3. `NotificationService.PersistAsync` fails silently (logs error, does not surface to caller) if the database write fails, so the broadcast path is never blocked. |
| T044 | Implement notification read/unread endpoints | Notifications | Implement `GET /api/v1/notifications` (returns paginated unread + read notifications for the authenticated user, cursor-based, newest first) and `PATCH /api/v1/notifications/{id}/read` (marks a notification as read; sets `is_read = true` and `read_at = NOW()`). Add `PATCH /api/v1/notifications/read-all` for bulk mark-as-read. | `src/FreshFlow.API/Controllers/AdminController.cs`, `src/Modules/Notifications/FreshFlow.Notifications.Application/Queries/GetNotificationsQuery.cs`, `src/Modules/Notifications/FreshFlow.Notifications.Application/Commands/MarkNotificationReadCommand.cs`, `src/Modules/Notifications/FreshFlow.Notifications.Application/DTOs/NotificationDto.cs`, `src/Modules/Notifications/FreshFlow.Notifications.Application/Services/NotificationService.cs` | T043 | S | 1. `GET /notifications` returns the authenticated user's notifications sorted by `createdAt DESC`, paginated with cursor. 2. `PATCH /notifications/{id}/read` sets `is_read = true` and `read_at`; a subsequent `GET /notifications` reflects the update. 3. A user can only read/mark their own notifications; accessing another user's notification ID returns HTTP 403. |

---

### Group I — Angular Frontend Web (T045–T047)

| Task ID | Title | Domain/Module | Description | Files to Create/Modify | Dependencies | Complexity | Acceptance Criteria |
|---------|-------|---------------|-------------|------------------------|--------------|------------|---------------------|
| T045 | Scaffold Angular app with feature modules + auth guard + JWT interceptor | Frontend-Web | Scaffold `freshflow-web/` Angular app with strict mode, standalone components, and lazy-loaded feature routes (Auth, Pricing, Orders, Logistics, Hub, Analytics, Admin). Implement `AuthGuard` (redirects unauthenticated users to `/login`), `RoleGuard` (enforces role-based route access), and `AuthInterceptor` (attaches `Authorization: Bearer` header and automatically calls refresh token before expiry). Implement `AuthService` and `SignalrService` in core. | `freshflow-web/src/app/app.routes.ts`, `freshflow-web/src/app/core/guards/auth.guard.ts`, `freshflow-web/src/app/core/interceptors/auth.interceptor.ts`, `freshflow-web/src/app/core/services/auth.service.ts`, `freshflow-web/src/app/core/services/signalr.service.ts`, `freshflow-web/src/environments/environment.ts`, `freshflow-web/src/environments/environment.prod.ts` | T009, T010 | L | 1. `ng build --configuration production` completes without errors. 2. A user without a token navigating to `/pricing` is redirected to `/login`; after login, they are redirected back to `/pricing`. 3. The `AuthInterceptor` automatically refreshes the access token before it expires and retries the original request transparently. |
| T046 | Implement pricing dashboard with SignalR live price feed | Frontend-Web | Implement the `PricingDashboardComponent` (in `features/pricing/`) displaying a live price board for a selected market. On load, call `GET /markets/{id}/products`. Subscribe to `PriceUpdated` and `SignificantPriceAlert` SignalR events via `SignalrService`. On event receipt, update the relevant row in the component's price table reactively using the `async` pipe and `OnPush` change detection. Highlight rows with significant price changes. | `freshflow-web/src/app/features/pricing/pricing-dashboard.component.ts`, `freshflow-web/src/app/features/pricing/pricing-dashboard.component.html`, `freshflow-web/src/app/features/pricing/pricing.service.ts` | T045, T017 | M | 1. The pricing dashboard displays real-time price updates without page refresh when a Kiosk Staff updates a price. 2. A significant price change highlights the affected row with a visual indicator within 500 ms of the event receipt. 3. All components use `OnPush` change detection and the `async` pipe; no manual `subscribe` calls in component code. |
| T047 | Implement order management UI | Frontend-Web | Implement `OrderListComponent`, `OrderDetailComponent`, and `CreateOrderComponent` in `features/orders/`. `CreateOrderComponent` allows Restaurant users to browse available products by market, add line items, and submit `POST /orders`. `OrderListComponent` shows paginated order history with status filter. Order status updates from `OrderStatusChanged` SignalR events are reflected live in `OrderListComponent`. | `freshflow-web/src/app/features/orders/order-list.component.ts`, `freshflow-web/src/app/features/orders/order-detail.component.ts`, `freshflow-web/src/app/features/orders/create-order.component.ts`, `freshflow-web/src/app/features/orders/orders.service.ts` | T045, T025 | L | 1. A Restaurant user can create an order with at least one line item; the new order appears in `OrderListComponent` immediately after creation. 2. When an Admin updates an order's status, the `OrderListComponent` updates the status row in real time via SignalR without a page refresh. 3. Attempting to cancel a `delivered` order shows an error message returned from the API. |

---

### Group J — React Native Mobile (T048–T050)

| Task ID | Title | Domain/Module | Description | Files to Create/Modify | Dependencies | Complexity | Acceptance Criteria |
|---------|-------|---------------|-------------|------------------------|--------------|------------|---------------------|
| T048 | Scaffold React Native app with navigation and auth flow | Frontend-Mobile | Scaffold `freshflow-mobile/` React Native app (TypeScript strict mode, Expo or bare React Native). Set up React Navigation with `AppNavigator`, `KioskNavigator`, and `RestaurantNavigator`. Implement `LoginScreen`. Implement `auth.service.ts` wrapping `api.service.ts` for login, refresh, and logout. Store tokens in secure storage (`expo-secure-store` or `react-native-keychain`). Implement automatic token refresh before expiry. Set up Zustand or Redux Toolkit for global auth state. | `freshflow-mobile/src/screens/auth/LoginScreen.tsx`, `freshflow-mobile/src/navigation/AppNavigator.tsx`, `freshflow-mobile/src/navigation/KioskNavigator.tsx`, `freshflow-mobile/src/navigation/RestaurantNavigator.tsx`, `freshflow-mobile/src/services/api.service.ts`, `freshflow-mobile/src/services/auth.service.ts`, `freshflow-mobile/src/store/authStore.ts` | T009 | L | 1. The app builds without TypeScript errors (`tsc --noEmit` exits 0). 2. A user can log in with valid credentials; role-based navigation routes Kiosk Staff to `KioskNavigator` and Restaurant users to `RestaurantNavigator`. 3. Tokens are persisted in secure storage and survive app restart; the user is not forced to re-login. |
| T049 | Implement kiosk price update screen with SignalR connection | Frontend-Mobile | Implement `ProductListScreen` (lists products for the Kiosk Staff's assigned market, reads current price/quantity from `GET /markets/{id}/products`) and `PriceUpdateScreen` (allows updating price or quantity via `PATCH`, with no more than 4 taps from the product list view). Connect to `PricingHub` via `signalr.service.ts` on login; implement exponential backoff reconnection. | `freshflow-mobile/src/screens/kiosk/ProductListScreen.tsx`, `freshflow-mobile/src/screens/kiosk/PriceUpdateScreen.tsx`, `freshflow-mobile/src/services/signalr.service.ts` | T048, T016, T017 | M | 1. A Kiosk Staff can update a product price in 4 taps or fewer from the product list screen. 2. After a successful price update, the `ProductListScreen` reflects the new price immediately (optimistic update or SignalR confirmation). 3. The SignalR connection reconnects automatically with exponential backoff (initial 1 s, max 30 s) after a network disruption. |
| T050 | Implement restaurant mobile order view with real-time status updates | Frontend-Mobile | Implement `PricingDashboardScreen` (displays live price board via `GET /markets/{id}/products`, subscribes to `PriceUpdated` events), `OrderListScreen` (paginated order history, filterable by status), and `OrderDetailScreen` (shows order details and live status). Subscribe to `OrderStatusChanged` events via `signalr.service.ts`; update order status in real time without polling. | `freshflow-mobile/src/screens/restaurant/PricingDashboardScreen.tsx`, `freshflow-mobile/src/screens/restaurant/OrderListScreen.tsx`, `freshflow-mobile/src/screens/restaurant/OrderDetailScreen.tsx` | T048, T025, T023 | M | 1. `OrderDetailScreen` updates the displayed order status in real time when an Admin transitions the order status, without requiring a manual refresh. 2. `PricingDashboardScreen` displays price updates within 500 ms of a Kiosk Staff update via SignalR. 3. All API calls go through `api.service.ts`; no direct `fetch` calls in screen components. |

---

## 2. Implementation Order (Critical Path)

### 2.1 Critical Path (Tasks That Block Others)

The following sequence represents the minimum ordered chain of tasks where each task is a prerequisite for the next. Delay on any of these tasks cascades to downstream work.

```
T001 → T002 → T003 → T004 → T009 → T010 → T013 → T015 → T016 → T017 → T022 → T025 → T030 → T031 → T032
```

| Step | Task | Blocks | Why It Is a Blocker |
|------|------|--------|---------------------|
| 1 | **T001** — Solution structure | All tasks | Every other task depends on the project existing |
| 2 | **T002** — PostgreSQL + EF Core | T009, T013, T020, T028, T034 | No database = no persistence anywhere |
| 3 | **T003** — Redis infrastructure | T015, T016, T022, T031, T033 | Cache and soft-reservation layer required |
| 4 | **T004** — JWT middleware | T009, T012 | Auth endpoints cannot function without token validation |
| 5 | **T009** — Login endpoint | T010, T011, T012, T021, T022 | All protected endpoints require login to be working |
| 6 | **T010** — Refresh token | T045, T048 | Frontend auth flows depend on token rotation |
| 7 | **T013** — Price snapshots migration | T014, T015, T016, T018, T019 | Pricing tables must exist before any pricing logic |
| 8 | **T015** — Market products listing | T016, T022 | Price update and order creation read current prices |
| 9 | **T016** — Price update endpoint | T017, T019 | Broadcast and significant change detection trigger here |
| 10 | **T017** — PricingHub broadcast | T019, T046, T049 | SignalR price feed is the core real-time value proposition |
| 11 | **T022** — Bulk order creation | T023, T024, T025, T027 | Orders must exist before status, cancel, or scheduled work |
| 12 | **T025** — Order status + OrderHub | T026, T047, T050 | Order status transitions and broadcast are required for UX |
| 13 | **T030** — VRP route engine | T031 | Route calculation must be built before persistence/assignment |
| 14 | **T031** — Route creation endpoints | T032, T033, T038 | Routes must exist before delivery tracking or hub redistribution |
| 15 | **T032** — Delivery status + DeliveryHub | T041, T043, T050 | Delivery events feed into analytics and notifications |

**Absolute blockers** (delay cascades to everything downstream):

- **T001** — Nothing can start without the solution.
- **T002** — No persistence without EF Core + PostgreSQL.
- **T004** — No authentication without JWT middleware.
- **T009** — No end-to-end user flow without login.

---

### 2.2 Parallelization Table

Tasks within the same phase share the same dependency prerequisites and can be worked on simultaneously by different developers.

| Phase | Tasks (Parallel) | Prerequisite Complete | Duration Estimate |
|-------|------------------|-----------------------|-------------------|
| **Phase 0 — Foundation** | T001 | — | 1 day |
| **Phase 1 — Core Infrastructure** | T002, T005, T008 | T001 | 2 days |
| **Phase 2 — Auth Infrastructure** | T003, T004, T006, T007 | T001, T002 | 2 days |
| **Phase 3 — Auth Endpoints** | T009, T010, T011, T012 | T002, T004 | 2 days |
| **Phase 4 — Schema Migrations** | T013, T020, T028, T034 | T002 | 1 day |
| **Phase 5 — Pricing Catalog + Listing** | T014, T015 | T013, T003 | 2 days |
| **Phase 6 — Price Updates + Real-Time** | T016, T017 | T015, T004, T007 | 3 days |
| **Phase 7 — Pricing Enhancements** | T018, T019 | T016, T017 | 2 days |
| **Phase 8 — Orders Foundation** | T021, T022 | T020, T015, T009 | 3 days |
| **Phase 9 — Orders Listing + Cancel** | T023, T024 | T022 | 2 days |
| **Phase 10 — Orders Advanced** | T025, T026, T027 | T023, T007 | 4 days |
| **Phase 11 — Logistics + Hub** | T029, T030, T035 | T028, T034 | 3 days |
| **Phase 12 — Routes + Hub Ops** | T031, T036, T037 | T030, T035, T026 | 3 days |
| **Phase 13 — Real-Time Delivery + Hub Inventory** | T032, T033, T038 | T031, T037 | 3 days |
| **Phase 14 — Analytics + Notifications** | T039, T040, T041, T042, T043, T044 | T032, T018, T023 | 5 days |
| **Phase 15 — Frontend** | T045, T046, T047, T048, T049, T050 | T017, T025, T032, T009 | 10 days |

---

## 3. MVP Scope

**MVP Definition:** The minimum set of working features needed to demonstrate end-to-end value to a real user: a Kiosk Staff member can update a market price in real time, a Restaurant user can see the live price board update without refresh, place a bulk order, and track its status — all authenticated and secured.

**MVP Scope:** T001–T017 + T020–T025

| Task | MVP? | Post-MVP Phase | Reason for Deferral |
|------|------|----------------|---------------------|
| T001 — Solution structure | **MVP** | — | Foundation |
| T002 — PostgreSQL + EF Core | **MVP** | — | Foundation |
| T003 — Redis infrastructure | **MVP** | — | Required for caching and soft-reservation |
| T004 — JWT middleware | **MVP** | — | Required for all authentication |
| T005 — Docker Compose | **MVP** | — | Required to run the stack locally |
| T006 — CI/CD pipeline | **MVP** | — | Required for quality gates |
| T007 — SignalR + Redis backplane | **MVP** | — | Required for real-time price feed |
| T008 — Logging + health checks | **MVP** | — | Required for observability |
| T009 — Login endpoint | **MVP** | — | Core auth flow |
| T010 — Refresh token rotation | **MVP** | — | Core auth flow |
| T011 — Logout endpoint | **MVP** | — | Core auth flow |
| T012 — Admin user registration | **MVP** | — | Required to create users |
| T013 — Price snapshots migration | **MVP** | — | Required for pricing |
| T014 — Product catalog endpoints | **MVP** | — | Required to browse products |
| T015 — Market products listing | **MVP** | — | Required for restaurant price view |
| T016 — Price update endpoint | **MVP** | — | Core kiosk value |
| T017 — PricingHub broadcast | **MVP** | — | Core real-time value proposition |
| T018 — Price history endpoint | Post-MVP | Phase 1 | Useful but not required for first demo |
| T019 — Significant price change | Post-MVP | Phase 1 | Enhancement to core pricing |
| T020 — Orders DB migration | **MVP** | — | Required for order creation |
| T021 — Restaurant profile | **MVP** | — | Required before order placement |
| T022 — Bulk order creation | **MVP** | — | Core restaurant value |
| T023 — Order listing + detail | **MVP** | — | Required to view placed orders |
| T024 — Order cancellation | **MVP** | — | Core order lifecycle |
| T025 — Order status + OrderHub | **MVP** | — | Core real-time order tracking |
| T026 — Order grouping | Post-MVP | Phase 1 | Logistics optimization, not core flow |
| T027 — Scheduled orders | Post-MVP | Phase 1 | Convenience feature, not core |
| T028 — Logistics DB migration | Post-MVP | Phase 1 | Logistics not in MVP scope |
| T029 — Vehicle CRUD | Post-MVP | Phase 1 | Logistics not in MVP scope |
| T030 — VRP route engine | Post-MVP | Phase 1 | Logistics not in MVP scope |
| T031 — Route creation endpoints | Post-MVP | Phase 1 | Logistics not in MVP scope |
| T032 — Delivery status tracking | Post-MVP | Phase 1 | Logistics not in MVP scope |
| T033 — Route caching | Post-MVP | Phase 1 | Optimization for logistics |
| T034 — Hub DB migration | Post-MVP | Phase 2 | Hub management is post-logistics |
| T035 — Hub CRUD | Post-MVP | Phase 2 | Hub management is post-logistics |
| T036 — Inbound goods recording | Post-MVP | Phase 2 | Hub management is post-logistics |
| T037 — Outbound goods recording | Post-MVP | Phase 2 | Hub management is post-logistics |
| T038 — Hub inventory + redistribution | Post-MVP | Phase 2 | Advanced hub feature |
| T039 — Price trend analytics | Post-MVP | Phase 2 | Analytics requires data volume to be meaningful |
| T040 — Demand heatmap | Post-MVP | Phase 2 | Analytics requires data volume |
| T041 — Delivery performance KPIs | Post-MVP | Phase 2 | Requires logistics data (Phase 1) |
| T042 — Async CSV export | Post-MVP | Phase 2 | Nice-to-have reporting feature |
| T043 — Notification persistence | Post-MVP | Phase 2 | Real-time events cover MVP; persistence is enhancement |
| T044 — Notification read/unread | Post-MVP | Phase 2 | Depends on persistence (T043) |
| T045 — Angular scaffold | Post-MVP | Phase 3 | MVP can be demonstrated via REST client |
| T046 — Pricing dashboard UI | Post-MVP | Phase 3 | MVP backend is sufficient for demo |
| T047 — Order management UI | Post-MVP | Phase 3 | MVP backend is sufficient for demo |
| T048 — React Native scaffold | Post-MVP | Phase 3 | MVP backend is sufficient for demo |
| T049 — Kiosk price update screen | Post-MVP | Phase 3 | Mobile UI is a Phase 3 deliverable |
| T050 — Restaurant mobile order view | Post-MVP | Phase 3 | Mobile UI is a Phase 3 deliverable |

---

## 4. Folder Structure

### 4a. ASP.NET Core Backend — Clean Architecture per Module (Microservice-Ready)

> **Quy tắc dependency (được enforce bởi .csproj references):**
> - `.Domain` → `SharedKernel` only
> - `.Application` → `.Domain` + `SharedKernel` + `Contracts`
> - `.Infrastructure` → `.Application` + EF Core/Redis packages
> - `FreshFlow.API` (host) → tất cả `.Infrastructure` projects (chỉ để DI registration)
>
> **Không module nào được reference Domain/Application của module khác.** Cross-module communication chỉ qua `FreshFlow.Contracts` (MediatR `INotification`).

```
FreshFlow.sln
│
├── src/
│   │
│   ├── Shared/
│   │   ├── FreshFlow.SharedKernel/               # Primitives dùng chung
│   │   │   ├── Domain/
│   │   │   │   ├── BaseEntity.cs                 # Id + DomainEvents collection
│   │   │   │   ├── AggregateRoot.cs
│   │   │   │   ├── ValueObject.cs
│   │   │   │   └── IDomainEvent.cs               # marker interface
│   │   │   ├── Application/
│   │   │   │   ├── Result.cs                     # Result<TValue>, Result<TValue, TError>
│   │   │   │   ├── Error.cs                      # Error record (Code, Description)
│   │   │   │   ├── ICommand.cs                   # ICommand<TResponse> : IRequest<TResponse>
│   │   │   │   └── IQuery.cs                     # IQuery<TResponse> : IRequest<TResponse>
│   │   │   └── FreshFlow.SharedKernel.csproj
│   │   │
│   │   └── FreshFlow.Contracts/                  # Integration events (cross-module DTOs)
│   │       ├── Auth/
│   │       │   └── UserCreatedIntegrationEvent.cs
│   │       ├── Pricing/
│   │       │   └── PriceUpdatedIntegrationEvent.cs
│   │       ├── Orders/
│   │       │   ├── OrderCreatedIntegrationEvent.cs
│   │       │   └── OrderStatusChangedIntegrationEvent.cs
│   │       ├── Logistics/
│   │       │   └── DeliveryStatusChangedIntegrationEvent.cs
│   │       └── FreshFlow.Contracts.csproj        # refs: SharedKernel only
│   │
│   ├── Modules/
│   │   │
│   │   ├── Auth/
│   │   │   ├── FreshFlow.Auth.Domain/
│   │   │   │   ├── Entities/
│   │   │   │   │   ├── User.cs
│   │   │   │   │   └── RefreshToken.cs
│   │   │   │   ├── Enums/
│   │   │   │   │   └── UserRole.cs
│   │   │   │   ├── Events/
│   │   │   │   │   └── UserCreatedDomainEvent.cs
│   │   │   │   ├── Repositories/
│   │   │   │   │   ├── IUserRepository.cs
│   │   │   │   │   └── IRefreshTokenRepository.cs
│   │   │   │   └── FreshFlow.Auth.Domain.csproj  # refs: SharedKernel
│   │   │   │
│   │   │   ├── FreshFlow.Auth.Application/
│   │   │   │   ├── Commands/
│   │   │   │   │   ├── Login/
│   │   │   │   │   │   ├── LoginCommand.cs
│   │   │   │   │   │   ├── LoginCommandHandler.cs
│   │   │   │   │   │   └── LoginCommandValidator.cs
│   │   │   │   │   ├── RefreshToken/
│   │   │   │   │   │   ├── RefreshTokenCommand.cs
│   │   │   │   │   │   └── RefreshTokenCommandHandler.cs
│   │   │   │   │   ├── Logout/
│   │   │   │   │   │   ├── LogoutCommand.cs
│   │   │   │   │   │   └── LogoutCommandHandler.cs
│   │   │   │   │   └── RegisterUser/
│   │   │   │   │       ├── RegisterUserCommand.cs
│   │   │   │   │       ├── RegisterUserCommandHandler.cs
│   │   │   │   │       └── RegisterUserCommandValidator.cs
│   │   │   │   ├── DTOs/
│   │   │   │   │   ├── LoginRequest.cs
│   │   │   │   │   ├── LoginResponse.cs
│   │   │   │   │   └── TokenPair.cs
│   │   │   │   ├── Abstractions/
│   │   │   │   │   ├── ITokenService.cs
│   │   │   │   │   └── IPasswordService.cs
│   │   │   │   ├── EventHandlers/
│   │   │   │   │   └── UserCreatedDomainEventHandler.cs  # publishes UserCreatedIntegrationEvent
│   │   │   │   └── FreshFlow.Auth.Application.csproj    # refs: Domain, SharedKernel, Contracts
│   │   │   │
│   │   │   └── FreshFlow.Auth.Infrastructure/
│   │   │       ├── Persistence/
│   │   │       │   ├── Configurations/
│   │   │       │   │   ├── UserConfiguration.cs
│   │   │       │   │   └── RefreshTokenConfiguration.cs
│   │   │       │   └── Repositories/
│   │   │       │       ├── UserRepository.cs
│   │   │       │       └── RefreshTokenRepository.cs
│   │   │       ├── Services/
│   │   │       │   ├── JwtTokenService.cs
│   │   │       │   └── BcryptPasswordService.cs
│   │   │       ├── DependencyInjection.cs        # AddAuthModule(services, config)
│   │   │       └── FreshFlow.Auth.Infrastructure.csproj
│   │   │
│   │   ├── Pricing/
│   │   │   ├── FreshFlow.Pricing.Domain/
│   │   │   │   ├── Entities/
│   │   │   │   │   ├── Market.cs
│   │   │   │   │   ├── Product.cs
│   │   │   │   │   ├── MarketProduct.cs          # Aggregate root (price + qty per market)
│   │   │   │   │   └── PriceSnapshot.cs          # append-only value record
│   │   │   │   ├── Events/
│   │   │   │   │   ├── PriceUpdatedDomainEvent.cs
│   │   │   │   │   └── SignificantPriceChangeDomainEvent.cs
│   │   │   │   ├── Repositories/
│   │   │   │   │   ├── IMarketRepository.cs
│   │   │   │   │   ├── IProductRepository.cs
│   │   │   │   │   ├── IMarketProductRepository.cs
│   │   │   │   │   └── IPriceSnapshotRepository.cs
│   │   │   │   └── FreshFlow.Pricing.Domain.csproj
│   │   │   │
│   │   │   ├── FreshFlow.Pricing.Application/
│   │   │   │   ├── Commands/
│   │   │   │   │   ├── UpdatePrice/
│   │   │   │   │   │   ├── UpdatePriceCommand.cs
│   │   │   │   │   │   ├── UpdatePriceCommandHandler.cs
│   │   │   │   │   │   └── UpdatePriceCommandValidator.cs
│   │   │   │   │   └── CreateProduct/
│   │   │   │   │       ├── CreateProductCommand.cs
│   │   │   │   │       ├── CreateProductCommandHandler.cs
│   │   │   │   │       └── CreateProductCommandValidator.cs
│   │   │   │   ├── Queries/
│   │   │   │   │   ├── GetMarketProducts/
│   │   │   │   │   │   ├── GetMarketProductsQuery.cs
│   │   │   │   │   │   └── GetMarketProductsQueryHandler.cs
│   │   │   │   │   └── GetPriceHistory/
│   │   │   │   │       ├── GetPriceHistoryQuery.cs
│   │   │   │   │       └── GetPriceHistoryQueryHandler.cs
│   │   │   │   ├── DTOs/
│   │   │   │   │   ├── MarketProductDto.cs
│   │   │   │   │   ├── PriceUpdateRequest.cs
│   │   │   │   │   ├── PriceUpdateResponse.cs
│   │   │   │   │   └── PriceSnapshotDto.cs
│   │   │   │   ├── Abstractions/
│   │   │   │   │   ├── IPricingCacheWriter.cs
│   │   │   │   │   └── IPricingBroadcastService.cs
│   │   │   │   ├── EventHandlers/
│   │   │   │   │   └── PriceUpdatedDomainEventHandler.cs  # triggers broadcast + cache
│   │   │   │   ├── BackgroundJobs/
│   │   │   │   │   └── PartitionMaintenanceJob.cs
│   │   │   │   └── FreshFlow.Pricing.Application.csproj
│   │   │   │
│   │   │   └── FreshFlow.Pricing.Infrastructure/
│   │   │       ├── Persistence/
│   │   │       │   ├── Configurations/
│   │   │       │   │   ├── MarketConfiguration.cs
│   │   │       │   │   ├── ProductConfiguration.cs
│   │   │       │   │   ├── MarketProductConfiguration.cs
│   │   │       │   │   └── PriceSnapshotConfiguration.cs
│   │   │       │   └── Repositories/
│   │   │       │       ├── MarketRepository.cs
│   │   │       │       ├── ProductRepository.cs
│   │   │       │       ├── MarketProductRepository.cs
│   │   │       │       └── PriceSnapshotRepository.cs
│   │   │       ├── Caching/
│   │   │       │   ├── RedisPricingCacheWriter.cs
│   │   │       │   └── RedisKeys.cs
│   │   │       ├── Realtime/
│   │   │       │   └── SignalRPricingBroadcastService.cs
│   │   │       ├── DependencyInjection.cs        # AddPricingModule(services, config)
│   │   │       └── FreshFlow.Pricing.Infrastructure.csproj
│   │   │
│   │   ├── Orders/
│   │   │   ├── FreshFlow.Orders.Domain/
│   │   │   │   ├── Entities/
│   │   │   │   │   ├── Restaurant.cs
│   │   │   │   │   ├── Order.cs                  # Aggregate root
│   │   │   │   │   ├── OrderItem.cs              # child entity
│   │   │   │   │   ├── OrderGroup.cs
│   │   │   │   │   └── ScheduledOrder.cs
│   │   │   │   ├── Enums/
│   │   │   │   │   ├── OrderStatus.cs
│   │   │   │   │   └── OrderGroupStatus.cs
│   │   │   │   ├── Events/
│   │   │   │   │   ├── OrderCreatedDomainEvent.cs
│   │   │   │   │   ├── OrderStatusChangedDomainEvent.cs
│   │   │   │   │   └── OrderCancelledDomainEvent.cs
│   │   │   │   ├── Repositories/
│   │   │   │   │   ├── IOrderRepository.cs
│   │   │   │   │   ├── IOrderGroupRepository.cs
│   │   │   │   │   ├── IRestaurantRepository.cs
│   │   │   │   │   └── IScheduledOrderRepository.cs
│   │   │   │   └── FreshFlow.Orders.Domain.csproj
│   │   │   │
│   │   │   ├── FreshFlow.Orders.Application/
│   │   │   │   ├── Commands/
│   │   │   │   │   ├── CreateOrder/
│   │   │   │   │   │   ├── CreateOrderCommand.cs
│   │   │   │   │   │   ├── CreateOrderCommandHandler.cs
│   │   │   │   │   │   └── CreateOrderCommandValidator.cs
│   │   │   │   │   ├── CancelOrder/
│   │   │   │   │   ├── UpdateOrderStatus/
│   │   │   │   │   ├── CreateOrderGroup/
│   │   │   │   │   └── CreateScheduledOrder/
│   │   │   │   ├── Queries/
│   │   │   │   │   ├── GetOrder/
│   │   │   │   │   └── ListOrders/
│   │   │   │   ├── DTOs/
│   │   │   │   │   ├── CreateOrderRequest.cs
│   │   │   │   │   ├── OrderResponse.cs
│   │   │   │   │   └── OrderListItem.cs
│   │   │   │   ├── Abstractions/
│   │   │   │   │   ├── IStockReservationService.cs
│   │   │   │   │   └── IOrderBroadcastService.cs
│   │   │   │   ├── EventHandlers/
│   │   │   │   │   ├── OrderCreatedDomainEventHandler.cs    # publishes integration event
│   │   │   │   │   └── OrderStatusChangedDomainEventHandler.cs
│   │   │   │   ├── BackgroundJobs/
│   │   │   │   │   ├── ScheduledOrderJob.cs
│   │   │   │   │   └── ReservationExpiryJob.cs
│   │   │   │   └── FreshFlow.Orders.Application.csproj
│   │   │   │
│   │   │   └── FreshFlow.Orders.Infrastructure/
│   │   │       ├── Persistence/
│   │   │       │   ├── Configurations/
│   │   │       │   └── Repositories/
│   │   │       ├── Caching/
│   │   │       │   └── RedisStockReservationService.cs
│   │   │       ├── Realtime/
│   │   │       │   └── SignalROrderBroadcastService.cs
│   │   │       ├── DependencyInjection.cs        # AddOrdersModule(services, config)
│   │   │       └── FreshFlow.Orders.Infrastructure.csproj
│   │   │
│   │   ├── Logistics/
│   │   │   ├── FreshFlow.Logistics.Domain/
│   │   │   │   ├── Entities/
│   │   │   │   │   ├── Vehicle.cs
│   │   │   │   │   ├── DeliveryRoute.cs          # Aggregate root
│   │   │   │   │   └── Delivery.cs               # child entity (one stop)
│   │   │   │   ├── Enums/
│   │   │   │   │   ├── RouteStatus.cs
│   │   │   │   │   └── DeliveryStatus.cs
│   │   │   │   ├── Events/
│   │   │   │   │   └── DeliveryStatusChangedDomainEvent.cs
│   │   │   │   ├── Repositories/
│   │   │   │   │   ├── IVehicleRepository.cs
│   │   │   │   │   ├── IDeliveryRouteRepository.cs
│   │   │   │   │   └── IDeliveryRepository.cs
│   │   │   │   ├── Services/
│   │   │   │   │   └── IVrpSolver.cs             # domain service interface
│   │   │   │   └── FreshFlow.Logistics.Domain.csproj
│   │   │   │
│   │   │   ├── FreshFlow.Logistics.Application/
│   │   │   │   ├── Commands/
│   │   │   │   │   ├── CalculateRoute/
│   │   │   │   │   │   ├── CalculateRouteCommand.cs
│   │   │   │   │   │   └── CalculateRouteCommandHandler.cs
│   │   │   │   │   └── AssignVehicle/
│   │   │   │   ├── Queries/
│   │   │   │   │   ├── ListRoutes/
│   │   │   │   │   └── ListVehicles/
│   │   │   │   ├── DTOs/
│   │   │   │   │   ├── RouteCalculationRequest.cs
│   │   │   │   │   ├── RouteCalculationResponse.cs
│   │   │   │   │   └── VehicleDto.cs
│   │   │   │   ├── Abstractions/
│   │   │   │   │   ├── IRouteCache.cs
│   │   │   │   │   └── IDeliveryBroadcastService.cs
│   │   │   │   ├── EventHandlers/
│   │   │   │   │   └── OrderCreatedIntegrationEventHandler.cs  # handles cross-module event
│   │   │   │   └── FreshFlow.Logistics.Application.csproj
│   │   │   │
│   │   │   └── FreshFlow.Logistics.Infrastructure/
│   │   │       ├── Persistence/
│   │   │       │   ├── Configurations/
│   │   │       │   └── Repositories/
│   │   │       ├── Algorithms/
│   │   │       │   └── NearestNeighborVrpSolver.cs  # implements IVrpSolver
│   │   │       ├── Caching/
│   │   │       │   └── RedisRouteCache.cs
│   │   │       ├── Realtime/
│   │   │       │   └── SignalRDeliveryBroadcastService.cs
│   │   │       ├── DependencyInjection.cs        # AddLogisticsModule(services, config)
│   │   │       └── FreshFlow.Logistics.Infrastructure.csproj
│   │   │
│   │   ├── Hub/
│   │   │   ├── FreshFlow.Hub.Domain/
│   │   │   │   ├── Entities/
│   │   │   │   │   ├── DistributionHub.cs        # Aggregate root (named DistributionHub to avoid conflict with SignalR Hub)
│   │   │   │   │   ├── HubInventory.cs
│   │   │   │   │   ├── HubInboundEvent.cs
│   │   │   │   │   ├── HubOutboundEvent.cs
│   │   │   │   │   └── CrossDockTransfer.cs
│   │   │   │   ├── Repositories/
│   │   │   │   │   ├── IDistributionHubRepository.cs
│   │   │   │   │   ├── IHubInventoryRepository.cs
│   │   │   │   │   └── IHubEventRepository.cs
│   │   │   │   └── FreshFlow.Hub.Domain.csproj
│   │   │   │
│   │   │   ├── FreshFlow.Hub.Application/
│   │   │   │   ├── Commands/
│   │   │   │   │   ├── RecordInbound/
│   │   │   │   │   ├── RecordOutbound/
│   │   │   │   │   └── CreateHub/
│   │   │   │   ├── Queries/
│   │   │   │   │   ├── GetHubInventory/
│   │   │   │   │   └── GetRedistributionSuggestions/
│   │   │   │   ├── DTOs/
│   │   │   │   └── FreshFlow.Hub.Application.csproj
│   │   │   │
│   │   │   └── FreshFlow.Hub.Infrastructure/
│   │   │       ├── Persistence/
│   │   │       ├── DependencyInjection.cs        # AddHubModule(services, config)
│   │   │       └── FreshFlow.Hub.Infrastructure.csproj
│   │   │
│   │   ├── Analytics/
│   │   │   │   # Analytics là pure read-side (CQRS query only) — không có Domain layer
│   │   │   ├── FreshFlow.Analytics.Application/
│   │   │   │   ├── Queries/
│   │   │   │   │   ├── GetPriceTrend/
│   │   │   │   │   │   ├── GetPriceTrendQuery.cs
│   │   │   │   │   │   └── GetPriceTrendQueryHandler.cs
│   │   │   │   │   ├── GetDemandHeatmap/
│   │   │   │   │   └── GetDeliveryPerformance/
│   │   │   │   ├── Commands/
│   │   │   │   │   └── CreateExportJob/
│   │   │   │   ├── DTOs/
│   │   │   │   │   ├── PriceTrendDto.cs
│   │   │   │   │   ├── DemandHeatmapDto.cs
│   │   │   │   │   └── DeliveryPerformanceDto.cs
│   │   │   │   ├── Abstractions/
│   │   │   │   │   └── IAnalyticsCache.cs
│   │   │   │   └── FreshFlow.Analytics.Application.csproj  # refs: SharedKernel, Contracts
│   │   │   │
│   │   │   └── FreshFlow.Analytics.Infrastructure/
│   │   │       ├── Persistence/
│   │   │       │   ├── Configurations/
│   │   │       │   │   └── AnalyticsAggregationConfiguration.cs
│   │   │       │   └── Repositories/
│   │   │       │       └── AnalyticsQueryRepository.cs  # raw SQL + Dapper for complex reads
│   │   │       ├── Caching/
│   │   │       │   └── RedisAnalyticsCache.cs
│   │   │       ├── BackgroundJobs/
│   │   │       │   ├── AnalyticsAggregationJob.cs
│   │   │       │   └── ExportProcessorJob.cs
│   │   │       ├── DependencyInjection.cs        # AddAnalyticsModule(services, config)
│   │   │       └── FreshFlow.Analytics.Infrastructure.csproj
│   │   │
│   │   └── Notifications/
│   │       ├── FreshFlow.Notifications.Domain/
│   │       │   ├── Entities/
│   │       │   │   └── Notification.cs
│   │       │   ├── Enums/
│   │       │   │   └── NotificationType.cs
│   │       │   ├── Repositories/
│   │       │   │   └── INotificationRepository.cs
│   │       │   └── FreshFlow.Notifications.Domain.csproj
│   │       │
│   │       ├── FreshFlow.Notifications.Application/
│   │       │   ├── Commands/
│   │       │   │   └── MarkNotificationRead/
│   │       │   ├── Queries/
│   │       │   │   └── GetNotifications/
│   │       │   ├── EventHandlers/
│   │       │   │   ├── PriceUpdatedIntegrationEventHandler.cs   # persists notification
│   │       │   │   ├── OrderCreatedIntegrationEventHandler.cs
│   │       │   │   └── OrderStatusChangedIntegrationEventHandler.cs
│   │       │   ├── Abstractions/
│   │       │   │   └── INotificationPushService.cs
│   │       │   └── FreshFlow.Notifications.Application.csproj
│   │       │
│   │       └── FreshFlow.Notifications.Infrastructure/
│   │           ├── Persistence/
│   │           │   ├── Configurations/
│   │           │   └── Repositories/
│   │           │       └── NotificationRepository.cs
│   │           ├── Realtime/
│   │           │   └── SignalRNotificationPushService.cs
│   │           ├── DependencyInjection.cs        # AddNotificationsModule(services, config)
│   │           └── FreshFlow.Notifications.Infrastructure.csproj
│   │
│   ├── FreshFlow.Infrastructure.Persistence/     # Shared: AppDbContext + Migrations
│   │   ├── AppDbContext.cs                       # Scans IEntityTypeConfiguration via reflection
│   │   ├── Migrations/
│   │   │   ├── 20260101_000001_InitialSchema.cs
│   │   │   ├── 20260101_000002_SeedData.cs
│   │   │   ├── 20260101_000003_PriceSnapshotsPartitions.cs
│   │   │   ├── 20260101_000004_OrdersTables.cs
│   │   │   ├── 20260101_000005_LogisticsTables.cs
│   │   │   ├── 20260101_000006_HubTables.cs
│   │   │   └── 20260101_000007_NotificationsTables.cs
│   │   └── FreshFlow.Infrastructure.Persistence.csproj  # refs: all Module.Infrastructure projects
│   │
│   └── FreshFlow.API/                            # Host — wires all modules
│       ├── Controllers/
│       │   ├── Auth/
│       │   │   └── AuthController.cs
│       │   ├── Pricing/
│       │   │   ├── ProductsController.cs
│       │   │   └── MarketPricingController.cs
│       │   ├── Orders/
│       │   │   ├── OrdersController.cs
│       │   │   └── OrderGroupsController.cs
│       │   ├── Logistics/
│       │   │   ├── RoutesController.cs
│       │   │   └── VehiclesController.cs
│       │   ├── Hubs_Management/
│       │   │   └── HubsController.cs
│       │   ├── Analytics/
│       │   │   └── AnalyticsController.cs
│       │   └── Admin/
│       │       └── AdminController.cs
│       ├── SignalR/
│       │   ├── PricingHub.cs
│       │   ├── OrderHub.cs
│       │   └── DeliveryHub.cs
│       ├── Middleware/
│       │   ├── ExceptionHandlingMiddleware.cs
│       │   └── RateLimitingMiddleware.cs
│       ├── Program.cs                            # Calls AddAuthModule() + AddPricingModule() + ...
│       ├── Dockerfile
│       └── appsettings.json
│
└── tests/
    ├── Unit/
    │   ├── FreshFlow.Auth.UnitTests/
    │   │   ├── Commands/
    │   │   │   └── LoginCommandHandlerTests.cs
    │   │   └── Validators/
    │   │       └── LoginCommandValidatorTests.cs
    │   ├── FreshFlow.Pricing.UnitTests/
    │   │   ├── Commands/
    │   │   │   └── UpdatePriceCommandHandlerTests.cs
    │   │   └── Validators/
    │   │       └── UpdatePriceCommandValidatorTests.cs
    │   ├── FreshFlow.Orders.UnitTests/
    │   │   ├── Commands/
    │   │   └── Domain/
    │   │       └── OrderAggregateTests.cs
    │   └── FreshFlow.Logistics.UnitTests/
    │       └── Algorithms/
    │           └── NearestNeighborVrpSolverTests.cs
    └── Integration/
        └── FreshFlow.IntegrationTests/
            ├── Auth/
            │   └── AuthEndpointsTests.cs
            ├── Pricing/
            │   └── PriceUpdateFlowTests.cs       # update → Redis cache → SignalR broadcast
            ├── Orders/
            │   └── OrderCreationFlowTests.cs     # create → reservation → status push
            └── Logistics/
                └── RouteCalculationTests.cs
```

**Program.cs — cách đăng ký modules:**

```csharp
// FreshFlow.API/Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddSharedInfrastructure(builder.Configuration)   // AppDbContext, Redis
    .AddAuthModule(builder.Configuration)
    .AddPricingModule(builder.Configuration)
    .AddOrdersModule(builder.Configuration)
    .AddLogisticsModule(builder.Configuration)
    .AddHubModule(builder.Configuration)
    .AddAnalyticsModule(builder.Configuration)
    .AddNotificationsModule(builder.Configuration)
    .AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(/* all Application assemblies */))
    .AddSignalR().AddStackExchangeRedis(/* connection string */);
```

**Khi extract một module thành microservice — chỉ cần 3 bước:**
1. Tạo solution mới, copy 3 projects `{Module}.Domain/Application/Infrastructure`
2. Thêm `Program.cs` mới, gọi `Add{Module}Module()`
3. Thay `INotificationHandler<IntegrationEvent>` bằng message bus consumer (RabbitMQ/ServiceBus)

---

### 4b. Angular Frontend Web

```
freshflow-web/
├── src/
│   ├── app/
│   │   ├── core/                                   # Singleton services, interceptors, guards
│   │   │   ├── interceptors/
│   │   │   │   └── auth.interceptor.ts
│   │   │   ├── guards/
│   │   │   │   ├── auth.guard.ts
│   │   │   │   └── role.guard.ts
│   │   │   └── services/
│   │   │       ├── auth.service.ts
│   │   │       └── signalr.service.ts
│   │   ├── shared/                                 # Shared components, pipes, directives
│   │   │   ├── components/
│   │   │   │   ├── loading-spinner.component.ts
│   │   │   │   └── error-message.component.ts
│   │   │   └── pipes/
│   │   │       └── vnd-currency.pipe.ts
│   │   ├── features/
│   │   │   ├── auth/                               # Login page (lazy-loaded)
│   │   │   │   ├── login.component.ts
│   │   │   │   └── login.component.html
│   │   │   ├── pricing/                            # Pricing dashboard (lazy-loaded)
│   │   │   │   ├── pricing-dashboard.component.ts
│   │   │   │   ├── pricing-dashboard.component.html
│   │   │   │   └── pricing.service.ts
│   │   │   ├── orders/                             # Order management (lazy-loaded)
│   │   │   │   ├── order-list.component.ts
│   │   │   │   ├── order-detail.component.ts
│   │   │   │   ├── create-order.component.ts
│   │   │   │   └── orders.service.ts
│   │   │   ├── logistics/                          # Route management (lazy-loaded)
│   │   │   │   ├── route-calculator.component.ts
│   │   │   │   └── logistics.service.ts
│   │   │   ├── hub/                                # Hub management (lazy-loaded)
│   │   │   │   ├── hub-dashboard.component.ts
│   │   │   │   └── hub.service.ts
│   │   │   ├── analytics/                          # Analytics dashboards (lazy-loaded)
│   │   │   │   ├── price-trends.component.ts
│   │   │   │   ├── demand-heatmap.component.ts
│   │   │   │   └── analytics.service.ts
│   │   │   └── admin/                              # Admin panel (lazy-loaded)
│   │   │       ├── user-management.component.ts
│   │   │       └── admin.service.ts
│   │   ├── app.routes.ts
│   │   └── app.component.ts
│   └── environments/
│       ├── environment.ts
│       └── environment.prod.ts
├── angular.json
├── tsconfig.json
└── package.json
```

---

### 4c. React Native Mobile App

```
freshflow-mobile/
├── src/
│   ├── screens/
│   │   ├── auth/
│   │   │   └── LoginScreen.tsx
│   │   ├── kiosk/
│   │   │   ├── PriceUpdateScreen.tsx
│   │   │   └── ProductListScreen.tsx
│   │   └── restaurant/
│   │       ├── PricingDashboardScreen.tsx
│   │       ├── OrderListScreen.tsx
│   │       └── OrderDetailScreen.tsx
│   ├── navigation/
│   │   ├── AppNavigator.tsx
│   │   ├── KioskNavigator.tsx
│   │   └── RestaurantNavigator.tsx
│   ├── services/
│   │   ├── api.service.ts
│   │   ├── auth.service.ts
│   │   └── signalr.service.ts
│   ├── store/                                      # Zustand or Redux Toolkit
│   │   └── authStore.ts
│   └── components/
│       └── shared/
│           ├── LoadingSpinner.tsx
│           └── ErrorBoundary.tsx
├── tsconfig.json
└── package.json
```

---

## 5. Coding Standards

### 5.1 C# / ASP.NET Core

**Naming:**
- PascalCase: classes, methods, properties, file names
- camelCase: local variables, method parameters
- Interfaces prefixed with `I` — e.g., `IOrderService`, `IUserRepository`
- Async methods suffixed with `Async` — e.g., `CreateOrderAsync`, `LoginAsync`

**Patterns:**
- Use `Result<T>` pattern for all service-layer return values. Services never throw business-rule exceptions; they return a typed `Result<T>` with `IsSuccess`, `Value`, and `Error` (an enum or error code string). Controllers inspect the result and map to HTTP status codes.
- No business logic in controllers. Controllers do: validate model state → call one service method → map result to HTTP response.
- Use FluentValidation for all incoming request DTOs. Register validators with `services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>()`.
- No raw SQL in business logic. Use EF Core LINQ exclusively. Exception: the Analytics module may use `FromSqlRaw` for complex aggregation queries, with parameterized inputs only (no string interpolation).
- Use `record` types for all DTOs (immutable, value equality by default).

**Example service signature:**
```csharp
// Good
public async Task<Result<OrderResponseDto>> CreateOrderAsync(
    CreateOrderCommand command,
    CancellationToken cancellationToken = default)

// Bad — throws exception for business rule violations
public async Task<OrderResponseDto> CreateOrderAsync(CreateOrderCommand command)
```

**Example controller pattern:**
```csharp
[HttpPost]
public async Task<IActionResult> CreateOrder(
    [FromBody] CreateOrderRequestDto request,
    CancellationToken cancellationToken)
{
    var command = new CreateOrderCommand(UserId, request);
    var result = await _orderService.CreateOrderAsync(command, cancellationToken);
    return result.IsSuccess
        ? CreatedAtAction(nameof(GetOrder), new { id = result.Value.Id }, ApiResponse.Ok(result.Value))
        : result.Error.ToActionResult();
}
```

### 5.2 Angular

- Strict mode enabled (`strict: true` in `tsconfig.json`)
- `OnPush` change detection for all components — no exceptions
- `async` pipe in all templates; no manual `.subscribe()` in component code
- Services in `core/` are provided in root (`providedIn: 'root'`) — singletons across the app
- Feature services are provided in their feature's route configuration
- No `any` type — use `unknown` and type guards where the type is genuinely unknown
- Use `inject()` function for dependency injection in standalone components

### 5.3 React Native

- Functional components with hooks only — no class components
- TypeScript strict mode (`strict: true`)
- All API calls go through `api.service.ts` — no `fetch` or `axios` calls directly in screen components
- State management via Zustand (preferred) or Redux Toolkit — no local state for server data
- Expo `SecureStore` or `react-native-keychain` for token storage — never `AsyncStorage` for sensitive data

### 5.4 Git Branching Strategy

| Branch | Purpose |
|--------|---------|
| `main` | Production-ready only. Direct pushes blocked. Merges via PR with CI passing. |
| `dev` | Integration branch. All feature branches merge here first. |
| `feature/T001-solution-structure` | Feature branches named by Task ID and short description. |
| `fix/T016-price-race-condition` | Bug fix branches named by Task ID and short description. |

### 5.5 Commit Message Format (Conventional Commits)

```
<type>(<scope>): <description> (<TaskID>)
```

| Type | When to Use |
|------|------------|
| `feat` | New feature or endpoint |
| `fix` | Bug fix |
| `chore` | Infrastructure, CI, config changes |
| `refactor` | Code restructuring without behavior change |
| `test` | Adding or updating tests only |
| `docs` | Documentation changes only |

**Examples:**
```
feat(auth): implement JWT refresh token rotation (T010)
fix(pricing): correct race condition in price update cache write (T016)
chore(infra): add Docker Compose healthchecks for postgres and redis (T005)
test(orders): add integration tests for order cancellation flow (T024)
refactor(logistics): extract VrpSolver into separate testable class (T030)
```

### 5.6 PR Review Checklist

Every pull request must satisfy the following before merge:

- [ ] Unit tests added or updated for all changed service-layer classes
- [ ] No secrets, credentials, or connection strings in code or commit history
- [ ] EF Core migration generated if any schema change was made
- [ ] API contract matches `docs/04-api-design.md` (field names, HTTP status codes, error codes)
- [ ] Endpoint role access is enforced via `[Authorize(Roles = "...")]` and tested in integration tests
- [ ] All FluentValidation validators have corresponding unit tests
- [ ] No `any` type in TypeScript files
- [ ] No `.subscribe()` in Angular component files (use `async` pipe)
- [ ] Docker Compose `docker compose up` still works after the change

---

## 6. Testing Strategy

### 6.1 Unit Tests (`tests/Unit/FreshFlow.{Module}.UnitTests/`)

**Target:** All command handlers and domain services in each module's `.Application` layer. Test business logic in complete isolation from infrastructure.

**Mandatory unit test coverage:**

| Class | Test File | Key Scenarios |
|-------|-----------|---------------|
| `AuthService` | `Auth/AuthServiceTests.cs` | Login success, invalid credentials, account inactive, refresh rotation, family invalidation on reuse |
| `PricingService` | `Pricing/PricingServiceTests.cs` | Price update success, market assignment violation, optimistic concurrency conflict, zero-price validation |
| `SignificantChangeDetector` | `Pricing/SignificantChangeDetectorTests.cs` | 5% threshold boundary (inclusive), MEDIUM vs HIGH severity classification, quantity-only change does not trigger |
| `OrderService` | `Orders/OrderServiceTests.cs` | Successful order creation, insufficient stock, invalid product, soft-reservation rollback on failure |
| `SoftReservationService` | `Orders/SoftReservationServiceTests.cs` | Reserve, release, expiry idempotency |
| `OrderStatusService` | `Orders/OrderStatusServiceTests.cs` | Valid transitions, invalid transitions, cancellation of in-transit order |
| `VrpSolver` | `Logistics/VrpSolverTests.cs` | 2-stop route, 3-stop with 2-opt improvement, stop limit enforcement, all three optimization criteria |
| `LoginRequestValidator` | `Auth/LoginRequestValidatorTests.cs` | Missing email, invalid email format, missing password |
| `CreateOrderValidator` | `Orders/CreateOrderValidatorTests.cs` | Empty line items, zero quantity, invalid productId format |
| `UpdatePriceValidator` | `Pricing/UpdatePriceValidatorTests.cs` | Zero price, negative price, negative quantity, non-integer quantity |

**Pattern:** Arrange-Act-Assert. Mock all infrastructure dependencies using `NSubstitute` or `Moq`. No database or Redis access in unit tests.

**File naming:** `{ClassName}Tests.cs`

**Coverage enforcement:** Minimum 70% line coverage enforced in CI via `dotnet-coverage` + `reportgenerator`. Pipeline fails if coverage drops below threshold.

**Example unit test structure:**
```csharp
public class AuthServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _tokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordService _passwordService = Substitute.For<IPasswordService>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_userRepository, _tokenRepository, _passwordService, _tokenService);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsSuccessWithTokens()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", Role = UserRole.Restaurant };
        _userRepository.FindByEmailAsync("test@example.com").Returns(user);
        _passwordService.Verify("password", user.PasswordHash).Returns(true);
        _tokenService.GenerateAccessToken(user).Returns("access-token");

        // Act
        var result = await _sut.LoginAsync(new LoginCommand("test@example.com", "password"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");
    }
}
```

---

### 6.2 Integration Tests (`FreshFlow.IntegrationTests`)

**Target:** All REST API endpoints via `WebApplicationFactory<Program>`. Tests run against real PostgreSQL and real Redis instances managed by Testcontainers.

**Mandatory integration test coverage:**

| Controller | Test File | Key Scenarios |
|------------|-----------|---------------|
| `AuthController` | `Auth/AuthIntegrationTests.cs` | Full login-refresh-logout cycle; token reuse triggers family invalidation; duplicate email registration |
| `PricingController` | `Pricing/PricingIntegrationTests.cs` | Price update persists to DB and Redis; SignalR broadcast verifiable via `HubConnection` in test; market assignment enforcement |
| `OrdersController` | `Orders/OrdersIntegrationTests.cs` | Order creation with soft-reservation; insufficient stock rejection; order cancellation releases reservation |
| `LogisticsController` | `Logistics/LogisticsIntegrationTests.cs` | Route calculation returns ordered stops; 21-stop limit enforced; vehicle assignment conflict detection |

**Test database strategy:**
- Each test class gets a fresh Testcontainers PostgreSQL instance (or a shared instance with transaction rollback per test).
- EF Core migrations are applied at test startup via `dbContext.Database.MigrateAsync()`.
- Seed data is applied from a shared `TestDataSeeder` helper.

**SignalR testing:** Use `HubConnection` client in integration tests with `WebApplicationFactory` to verify that price updates trigger `PriceUpdated` events on connected clients within a 2-second timeout window.

**File naming:** `{ControllerName}IntegrationTests.cs`

**Example integration test structure:**
```csharp
public class AuthIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithTokens()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@freshflow.vn",
            password = "SeedPassword123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
        body!.Data.AccessToken.Should().NotBeNullOrEmpty();
        body.Data.RefreshToken.Should().NotBeNullOrEmpty();
    }
}
```

---

### 6.3 E2E Tests (Playwright for Angular)

**Target:** Critical user journeys validated in a real browser against the full running stack.

**Mandatory E2E scenarios:**

| Scenario | Steps | Assertion |
|----------|-------|-----------|
| **Login flow — all 3 roles** | Navigate to `/login`, enter credentials for Admin / Kiosk Staff / Restaurant, submit | Each role is redirected to their role-appropriate dashboard; JWT is stored in session |
| **Kiosk price update → restaurant sees change** | Log in as Kiosk Staff; navigate to product list; update price; simultaneously log in as Restaurant in second browser context; open pricing dashboard | Restaurant pricing dashboard row updates within 2 seconds of the Kiosk Staff update without page refresh |
| **Restaurant creates order → admin sees new order** | Log in as Restaurant; create an order with 2 line items; confirm submission | Admin's order list (in second browser context) shows the new order; order status is `pending` |

**Test runner:** Playwright with TypeScript. Test files in `freshflow-web/e2e/`.

**Environment:** E2E tests run against a fully composed Docker stack (`docker compose -f docker-compose.test.yml up`). They are not run in the PR CI pipeline by default (too slow); they run on merge to `dev` and on release candidates.

---

*End of FreshFlow Implementation Plan v1.0*

*Prepared by: Implementation Planning Agent | Project: FFX Capstone 2026*
