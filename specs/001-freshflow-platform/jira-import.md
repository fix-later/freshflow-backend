# FreshFlow — Jira Import

**Project**: FreshFlow Platform  
**Total**: 59 stories | 6 sprints | 4 assignees  
**Story points**: S = 2 · M = 3–5 · L = 8  
**Assignees**: BE/DevOps · FE1-Web · FE2-Web · FE3-Mobile

> Copy từng story vào Jira. Mỗi story gồm: Summary, Description (AC), Story Points, Assignee, Sprint, Epic, Labels.

---

## Epic Map

| Epic | Covers |
|------|--------|
| `EPIC-INFRA` | Solution scaffold, Docker, CI/CD |
| `EPIC-AUTH` | JWT, login, refresh, logout, register |
| `EPIC-PRICING` | Price feed, SignalR real-time, market products |
| `EPIC-ORDERS` | Order lifecycle, recurring orders, order grouping |
| `EPIC-LOGISTICS` | VRP routing, vehicles, delivery tracking |
| `EPIC-HUB` | Distribution hubs, inbound/outbound, inventory |
| `EPIC-ANALYTICS` | Price trends, KPIs, heatmap, CSV export |
| `EPIC-NOTIFICATIONS` | Notification persistence, read endpoints |
| `EPIC-FRONTEND-WEB` | Angular (admin, restaurant, kiosk) |
| `EPIC-FRONTEND-MOBILE` | React Native (kiosk + restaurant) |
| `EPIC-DEPLOY` | Testing polish, Nginx TLS, CI coverage gate |

---

## Sprint 1 — Foundation & Auth

### FFX-001
**Summary**: Scaffold .NET 10 solution with Clean Architecture  
**Epic**: EPIC-INFRA  
**Assignee**: BE/DevOps  
**Story Points**: 8  
**Sprint**: Sprint 1  
**Labels**: scaffold, blocking  
**Status**: ✅ Done

**Description**:
Tạo `FreshFlow.slnx` với toàn bộ 26 project theo Clean Architecture.

**Acceptance Criteria**:
1. `dotnet build FreshFlow.slnx` exits 0, không warnings hay errors.
2. Thêm reference từ Domain project sang module khác gây build error (enforced bằng .csproj).
3. API docs accessible tại `/scalar/v1` trong Development environment.
4. `dotnet test FreshFlow.slnx` exits 0 (empty test projects không produce failures).

**Depends on**: —

---

### FFX-002
**Summary**: Set up PostgreSQL + EF Core + Auth schema migration  
**Epic**: EPIC-INFRA  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 1  
**Labels**: database, ef-core  

**Description**:
Cài EF Core 10 + Npgsql. Cấu hình `AppDbContext` với `ApplyConfigurationsFromAssembly()`. Tạo migration đầu tiên cho Auth schema.

**Tables**: `users`, `refresh_tokens`, `user_market_assignments`  
**Enums**: `user_role`, `order_status`, `order_group_status`, `delivery_status`, `route_status`, `notification_type`

**Acceptance Criteria**:
1. `dotnet ef database update` tạo đúng 3 tables với FK constraints và indexes khớp `docs/03-database-schema.md`.
2. `dotnet run` với PostgreSQL healthy thì migrations tự apply trước `app.Run()`.
3. Constraint `users_email_unique` tồn tại; insert duplicate email raise violation.

**Depends on**: FFX-001

---

### FFX-003
**Summary**: Set up Redis connection và RedisCacheService  
**Epic**: EPIC-INFRA  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 1  
**Labels**: redis, caching  

**Description**:
Implement `RedisCacheService` implement `ICacheService`. Graceful degrade khi Redis down (return `null`, log Warning, không throw exception).

**Key patterns** (định nghĩa trong `RedisKeys.cs`):
- `price:{marketId}:{productId}`
- `reservation:{marketId}:{productId}`
- `route:{hash}`
- `analytics:{type}:{date}`

**Acceptance Criteria**:
1. `SetAsync("test", "value", 1min)` lưu vào Redis; `GetAsync<string>("test")` trả về `"value"`.
2. Khi Redis unreachable, `GetAsync` trả về `null`, log Warning — không throw exception.
3. `RedisKeys` class định nghĩa constants cho tất cả key patterns.

**Depends on**: FFX-001

---

### FFX-004
**Summary**: Set up Docker Compose (nginx + api + postgres + redis)  
**Epic**: EPIC-INFRA  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 1  
**Labels**: docker, devops  

**Description**:
Tạo `docker-compose.yml` với 4 services: nginx (port 80/443), api (8080 internal), postgres, redis. Multi-stage Dockerfile cho API. `docker-compose.override.yml` expose ports 5432, 6379 cho local dev.

**Files**: `docker-compose.yml`, `docker-compose.override.yml`, `nginx/nginx.conf`, `src/FreshFlow.API/Dockerfile`, `.env.example`

**Acceptance Criteria**:
1. `docker compose up -d` → tất cả 4 containers healthy trong 60 giây.
2. `curl http://localhost/health` trả về `{"status":"Healthy"}` qua Nginx port 80.
3. `api` container không start cho đến khi `postgres` và `redis` pass healthcheck.
4. Developer mới theo `quickstart.md` có stack chạy trong < 10 phút.

**Depends on**: FFX-001

---

### FFX-005
**Summary**: Configure GitHub Actions CI/CD pipeline skeleton  
**Epic**: EPIC-DEPLOY  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 1  
**Labels**: ci-cd, github-actions  

**Description**:
Tạo `.github/workflows/ci.yml` với các stages: restore → build → unit-tests → integration-tests → format-check → Docker build (main only) → push (main only).

**Files**: `.github/workflows/ci.yml`, `.github/pull_request_template.md`

**Acceptance Criteria**:
1. PR to `main` trigger CI; tất cả stages pass trên clean repo.
2. Unit test failure làm pipeline fail và block PR merge.
3. `dotnet format --verify-no-changes` fail nếu có formatting violations.
4. Docker build chỉ chạy khi push to `main`, không chạy trên PRs.

**Depends on**: FFX-001

---

### FFX-006
**Summary**: Configure JWT auth middleware và BCrypt password service  
**Epic**: EPIC-AUTH  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 1  
**Labels**: auth, jwt, security  

**Description**:
Implement `JwtTokenService` (HMAC-SHA256, claims: `sub`, `email`, `role`, `iat`, `exp`, TTL 900s) và `BcryptPasswordService` (work factor ≥ 12). Cấu hình `AddAuthentication().AddJwtBearer()` trong `Program.cs`.

**Files**:
- `FreshFlow.Auth.Application/Abstractions/ITokenService.cs`
- `FreshFlow.Auth.Application/Abstractions/IPasswordService.cs`
- `FreshFlow.Auth.Infrastructure/Services/JwtTokenService.cs`
- `FreshFlow.Auth.Infrastructure/Services/BcryptPasswordService.cs`

