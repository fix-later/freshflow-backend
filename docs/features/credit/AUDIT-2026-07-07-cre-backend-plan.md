# Báo cáo khảo sát & kế hoạch triển khai: CRE — B2B Credit, Debt & Statement

| | |
|---|---|
| **Ngày khảo sát** | 2026-07-07 |
| **Branch** | `SCRUM-254-CRE-B2B-Credit-Debt` |
| **Epic** | SCRUM-254 — B2B Credit, Debt & Statement |
| **Phạm vi** | Credit overview, ghi nợ khi xác nhận đơn, thanh toán công nợ, sao kê định kỳ, cảnh báo hạn mức — nằm TRONG module **Orders** (KHÔNG tạo module Credit mới) |
| **Tác giả** | Leader agent (team `backend-dev`), điều phối coder + reviewer qua supervisor |
| **Mục đích tài liệu** | Cung cấp cho AI coding agent đủ ngữ cảnh để implement epic CRE theo đúng quyết định đã chốt — không cần hỏi lại; là nguồn tham chiếu chính, KHÔNG chỉ TaskList |
| **Trạng thái** | Khảo sát xong; SCRUM-257/260/264/261/266 ✅ (261 = bản ALWAYS-LEDGER trong working tree; 266 = Option B stub log handler). SCRUM-269 HOÃN. |

> **Quy ước cập nhật tài liệu:** mỗi khi một task hoàn thành (reviewer pass) hoặc có quyết định/thay đổi giữa chừng, phải cập nhật lại file này — đặc biệt §4 (Task breakdown) và §6 (Nhật ký tiến độ). Đồng bộ với TaskList, không thay thế.

---

## 0. Bảng SCRUM key theo task

| Task | SCRUM Key | Loại | Trạng thái |
|---|---|---|---|
| Credit Overview & Transaction History | SCRUM-257 | Verify + polish | ✅ Hoàn thành (reviewer đạt) |
| Record Order as Credit | SCRUM-260 | Verify (đã implement) | ✅ Hoàn thành (reviewer đạt) |
| Record Debt Payment | SCRUM-264 | Verify + polish | ✅ Hoàn thành (reviewer-2 PASS sau fix) |
| Periodic Statement | SCRUM-261 | Net-new (lớn nhất) | ✅ Hoàn thành (bản ALWAYS-LEDGER là bản shipped trong tree) |
| Alert Credit Limit | SCRUM-266 | Net-new | ✅ Hoàn thành — Option B (credit-scoped, stub log handler, defer Notifications persist) |
| ~~SCRUM-269~~ | SCRUM-269 | — | ⛔ HOÃN — bỏ qua đợt này |

**Quy ước commit:** `feat(orders): SCRUM-XXX <description>` — một commit (hoặc nhóm nhỏ) ứng với một task, dùng đúng key của task đó. KHÔNG commit khi chưa được supervisor cấp/xác nhận key (giữ trong working tree).

---

## 1. Hiện trạng repo tại thời điểm khảo sát

Credit ĐÃ có nền tảng vững trong module Orders — **không rebuild**, chỉ mở rộng.

### Domain (`Orders.Domain`)
- `Entities/RestaurantCredit.cs` — aggregate: `RestaurantId`, `CreditLimit`, `OutstandingBalance`, `UpdatedAt`, `AvailableCredit = CreditLimit - OutstandingBalance`. Methods: `CanCharge`, `Charge`, `Settle`, `Refund`, `SetCreditLimit` (chặn set dưới outstanding). Không throw cho business rule ở service layer (service map sang `Result`).
- `Entities/CreditTransaction.cs` — append-only: `Id, RestaurantId, OrderId?, Type, Amount, BalanceAfter, Note, PaymentMethod?, Reference?, CreatedAt` (2 field cuối thêm ở SCRUM-264, chỉ hợp lệ cho type Settlement). **`CreatedAt` chính là `recorded_at`.**
- `Enums/CreditTransactionType.cs` — `Charge=1, Settlement=2, Refund=3, Adjustment=4`.

### Application (`Orders.Application`)
- `Services/CreditService.cs` — `CanChargeAsync, ChargeAsync, RefundAsync, SettleAsync, SetCreditLimitAsync`. Dùng `Result<T>`, optimistic concurrency qua `CreditConcurrencyException` → `Error.Conflict`.
- `Abstractions/` — `ICreditService`, `ICreditRepository`, `CreditConcurrencyException`.
- `Commands/SetRestaurantCreditLimit/`, `Commands/SettleRestaurantCredit/` — command + handler + validator.
- `Queries/GetRestaurantCredit/`, `Queries/GetCreditTransactions/` — query + handler + validator.
- `Dtos/` — `RestaurantCreditDto`, `CreditTransactionDto`, `CreditCheckDto`, `CreditDtoMapper` (snake_case type).

