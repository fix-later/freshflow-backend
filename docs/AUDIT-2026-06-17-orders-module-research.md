# Báo cáo nghiên cứu & kế hoạch triển khai: Orders Module

| | |
|---|---|
| **Ngày nghiên cứu** | 2026-06-17 |
| **Branch** | `SCRUM-150-PRI-Real-time-Pricing-Price-Alerts` |
| **Phạm vi** | Order & Cart Management — UC-ORD-01 → UC-ORD-21 |
| **Tác giả** | Leader agent (team `backend-dev`), điều phối qua supervisor |
| **Mục đích tài liệu** | Cung cấp cho AI coding agent đủ ngữ cảnh để implement Orders module theo đúng quyết định đã chốt với product owner — không cần hỏi lại các câu đã trả lời ở đây |
| **Trạng thái** | Quyết định đã chốt, Jira key đã có cho toàn bộ 21 UC (xem §0). Implement theo thứ tự task; commit theo UC dùng key tương ứng |

> **Quy ước cập nhật tài liệu:** mỗi khi một task hoàn thành (reviewer pass) hoặc có thay đổi/quyết định mới phát sinh trong quá trình implement, phải cập nhật lại file này (đặc biệt §4 Task breakdown và §7 Nhật ký tiến độ) — đây là nguồn tham chiếu chính cho AI agent, không phải chỉ TaskList.

---

## 0. Bảng Jira key theo UC-ORD (commit theo từng UC, KHÔNG dùng 1 key chung cho cả phase)

| UC | Jira Key | UC | Jira Key | UC | Jira Key |
|---|---|---|---|---|---|
| UC-ORD-01 | SCRUM-180 | UC-ORD-08 | SCRUM-197 | UC-ORD-15 | SCRUM-214 |
| UC-ORD-02 | SCRUM-183 | UC-ORD-09 | SCRUM-198 | UC-ORD-16 | SCRUM-217 |
| UC-ORD-03 | SCRUM-186 | UC-ORD-10 | SCRUM-201 | UC-ORD-17 | SCRUM-220 |
| UC-ORD-04 | SCRUM-189 | UC-ORD-11 | SCRUM-204 | UC-ORD-18 | SCRUM-222 |
| UC-ORD-05 | SCRUM-192 | UC-ORD-12 | SCRUM-205 | UC-ORD-19 | SCRUM-225 |
| UC-ORD-06 | SCRUM-193 | UC-ORD-13 | SCRUM-208 | UC-ORD-20 | SCRUM-228 |
| UC-ORD-07 | SCRUM-196 | UC-ORD-14 | SCRUM-211 | UC-ORD-21 | SCRUM-231 |

**Quy ước commit:** format `feat(orders): SCRUM-XXX <description>`, một commit (hoặc nhóm commit nhỏ) ứng với một UC, dùng đúng key của UC đó — KHÔNG gộp nhiều UC khác Jira key vào 1 commit.

**Task #1 (Phase 0 — Domain foundation) không map 1:1 với một UC cụ thể** (đây là nền tảng cho toàn bộ module). Quy ước: dùng key của UC đầu tiên mà nó phục vụ trực tiếp — **SCRUM-180 (UC-ORD-01 — Create Draft Order)** — vì `Order`/`OrderItem` aggregate là điều kiện tiên quyết để tạo draft order. Ghi rõ trong commit body là phần nền tảng dùng chung cho cả module.

**Task #5 (admin set/adjust credit limit) dùng chung key SCRUM-193** (mở rộng tự nhiên của Task #3 Credit, không xin key riêng — supervisor xác nhận 2026-06-18).

---

## 1. Hiện trạng repo tại thời điểm nghiên cứu

- `src/Modules/Orders/*` chỉ có **scaffold rỗng**: 3 `.csproj` (`Orders.Domain` → `SharedKernel`; `Orders.Application` → `Domain + Contracts + SharedKernel`; `Orders.Infrastructure` → `Application`). Không có file `.cs` nào.
- Chưa có `tests/Unit/FreshFlow.Orders.UnitTests`.
- Chưa có `OrderHub` trong `FreshFlow.API/SignalR/`.
- `FreshFlow.Contracts` chưa có integration event nào cho Orders.
- Pattern tham chiếu ổn định cần theo (xem code Pricing/Auth/Catalog đã implement):
  - `Result<T>` — không throw exception cho business rule.
  - MediatR command/query + `FluentValidation` validator co-located (`Commands/{Name}/{Name}Validator.cs`).
  - `ValidationBehavior` pipeline.
  - Repository qua interface định nghĩa ở `{Module}.Application/Abstractions`.
  - EF configuration ở `{Module}.Infrastructure/Persistence/Configurations`.
  - Đọc dữ liệu cross-module qua reader riêng (ví dụ `MarketProductReader` — Orders sẽ dùng pattern này để đọc `market_products` từ Pricing/Catalog).

### Schema đã có sẵn (docs/03-database-schema.md)

Bảng: `orders`, `order_items`, `scheduled_orders`, `order_groups`.
Enum: `order_status` (`draft, payment_pending, confirmed, batched, picked_up, at_hub, delivering, delivered, cancelled`), `payment_status`, `order_group_status`.

`Order` là aggregate root; `order_items` là entity nội bộ (không có `deleted_at` riêng — xoá item nghĩa là hủy cả đơn).

### Schema gaps đã verify trực tiếp trong docs/03