**Acceptance Criteria**:
1. `GenerateAccessToken(user)` trả về JWT với claims `sub`, `email`, `role`, `exp - iat = 900`.
2. Request tới `[Authorize]` endpoint với valid JWT → response bình thường; thiếu JWT → 401.
3. `Hash("password")` tạo BCrypt hash (bắt đầu `$2`) mà `Verify("password", hash)` trả `true`.
4. Work factor ≥ 12 trong `appsettings.json`.

**Depends on**: FFX-001

---

### FFX-007
**Summary**: Configure SignalR với Redis backplane (stub hubs) ⚠️ HIGH-RISK  
**Epic**: EPIC-INFRA  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 1  
**Labels**: signalr, redis, high-risk  

**⚠️ HIGH-RISK**: First integration của SignalR với Redis backplane. Lỗi thường gặp: token không pass đúng qua `?access_token=` query param. Test sớm.  
**Mitigation**: Integration test với 2 `WebApplicationFactory` instances share 1 Redis container.

**Description**:
Tạo 3 stub hubs: `PricingHub`, `OrderHub`, `DeliveryHub`. Cấu hình `AddSignalR().AddStackExchangeRedis()`. Toggle `SignalR__UseRedis=false` cho local dev không cần Redis.

**Files**:
- `src/FreshFlow.API/SignalR/PricingHub.cs` (stub)
- `src/FreshFlow.API/SignalR/OrderHub.cs` (stub)
- `src/FreshFlow.API/SignalR/DeliveryHub.cs` (stub)

**Acceptance Criteria**:
1. Client connect `/hubs/pricing` với valid JWT qua `?access_token=` → HTTP 101 accepted.
2. Client không có JWT → 401 trên `/hubs/pricing/negotiate`.
3. `SignalR__UseRedis=true` → Redis backplane connect thành công (visible trong logs).
4. `SignalR__UseRedis=false` → SignalR chạy không cần Redis.

**Depends on**: FFX-003, FFX-006

---

### FFX-008
**Summary**: Configure Serilog + correlation ID middleware + /health endpoint  
**Epic**: EPIC-INFRA  
**Assignee**: BE/DevOps  
**Story Points**: 2  
**Sprint**: Sprint 1  
**Labels**: logging, observability  

**Description**:
Cài Serilog structured logging với JSON output. Implement `CorrelationIdMiddleware` (propagate `X-Correlation-Id` header). Cấu hình `/health` endpoint với PostgreSQL + Redis health checks.

**Files**:
- `src/FreshFlow.API/Middleware/CorrelationIdMiddleware.cs`

**Acceptance Criteria**:
1. `GET /health` trả về `{"status":"Healthy","components":{"postgresql":{"status":"Healthy"},"redis":{"status":"Healthy"}}}` khi cả 2 services up.
2. PostgreSQL unreachable → `GET /health` trả HTTP 503.
3. Mỗi log line chứa `correlationId`, `timestamp`, `level` trong JSON format.
4. `X-Correlation-Id` header trên request được propagate vào tất cả log entries của request đó.

**Depends on**: FFX-002, FFX-003

---

### FFX-009
**Summary**: Implement POST /auth/login endpoint  
**Epic**: EPIC-AUTH  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 1  
**Labels**: auth, api  

**Description**:
Implement `LoginCommand` + `LoginCommandHandler` + `LoginCommandValidator`. Tạo `UserRepository`, `RefreshTokenRepository`. Tạo `AddAuthModule()` DI extension.

**Files**:
- `FreshFlow.API/Controllers/AuthController.cs`
- `FreshFlow.Auth.Application/Commands/Login/LoginCommand.cs`
- `FreshFlow.Auth.Application/Commands/Login/LoginCommandHandler.cs`
- `FreshFlow.Auth.Application/Commands/Login/LoginCommandValidator.cs`
- `FreshFlow.Auth.Infrastructure/Persistence/Repositories/UserRepository.cs`
- `FreshFlow.Auth.Infrastructure/Persistence/Repositories/RefreshTokenRepository.cs`
- `FreshFlow.Auth.Infrastructure/DependencyInjection.cs`

**Acceptance Criteria**:
1. `POST /api/v1/auth/login` với credentials hợp lệ → HTTP 200 với `accessToken`, `refreshToken`, `expiresIn: 900`, `user.role`.
2. Sai password → HTTP 401 `INVALID_CREDENTIALS` (không có token trong body).
3. Email không tồn tại → HTTP 401 cùng code `INVALID_CREDENTIALS` (không leak user existence).
4. JWT payload chứa `sub`, `email`, `role`, `iat`, `exp`; `exp - iat = 900`.
5. Thiếu field `email` → HTTP 400 với FluentValidation field-level error.

**Depends on**: FFX-006, FFX-002

---

### FFX-010
**Summary**: Implement POST /auth/refresh với token rotation  
**Epic**: EPIC-AUTH  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 1  
**Labels**: auth, security  

**Description**:
Implement `RefreshTokenCommand` với full rotation logic: revoke old token, issue new token in same `family_id`. Reuse detection: nếu đã revoke → invalidate entire family.

**Files**:
- `FreshFlow.Auth.Application/Commands/RefreshToken/RefreshTokenCommand.cs`
- `FreshFlow.Auth.Application/Commands/RefreshToken/RefreshTokenCommandHandler.cs`
- `FreshFlow.Auth.Application/Commands/RefreshToken/RefreshTokenCommandValidator.cs`

**Acceptance Criteria**:
1. `POST /api/v1/auth/refresh` với valid token → HTTP 200 với new `accessToken` + `refreshToken`; old token `revoked_at` được set trong DB.
2. Reuse token lần 2 → HTTP 401 `REFRESH_TOKEN_REUSE`; TẤT CẢ tokens trong cùng `family_id` bị revoke (verify trong DB).
3. Token hết hạn → HTTP 401 `REFRESH_TOKEN_EXPIRED`.
4. Token chưa bao giờ được issue → HTTP 401.

**Depends on**: FFX-009

---

### FFX-011
**Summary**: Implement POST /auth/logout endpoint  
**Epic**: EPIC-AUTH  
**Assignee**: BE/DevOps  
**Story Points**: 2  
**Sprint**: Sprint 1  
**Labels**: auth  

**Description**:
Implement `LogoutCommand` + handler. Set `revoked_at = NOW()` cho refresh token trong DB.

**Files**:
- `FreshFlow.Auth.Application/Commands/Logout/LogoutCommand.cs`
- `FreshFlow.Auth.Application/Commands/Logout/LogoutCommandHandler.cs`

**Acceptance Criteria**:
1. `POST /api/v1/auth/logout` với valid Bearer + valid `refreshToken` → HTTP 204; token revoked trong DB.
2. Sau logout, `POST /auth/refresh` với token đã revoke → HTTP 401.
3. Gọi logout không có `Authorization` header → HTTP 401.
4. Logout không ảnh hưởng active sessions khác của cùng user (device-level isolation).

**Depends on**: FFX-010