### Infrastructure (`Orders.Infrastructure`)
- `Repositories/CreditRepository.cs` — EF; `GetTransactionsPageAsync` (SCRUM-257: đã thay cho `GetTransactionsAsync` unbounded cũ) — cursor-paginated, order `CreatedAt DESC, Id DESC`, filter `BalanceMovingTypes`.
- `Persistence/Configurations/{RestaurantCredit,CreditTransaction}Configuration.cs`.

### API
- `Controllers/RestaurantCreditController.cs` — `[Authorize(Roles="admin,restaurant")]`, route `api/v1/restaurants/{restaurantId:guid}/credit`. Actions: `GET` (credit), `GET transactions`.

### Persistence
- Migration `20260617161316_AddRestaurantCredit` đã tồn tại.

### Pattern tham chiếu để tái sử dụng
- **Cursor pagination:** `Pricing.Application/Queries/GetPriceChangeHistory/` + `Dtos/PriceHistoryPageDto.cs` + repo method. Envelope: `ApiResponse.OkPaged(data, pageSize, nextCursor)` (đã có sẵn). Cursor = base64 của `(CreatedAt, Id)`.
- **Background job:** `PartitionMaintenanceJob` (hosted service) — mẫu cho job sao kê hàng tháng.
- **Domain event → Integration event:** xem CLAUDE.md; cross-module CHỈ qua `FreshFlow.Contracts`, MediatR `INotificationHandler<T>` hai đầu.

---

## 2. Ba quyết định đã chốt (KHÔNG hỏi lại)

1. **Charge-at-confirm** — ghi nợ lúc XÁC NHẬN đơn (`ConfirmOrderCommandHandler` → `CreditService.ChargeAsync`), KHÔNG phải lúc giao hàng.
2. **Statement bất biến** — sao kê là snapshot append-only; tính MỘT LẦN khi generate rồi đóng băng, KHÔNG tính lại mỗi lần xem.
3. **Credit nằm trong module Orders** — không tách module riêng.

### Quyết định thiết kế bổ sung (leader, 2026-07-07)

- **DEC-CRE-01 — `credit_transactions` là ledger số dư thuần.** Hiện `SetCreditLimitAsync` ghi một dòng `Adjustment` mỗi lần đổi hạn mức (amount = |newLimit − previousLimit|, balanceAfter KHÔNG đổi) → làm nhiễu ledger. Quyết định: **ngừng ghi dòng Adjustment trong `SetCreditLimitAsync`**; giữ enum value. Bổ sung filter đọc chỉ lấy loại dịch chuyển số dư (Charge/Settlement/Refund) để mọi dòng Adjustment cũ (nếu có) không lộ ra. Nếu sau này cần audit đổi hạn mức → bảng audit riêng (SCRUM khác, YAGNI hiện tại). Quyết định này làm SCRUM-261 (statement "chỉ tính giao dịch dịch chuyển số dư") sạch và nhất quán.
- **DEC-CRE-02 — Pagination theo cursor** (không offset) để nhất quán với convention repo (`OkPaged`, price history).
- **DEC-CRE-03 — Timezone sao kê = `Asia/Ho_Chi_Minh`.** Ranh giới kỳ (đầu/cuối tháng) tính theo giờ VN rồi lưu UTC.
- **DEC-CRE-04 — Cơ chế phát alert 266 = biến `RestaurantCredit` thành `AggregateRoot`.** Khảo sát 260 xác nhận `RestaurantCredit` hiện là entity thuần, không raise được domain event (khác `Order`/`AggregateRoot`). Quyết định cho SCRUM-266: cho `RestaurantCredit` kế thừa `AggregateRoot`, thêm field trạng thái ngưỡng đã cảnh báo (vd `LastAlertedThreshold`) để chống spam, và raise `CreditLimitThresholdReachedDomainEvent` NGAY TRONG `Charge()` khi vượt ngưỡng mới. Đây là cách nhất quán với pattern FreshFlow (domain event raise trên aggregate → `INotificationHandler` trong Orders publish integration event sang `FreshFlow.Contracts` → Notifications consume). KHÔNG raise event rời rạc từ `CreditService`/handler. Cần migration nhỏ cho field trạng thái ngưỡng khi làm 266.
- **DEC-CRE-05 (REVISED 2026-07-08) — RBAC statement + guard kỳ đã đóng:** cả 3 endpoint generate/get/list = admin HOẶC chủ nhà hàng, đặt chung ở `RestaurantCreditController` (class-level `[Authorize(Roles="admin,restaurant")]`). Chấp nhận đề xuất của coder-2: generate idempotent (sinh lại chỉ trả bản có sẵn) + có kiểm tra ownership (không IDOR) → cho restaurant tự sinh sao kê của chính mình là hợp lý cho 'on-demand'. **BẮT BUỘC (quan trọng hơn ai được gọi):** validator generate phải TỪ CHỐI mọi kỳ CHƯA kết thúc — chỉ cho sinh kỳ đã trôi qua hoàn toàn (`PeriodEnd ≤ now` theo giờ VN). Vì statement bất biến (DEC + idempotent), nếu sinh nhầm tháng hiện tại/tương lai sẽ đóng băng snapshot dở dang và job cuối tháng sẽ trả về bản dở đó. Áp dụng cho CẢ admin lẫn owner.
- **DEC-CRE-06 — Cách tính opening/closing (từ ledger BalanceAfter):** `opening` = `BalanceAfter` của giao dịch dịch chuyển số dư gần nhất có `CreatedAt < PeriodStart` (không có → 0). Line items = các giao dịch dịch chuyển số dư `PeriodStart ≤ CreatedAt < PeriodEnd`. `TotalCharges/Settlements/Refunds` = tổng theo type. `closing` = `opening + TotalCharges − TotalSettlements − TotalRefunds`. Chỉ Charge/Settlement/Refund (bỏ Adjustment) — nhất quán DEC-CRE-01.
- **DEC-CRE-07 — Job hàng tháng = tick idempotent:** `BackgroundService` (mẫu `ScheduledOrderGenerationHostedService`), config-gated enable + interval; mỗi tick tính 'tháng trước' theo giờ VN (resolver như `OrderCutoffScheduler`, fallback `SE Asia Standard Time`), duyệt mọi credit account, gửi `GenerateStatementCommand` (idempotent → chạy lại là no-op). Không cần cron chính xác.