| Gap | Ghi chú |
|---|---|
| `restaurants` không có cột credit | Cần bổ sung cho mô hình B2B credit (xem §2, Q1) |
| `market_products.reserved_quantity` đã tồn tại | Thuộc về Pricing — reservation logic nên nằm ở Pricing, Orders chỉ gọi qua interface |
| Chưa có interface reservation/credit nào trong code Pricing | Phải tạo mới (`IPricingReservationService`, `ICreditService`) |
| Chưa có bảng cho issue-report của restaurant (UC-19) | Cần bảng mới `order_issues` |

---

## 2. Quyết định đã chốt với product owner (KHÔNG hỏi lại)

### Q1 — Payment model: **B2B Credit/công nợ**
Đơn KHÔNG đi qua payment gateway per-order (VNPay/MoMo/ZaloPay) dù docs/01 + docs/04 còn mô tả luồng `PAYMENT_PENDING` + webhook. Quyết định: bỏ `PAYMENT_PENDING` khỏi luồng confirm ở application layer; `DRAFT → CONFIRMED` trực tiếp nếu nhà hàng còn hạn mức tín dụng; vượt hạn mức → lỗi `422 CREDIT_LIMIT_EXCEEDED`.

> Quyết định gốc của product owner ghi nhận ngày 2026-06-04 (xem memory `project_payment_model`), được xác nhận lại ngày 2026-06-17 khi lập plan Orders module.

### Q2 — Scope: **21 UC-ORD làm chuẩn** (không bám số FR-ORD hẹp hơn trong docs/01)
UC-18 (Confirm Receipt), UC-19 (Report Order Issue), UC-21 (ReOrder from History) đều **trong scope v1**, dù DB schema hiện tại chưa có bảng/cột tương ứng — cần bổ sung schema (xem §3).

### Q3 — Ranh giới module cho stock/reservation: **Orders gọi qua interface của Pricing**
Orders KHÔNG tự ghi Redis. Phải định nghĩa `IPricingReservationService` trong `Pricing.Application/Abstractions`, implement ở `Pricing.Infrastructure` (Redis + `market_products.reserved_quantity`). Orders chỉ inject và gọi interface này.

### Q4 — Hạ tầng job cho cutoff/recurring: **default = hosted service**
Dùng pattern hosted service hiện có (giống `PartitionMaintenanceJob`) cho auto-batch / generate-from-schedule / release-reservation, trừ khi có chỉ đạo khác (Hangfire/Quartz) trước khi vào Phase 5.

### Q5 — Real-time: **REST trước, real-time sau**
`OrderHub` + `IOrderBroadcastService` (UC-14) tách thành phase riêng, làm SAU khi REST ổn định. Không nằm trong đợt implement đầu.