---

### FFX-012
**Summary**: Implement POST /admin/users (Admin only) + market assignment and restaurant approval  
**Epic**: EPIC-AUTH  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 1  
**Labels**: auth, admin  

**Description**:
Implement `CreateUserCommand` + validator. Chỉ Admin role được phép. Thêm endpoint `PATCH /admin/restaurants/{id}/approve` và market assignment khi tạo `market_agent`.

**Files**:
- `FreshFlow.Auth.Application/Commands/CreateUser/CreateUserCommand.cs`
- `FreshFlow.Auth.Application/Commands/CreateUser/CreateUserCommandHandler.cs`
- `FreshFlow.Auth.Application/Commands/CreateUser/CreateUserCommandValidator.cs`
- `FreshFlow.Auth.Application/Commands/ApproveRestaurant/ApproveRestaurantCommand.cs`
- `FreshFlow.API/Controllers/AdminController.cs`

**Acceptance Criteria**:
1. `POST /api/v1/admin/users` bởi Admin với `role: market_agent` → HTTP 201 với `userId` và market assignment nếu có `marketId`.
2. Token không phải Admin → HTTP 403.
3. Email đã tồn tại → HTTP 409 `EMAIL_ALREADY_EXISTS`.
4. Tạo `role: restaurant` tạo Restaurant account với `is_approved = false`.
5. `PATCH /api/v1/admin/restaurants/{id}/approve` set `is_approved = true`.

**Depends on**: FFX-009

---

### FFX-013
**Summary**: Scaffold Angular app với routing, auth guard, HTTP interceptor  
**Epic**: EPIC-FRONTEND-WEB  
**Assignee**: FE1-Web  
**Story Points**: 8  
**Sprint**: Sprint 1  
**Labels**: angular, frontend, auth  

**Description**:
Tạo Angular 17 project với standalone components, OnPush, lazy-loaded routes. Implement `AuthGuard`, `RoleGuard`, `AuthInterceptor` (attach Bearer token, proactive refresh tại exp-60s).

**Files**:
- `freshflow-web/src/app/app.routes.ts`
- `freshflow-web/src/app/core/guards/auth.guard.ts`
- `freshflow-web/src/app/core/guards/role.guard.ts`
- `freshflow-web/src/app/core/interceptors/auth.interceptor.ts`
- `freshflow-web/src/app/core/services/auth.service.ts`

**Acceptance Criteria**:
1. `ng build --configuration production` không TypeScript errors hay warnings.
2. Navigate `/pricing` không có token → redirect `/login`; sau login → redirect back `/pricing`.
3. `AuthInterceptor` attach `Authorization: Bearer <token>` cho tất cả API requests.
4. 401 response → interceptor thử refresh 1 lần và retry original request; 401 lần 2 → logout.
5. Tất cả components dùng `OnPush`; không có `any` types.

**Depends on**: FFX-001

---

### FFX-014
**Summary**: Implement Angular login page và auth service  
**Epic**: EPIC-FRONTEND-WEB  
**Assignee**: FE1-Web  
**Story Points**: 5  
**Sprint**: Sprint 1  
**Labels**: angular, frontend  

**Description**:
Implement `LoginComponent` (form, spinner, inline error) và hoàn thiện `AuthService` (login, logout, token storage trong localStorage).

**Files**:
- `freshflow-web/src/app/features/auth/login/login.component.ts`
- `freshflow-web/src/app/features/auth/login/login.component.html`
- `freshflow-web/src/app/core/services/auth.service.ts`

**Acceptance Criteria**:
1. Submit credentials hợp lệ → navigate `/dashboard`; credentials sai → inline error (không reload page).
2. Loading spinner hiển thị trong khi gọi API.
3. Tokens lưu trong `localStorage`; sau browser refresh user vẫn logged in.
4. Nút "Logout" gọi `POST /auth/logout` và redirect `/login`.

**Depends on**: FFX-013, FFX-009

---

### FFX-015
**Summary**: Set up Angular shared component library + kiosk module scaffold  
**Epic**: EPIC-FRONTEND-WEB  
**Assignee**: FE2-Web  
**Story Points**: 5  
**Sprint**: Sprint 1  
**Labels**: angular, frontend, components  

**Description**:
Tạo shared component library: `FfButtonComponent`, `FfInputComponent`, `FfBadgeComponent`, `FfSpinnerComponent`, `VndCurrencyPipe`. Scaffold kiosk feature module.

**Files**:
- `freshflow-web/src/app/shared/components/`
- `freshflow-web/src/app/shared/pipes/vnd-currency.pipe.ts`
- `freshflow-web/src/app/features/kiosk/kiosk.routes.ts`

**Acceptance Criteria**:
1. `FfButtonComponent` nhận `[variant]` (`primary`/`secondary`), `[loading]`, `[disabled]`; render đúng.
2. Tất cả shared components dùng `OnPush` và standalone.
3. Kiosk route lazy-load từ `/kiosk` không lỗi.
4. `VndCurrencyPipe` format `150000` → `₫150,000`.

**Depends on**: FFX-013

---

### FFX-016
**Summary**: Scaffold React Native app với navigation và auth state  
**Epic**: EPIC-FRONTEND-MOBILE  
**Assignee**: FE3-Mobile  
**Story Points**: 8  
**Sprint**: Sprint 1  
**Labels**: react-native, mobile, auth  

**Description**:
Tạo Expo project (TypeScript, React Navigation, Zustand, expo-secure-store). Implement `AppNavigator` (Auth stack vs App tabs dựa trên auth state). `authStore` Zustand với `token`, `user`, `login()`, `logout()`, `restoreToken()`.

**Files**:
- `freshflow-mobile/src/navigation/AppNavigator.tsx`
- `freshflow-mobile/src/store/authStore.ts`
- `freshflow-mobile/src/services/api.service.ts` (Axios instance stub)

**Acceptance Criteria**:
1. `tsc --noEmit` exits 0 với strict mode.
2. App launch không lỗi trên Android emulator (Expo Go hoặc bare).
3. Cold launch với stored valid token → navigate thẳng vào app, không show login screen.
4. Cold launch không có token → show login screen.

**Depends on**: FFX-001

---

### FFX-017
**Summary**: Implement React Native login screen + expo-secure-store token storage  
**Epic**: EPIC-FRONTEND-MOBILE  
**Assignee**: FE3-Mobile  
**Story Points**: 5  
**Sprint**: Sprint 1  
**Labels**: react-native, mobile, auth  

**Description**:
Implement `LoginScreen` và `auth.service.ts` (login, logout, refreshToken). Lưu tokens trong `expo-secure-store` (không dùng `AsyncStorage`).

**Files**:
- `freshflow-mobile/src/screens/auth/LoginScreen.tsx`
- `freshflow-mobile/src/services/auth.service.ts`