---

## 3. Rủi ro & bề mặt bảo mật

- **RBAC:** mọi endpoint credit phải giữ kiểm tra admin HOẶC đúng chủ nhà hàng (pattern có sẵn trong `GetCreditTransactionsQueryHandler`). IDOR nếu bỏ sót.
- **Idempotency (SCRUM-260):** confirm lặp KHÔNG được double-charge → cần guard theo OrderId / trạng thái đơn.
- **Atomicity (SCRUM-260):** chuyển trạng thái đơn + ghi nợ commit chung một transaction.
- **Anti-spam (SCRUM-266):** chỉ phát alert lần đầu vượt ngưỡng; re-arm khi tụt dưới ngưỡng.
- **Bất biến statement (SCRUM-261):** chặn tạo trùng (restaurantId, kỳ); regenerate trả bản đã có.
- **Migration:** tạo migration nhưng KHÔNG apply lên DB thật, KHÔNG commit khi chưa có key.

---

## 4. Phân rã task

### SCRUM-257 — Credit Overview & Transaction History (verify + polish) — ✅ hoàn thành
- Cursor pagination + lọc theo kỳ cho `GET .../credit/transactions` (theo mẫu `GetPriceChangeHistory` nguyên bản).
  - Query thêm `Cursor?, PageSize=50, From?, To?`; validator `PageSize ∈ [1,200]`, `From ≤ To`.
  - DTO mới `CreditTransactionPageDto(Items, PageSize, NextCursor)`.
  - Repo: thay `GetTransactionsAsync` bằng method phân trang (from/to/cursor/pageSize), lấy `PageSize+1` để tính NextCursor; giữ order `CreatedAt DESC, Id DESC`.
  - Controller: parse query params, trả `ApiResponse.OkPaged`.
- DEC-CRE-01: ngừng ghi Adjustment trong `SetCreditLimitAsync` + filter đọc chỉ loại dịch chuyển số dư.
- Giữ RBAC. TDD ≥80%.
- **Đã implement & reviewer pass** (2026-07-07): `GetTransactionsAsync` → `GetTransactionsPageAsync` (cursor base64 `(Id, CreatedAt)`, cùng scheme `SnapshotCursor` bên Pricing), `CreditTransactionPageDto` mới, filter `BalanceMovingTypes` (Charge/Settlement/Refund) trong repo, `SetCreditLimitAsync` ngừng ghi dòng Adjustment. Test mới: `CreditRepositoryPageSizeTests`, `CreditRepositoryLedgerFilterTests`; cập nhật `GetCreditTransactionsQueryHandlerTests`, `CreditServiceTests`, `PersistenceConfigurationTests`.