### Quyết định bổ sung (2026-06-17, sau khi lập task list): **Đợt đầu chỉ implement thuần DB, KHÔNG implement Redis**
Toàn bộ phần phụ thuộc Redis (soft-reservation qua `IPricingReservationService`, job release 30 phút, reconciliation) bị tách ra khỏi mọi task DB-only và gom vào task riêng (#10, Phase 8 — DEFERRED). Hệ quả được chấp nhận: có khả năng over-allocation tồn kho (2 đơn cùng giành 1 sản phẩm) trong đợt đầu vì không giữ chỗ — đây là trade-off đã biết, KHÔNG coi là bug, sẽ giải quyết khi làm Phase 8.

### Quyết định về commit: **Không tự động commit nếu chưa có Jira/SCRUM key**
Coder/reviewer được phép code, test, để thay đổi ở working tree, nhưng KHÔNG được tự `git commit`. Chỉ commit khi supervisor cung cấp Jira key cho task cụ thể, theo format `feat(orders): SCRUM-XXX ...`.

---

## 3. Đề xuất schema/API bổ sung (đã được chấp thuận ngầm theo Q1/Q2 — chỉ cần hỏi lại nếu phát sinh thay đổi NGOÀI phạm vi dưới đây)

### A) Credit/công nợ (cho task #3, dùng ở #4 và #6)

- Bảng mới `restaurant_credit`: `restaurant_id` (PK/FK), `credit_limit`, `outstanding_balance`, `updated_at`.
- Bảng mới `credit_transactions` (append-only ledger): `id`, `restaurant_id`, `order_id?`, `type` (`charge | settlement | refund | adjustment`), `amount`, `balance_after`, `note`, `created_at`.
- API mới: `GET /restaurants/{id}/credit`, `GET /restaurants/{id}/credit/transactions`, `POST /admin/restaurants/{id}/credit/settle`.
- `ICreditService`: `CanChargeAsync` / `ChargeAsync` / `RefundAsync` / `SettleAsync` (tất cả trả `Result`).

### B) Receipt/Issue (cho task #8)

- Bảng mới `order_issues`: `id`, `order_id`, `order_item_id?`, `reported_by`, `issue_type` (`missing | wrong | damaged`), `affected_quantity`, `description`, `status` (`open | resolved`), `created_at`, `resolved_at?`, `deleted_at`.
- Cột mới `confirmed_receipt_at` trên `orders` (cho UC-18).

### C) Reservation interface (cho task #10 — DEFERRED, chưa code đợt này)

`IPricingReservationService` (`Pricing.Application/Abstractions`): `ReserveAsync` / `ReleaseAsync` / `ConfirmAsync` + đọc availability. Implementation ở `Pricing.Infrastructure` dùng Redis kết hợp `market_products.reserved_quantity`.

---

## 4. Task breakdown (đã tạo qua TaskCreate, theo dõi tiến độ ở đó — đây là bản mô tả đầy đủ cho AI agent)

### DB-ONLY — implement đợt này, theo đúng thứ tự ID (dependency)

**#1 — Phase 0: Domain foundation + module wiring** *(nền, block tất cả)*
- `Order` (aggregate root) + `OrderItem` (entity nội bộ) + `ScheduledOrder` + enums (KHÔNG có `payment_pending` ở application layer theo Q1).
- State machine cho `order_status`.
- Domain events: `OrderCreated`, `OrderConfirmed`, `OrderCancelled`, `OrderStatusChanged`.
- Integration events tương ứng trong `FreshFlow.Contracts`.
- EF configurations (`Orders.Infrastructure/Persistence/Configurations`).
- `AddOrdersModule` extension method, đăng ký trong `Program.cs`.
- Tạo `tests/Unit/FreshFlow.Orders.UnitTests`.

**#2 — Phase 1: Draft/Cart (UC-ORD-01..05)** *(blockedBy #1)*
- `CreateDraftOrder`, `AddItem`, `UpdateItem`, `RemoveItem`.
- **DB-ONLY pass — KHÔNG soft-reservation Redis vòng này.**
- UC-05 (Validate Order Items): kiểm tra product active + quantity hợp lệ + đủ tồn kho **bằng DB query thuần** đọc `market_products` qua cross-module reader hiện có (pattern `MarketProductReader`), so `requestedQty` với available quantity trong DB. Trả lỗi `INVALID_PRODUCT` / `INSUFFICIENT_STOCK`. Lấy `unit_price` + `product_name_snapshot` từ cùng read.
- **DEFERRED (cần Redis — KHÔNG làm vòng này):** reserve/release qua `IPricingReservationService` → theo dõi ở task #10.
- REST endpoints: `POST /api/v1/orders`, các mutation cho item.

**#3 — Phase 2a: Credit schema + AR endpoints** *(blockedBy #1)*
- Schema + API + `ICreditService` theo §3.A.

**#4 — Phase 2b: Confirm order + cutoff + price snapshot (UC-ORD-06,07,08)** *(blockedBy #2, #3)*
- Confirm thuần DB: check credit (`ICreditService.CanChargeAsync`) → `DRAFT → CONFIRMED` → lock giá (price snapshot) → charge công nợ → publish domain/integration event.
- Cutoff 22:00: đơn confirm sau cutoff chuyển `scheduledFor` sang chu kỳ giao tiếp theo.
- **DEFERRED:** confirm reservation qua Redis → task #10.

**#5 — Phase 3: Order viewing + history (UC-ORD-12,13,20)** *(blockedBy #2)*
- `GET` list (phân trang, lọc theo ngày/trạng thái), `GET` detail, order history (UC-20 dùng chung filter với UC-12). REST only — không real-time ở phase này.

**#6 — Phase 4: Cancellation + adjustment (UC-ORD-15,16,17)** *(blockedBy #4)*
- Cancel: chỉ cho phép khi đơn còn ở `DRAFT`/`CONFIRMED` trước khi `BATCHED`. Lưu `cancellation_reason`. Hoàn công nợ qua `ICreditService.RefundAsync` nếu đơn đã `CONFIRMED`.
- Adjustment (Operations Manager): cập nhật `actual_quantity` khi thiếu hàng/hư hỏng.
- **DEFERRED:** release reservation Redis → task #10.

**#7 — Phase 5: Recurring orders + generation job (UC-ORD-09,10,11)** *(blockedBy #2)*
- `CreateRecurringOrder`, quản lý (xem/sửa/pause/cancel).
- Background job sinh order instance từ `scheduled_orders` (hosted service pattern, theo Q4 default) + xử lý missed-execution recovery. Thuần DB.
- **DEFERRED:** job release-reservation 30 phút → task #10.

**#8 — Phase 6: Receipt + issue report + reorder (UC-ORD-18,19,21)** *(blockedBy #5)*
- `ConfirmReceipt` (chuyển trạng thái khi đã `DELIVERED`, set `confirmed_receipt_at`).
- `ReportOrderIssue` (tạo bản ghi `order_issues`).
- `ReOrderFromHistory` (tạo draft order mới từ đơn cũ).
- **Cần schema mới** theo §3.B — chạy migration tương ứng.

### CẦN HẠ TẦNG NGOÀI DB — để sau, chỉ làm khi có yêu cầu riêng

**#9 — Phase 7: Real-time OrderHub (UC-ORD-14)** *(blockedBy #5, DEFERRED theo Q5)*
- `OrderHub` + `IOrderBroadcastService` (pattern giống `PricingHub`/`IPricingBroadcastService`), broadcast `OrderStatusChanged` tới group `restaurant:{restaurantId}`.

**#10 — Phase 8: Redis soft-reservation (DEFERRED — cần Redis)**
- Gom toàn bộ phần Redis đã gỡ khỏi #2/#4/#6/#7: `IPricingReservationService` (Reserve/Release/Confirm + đọc availability backed by Redis), hook vào lại Phase 1/2b/4, job release 30 phút, reconciliation job 5 phút.
- Chưa có `blockedBy` cố định — kích hoạt khi user yêu cầu tiếp.

---

## 5. Rủi ro & lưu ý cho AI agent khi implement

1. **Auto-batch (UC-07) giao thoa với Logistics module** (`order_groups` + delivery routes). Scope đợt này CHỈ làm phần cutoff/`scheduledFor` ở Orders; auto-batch service đầy đủ để Logistics/Admin xử lý riêng, tránh phình scope. Nếu cần đổi, phải hỏi lại trước khi code.
2. **Over-allocation tồn kho là chấp nhận được trong đợt DB-only** (xem §2) — reviewer KHÔNG được coi đây là bug khi review task #2/#4/#6/#7.
3. **Schema mới ở #3 và #8 sẽ phát sinh EF migration** — chạy `dotnet ef migrations add` theo đúng convention dự án (xem CLAUDE.md root), nhưng KHÔNG migrate database thật nếu chưa được xác nhận; tạo migration file là đủ trừ khi được yêu cầu apply.
4. **Commit theo UC, dùng đúng Jira key tương ứng** (xem bảng §0) — KHÔNG còn ở trạng thái "chưa có key nên không commit" nữa (đã chốt 2026-06-17), nhưng vẫn KHÔNG gộp nhiều UC khác key vào 1 commit. Xem memory `feedback_no_commit_without_jira` cho lý do quy tắc gốc.
5. Tuân thủ dependency rule của repo: `Domain → SharedKernel` only; `Application → Domain + SharedKernel + Contracts`; `Infrastructure → Application + EF/Redis`; không có project Orders nào được reference trực tiếp project khác ngoài Contracts.
6. Mọi task: TDD (RED → GREEN → REFACTOR), coverage ≥ 80%, `Result<T>` pattern (không throw cho business rule), FluentValidation validator co-located, DTO là `record`, method async có suffix `Async`, interface có prefix `I`.

---

## 6. Tham chiếu

- `CLAUDE.md` (root repo) — coding convention, module registration pattern, dependency rules.
- `docs/01-requirements-spec.md` — FR-ORD-001 → FR-ORD-0xx (lưu ý: đánh số khác với UC-ORD trong yêu cầu gốc, xem Q2).
- `docs/02-system-architecture.md` — SignalR design, caching strategy.
- `docs/03-database-schema.md` — DDL `orders`, `order_items`, `scheduled_orders`, `order_groups`; cần đối chiếu khi thêm bảng mới ở §3.
- `docs/04-api-design.md` — endpoint spec gốc (có phần đã lệch theo Q1, lấy quyết định trong tài liệu này làm chuẩn).
- Memory: `project_payment_model`, `feedback_no_commit_without_jira`, `feedback_commit_format`.

---

## 7. Nhật ký tiến độ

| Ngày | Việc | Trạng thái |
|---|---|---|
| 2026-06-17 | Task #1 (Phase 0 — Domain foundation + module wiring) | **completed** — reviewer pass sau 1 vòng fix (HIGH finding: thiếu FK constraint, đã sửa qua migration `migrationBuilder.Sql`) |
| 2026-06-17 | Jira key đã có cho toàn bộ 21 UC (xem §0) | Áp dụng — commit theo UC, không còn giữ ở working tree vô thời hạn |
| 2026-06-17 | Task #2 (Phase 1 — Draft/Cart UC-ORD-01..05) | **completed** — reviewer pass sau 1 vòng fix (HIGH: EF tracking khi add item vào order đã tồn tại; HIGH: duplicate `marketProductId` vượt tồn kho trong cùng order; MEDIUM: Orders DTO status/paymentStatus trả `snake_case` string). `dotnet test tests/Unit/FreshFlow.Orders.UnitTests/FreshFlow.Orders.UnitTests.csproj --no-restore` pass 95/95; `dotnet test --no-restore` pass toàn repo |
| 2026-06-17 | Task #3 (Phase 2a — Credit schema + AR endpoints) | **completed** — reviewer pass sau 1 vòng fix (HIGH: migration cần raw SQL FK tới `restaurants`/`orders`; MEDIUM: credit save concurrency phải map về `OPTIMISTIC_CONCURRENCY_CONFLICT`; LOW: format/encoding migration và `restaurant_id` PK không generated). Implemented `restaurant_credit`, `credit_transactions`, `ICreditService`, credit read endpoints và admin settle endpoint. `dotnet test tests/Unit/FreshFlow.Orders.UnitTests/FreshFlow.Orders.UnitTests.csproj --no-restore` pass 131/131; `dotnet ef migrations has-pending-model-changes --project src/FreshFlow.Infrastructure.Persistence --startup-project src/FreshFlow.API` no changes; `dotnet test --no-restore` pass toàn repo; `dotnet format FreshFlow.slnx --verify-no-changes` pass |
| 2026-06-18 | Re-review Task #2 + #3 sau khi codex đụng code | **passed** — reviewer xác nhận lại từ đầu, không phát sinh CRITICAL/HIGH mới ngoài 1 MEDIUM (xem dòng dưới) |
| 2026-06-18 | MEDIUM fix: `CreditService`/`CreditRepository` first-insert race (2 request charge đầu tiên cùng insert PK `restaurant_id` → `DbUpdateException` không kiểm soát thay vì 409) | **completed** — fix ở `CreditRepository.SaveChangesAsync`: catch `DbUpdateException` khi inner exception là `Npgsql.PostgresException` với `SqlState == "23505"`, map sang `CreditConcurrencyException` → `OPTIMISTIC_CONCURRENCY_CONFLICT` (409), mirror pattern `MarketProductRepository.AddAndSaveAsync` (Pricing). Thêm `Npgsql.EntityFrameworkCore.PostgreSQL` PackageReference trực tiếp cho `Orders.Infrastructure` + `InternalsVisibleTo` cho test project. Test mới ở `tests/Unit/FreshFlow.Orders.UnitTests/Persistence/CreditRepositorySaveChangesTests.cs` (3 case cho helper `IsUniqueViolation`). Phát hiện phụ trong lúc fix: `RestaurantCredit` luôn tạo mới với `creditLimit=0`, chưa có cách set limit > 0 → tách thành task riêng (xem dòng dưới). `dotnet test tests/Unit/FreshFlow.Orders.UnitTests/FreshFlow.Orders.UnitTests.csproj --no-restore` pass 134/134; build/format/EF check pass |
| 2026-06-18 | Bổ sung: Admin endpoint set `restaurant_credit.credit_limit` (gap phát hiện khi fix MEDIUM trên — chặn toàn bộ luồng confirm B2B credit vì `ChargeAsync` luôn fail `CREDIT_LIMIT_EXCEEDED` khi limit=0) | **completed** — thêm `RestaurantCredit.SetCreditLimit(decimal)` (domain, validate `newLimit >= 0` và `newLimit >= OutstandingBalance`), `ICreditService.SetCreditLimitAsync` (tạo account nếu chưa có, ghi `credit_transactions` type `Adjustment` với `Amount = |newLimit - oldLimit|` khi có thay đổi thực sự, skip ghi ledger nếu limit không đổi), `SetRestaurantCreditLimitCommand`/Handler/Validator (FluentValidation `CreditLimit >= 0`), endpoint `PUT /api/v1/admin/restaurants/{id}/credit/limit` `[Authorize(Roles="admin")]` trong `AdminController`. Test mới: 4 domain test (`RestaurantCreditTests.SetCreditLimit_*`) + 6 service test (`CreditServiceTests.SetCreditLimit_*`). Không cần migration (dùng lại cột `credit_limit` đã có). `dotnet test tests/Unit/FreshFlow.Orders.UnitTests/FreshFlow.Orders.UnitTests.csproj --no-restore` pass 144/144; `dotnet test FreshFlow.slnx --no-restore` pass toàn repo (Catalog 136/136, Auth 347/347, Integration 105/105, Pricing 247/247); `dotnet ef migrations has-pending-model-changes` no changes; `dotnet format FreshFlow.slnx --verify-no-changes` pass |
| 2026-06-18 | Re-review lại Task #2 + #3 (lý do: tool ngoài team "codex" đã đụng code task #2/#3 sau vòng review trước, không tin được kết quả cũ) | **PASS cả hai** — reviewer review lại từ đầu trên working tree hiện tại, KHÔNG dựa nhật ký cũ. `dotnet build FreshFlow.slnx` 0 errors (11 warning EFCore version-conflict pre-existing, không liên quan Orders); Orders 131/131, Pricing 247/247, Auth 347, Catalog 136, Integration 105 pass; `ef migrations has-pending-model-changes` no changes; `dotnet format --verify-no-changes` clean; coverage business code >80%. Xác nhận các fix cũ còn nguyên (EF tracking, duplicate marketProductId, DTO status; credit FK raw SQL, OPTIMISTIC_CONCURRENCY_CONFLICT, ownership/IDOR check, admin settle có `[Authorize(Roles=admin)]`). B2B credit/DB-only/over-allocation đúng §2. `Cors.AllowedOrigins` trong appsettings.Development.json do commit `bfae3fb` (SCRUM-192) thêm vào, không liên quan business logic Orders — verdict reviewer: GIỮ NGUYÊN, là CORS dev config hợp lệ (port frontend local phổ biến 4200/3000/5500/5501 để test thủ công Orders endpoint), không phải secret/leak, KHÔNG cần commit revert riêng; chỉ nhắc coder diff cẩn thận trước commit. `JWT.Key` không đụng (thay đổi cố ý của supervisor). Phát hiện 1 MEDIUM mới → tách task fix riêng. |
| 2026-06-18 | MEDIUM: race first-charge insert ở `CreditService` (SCRUM-193) | **completed (committed `be72d7d`)** — `GetAccountOrDefaultAsync`/`EnsureTrackedAccountAsync` (`CreditService.cs` ~L133-151): 2 request charge đồng thời cho restaurant chưa có dòng `restaurant_credit` đều thấy null → cùng `AddAccountAsync` → unique PK violation ném `DbUpdateException` (không phải `DbUpdateConcurrencyException`), mà `CreditRepository.SaveChangesAsync` (~L41-52) chỉ bắt `DbUpdateConcurrencyException` → leak 500. KHÔNG nằm trong waiver over-allocation §2 (đó là race `market_products` ở Pricing, không phải insert PK ở Orders). Fix: bắt thêm `DbUpdateException` unique-violation → map `CreditConcurrencyException` (409 OPTIMISTIC_CONCURRENCY_CONFLICT), hoặc `INSERT ... ON CONFLICT DO NOTHING` + re-fetch; thêm unit test first-insert race (test cũ `CreditServiceTests.cs:99` chỉ cover update-conflict). |
| 2026-06-18 | GAP phát hiện khi fix MEDIUM: chưa có cách set `CreditLimit > 0` | **new task (TaskList #5)** — `RestaurantCredit` luôn tạo với `creditLimit=0` (`CreditService.cs:143`, `GetRestaurantCreditQueryHandler.cs:30`), không có endpoint/seed nào set limit dương → `ChargeAsync` LUÔN fail `CREDIT_LIMIT_EXCEEDED`, chặn toàn bộ luồng B2B credit confirm. Là **tiền đề cho Task #4 (Phase 2b confirm)** vì confirm cần `CanChargeAsync` pass được. Cần admin endpoint set/adjust `credit_limit` (`[Authorize(Roles=admin)]`, validate >= 0, ghi `credit_transactions` type `adjustment`, optimistic-concurrency safe) và/hoặc seed; đối chiếu shape với `docs/04-api-design.md` §3.A. **Sequence: làm #5 TRƯỚC Phase 2b**, KHÔNG gộp vào hotfix MEDIUM hiện tại. |
| 2026-06-18 | Fix MEDIUM committed `be72d7d` (`fix(orders): SCRUM-193 ...`) | **reviewer PASS (tự chạy lại)** — chọn option (a): `CreditRepository.SaveChangesAsync` catch `DbUpdateException` khi `IsUniqueViolation` (PostgresErrorCodes.UniqueViolation `23505`), đặt SAU catch `DbUpdateConcurrencyException` (đúng thứ tự kế thừa) → `CreditConcurrencyException` → 409 OPTIMISTIC_CONCURRENCY_CONFLICT, mirror `MarketProductRepository`. Thêm Npgsql package ref (version pin sẵn 10.0.2) + `InternalsVisibleTo` + test `CreditRepositorySaveChangesTests.cs` (3 case: 23505→true, FK violation→false, non-Postgres inner→false). `CreditService.cs` KHÔNG đổi (fix ở save-layer). Tests 869/869 pass (Orders 134, Pricing 247, Auth 347, Catalog 136, Integration 105); build 0 errors; ef no pending; format clean. Commit chỉ stage file team sửa; `appsettings.Development.json` (JWT.Key cố ý của supervisor) KHÔNG commit, để nguyên ở working tree. |
| 2026-06-18 | Supervisor duyệt sequencing | **approved** — làm Task #5 (admin set credit limit) TRƯỚC Task #4 (Phase 2b Confirm) vì set limit là tiền đề test luồng confirm B2B credit. Task #5 dùng key **SCRUM-193** (gộp vào Credit, không key riêng). Endpoint chốt: `PUT /api/v1/admin/restaurants/{id}/credit/limit`, `[Authorize(Roles=admin)]`, validate `limit >= 0`, ghi `credit_transactions` type `adjustment`, optimistic-concurrency safe. |
| 2026-06-18 | Task #5 (admin set credit limit, SCRUM-193) | **reviewer PASS, đã commit `efbb8f5`** (`feat(orders): SCRUM-193 ...`) — domain `RestaurantCredit.SetCreditLimit` (reject âm; reject hạ xuống dưới `OutstandingBalance` → `CREDIT_LIMIT_BELOW_OUTSTANDING_BALANCE`), `ICreditService.SetCreditLimitAsync`, command/handler/validator `SetRestaurantCreditLimit` (limit>=0, note<=500), endpoint `PUT /api/v1/admin/restaurants/{restaurantId}/credit/limit` `[Authorize(Roles=admin)]`. Tạo row mới đi qua first-insert path SCRUM-193, ghi ledger `Adjustment` (Amount=|delta|, skip nếu không đổi), tái dùng cột `credit_limit` (no migration). 10 test mới, Orders 144/144, toàn repo 879/879 pass, build/ef/format xanh. Chỉ commit file team-authored; appsettings KHÔNG commit. **1 LOW (không block):** dòng ledger Adjustment ghi Amount=magnitude delta + BalanceAfter=outstanding (không đổi) → 1 dòng không tự suy ra limit mới/chiều tăng-giảm, phụ thuộc `note` tự do; có thể bổ sung structured field/auto-note ở phase sau nếu cần audit trail tốt hơn. `docs/04-api-design.md` KHÔNG có nội dung credit (doc có trước khi chốt B2B credit) nên không xung đột. |
| 2026-06-18 | Quy tắc cutoff/scheduledFor cho UC-ORD-07 (SCRUM-196) — supervisor chốt | **decision** — Nhà hàng chọn ngày giao trong window 7 ngày (D..D+7, D=ngày local Asia/Ho_Chi_Minh khi confirm). Ngày sớm nhất hợp lệ: confirm TRƯỚC 22:00 → D+1; confirm SAU 22:00 → D+2. Chuẩn hoá `requestedScheduledFor`: trong [earliestValid, D+7] → giữ nguyên; < earliestValid hoặc null → đẩy lên earliestValid; > D+7 → lỗi `DELIVERY_DATE_OUT_OF_WINDOW` (422, không fallback ngầm — leader chốt). Phân chia: upper-bound D+7 + không-quá-khứ = FluentValidation ở mọi entry point nhận ScheduledFor (CreateDraftOrder + ConfirmOrder); earliest-day phụ thuộc cutoff (D+1/D+2) + normalize-up = `OrderCutoffScheduler.ResolveScheduledFor` lúc confirm (KHÔNG hard-reject cutoff-relative trong validator vì mốc dịch lúc 22:00). Sửa code: scheduler hiện dùng D+1 vô điều kiện → phải D+2 khi past cutoff + normalize too-early/null. |
| 2026-06-18 | Lưu ý kỹ thuật khi implement quy tắc trên: `DELIVERY_DATE_OUT_OF_WINDOW` KHÔNG thể nằm ở FluentValidation layer | **finding** — `ValidationBehavior` (Orders) ném `FluentValidation.ValidationException`, global exception handler ở `Program.cs:164-185` LUÔN chuẩn hoá mọi lỗi validator thành `400 VALIDATION_ERROR` cố định (bỏ qua `WithErrorCode` tuỳ chỉnh) — cơ chế đã có từ trước, không phải bug mới. Để đạt đúng `422 DELIVERY_DATE_OUT_OF_WINDOW` như spec, check phải nằm ở **handler level** (`Result.Failure(Error.Validation(...))`), giống pattern `RESTAURANT_NOT_APPROVED`/`INVALID_PRODUCT`. Áp dụng cho cả `CreateDraftOrderCommandHandler` (check `request.ScheduledFor` nếu có) và `ConfirmOrderCommandHandler` (check sau khi resolve `rescheduledFor` qua scheduler, dùng `confirmedAtUtc` làm mốc "now"). |
| 2026-06-18 | Task #6/Phase 2b (Confirm order + cutoff + price snapshot, UC-ORD-06/07/08) | **completed, chờ reviewer** — Domain: `Order.RescheduleFor(DateTime)` (mới, reject khi `Cancelled`/`Delivered` → `ORDER_CANNOT_RESCHEDULE`). Application: `OrderCutoffScheduler` (mới, `Services/`) với `ResolveScheduledFor` (D+1/D+2 theo cutoff 22:00 Asia/Ho_Chi_Minh, normalize null/too-early lên earliestValid) và `IsWithinDeliveryWindow` (D+7 upper-bound + no-past-date, dùng ở cả 2 handler). `ConfirmOrderCommand`/Handler/Validator (mới, `Commands/ConfirmOrder/`): load order (404) → check ownership (403 FORBIDDEN) → `ICreditService.CanChargeAsync` (422 nếu vượt limit) → resolve cutoff + check D+7 (422 DELIVERY_DATE_OUT_OF_WINDOW) → `order.RescheduleFor` nếu cần → `order.Confirm()` (lock giá, domain event) → `ICreditService.ChargeAsync` (charge công nợ, cùng AppDbContext/transaction với order vì 2 repository share 1 scoped DbContext — KHÔNG gọi `orderRepository.SaveChangesAsync` riêng, để `ChargeAsync`'s save commit cả hai cùng lúc). `CreateDraftOrderCommandHandler` thêm check D+7/no-past-date cho `ScheduledFor` input (422, trước bước resolve product). Endpoint `POST /api/v1/orders/{orderId}/confirm` trong `OrdersController`. `ErrorExtensions` thêm mapping `INVALID_CREDIT_LIMIT`/`CREDIT_LIMIT_BELOW_OUTSTANDING_BALANCE`/`DELIVERY_DATE_OUT_OF_WINDOW` → 422, `ORDER_CANNOT_RESCHEDULE` → 409 (thiếu từ task #5, bổ sung luôn). 27 test mới (2 domain RescheduleFor, 11 OrderCutoffScheduler, 7 ConfirmOrderCommandHandler, 3 ConfirmOrderCommandValidator, 3 CreateDraftOrder handler D+7/past-date, 4 ErrorExtensions). Orders 171/171, toàn repo pass (Catalog 136, Auth 351, Integration 105, Pricing 247). Không cần migration. Auto-batch đầy đủ (FR-ORD-005) KHÔNG làm ở task này — chỉ phần cutoff/`scheduledFor`, đúng audit §5 rủi ro #1. |
| 2026-06-18 | Ràng buộc pipeline: error code riêng phải ở HANDLER, không phải FluentValidation | **note kỹ thuật** — `ValidationBehavior` + global exception handler (`Program.cs`) chuẩn hoá MỌI lỗi FluentValidation thành `400 VALIDATION_ERROR` (code cố định, chi tiết ở `details[]`), bỏ qua `WithErrorCode`. Vì vậy `DELIVERY_DATE_OUT_OF_WINDOW` (422) và các code nghiệp vụ riêng phải trả ở handler qua `Result.Failure(Error.Validation(code, ...))` + map ErrorExtensions, giống `INVALID_PRODUCT`/`INSUFFICIENT_STOCK`/`RESTAURANT_NOT_APPROVED`. Áp dụng: check D+7/no-past-date ở `CreateDraftOrderCommandHandler` và `ConfirmOrderCommandHandler` (sau load order, so với now), KHÔNG ở validator. Sửa lại chỉ đạo trước đó của leader (ban đầu nói để ở FluentValidation — đảo lại sau khi coder phát hiện ràng buộc pipeline). |
| 2026-06-18 | Codex review Task #6/Phase 2b | **in_progress** — bắt đầu review lại working tree hiện tại trước khi commit; sẽ kiểm tra code path confirm/cutoff/credit charge/transaction boundary, file ngoài scope (`appsettings.Development.json`), chạy Orders/full tests và cập nhật verdict sau |
| 2026-06-18 | Codex review fix Task #6/Phase 2b | **in_progress** — phát hiện MEDIUM precedence bug: `ConfirmOrderCommandHandler` gọi `ICreditService.CanChargeAsync` trước domain confirm validation, nên draft rỗng (`TotalAmount=0`) có thể trả `INVALID_AMOUNT` thay vì `ORDER_EMPTY` với implementation thật. Fix: thêm `Order.CanConfirm()` không mutate, handler check `CanConfirm()` trước credit; chuyển `orderRepository.Track(order)` lên trước `CreditService.ChargeAsync` để save của credit service commit order+credit cùng scoped `AppDbContext`, không mark modified sau save. Thêm regression tests; Orders unit pass 174/174. Đang chạy full verification |
| 2026-06-18 | Codex review Task #6/Phase 2b verdict | **reviewer PASS, committed** — MEDIUM precedence bug đã fix; giữ nguyên scope DB-only/B2B credit/cutoff, không làm auto-batch đầy đủ. Commit subject: `feat(orders): SCRUM-193 implement order confirmation flow` (body ghi đủ SCRUM-193/196/197). Verification: Orders unit pass 174/174; `dotnet ef migrations has-pending-model-changes --project src/FreshFlow.Infrastructure.Persistence --startup-project src/FreshFlow.API` no changes; `dotnet format FreshFlow.slnx --verify-no-changes` pass; `dotnet test FreshFlow.slnx --no-restore` pass toàn repo. `src/FreshFlow.API/appsettings.Development.json` vẫn modified ngoài scope nên KHÔNG stage/commit |
| 2026-06-18 | Task #5/Phase 3 (Order viewing + history, UC-ORD-12/13/20) | **in_progress** — bắt đầu implement REST-only order list/detail/history sau Task #6 commit `9349336`; scope: `GET /api/v1/orders` phân trang + lọc ngày/trạng thái, `GET /api/v1/orders/{orderId}` detail, history dùng chung list filter; enforce restaurant ownership qua `IRestaurantReader`; admin xem được mọi restaurant. `src/FreshFlow.API/appsettings.Development.json` đang modified ngoài scope nên không stage/commit |
| 2026-06-18 | Task #5/Phase 3 implementation update | **in_progress** — đã wire application/infrastructure/API skeleton: `IOrderRepository.SearchAsync(OrderSearchCriteria)` filter DB theo `restaurantId/status/from/to`, sort `createdAt`, offset `page/pageSize/total`; thêm `ListOrdersQuery` + `GetOrderQuery`; `OrdersController` mở GET cho `admin,restaurant`, giữ create/cart/confirm chỉ `restaurant`; thêm alias `GET /api/v1/orders/history` dùng chung filter list. Chưa review/test xong, chưa commit |
| 2026-06-18 | Task #5/Phase 3 focused tests | **in_progress** — thêm unit coverage cho `ListOrdersQueryHandler` (restaurant ownership, forbidden filter khác restaurant, admin filter không lookup ownership, status alias, invalid status, `to` date-only normalize cuối ngày), `GetOrderQueryHandler` (404/403/owner/admin), validators list/detail, và `OrderRepository.SearchAsync` InMemory filter/sort/page. `dotnet test tests/Unit/FreshFlow.Orders.UnitTests/FreshFlow.Orders.UnitTests.csproj --no-restore` pass **200/200** (chỉ còn warning EF package version conflict sẵn có trong output). Đang review diff + chạy verification rộng |
| 2026-06-18 | Task #5/Phase 3 verdict | **reviewer PASS, ready to commit** — hoàn thành UC-ORD-12/13/20 REST-only: `GET /api/v1/orders` offset pagination + filter `restaurantId/status/from/to` + sort `createdAt`, `GET /api/v1/orders/{orderId}` detail, `GET /api/v1/orders/history` alias dùng chung filter; restaurant ownership enforced qua `IRestaurantReader`, admin bypass ownership; mutation endpoints vẫn `[Authorize(Roles=restaurant)]`. No schema/migration. Verification: Orders unit pass **200/200**; `dotnet format FreshFlow.slnx --verify-no-changes` pass; `dotnet ef migrations has-pending-model-changes --project src/FreshFlow.Infrastructure.Persistence --startup-project src/FreshFlow.API` no changes; `dotnet test FreshFlow.slnx --no-restore` pass toàn repo. `src/FreshFlow.API/appsettings.Development.json` vẫn modified ngoài scope nên KHÔNG stage/commit |
| 2026-06-18 | Task #5/Phase 3 commit | **committed `7339772`** — `feat(orders): SCRUM-205 add order viewing endpoints` (body ghi đủ SCRUM-205/UC-ORD-12, SCRUM-208/UC-ORD-13, SCRUM-228/UC-ORD-20). Sau commit code, working tree chỉ còn `src/FreshFlow.API/appsettings.Development.json` modified ngoài scope |
| 2026-06-18 | Task #6/Phase 4 (Cancellation + adjustment, UC-ORD-15/16/17) | **in_progress** — bắt đầu implement sau Task #5 commit. Scope theo audit: `PATCH /api/v1/orders/{orderId}/cancel` cho restaurant owner/admin, chỉ `DRAFT`/`CONFIRMED` trước `BATCHED`, lưu `cancellation_reason`, refund công nợ qua `ICreditService.RefundAsync` nếu order đã `CONFIRMED`; Operations Manager adjustment cập nhật `order_items.actual_quantity` khi thiếu/hư hỏng. Redis release reservation vẫn deferred Task #10. `src/FreshFlow.API/appsettings.Development.json` đang modified ngoài scope nên không stage/commit |
| 2026-06-18 | Task #6/Phase 4 focused tests | **in_progress** — implement cancellation/adjustment đã qua focused Orders unit test: `dotnet test tests/Unit/FreshFlow.Orders.UnitTests/FreshFlow.Orders.UnitTests.csproj --no-restore` pass **236/236**. Đang review diff kỹ và chạy verification rộng trước khi commit. `src/FreshFlow.API/appsettings.Development.json` vẫn modified ngoài scope nên không stage/commit |
| 2026-06-18 | Task #6/Phase 4 verdict | **reviewer PASS, ready to commit** — hoàn thành UC-ORD-15/16/17 DB-only: cancel endpoint `PATCH /api/v1/orders/{orderId}/cancel` cho restaurant owner/admin, chỉ `DRAFT`/`CONFIRMED`, lưu `cancelled_at`/`cancellation_reason`, refund AR qua `ICreditService.RefundAsync` nếu order đã `CONFIRMED`; adjustment endpoint `PATCH /api/v1/orders/{orderId}/items/{itemId}/actual-quantity` cho `admin,operations_manager`, validate item/status/quantity và trả `actualQuantity` trong DTO. Không làm Redis release/partial discrepancy schema/refund vì deferred Task #10/#8 theo audit. Verification: `git diff --check` clean; `dotnet test tests/Unit/FreshFlow.Orders.UnitTests/FreshFlow.Orders.UnitTests.csproj --no-restore` pass **236/236**; `dotnet format FreshFlow.slnx --verify-no-changes` pass; `dotnet ef migrations has-pending-model-changes --project src/FreshFlow.Infrastructure.Persistence --startup-project src/FreshFlow.API` no changes; `dotnet test FreshFlow.slnx --no-restore` pass toàn repo (**1078/1078**). `src/FreshFlow.API/appsettings.Development.json` vẫn modified ngoài scope nên KHÔNG stage/commit |