**Acceptance Criteria**:
1. Submit credentials hợp lệ → navigate đúng navigator (`kiosk_staff` → KioskNavigator; `restaurant` → RestaurantNavigator).
2. Credentials sai → inline error toast, không crash.
3. Tokens lưu trong `expo-secure-store`; app restart không force re-login nếu token còn valid.
4. Nút Login disabled trong khi API request in-flight.

**Depends on**: FFX-016, FFX-009

---

### FFX-018
**Summary**: Implement React Native api.service.ts + auto token refresh  
**Epic**: EPIC-FRONTEND-MOBILE  
**Assignee**: FE3-Mobile  
**Story Points**: 5  
**Sprint**: Sprint 1  
**Labels**: react-native, mobile  

**Description**:
Hoàn thiện `api.service.ts`: Axios instance + request interceptor check token expiry (< 60s → proactive refresh) với in-flight deduplication via `refreshPromise` guard.

**Files**:
- `freshflow-mobile/src/services/api.service.ts`
- `freshflow-mobile/src/services/auth.service.ts`

**Acceptance Criteria**:
1. API call với token expiring < 60s → refresh tự động; original request retry với new token.
2. 2 API calls đồng thời khi token gần hết hạn → chỉ 1 refresh request được gửi (in-flight deduplication).
3. 401 trên refresh call → logout và navigate `LoginScreen`.
4. Tất cả API calls qua `api.service.ts`; không có direct `fetch`/`axios` trong screen components.

**Depends on**: FFX-017, FFX-010

---

## Sprint 2 — Real-Time Pricing

### FFX-019
**Summary**: DB migration cho Pricing module tables  
**Epic**: EPIC-PRICING  
**Assignee**: BE/DevOps  
**Story Points**: 2  
**Sprint**: Sprint 2  
**Labels**: database, migration  

**Description**:
Tạo migration cho `markets`, `products`, `market_products`, `price_snapshots` (PARTITION BY RANGE), `system_config`. Dùng raw SQL trong migration cho partition DDL. Seed 3 markets (Hoc Mon, Binh Dien, Thu Duc).

**Acceptance Criteria**:
1. Migration tạo đúng 5 tables với FK constraints, indexes khớp `docs/03-database-schema.md`.
2. `INSERT INTO price_snapshots` với `recorded_at = NOW()` nằm trong current month partition, không phải default partition.
3. `idx_price_snapshots_market_product_recorded_at` index tồn tại trên parent table.
4. Seeded: 3 markets với coordinates; `daily_order_cutoff_time = 22:00` và `price_band_tolerance_percent = 10.00` trong `system_config`.

**Depends on**: FFX-002

---

### FFX-020
**Summary**: Implement product catalog endpoints  
**Epic**: EPIC-PRICING  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 2  
**Labels**: api, pricing  

**Description**:
Implement `GET /products`, `GET /markets`, `POST /admin/products`, `PATCH /admin/products/{id}`. Tạo `AddPricingModule()` DI extension.

**Acceptance Criteria**:
1. `GET /api/v1/products` chỉ trả active products; Admin + `?includeInactive=true` trả tất cả.
2. `POST /api/v1/admin/products` bởi Admin → 201 với `productId`; Kiosk Staff → 403.
3. `GET /api/v1/markets` trả 3 seeded markets với `id`, `name`, `latitude`, `longitude`.
4. PATCH hỗ trợ soft-delete via `status: inactive`.

**Depends on**: FFX-019, FFX-006

---

### FFX-021
**Summary**: Implement market products listing với Redis cache  
**Epic**: EPIC-PRICING  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 2  
**Labels**: api, pricing, caching  

**Description**:
Implement `GET /markets/{marketId}/products`. Cache logic: check `price:{marketId}:{productId}` Hash trước, cache miss → query PG + populate cache. `availableQuantity = current_quantity - reserved_quantity`.

**Acceptance Criteria**:
1. Endpoint trả array với `productId`, `name`, `unit`, `currentPrice`, `currentQuantity`, `availableQuantity`, `updatedAt`.
2. Request thứ 2 trong TTL window được serve từ Redis cache.
3. Redis down → endpoint vẫn trả data đúng từ PostgreSQL.
4. `availableQuantity` phản ánh đúng soft-reservations.

**Depends on**: FFX-019, FFX-003

---

### FFX-022
**Summary**: Implement price + quantity update endpoints + Redis cache write  
**Epic**: EPIC-PRICING  
**Assignee**: BE/DevOps  
**Story Points**: 8  
**Sprint**: Sprint 2  
**Labels**: api, pricing, critical-path  

**Description**:
Implement `PATCH /markets/{id}/products/{id}/price` và `/quantity`. Transaction: `BEGIN → INSERT price_snapshots → UPDATE market_products → COMMIT → HSET Redis (fire-and-forget)`.

**Acceptance Criteria**:
1. PATCH price với `{"price": 25000}` → 200 với `updatedPrice`, `updatedAt`.
2. Kiosk Staff assigned Market A gọi cho Market B → 403.
3. `price <= 0` → 422 với field error `"Must be greater than 0"`.
4. Sau PATCH, GET market products ngay lập tức reflect giá mới (Redis + PG đều updated).
5. Redis failure KHÔNG rollback PG transaction — write vẫn thành công, log Warning.

**Depends on**: FFX-021, FFX-006

---

### FFX-023
**Summary**: Implement PricingHub group management + broadcast ⚠️ HIGH-RISK  
**Epic**: EPIC-PRICING  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 2  
**Labels**: signalr, real-time, high-risk  

**⚠️ HIGH-RISK**: Real-time message delivery với Redis backplane.  
**Mitigation**: Integration test với 2 `WebApplicationFactory` instances share 1 Redis container. Gửi price update tới instance A, verify instance B's client nhận `PriceUpdated` trong 500ms. Test market-assignment enforcement.

**Description**:
Implement `PricingHub.JoinMarketGroup()`, `LeaveMarketGroup()`, `OnConnectedAsync()` (kiosk auto-join). Implement `IPricingBroadcastService` + `PricingBroadcastService`. Gọi broadcast (fire-and-forget) sau `UpdatePriceCommandHandler`.

**Acceptance Criteria**:
1. Sau `PATCH /price`, SignalR client subscribed `market:{marketId}` nhận `PriceUpdated` trong 500ms; payload có `productId`, `marketId`, `newPrice`, `newQuantity`, `updatedAt`.
2. Client KHÔNG subscribed market group KHÔNG nhận event.
3. Kiosk Staff join `market:{marketId}` cho market không được assign → hub error.
4. Broadcast failure (Redis down) KHÔNG làm PATCH endpoint trả error.

**Depends on**: FFX-022, FFX-007

---

### FFX-024
**Summary**: Implement price history endpoint (cursor pagination)  
**Epic**: EPIC-PRICING  
**Assignee**: BE/DevOps  
**Story Points**: 2  
**Sprint**: Sprint 2  
**Labels**: api, pricing  

**Description**:
Implement `GET /markets/{id}/products/{id}/price-history` với cursor pagination. Cursor là base64-encoded `{"id":"<uuid>","recordedAt":"<iso>"}`.