### SCRUM-260 — Record Order as Credit (verify) — ✅ hoàn thành
- Verify idempotency (confirm lặp charge một lần) + atomicity (đơn + charge chung transaction).
- Để lại seam sạch cho alert 266 (aggregate lộ balance/limit post-charge để raise domain event ở 266). KHÔNG implement alert ở đây.
- **Khảo sát (2026-07-07):** cơ chế đã ĐỦ, không cần sửa production code cho idempotency/atomicity — chỉ viết test chứng minh:
  - Idempotency: `Order.CanConfirm()` đã chặn confirm lần 2 (`Status != Draft` → `ORDER_NOT_DRAFT`, trả về TRƯỚC khi gọi `ChargeAsync`).
  - Atomicity: `ConfirmOrderCommandHandler` dùng chung một `AppDbContext` scoped cho `OrderRepository.Track(order)` và `CreditRepository` — `ChargeAsync` chỉ gọi `SaveChangesAsync` MỘT LẦN, cộng với `RestaurantCredit.UpdatedAt` là concurrency token → xung đột đồng thời ném `CreditConcurrencyException` thay vì double-charge âm thầm.
  - Test mới: `ConfirmOrderCommandHandlerTests.Handle_CalledTwiceForSameOrder_ChargesCreditOnlyOnceAsync` (idempotency ở tầng handler); `ConfirmOrderAtomicityTests` (3 test, DbContext InMemory thật, bypass `RestaurantReader` vì nó dùng `ToSqlQuery` không chạy được trên InMemory) — chứng minh (a) order + ledger commit cùng nhau qua 1 `SaveChangesAsync`, (b) xung đột đồng thời ném `CreditConcurrencyException` đúng cơ chế (rollback cross-entity thật sự phụ thuộc transaction ngầm của Npgsql, không kiểm chứng được bằng InMemory — đã ghi chú rõ trong test), (c) khi `ChargeAsync` tự fail ở bước re-check `CanCharge` nội bộ (trước khi gọi `SaveChangesAsync`) thì mutation `order.Confirm()` trong bộ nhớ KHÔNG bao giờ được flush xuống DB.
  - Seam cho 266: thêm comment trong `ConfirmOrderCommandHandler` ngay sau `ChargeAsync` thành công, chỉ rõ `chargeResult.Value` đã có `CreditLimit/OutstandingBalance/AvailableCredit` để 266 hook vào.
  - **Finding cho leader (chưa implement):** `RestaurantCredit` là entity thường, không phải `AggregateRoot` — không có cơ chế raise domain event. Nếu 266 cần domain event thay vì chỉ đọc DTO sẵn có, cần nâng cấp `RestaurantCredit` thành `AggregateRoot` hoặc raise event từ `CreditService`/handler.
  - Build 0 lỗi, format sạch, Orders unit tests 363/363 GREEN, reviewer đạt (không có phát hiện CRITICAL/HIGH).

### SCRUM-264 — Record Debt Payment (verify + polish) — ✅ hoàn thành
- Thêm `paymentMethod` (enum `bank_transfer | manual`) + `reference` (string?) vào `SettleRestaurantCreditCommand` + validator.
- Thêm cột `paymentMethod`/`reference` (nullable) vào `CreditTransaction` + EF config + migration; thread qua `SettleAsync`; lộ ra `CreditTransactionDto`; cập nhật endpoint body.
- **Đã implement (2026-07-07):** domain enum mới `Orders.Domain.Enums.PaymentMethod` (BankTransfer/Manual); `CreditTransaction` ctor nhận `paymentMethod?/reference?` tuỳ chọn, chặn (throw) nếu gán cho type khác Settlement; `CreditTransactionConfiguration` thêm cột nullable `payment_method`/`reference`; migration `20260707114548_AddCreditTransactionPaymentDetails` (tạo, CHƯA apply); `ICreditService.SettleAsync` + `CreditService.SettleAsync` + `SettleRestaurantCreditCommand`/Validator/Handler thread `paymentMethod` (required, `IsInEnum`) + `reference` (optional, max 200); `CreditTransactionDto` lộ `paymentMethod`/`reference` dạng snake_case string (tái dùng `ToSnakeCase` như `Type`); `AdminController.SettleRestaurantCreditAsync` parse string `"bank_transfer"|"manual"` từ request body, trả 400 VALIDATION_ERROR nếu sai. Test mới: `CreditTransactionTests` (4 test domain guard), validator/handler/service tests cập nhật + bổ sung. Build 0 lỗi, format sạch, Orders unit tests 371/371 GREEN.
- **Finding HIGH (reviewer-2, 2026-07-08 — ĐÃ FIX, PASS):** `HasConversion` cho `PaymentMethod` trong `CreditTransactionConfiguration.cs` dùng `.ToLowerInvariant()` → persist `"banktransfer"` xuống DB, KHÁC contract `"bank_transfer"` mà DTO mapper (`ToSnakeCase`) và `AdminController` parser dùng. Round-trip trong EF vẫn OK (Enum.Parse case-insensitive) nên test không bắt được, nhưng giá trị cột thô lệch contract → rủi ro khi Analytics/script query trực tiếp. Fix (coder-2): HasConversion `PaymentMethod` chuyển sang private `ToSnakeCase`/`FromSnakeCase` (switch/throw ngoài lambda vì expression tree không hỗ trợ switch/throw) — BankTransfer→"bank_transfer", Manual→"manual", khác → throw. Test mới trong `PersistenceConfigurationTests` gọi thẳng `ValueConverter.ConvertToProvider/ConvertFromProvider` assert literal "bank_transfer"/"manual" (đóng đúng blind-spot round-trip). `has-pending-model-changes` = no drift. Orders 375/375 GREEN. reviewer-2 PASS.