**Acceptance Criteria**:
1. Endpoint trả records sorted descending by `recordedAt` với `nextCursor` trong `meta`.
2. Sau 10 price updates, endpoint trả ≥ 10 records với distinct `recordedAt`.
3. Không có `DELETE` endpoint; `PriceSnapshotRepository` chỉ expose append và read.

**Depends on**: FFX-022

---

### FFX-026
**Summary**: Implement Angular pricing dashboard với live SignalR feed  
**Epic**: EPIC-FRONTEND-WEB  
**Assignee**: FE1-Web  
**Story Points**: 5  
**Sprint**: Sprint 2  
**Labels**: angular, frontend, signalr  

**Description**:
Implement `PricingDashboardComponent` dùng `WritableSignal<MarketProductDto[]>`, connect `/hubs/pricing`, join `market:{marketId}` on market select, listen only for `PriceUpdated`.

**Acceptance Criteria**:
1. Market select → price table load qua `GET /markets/{id}/products`.
2. Kiosk staff update price → table row update trong 2 giây — không cần refresh.
3. Tất cả components dùng `OnPush`; không `.subscribe()` trong component; `async` pipe trong templates.

**Depends on**: FFX-021, FFX-023, FFX-013

---

### FFX-027
**Summary**: Implement Angular kiosk product list + price update screens  
**Epic**: EPIC-FRONTEND-WEB  
**Assignee**: FE2-Web  
**Story Points**: 5  
**Sprint**: Sprint 2  
**Labels**: angular, frontend, kiosk  

**Description**:
Implement kiosk UI với UX constraint: ≤ 4 taps từ product list đến confirmation. Large touch targets, high contrast (poor lighting).

**Acceptance Criteria**:
1. Kiosk Staff update price trong ≤ 4 taps từ product list.
2. Sau PATCH thành công, product list row ngay lập tức reflect giá mới (optimistic update).
3. 403 response (wrong market) → message "Not authorized for this market".

**Depends on**: FFX-022, FFX-015

---

### FFX-028
**Summary**: Implement React Native kiosk price update screen  
**Epic**: EPIC-FRONTEND-MOBILE  
**Assignee**: FE3-Mobile  
**Story Points**: 5  
**Sprint**: Sprint 2  
**Labels**: react-native, mobile, kiosk  

**Description**:
Implement `ProductListScreen` và `PriceUpdateScreen`. SignalR reconnect với exponential backoff (initial 1s, max 30s) khi mất kết nối.

**Acceptance Criteria**:
1. Product list load từ `GET /markets/{id}/products` on mount; pull-to-refresh hoạt động.
2. Kiosk Staff update price trong ≤ 4 taps.
3. Sau update thành công, list row update ngay (optimistic update hoặc SignalR echo).
4. SignalR reconnect tự động với exponential backoff sau network drop; không cần restart app.

**Depends on**: FFX-017, FFX-022, FFX-023

---

## Sprint 3 — Order Management

### FFX-029
**Summary**: DB migration cho Orders module tables  
**Epic**: EPIC-ORDERS  
**Assignee**: BE/DevOps  
**Story Points**: 2  
**Sprint**: Sprint 3  
**Labels**: database, migration  
**Note**: `order_items.subtotal` là `GENERATED ALWAYS AS (quantity * unit_price) STORED` — dùng raw SQL trong migration.

**Depends on**: FFX-019

---

### FFX-030
**Summary**: Implement restaurant profile endpoints  
**Epic**: EPIC-ORDERS  
**Assignee**: BE/DevOps  
**Story Points**: 2  
**Sprint**: Sprint 3  
**Labels**: api, orders  

**Acceptance Criteria**:
1. `POST /api/v1/restaurants` bởi Restaurant user → 201 với `restaurantId`.
2. Duplicate call (đã có profile) → 409.
3. `GET /api/v1/restaurants/me` trả profile với `isApproved` field.

**Depends on**: FFX-029, FFX-009

---

### FFX-031
**Summary**: Implement bulk order creation + stock validation + soft-reservation  
**Epic**: EPIC-ORDERS  
**Assignee**: BE/DevOps  
**Story Points**: 8  
**Sprint**: Sprint 3  
**Labels**: api, orders, critical-path  

**Description**:
Implement `CreateOrderCommand`. Soft-reservation: `INCRBY reservation:{marketId}:{productId} <qty>` trong Redis. On failure → `DECRBY` rollback.

**Acceptance Criteria**:
1. `POST /api/v1/orders` → 201 với `orderId`, `status: pending`, `totalAmount`; Redis reservation incremented.
2. Line item request > `availableQuantity` → 422 `INSUFFICIENT_STOCK` với `productId`, `requestedQty`, `availableQty`.
3. 2 concurrent requests cho last unit: đúng 1 thành công, 1 kia 422 `INSUFFICIENT_STOCK`.
4. Failure sau reservation → reservation rolled back (Redis DECRBY).
5. Unapproved restaurant → 403 `RESTAURANT_NOT_APPROVED`.

**Depends on**: FFX-030, FFX-021

---

### FFX-032
**Summary**: Implement order listing và detail endpoints  
**Epic**: EPIC-ORDERS  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 3  
**Labels**: api, orders  

**Acceptance Criteria**:
1. `GET /orders/{orderId}` trả order với line items cho owning restaurant; wrong restaurant → 403.
2. `GET /orders?status=pending&page=1&pageSize=20` trả paginated response với `totalCount`, `page`, `pageSize`.
3. `GET /admin/orders?restaurantId={id}` trả orders của restaurant đó; non-Admin → 403.

**Depends on**: FFX-031

---

### FFX-033
**Summary**: Implement order cancellation với soft-reservation release  
**Epic**: EPIC-ORDERS  
**Assignee**: BE/DevOps  
**Story Points**: 2  
**Sprint**: Sprint 3  
**Labels**: api, orders  

**Acceptance Criteria**:
1. `PATCH /orders/{id}/cancel` khi status `pending`/`confirmed` → 200; status → `cancelled`; Redis reservation decremented.
2. Cancel `in_transit` order → 409 `ORDER_NOT_CANCELLABLE`.
3. Restaurant khác → 403.

**Depends on**: FFX-031

---

### FFX-034
**Summary**: Implement order status transitions + OrderHub SignalR broadcast  
**Epic**: EPIC-ORDERS  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 3  
**Labels**: api, orders, signalr  

**Description**:
Flesh out `OrderHub.OnConnectedAsync()`: Restaurant auto-join `restaurant:{restaurantId}`; Admin auto-join `admin:all`. Implement `UpdateOrderStatusCommand` + `IOrderBroadcastService`.

**Acceptance Criteria**:
1. `PATCH /admin/orders/{id}/status` với `{"status":"confirmed"}` → restaurant client nhận `OrderStatusChanged` trong 500ms.
2. Payload: `{ orderId, previousStatus, newStatus, changedAt, changedByUserId }`.
3. Invalid transition (e.g., `pending → delivered`) → 422.
4. Non-Admin → 403.

**Depends on**: FFX-032, FFX-007

---

### FFX-035
**Summary**: Implement order grouping + Admin auto-batch trigger  
**Epic**: EPIC-ORDERS  
**Assignee**: BE/DevOps  
**Story Points**: 8  
**Sprint**: Sprint 3  
**Labels**: api, orders, logistics  

**Description**:
Implement manual order grouping plus shared auto-batching service. `POST /api/v1/admin/order-groups/auto-batch` uses the same `OrderBatchingService` as the daily 22:00 job, groups `confirmed` unbatched orders by `deliveryZone + sourceMarket`, supports optional `targetDate`, `dryRun`, and `force`, and is idempotent.

**Acceptance Criteria**:
1. `POST /api/v1/admin/order-groups` với `confirmed` order IDs → 201 với `orderGroupId`.
2. Include non-`confirmed` order → 422.
3. Order đã trong active group → 409.
4. `POST /api/v1/admin/order-groups/auto-batch` returns created batch count, batched order count, and skipped/conflict list.
5. `dryRun=true` previews groups and writes no DB changes.
6. Re-running the endpoint does not create duplicate groups for already batched orders.
7. Manual trigger and scheduled 22:00 job both move eligible orders to `batched` and emit `OrderGrouped`.

**Depends on**: FFX-034

---

### FFX-036
**Summary**: Implement scheduled recurring orders + background job  
**Epic**: EPIC-ORDERS  
**Assignee**: BE/DevOps  
**Story Points**: 8  
**Sprint**: Sprint 3  
**Labels**: api, orders, background-job  

**Description**:
Implement `ScheduledOrderJob` (IHostedService, 60s tick). Idempotent: check `last_executed_at` trước khi tạo instance. Timezone: `Asia/Ho_Chi_Minh`.

**Acceptance Criteria**:
1. `POST /orders/scheduled` → 201 với `scheduledOrderId`, `nextRunAt` trong `Asia/Ho_Chi_Minh`.
2. Background job tạo concrete order instance trong 60 giây từ scheduled time.
3. Job chạy 2 lần trong cùng window KHÔNG tạo duplicate (idempotent).
4. Missed instance được tạo trên next tick với `MISSED_EXECUTION` log entry.

**Depends on**: FFX-031

---

### FFX-037
**Summary**: Implement Angular order management UI  
**Epic**: EPIC-FRONTEND-WEB  
**Assignee**: FE1-Web  
**Story Points**: 8  
**Sprint**: Sprint 3  
**Labels**: angular, frontend, orders  

**Description**:
Implement order list (paginated, status filter, live updates), create order flow (market → product picker → submit), order detail (status timeline).

**Acceptance Criteria**:
1. Restaurant user browse sản phẩm theo market, add line items, submit order trong ≤ 3 interactions.
2. Admin advance order status → `OrderListComponent` update row real-time qua SignalR — không refresh.
3. `CreateOrderComponent` show per-item `INSUFFICIENT_STOCK` errors inline.
4. Tất cả components dùng `OnPush` và `async` pipe.

**Depends on**: FFX-032, FFX-034, FFX-013

---

### FFX-038
**Summary**: Implement React Native order list + detail với real-time status  
**Epic**: EPIC-FRONTEND-MOBILE  
**Assignee**: FE3-Mobile  
**Story Points**: 5  
**Sprint**: Sprint 3  
**Labels**: react-native, mobile, orders  

**Acceptance Criteria**:
1. `OrderDetailScreen` cập nhật status real-time khi Admin transition — không manual refresh.
2. Order list paginated và filterable by status.
3. Tất cả API calls qua `orders.service.ts`.

**Depends on**: FFX-017, FFX-034

---

## Sprint 4 — Logistics + Hub

### FFX-039
**Summary**: DB migration cho Logistics module tables  
**Epic**: EPIC-LOGISTICS  
**Assignee**: BE/DevOps  
**Story Points**: 2  
**Sprint**: Sprint 4  
**Labels**: database, migration  
**Note**: `route_metadata JSONB`; `deliveries_order_id_unique` constraint.  
**Depends on**: FFX-029

---

### FFX-040
**Summary**: Implement vehicle CRUD endpoints  
**Epic**: EPIC-LOGISTICS  
**Assignee**: BE/DevOps  
**Story Points**: 2  
**Sprint**: Sprint 4  
**Labels**: api, logistics  

**Acceptance Criteria**:
1. POST tạo vehicle; duplicate plate → 409.
2. `GET ?available=true` filter đúng.
3. PATCH hỗ trợ status change to inactive.

**Depends on**: FFX-039

---

### FFX-041
**Summary**: Implement VRP route calculation engine (nearest-neighbor + 2-opt) ⚠️ HIGH-RISK  
**Epic**: EPIC-LOGISTICS  
**Assignee**: BE/DevOps  
**Story Points**: 8  
**Sprint**: Sprint 4  
**Labels**: algorithm, logistics, high-risk  

**⚠️ HIGH-RISK**: Algorithmic task. Nearest-neighbor + 2-opt heuristic phải đủ chính xác cho geography 3 markets.  
**Mitigation**: Viết unit tests với known-optimal small cases (3-stop triangle, 4-stop square) TRƯỚC khi implement. 2-opt không bao giờ được tệ hơn nearest-neighbor.

**Description**:
Implement `VrpSolver` (pure in-memory, không DI dependencies). Algorithm:
1. Nearest-neighbor: start từ depot, pick nearest unvisited stop.
2. 2-opt: iterate all (i,j) pairs; swap nếu reverse segment giảm total cost. Repeat until no improvement.
3. Cost functions: `DISTANCE` = Haversine km; `TIME` = distance / 30 km/h; `COST` = distance × cost-per-km.

**Acceptance Criteria**:
1. `VrpSolver.Solve(3 stops, DISTANCE)` trả correct route trong < 100ms.
2. `VrpSolver.Solve(20 stops, COST)` < 100ms; 2-opt result ≤ nearest-neighbor result.
3. 21 stops → throw `StopLimitExceededException` (422 `STOP_LIMIT_EXCEEDED`).
4. `VrpSolver` không có constructor parameters referencing `IRepository`, `ICacheService` — testable bằng `new VrpSolver()`.

**Depends on**: FFX-039

---

### FFX-042
**Summary**: Implement route creation + vehicle assignment + Redis route cache  
**Epic**: EPIC-LOGISTICS  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 4  
**Labels**: api, logistics, caching  

**Description**:
Cache key: `route:{SHA256(sorted stop IDs + "|" + criterion)}`, TTL 1 hour.

**Acceptance Criteria**:
1. `POST /logistics/routes/calculate` trả route trong 3 giây; route persisted và cached.
2. Request giống hệt trong 1 hour → trả cached result (1 VRP computation, verify qua log).
3. Assign vehicle đã được schedule → 409 `VEHICLE_NOT_AVAILABLE`.