### SCRUM-261 — Periodic Statement (net-new, lớn nhất) — ✅ hoàn thành (ALWAYS-LEDGER)
- Domain: aggregate `CreditStatement` append-only (`PeriodStart/End`, `OpeningBalance`, `ClosingBalance`, `TotalCharges/Settlements/Refunds`, `GeneratedAt`) + `CreditStatementLine` snapshot (TransactionId, Type, Amount, BalanceAfter, OccurredAt, Note/Reference). Chỉ tính giao dịch dịch chuyển số dư.
- Application: `GenerateStatementCommand` (idempotent: 1 statement / (restaurant, kỳ)), `GetStatementQuery`, `ListStatementsQuery` (cursor paged, RBAC). Opening = closing kỳ trước.
- Infrastructure: EF config + repo + migration; đăng ký DI.
- Job: hosted service hàng tháng (Asia/Ho_Chi_Minh) sinh sao kê tháng trước cho mọi tài khoản active — theo mẫu `ScheduledOrderGenerationHostedService` (`PartitionMaintenanceJob` trong CLAUDE.md KHÔNG tồn tại). Xem DEC-CRE-07.
- API: generate / get / list.

### SCRUM-266 — Alert Credit Limit (net-new) — ✅ hoàn thành — Option B (user chốt)
- Ngưỡng: 80% = warning, ≥100% = exceeded (utilization = OutstandingBalance / CreditLimit).
- Chỉ phát lần đầu vượt ngưỡng; re-arm khi tụt dưới. Lưu trạng thái ngưỡng đã cảnh báo trên `RestaurantCredit`.
- Domain event (Orders) → Integration event trong `FreshFlow.Contracts` → Notifications `INotificationHandler` lưu notification. Hook vào charge path (dùng seam của 260).
- **DEC-CRE-04 (chốt sau khảo sát 260):** `RestaurantCredit` → `AggregateRoot`; lưu `LastAlertedThreshold` trên aggregate; raise `CreditLimitThresholdReachedDomainEvent` trong `Charge()` khi cross ngưỡng mới; re-arm khi tụt dưới. Cần migration nhỏ cho field trạng thái ngưỡng.
- **Khảo sát 2026-07-08 (blocker scope):** Module **Notifications RỖNG** — chỉ có 3 `.csproj` scaffold, KHÔNG file `.cs`, KHÔNG đăng ký trong `Program.cs` (chỉ Auth/Catalog/Pricing/Orders), bảng `notifications` = `[PLANNED]` trong docs/03. Integration event hiện có (`OrderConfirmedIntegrationEvent`) KHÔNG có consumer nào. ⇒ Phần 'Notifications persists a notification' của 266 = bootstrap cả module Notifications (entity + EF config + migration + DI + Program.cs) → escalate supervisor quyết định scope (Option A: bootstrap Notifications end-to-end; Option B — RECOMMEND: 266 chỉ tới integration event ở Contracts + stub handler log, defer Notifications persistence sang epic Notifications). Phần Orders-side (AggregateRoot + anti-spam + domain event + integration event contract + migration) làm ngay, không phụ thuộc quyết định này.
- **Gotcha base class:** `RestaurantCredit` hiện là class thuần; `AggregateRoot : BaseEntity` mang theo `Id/CreatedAt/UpdatedAt/DeletedAt`. Bảng `restaurant_credit` chỉ có `restaurant_id`(PK)/`credit_limit`/`outstanding_balance`/`updated_at`. Khi đổi base: dùng `UpdatedAt` của base (bỏ prop `UpdatedAt` tự khai của entity, giữ concurrency token), `Ignore(Id)/Ignore(CreatedAt)/Ignore(DeletedAt)` trong `RestaurantCreditConfiguration`; thêm cột mới `last_alerted_level` + migration.
- **Đã implement (2026-07-08):** `RestaurantCredit : AggregateRoot`, `LastAlertedLevel` anti-spam, `CreditLimitThresholdReachedDomainEvent`, translator sang `CreditLimitThresholdReachedIntegrationEvent`, migration `20260708075300_AddCreditAlertLevel`, EF ignore các field base không dùng, và `Notifications.Application` stub handler chỉ log + `AddNotificationsModule` trong API startup. Notifications persistence vẫn defer sang epic Notifications. Tests mới: domain threshold/re-arm, post-commit dispatch, translator, stub registration/publish log. Orders unit 464/464 GREEN; build solution 0 lỗi; EF no drift; format verify cục bộ sạch.