**Depends on**: FFX-041, FFX-040

---

### FFX-043
**Summary**: Implement delivery status tracking + DeliveryHub SignalR broadcast  
**Epic**: EPIC-LOGISTICS  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 4  
**Labels**: api, logistics, signalr  

**Description**:
Flesh out `DeliveryHub.OnConnectedAsync()`: Restaurant auto-join `restaurant:{restaurantId}`. Per-restaurant fan-out: multi-drop delivery, mỗi restaurant chỉ nhận events cho orders của mình.

**Acceptance Criteria**:
1. `PATCH /admin/deliveries/{id}/status` với `in_transit` → `DeliveryStarted` tới `restaurant:{restaurantId}` trong 500ms; payload có `scheduleId`, `routeId`, `estimatedArrivalAt`, `orderIds[]`.
2. Mỗi restaurant chỉ nhận events cho orders của chính mình.
3. `POST /logistics/schedules` validate vehicle capacity; exceed → 422 `VEHICLE_CAPACITY_EXCEEDED`.

**Depends on**: FFX-042, FFX-035, FFX-007

---

### FFX-044
**Summary**: DB migration cho Hub module tables  
**Epic**: EPIC-HUB  
**Assignee**: BE/DevOps  
**Story Points**: 2  
**Sprint**: Sprint 4  
**Labels**: database, migration  
**Note**: `hub_inventory.quantity_available` là `GENERATED ALWAYS AS (quantity_in - quantity_out) STORED`; `items` columns là `jsonb`.  
**Depends on**: FFX-039

---

### FFX-045
**Summary**: Implement hub CRUD endpoints  
**Epic**: EPIC-HUB  
**Assignee**: BE/DevOps  
**Story Points**: 2  
**Sprint**: Sprint 4  
**Labels**: api, hub  

**Acceptance Criteria**:
1. POST tạo hub; PATCH update.
2. Deactivate hub với pending deliveries → 409 `HUB_HAS_PENDING_DELIVERIES`.
3. GET trả `occupiedCapacityKg` và `availableCapacityKg`.

**Depends on**: FFX-044

---

### FFX-046
**Summary**: Implement hub inbound + outbound recording với transactional inventory update  
**Epic**: EPIC-HUB  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 4  
**Labels**: api, hub  
**Critical**: `hub_inventory` update PHẢI trong cùng DB transaction với event insert.

**Acceptance Criteria**:
1. Inbound tạo event và increment `quantity_in` atomically; duplicate delivery → 409 `ALREADY_RECEIVED`; capacity exceeded → 422.
2. Outbound deduct `quantity_out`; insufficient stock → 422 `INSUFFICIENT_HUB_STOCK`.

**Depends on**: FFX-045

---

### FFX-047
**Summary**: Implement hub inventory view + redistribution suggestions  
**Epic**: EPIC-HUB  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 4  
**Labels**: api, hub  
**SLA**: Response < 2 seconds.

**Acceptance Criteria**:
1. `GET /hubs/{id}/inventory` trả current stock per product.
2. `GET /hubs/{id}/redistribution-suggestions` trả `restaurantId`, `orderId`, `productId`, `suggestedQuantityKg`, `rationale` trong < 2s.
3. Không tự động dispatch.

**Depends on**: FFX-046, FFX-035

---

### FFX-048
**Summary**: Implement Angular logistics dashboard  
**Epic**: EPIC-FRONTEND-WEB  
**Assignee**: FE1-Web  
**Story Points**: 8  
**Sprint**: Sprint 4  
**Labels**: angular, frontend, logistics  

**Description**:
Route calculator (market + hub + restaurant pickers), vehicle list, schedule list, delivery tracker (real-time via `DeliveryHub`).

**Acceptance Criteria**:
1. Admin có thể calculate route, assign vehicle, create schedule trong 1 screen flow.
2. Delivery status update live qua `DeliveryStatusChanged` SignalR event.

**Depends on**: FFX-042, FFX-043, FFX-013

---

## Sprint 5 — Analytics + Notifications

### FFX-049
**Summary**: Implement price trend analytics endpoint + Redis cache  
**Epic**: EPIC-ANALYTICS  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 5  
**Labels**: api, analytics, caching  
**SLA**: < 800ms  
**Cache**: `analytics:price_trend:{mpId}:{from}:{to}`, TTL 15 phút.

**Acceptance Criteria**:
1. Trả `minPrice`, `maxPrice`, `avgPrice`, `priceVolatility` + time-series array < 800ms.
2. Request thứ 2 trong 15 phút được cached.
3. Range > 12 tháng → trả daily-aggregated points.

**Depends on**: FFX-024

---

### FFX-050
**Summary**: Implement demand heatmap endpoint  
**Epic**: EPIC-ANALYTICS  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 5  
**Labels**: api, analytics  
**Note**: Queries phải target read replica connection. Non-Admin → 403.

**Acceptance Criteria**:
1. Trả restaurant list với `totalOrderCount`, `totalOrderValueVND`, `dominantProductCategory`, `latitude`, `longitude`.
2. `/time-distribution` trả 7×24 matrix.

**Depends on**: FFX-032, FFX-054

---

### FFX-051
**Summary**: Implement delivery performance KPI endpoints  
**Epic**: EPIC-ANALYTICS  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 5  
**Labels**: api, analytics  
**Late threshold**: `actualArrivalAt > estimatedArrivalAt + 15 minutes`.

**Acceptance Criteria**:
1. Trả `onTimeRatePercent`, `avgDeliveryDurationMinutes`, `avgVehicleUtilizationPercent` < 800ms.
2. `/by-route` breakdown per route ID.

**Depends on**: FFX-043

---

### FFX-052
**Summary**: ~~AI price prediction~~ — DEFERRED to v2 ⚠️ OUT OF SCOPE  
**Epic**: EPIC-ANALYTICS  
**Assignee**: —  
**Story Points**: —  
**Sprint**: —  
**Labels**: deferred, high-risk  

**Note**: Per `research.md` Decision 5 và `spec.md` Assumptions, AI recommendations deferred. V2 path: 7-day hoặc 30-day rolling average qua `price_snapshots` trong `AnalyticsAggregationJob`. Không cần ML model hay external service. Story points nếu revisit: 5.

---

### FFX-053
**Summary**: Implement async CSV export + background job + polling endpoints  
**Epic**: EPIC-ANALYTICS  
**Assignee**: BE/DevOps  
**Story Points**: 8  
**Sprint**: Sprint 5  
**Labels**: api, analytics, background-job  

**Acceptance Criteria**:
1. `POST /analytics/export` → 202 với `jobId`.
2. `GET /analytics/export/{id}/status` trả `ready` khi xong.
3. `GET /analytics/export/{id}/download` trả CSV với đúng Content-Type.
4. File 404 sau 24-hour expiry.

**Depends on**: FFX-049

---

### FFX-054
**Summary**: Analytics aggregation background job + migration  
**Epic**: EPIC-ANALYTICS  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 5  
**Labels**: analytics, background-job, database  

**Description**:
`AnalyticsAggregationJob` (IHostedService) pre-compute `price_trend` và `demand_heatmap` rows nightly. `PartitionMaintenanceJob`: tạo next month's `price_snapshots` partition vào ngày 25.

**Acceptance Criteria**:
1. Job pre-computes đúng rows vào `analytics_aggregations`.
2. Next month's partition được tạo vào ngày 25.
3. Job chạy 2 lần cùng ngày → idempotent.

**Depends on**: FFX-049

---

### FFX-055
**Summary**: Implement notification persistence + read/mark-read endpoints  
**Epic**: EPIC-NOTIFICATIONS  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 5  
**Labels**: api, notifications  
**Note**: `NotificationService.PersistAsync` gọi fire-and-forget — DB write failure KHÔNG block broadcast path.

**Acceptance Criteria**:
1. Notification row inserted sau price update, order status change, delivery start.
2. `GET /notifications` trả newest-first với cursor pagination.
3. Mark-read set `is_read = true` và `read_at`.
4. User chỉ đọc được notifications của chính mình; khác → 403.

**Depends on**: FFX-023, FFX-034, FFX-043

---

### FFX-056
**Summary**: Implement Angular analytics dashboard  
**Epic**: EPIC-FRONTEND-WEB  
**Assignee**: FE1-Web  
**Story Points**: 8  
**Sprint**: Sprint 5  
**Labels**: angular, frontend, analytics  

**Description**:
Price trend chart (last 30 days default, extendable 12 months), demand heatmap, delivery KPIs. Admin-only routes guarded by `RoleGuard`.

**Acceptance Criteria**:
1. Price trend chart show 30 days mặc định; date range extendable tới 12 tháng.
2. Delivery KPIs display on-time rate, avg duration, utilization.
3. Admin-only routes guarded.

**Depends on**: FFX-049, FFX-050, FFX-051, FFX-013

---

## Sprint 6 — Deployment + Testing Polish

### FFX-057
**Summary**: Integration test suite (Testcontainers + WebApplicationFactory)  
**Epic**: EPIC-DEPLOY  
**Assignee**: BE/DevOps  
**Story Points**: 8  
**Sprint**: Sprint 6  
**Labels**: testing, integration-tests  

**Description**:
Tạo `IntegrationTestBase` spin up PostgreSQL + Redis containers qua Testcontainers. Test coverage: auth (login, refresh, logout), pricing (price update, PricingHub), orders (create, status broadcast), logistics (route calculation).

**Pattern**: Mỗi test class kế thừa `IntegrationTestBase`; real DB/Redis — không mock.

**Acceptance Criteria**:
1. Critical paths (login → price update → order create → status transition) covered.
2. Integration tests dùng real DB/Redis.
3. `dotnet test --filter Category=Integration` exits 0.

**Depends on**: FFX-010, FFX-022, FFX-031, FFX-042

---

### FFX-058
**Summary**: Unit tests cho VRP solver, order batching service, token service  
**Epic**: EPIC-DEPLOY  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 6  
**Labels**: testing, unit-tests  

**Coverage**:
- `VrpSolverTests`: 3-stop optimal, 20-stop boundary, 21-stop exception, DISTANCE vs TIME vs COST
- `OrderBatchingServiceTests`: groups by deliveryZone + sourceMarket, dry-run writes nothing, idempotent skip, emits OrderGrouped
- `JwtTokenServiceTests`: claim presence, TTL, signature validation

**Acceptance Criteria**:
1. 100% branch coverage trên `VrpSolver`.
2. Order batching tests cover grouping, dry-run, idempotency, and event emission.
3. `dotnet test --filter Category=Unit` exits 0.

**Depends on**: FFX-041, FFX-035, FFX-006

---

### FFX-059
**Summary**: Production Nginx TLS config + docker-compose.prod.yml  
**Epic**: EPIC-DEPLOY  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 6  
**Labels**: devops, production, nginx  

**Files**: `nginx/nginx.prod.conf`, `docker-compose.prod.yml`, `.env.prod.example`

**Acceptance Criteria**:
1. `docker compose -f docker-compose.prod.yml up` start 4 services — không expose dev ports 5432/6379.
2. HTTP → HTTPS redirect hoạt động; HSTS header set.
3. `curl -k https://localhost/health` trả Healthy.

**Depends on**: FFX-004

---

### FFX-060
**Summary**: CI/CD pipeline polish (coverage gate, Docker push, staging deploy)  
**Epic**: EPIC-DEPLOY  
**Assignee**: BE/DevOps  
**Story Points**: 5  
**Sprint**: Sprint 6  
**Labels**: ci-cd, devops  

**Description**:
Update `.github/workflows/ci.yml`: coverage gate ≥ 70%, Docker push to registry tagged với git SHA, staging deploy với rolling update.

**Acceptance Criteria**:
1. PR với < 70% coverage → pipeline fail.
2. Push to `main` → build + push Docker image tagged với git SHA.
3. Staging deploy: `docker compose pull && docker compose up -d --no-deps api` với health-check gating.

**Depends on**: FFX-005, FFX-057, FFX-058

---

## Summary Table

| Sprint | Stories | Total Points |
|--------|---------|-------------|
| Sprint 1 — Foundation & Auth | FFX-001 → FFX-018 (18 stories) | 91 pts |
| Sprint 2 — Real-time Pricing | FFX-019 → FFX-024, FFX-026 → FFX-028 (9 stories) | 42 pts |
| Sprint 3 — Order Management | FFX-029 → FFX-038 (10 stories) | 55 pts |
| Sprint 4 — Logistics + Hub | FFX-039 → FFX-048 (10 stories) | 44 pts |
| Sprint 5 — Analytics + Notifications | FFX-049 → FFX-056 (8 stories, FFX-052 deferred) | 43 pts |
| Sprint 6 — Deployment + Testing | FFX-057 → FFX-060 (4 stories) | 23 pts |
| **Total** | **59 stories** | **298 pts** |

## HIGH-RISK Stories

| Story | Risk | Mitigation |
|-------|------|-----------|
| FFX-007 | SignalR + Redis backplane integration | Test `?access_token=` JWT flow sớm; 2 WebApplicationFactory + 1 Redis container |
| FFX-023 | SignalR broadcast dưới load với Redis backplane | Market-assignment enforcement test; cross-instance message test |
| FFX-041 | VRP algorithm correctness | Unit tests với known-optimal cases TRƯỚC khi implement |
| FFX-052 | AI price prediction | **DEFERRED** — out of scope v1 |

## Dependency Chain (Critical Path)

```
FFX-001 → FFX-002 → FFX-006 → FFX-009 → FFX-010
                              → FFX-019 → FFX-021 → FFX-022 → FFX-023
                                                   → FFX-031 → FFX-034
                                                              → FFX-041 → FFX-042 → FFX-043
```