---

## 5. Ràng buộc chung

- TDD + convention FreshFlow: `.slnx`, `Result` pattern (no business-rule exceptions), record DTO, `Async` suffix, `I`-prefixed interface, FluentValidation co-located, MediatR.
- Module dependency rules: Domain→SharedKernel; Application→Domain+Contracts; cross-module chỉ qua `FreshFlow.Contracts`.
- Coder gửi kết quả cho **reviewer** (không phải leader). Reviewer pass → leader giao task kế.
- KHÔNG commit khi chưa có Jira key. `dotnet build-server shutdown` sau lệnh dotnet one-shot.

---

## 6. Nhật ký tiến độ

| Ngày | Sự kiện |
|---|---|
| 2026-07-07 | Khảo sát nền tảng credit trong Orders; chốt DEC-CRE-01/02/03; tạo backlog 5 task (SCRUM-257/260/264/261/266), bỏ 269. Giao SCRUM-257 cho coder. |
| 2026-07-07 | SCRUM-257 hoàn thành: cursor pagination + lọc theo kỳ cho `GET .../credit/transactions` (mẫu `GetPriceChangeHistory`), `CreditTransactionPageDto` mới, `SetCreditLimitAsync` ngừng ghi dòng `Adjustment` (DEC-CRE-01), repo filter chỉ Charge/Settlement/Refund. Build 0 lỗi, `dotnet format` sạch trên các file đã sửa, test suite GREEN toàn bộ (Pricing 264, Catalog 136, Assistant 64, Orders 359, Auth 361, Integration 119). Reviewer: không có phát hiện CRITICAL/HIGH, đạt. |
| 2026-07-07 | **SCRUM-260 ✅ reviewer PASS.** Verify-only (không sửa production code): idempotency qua `Order.CanConfirm()` (chặn non-Draft trước khi charge) + concurrency token `UpdatedAt` (OrderConfiguration.cs:43, không cần migration); atomicity qua chung 1 scoped `AppDbContext` → 1 `SaveChangesAsync` trong `ChargeAsync`. Test mới: `ConfirmOrderCommandHandlerTests.Handle_CalledTwiceForSameOrder_ChargesCreditOnlyOnceAsync`, `ConfirmOrderAtomicityTests` (gồm test chứng minh charge fail → order không persist Confirmed, 0 CreditTransaction). Seam 266 = comment-only. Orders unit 363/363 GREEN. Chốt DEC-CRE-04 (266 → RestaurantCredit thành AggregateRoot). Chưa commit. Kế tiếp: SCRUM-264. |
| 2026-07-08 | **SCRUM-264 ✅ reviewer-2 PASS (sau 1 vòng fix).** Ban đầu bị finding HIGH: `HasConversion` `PaymentMethod` dùng `.ToLowerInvariant()` → lưu `"banktransfer"` lệch contract `"bank_transfer"` (reviewer cũ bỏ sót, reviewer-2 mới bắt được). Fix: converter snake_case tường minh + test đọc giá trị cột thô. Orders unit 375/375 GREEN, migration không đổi (no drift), format sạch. Chưa commit. Kế tiếp: SCRUM-261 (đang làm) → SCRUM-266. |
| 2026-07-08 | **SCRUM-261 ✅ reviewer-2 PASS.** Net-new lớn nhất: `CreditStatement`/`CreditStatementLine` bất biến, `CreditStatementPeriodCalculator` (VN→UTC, start inclusive/end exclusive), `CreditStatementGenerationService` idempotent + guard kỳ chưa đóng (`periodEnd > UtcNow` → STATEMENT_PERIOD_NOT_CLOSED, chặn cả tháng hiện tại) + opening=closing kỳ trước (fallback net-movement) + xử lý race qua unique-index → re-fetch winner; RBAC admin-or-owner cả 3 endpoint + guard IDOR (get-by-id re-check RestaurantId → 404); `MonthlyCreditStatementHostedService` (mẫu ScheduledOrderGenerationHostedService). Migration `20260708005514_AddCreditStatements` (unique (restaurant_id, period_start), create-only, no drift). Orders unit 449/449 GREEN, format sạch. Chưa commit. Kế tiếp: SCRUM-266. |
| 2026-07-08 | LOW follow-ups (không chặn, ghi nhận từ review 261): (1) `CreditStatementLine` chưa snapshot `PaymentMethod` dù 264 đã thêm vào `CreditTransaction` — dòng Settlement đông cứng mất thông tin phương thức thanh toán; cân nhắc bổ sung nếu cần đối soát/audit (cần thêm cột `credit_statement_lines` + migration). (2) `ToSnakeCase` trùng lặp giữa `CreditDtoMapper` và `CreditStatementDtoMapper` — trích ra util dùng chung. |
| 2026-07-08 | **SCRUM-261 REOPEN — finding HIGH (reviewer-2, vòng review sâu hơn).** `CreditStatementGenerationService.BuildStatementAsync` lấy opening = `priorStatement.ClosingBalance` từ `FindLatestBeforeAsync` (statement gần nhất có `PeriodStart < periodStart`, KHÔNG kiểm tra liền kề), chỉ fallback ledger khi không có statement trước. Sai DEC-CRE-06 (opening phải tính TỪ LEDGER). Gap: nếu 1 tháng bị bỏ (job mất `_lastRunLocalDate` sau restart ngày 1, HOẶC owner tự sinh out-of-order theo DEC-CRE-05), sinh kỳ sau sẽ carry-forward closing từ statement KHÔNG liền kề → mất giao dịch tháng bị bỏ khỏi opening, và vì statement bất biến nên hỏng vĩnh viễn. **Quyết định leader: fix ALWAYS-LEDGER** (bỏ shortcut statement-chaining, luôn dùng `GetNetBalanceMovementBeforeAsync`) — đúng DEC-CRE-06, đơn giản, không tốn perf (sinh tối đa 1 lần/nhà hàng/tháng). Kèm test gap-scenario (bỏ 1 tháng → kỳ sau opening vẫn gồm giao dịch tháng bị bỏ). Task #4 về in_progress; PASS trước đó KHÔNG tính. |
| 2026-07-08 | **SCRUM-261 ✅ reviewer-2 PASS (sau fix HIGH).** Fix thực tế coder-2 chọn: ADJACENCY-GATED (giữ fast-path prior-statement khi `priorStatement.PeriodEnd == periodStart`, else fallback `GetNetBalanceMovementBeforeAsync`) — KHÁC chỉ đạo leader (always-ledger) nhưng ĐÚNG về mặt correctness và reviewer-2 đã verify đúng gap-scenario → leader CHẤP NHẬN, không bắt làm lại (churn thấp). Bonus: coder-2 phát hiện & sửa test adjacent cũ bị false-positive (PeriodEnd naive-UTC ≠ boundary tính theo VN tz) → nay dùng `CreditStatementPeriodCalculator.ResolvePeriod` cho cả 2 mốc. Test mới `GenerateAsync_PriorStatementExistsButIsNotAdjacent_FallsBackToLedgerForOpeningBalanceAsync`. Orders unit 451/451 GREEN, no drift, format sạch. Chưa commit. |
| 2026-07-08 | **SCRUM-261 — crossed-messages reconcile.** reviewer-2 PASS bản ADJACENCY-GATED; leader chấp nhận. Nhưng coder-2 (theo chỉ đạo always-ledger gửi TRƯỚC đó, chưa thấy tin 'đã chấp nhận adjacency') đã đổi tiếp sang ALWAYS-LEDGER: bỏ hẳn shortcut, opening luôn = `GetNetBalanceMovementBeforeAsync`, xoá `FindLatestBeforeAsync` (dead code, không caller khác), thêm test gap Jan/Feb-skip/Mar (repo thật). ⇒ Working tree HIỆN TẠI = always-ledger, reviewer-2 CHƯA review bản này. Leader GIỮ always-ledger (đúng chủ đích gốc, sạch hơn) và mở lại 261 để reviewer-2 verify đúng bản trong tree. Orders unit 448/448 GREEN (giảm 3 do bỏ test adjacency-specific), no drift, format sạch. |
| 2026-07-08 | **Leader respawn (cold start) — reconcile 261 + xác minh 266.** Đọc code THẬT `CreditStatementGenerationService.BuildStatementAsync`: bản trong working tree = **ALWAYS-LEDGER** (`GetNetBalanceMovementBeforeAsync` gọi vô điều kiện, comment ghi rõ lý do bỏ statement-chaining, KHÔNG còn adjacency fast-path, `FindLatestBeforeAsync` đã xoá). Đây là bản shipped — cập nhật §0/§4 + header cho khớp (bỏ trạng thái 🟡 re-review). Supervisor xác nhận coi 261 là PASSED, KHÔNG đổi code. Lưu ý: bản always-ledger là bản đơn giản hoá strict của adjacency (reviewer-2 đã verify gap-scenario ở vòng trước); reviewer-3 sẽ confirm nhanh khi review 266. |
| 2026-07-08 | **SCRUM-266 = CLEAN START (không phải 'làm dở').** Kiểm tra working tree: `RestaurantCredit` VẪN là `sealed class` thuần (chưa `AggregateRoot`), KHÔNG có `LastAlertedLevel`, KHÔNG có `CreditLimitThresholdReachedDomainEvent`/IntegrationEvent, `FreshFlow.Contracts` chỉ có OrderConfirmed/OrderCancelled, KHÔNG có migration `last_alerted_level`, KHÔNG stub handler. ⇒ 266 làm từ đầu. **Scope Option B (user chốt):** credit-scoped only, KHÔNG bootstrap Notifications. `RestaurantCredit`→`AggregateRoot`; anti-spam bằng `LastAlertedLevel` (None/Warning/Exceeded); ngưỡng 80%=warning, ≥100%=exceeded; raise domain event trong `Charge()` khi cross ngưỡng MỚI, re-arm trong `Settle()`/`Refund()` khi tụt xuống; handler dịch sang `CreditLimitThresholdReachedIntegrationEvent` ở Contracts; **1 stub handler chỉ log** (defer Notifications persist sang epic riêng); migration `last_alerted_level`. Giao coder. |
| 2026-07-08 | **SCRUM-261 ✅ reviewer-3 PASS (bản ALWAYS-LEDGER — bản shipped trong tree).** Re-review sau khi hợp nhất split-brain leader: xác nhận opening LUÔN tính qua `GetNetBalanceMovementBeforeAsync` (DEC-CRE-06), `FindLatestBeforeAsync` đã xoá hẳn (dead code), có gap regression test (bỏ 1 tháng → kỳ sau opening vẫn gồm giao dịch tháng bị bỏ). RBAC admin-or-owner + guard IDOR + idempotency + period-not-closed guard đều verified. Orders unit 448/448 GREEN, migration no drift, format sạch. KHÔNG có CRITICAL/HIGH. Task #4 = completed. (Lưu ý: PASS của reviewer-2 trước đó là cho bản adjacency-gated đã bị thay thế — không còn áp dụng; PASS hiện hành là của reviewer-3 trên bản always-ledger.) |
| 2026-07-08 | **SCRUM-266 — ADOPT-AND-CONTINUE (phát hiện partial work coder-2 cũ).** coder báo tree KHÔNG phải clean start: đã có domain-side 266 (mtime ~14:48-14:49, xuất hiện SAU khảo sát clean-start của leader-2) — do coder-2 cũ viết khi old-leader giao 266 song song trước khi bị shut down. leader-2 review: `RestaurantCredit : AggregateRoot` + `LastAlertedLevel` anti-spam (Charge raise khi cross ngưỡng MỚI `level <= LastAlertedLevel`; Settle/Refund re-arm không raise; guard CreditLimit<=0), `CreditAlertLevel {None,Warning,Exceeded}` ordinal, `CreditLimitThresholdReachedDomainEvent` record — ĐÚNG DEC-CRE-04, chất lượng tốt ⇒ GIỮ, KHÔNG redo. Giao coder hoàn thiện các tầng còn thiếu: integration event ở `FreshFlow.Contracts` (Level = string), translator handler + 1 stub log handler (Option B, defer Notifications persist), EF config (`last_alerted_level` int + Ignore Id/CreatedAt/DeletedAt/DomainEvents, giữ UpdatedAt concurrency token), migration `AddRestaurantCreditAlertLevel` (create-only, no drift), tests. Collision guard: coder re-check git/mtime trước khi sửa; leader-2 xin team-lead xác nhận không còn writer active. |
| 2026-07-08 | **SCRUM-266 — chốt quyền sở hữu, gỡ write-race.** team-lead xác nhận partial 266 trong tree do **coder-2** (bản revive) viết và coder-2 SỞ HỮU #5. leader-2 đảo ngược chỉ đạo adopt-and-continue đã gửi cho `coder` cold-start → cho `coder` STAND DOWN (idle standby, KHÔNG chạm file 266). Task #5 owner = coder-2. Roster active DUY NHẤT: **leader-2 + coder-2 + reviewer-3** (leader cũ, coder cold-start [standby], reviewer/reviewer-2 = zombie/standby). coder-2 hoàn thiện các tầng còn thiếu (integration event/translator/stub/config/migration/tests) → reviewer-3 → leader-2 đóng khi PASS. Nếu coder-2 chết lại mới promote coder cold-start đọc tree tiếp. |
| 2026-07-08 | **SCRUM-266 ✅ hoàn thành Option B.** Bổ sung mảnh còn thiếu từ review: `Notifications.Application` consumer `CreditLimitThresholdReachedIntegrationEventHandler` log `[STUB]` cho credit alert, `Notifications.Infrastructure.AddNotificationsModule()` scan handler bằng MediatR, API startup register Notifications module. Không bootstrap notification persistence/entity/table. Test mới `CreditLimitThresholdNotificationStubTests` chứng minh handler được DI register và `IPublisher.Publish()` tạo log stub. Verification: Orders unit 464/464 GREEN; `dotnet build FreshFlow.slnx --no-restore` 0 lỗi; `dotnet ef migrations has-pending-model-changes` no changes; format verify cục bộ sạch. Warnings còn lại là cảnh báo cũ về EF package version/tooling và obsolete API. |
