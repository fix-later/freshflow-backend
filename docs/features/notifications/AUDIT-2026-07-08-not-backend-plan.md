# Báo cáo khảo sát & kế hoạch triển khai: NOT — Notifications & Alerts (Backend)

| | |
|---|---|
| **Ngày khảo sát** | 2026-07-08 |
| **Branch** | `SCRUM-254-CRE-B2B-Credit-Debt` (làm việc tiếp trên nhánh hiện tại) |
| **Epic** | SCRUM-300 — Notifications & Alerts |
| **Phạm vi** | Chỉ **4 task BACKEND**: SCRUM-327 (register device), SCRUM-330 (send domain-event notifications / persist writer), SCRUM-331 (view & read), SCRUM-334 (history + retry). FE (328/329/332/333) **NGOÀI phạm vi**. |
| **Module** | **Notifications** (`src/Modules/Notifications/*`) — hiện là stub, đợt này **bootstrap persistence thật** cho module. |
| **Tác giả** | Leader agent (team `backend-dev`) — khảo sát & viết plan; coding do **codex** (người điều khiển) thực hiện, `reviewer` review sau. |
| **Mục đích tài liệu** | Cung cấp cho AI coding agent đủ ngữ cảnh để implement epic NOT theo đúng quyết định đã chốt — không cần hỏi lại; là nguồn tham chiếu chính, KHÔNG chỉ TaskList. Mirror cấu trúc của `docs/features/credit/AUDIT-2026-07-07-cre-backend-plan.md`. |
| **Trạng thái** | Khảo sát xong; DEC-NOT-01…12 đã chốt. **SCRUM-327 ✅ PASS & đã COMMIT** (`a4b5d46`; reviewer-not re-review: 30/30 GREEN, coverage 94.89%/90%, no drift, 3 finding đã fix). **✅ EPIC NOT BACKEND HOÀN TẤT 4/4** — 327 (`a4b5d46`) → 330 (`5514025`) → 331 (`ede04e0`) → 334 (`916311f`), mỗi task PASS review độc lập, không còn CRITICAL/HIGH tồn đọng. Còn lại ngoài phạm vi BE: FE 328/329/332/333; follow-up push provider thật (FCM/APNs) vào `IPushSender`. |

> **Quy ước cập nhật tài liệu:** mỗi khi một task hoàn thành (reviewer pass) hoặc có quyết định/thay đổi giữa chừng, phải cập nhật lại file này — đặc biệt §4 (Task breakdown) và §6 (Nhật ký tiến độ). Đồng bộ với TaskList, không thay thế.

---

## 0. Bảng SCRUM key theo task

| Task | SCRUM Key | Loại | Trạng thái |
|---|---|---|---|
| [BE] Register Device for Push Notification | SCRUM-327 | Net-new (+ bootstrap module persistence) | ✅ Hoàn thành — reviewer-not PASS sau 1 vòng fix, **đã COMMIT `a4b5d46`** |
| [BE] Send Domain Event Notifications (persist writer, thay stub CRE-266) | SCRUM-330 | Net-new (thay log stub) | ✅ DONE — committed `5514025` (reviewer-not PASS vòng 2, 57/57 GREEN, no drift) |
| [BE] View & Read Notifications | SCRUM-331 | Net-new | ✅ DONE — committed `ede04e0` (reviewer-not PASS, 84/84 GREEN, no drift) |
| [BE] Store Notification History & Retry Failed | SCRUM-334 | Net-new (đổi schema — migration `AddNotificationSendStatus`) | ✅ DONE — committed `916311f` (reviewer-not PASS, 106/106 GREEN, no drift) |

**Task FE ngoài phạm vi:** SCRUM-328, 329, 332, 333.
**Liên kết:** SCRUM-330 *Relates* SCRUM-266 (thay log stub credit-alert bằng persist writer thật).

**Quy ước commit:** `feat(notifications): SCRUM-XXX <description>` — một commit (hoặc nhóm nhỏ) ứng với một task, dùng đúng key của task đó. KHÔNG commit khi chưa được supervisor cấp/xác nhận key (giữ trong working tree).

---

## 1. Hiện trạng repo tại thời điểm khảo sát

### 1.1 Module Notifications = STUB (bootstrap từ CRE-266 Option B)

Cấu trúc project đã có 3 `.csproj` (`Domain` / `Application` / `Infrastructure`) nhưng **chỉ có 2 file `.cs` thật**:

- `FreshFlow.Notifications.Application/EventHandlers/CreditLimitThresholdReachedIntegrationEventHandler.cs` — **log-only stub** (`ILogger.LogWarning("[STUB] ...")`, `return Task.CompletedTask`). KHÔNG persist.
- `FreshFlow.Notifications.Infrastructure/DependencyInjection.cs` — `AddNotificationsModule()` chỉ `AddMediatR(scan Application assembly)` cho consumer. **KHÔNG** `EfAssemblyRegistry.Register`, KHÔNG repository, KHÔNG EF config, KHÔNG hosted service.
- `FreshFlow.Notifications.Domain` — **rỗng** (không có entity nào).
- Đã đăng ký trong `src/FreshFlow.API/Program.cs:187` → `builder.Services.AddNotificationsModule(builder.Configuration);` (sau Auth/Catalog/Pricing/Orders).

> **Cập nhật sau SCRUM-327 (2026-07-08):** module đã được bootstrap persistence thật — có `NotificationDevice` entity + `NotificationDeviceConfiguration` (plain Guid `user_id`, KHÔNG FK cross-module — DEC-NOT-12) + `NotificationDeviceRepository` + migration `20260708143833_AddNotificationDevices` + `EfAssemblyRegistry.Register`/`AddValidatorsFromAssembly` trong `AddNotificationsModule`. Phần dưới mô tả hiện trạng GỐC trước 327.

**Chưa tồn tại (tại thời điểm khảo sát gốc):** entity `Notification`, bảng `notifications`, `notification_devices`, EF config, migration, controller, query/command. Docs/03 đánh dấu bảng `notifications` = `[PLANNED]`.

### 1.2 Integration events HIỆN CÓ trong `FreshFlow.Contracts` (quan trọng cho SCRUM-330)

Chỉ có **3** record, tất cả `: INotification`:

| Event | Fields | Producer |
|---|---|---|
| `OrderConfirmedIntegrationEvent` | `OrderId, RestaurantId, TotalAmount, OccurredAt` | Orders |
| `OrderCancelledIntegrationEvent` | (tương tự order) | Orders |
| `CreditLimitThresholdReachedIntegrationEvent` | `RestaurantId, Level(string "warning"/"exceeded"), Utilization, OutstandingBalance, CreditLimit, OccurredAt` | Orders (SCRUM-266) |

**KHÔNG tồn tại integration event cho:** price update, hub discrepancy, delivery update. Pricing chỉ broadcast qua SignalR (`IPricingBroadcastService`), KHÔNG publish integration event. Hub & Logistics **chưa được implement / chưa đăng ký** trong `Program.cs`. ⇒ Xem §2 DEC-NOT-03 để chốt phạm vi consumer buildable-now.

### 1.3 Schema đích (docs/03-database-schema.md, `[PLANNED]`)

```sql
CREATE TABLE notifications (
    id          UUID                PRIMARY KEY,
    user_id     UUID                NOT NULL,
    type        notification_type   NOT NULL,
    title       TEXT                NOT NULL,
    body        TEXT                NOT NULL,
    payload     JSONB,
    is_read     BOOLEAN             NOT NULL DEFAULT false,
    read_at     TIMESTAMPTZ,
    created_at  TIMESTAMPTZ         NOT NULL,
    CONSTRAINT fk_notifications_user
        FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);
```
- Append-only: **không** `updated_at`, **không** `deleted_at` (nhưng `is_read`/`read_at` mutable — xem DEC-NOT-11).
- `notification_type` (docs/04.2 payload schema): `price_change`, `order_status`, `delivery_update`, `system`. Đợt này chỉ dùng subset buildable + thêm `credit_alert` (xem DEC-NOT-02).
- **Không có** bảng `notification_devices` trong docs/03 → SCRUM-327 tự định nghĩa (DEC-NOT-04).
- Quy ước enum toàn repo: **KHÔNG tạo PG enum type**; lưu `varchar` qua EF `HasConversion<string>()` snake_case (docs/03 §4.3). Áp dụng cho `notifications.type`.
- **Lưu ý FK cross-module (DEC-NOT-12):** dòng `fk_notifications_user` trong DDL docs/03 là aspirational. Convention EF thực tế của repo (xem `RestaurantCreditConfiguration`) **KHÔNG** model FK cross-module — cột id của module khác lưu là plain `Guid`. Xem DEC-NOT-12.

### 1.4 Pattern tham chiếu để TÁI SỬ DỤNG (đã verify trong repo)

- **Bootstrap persistence cho module:** `EfAssemblyRegistry.Register(Assembly.GetExecutingAssembly())` trong `AddXModule()` (xem `Orders.Infrastructure/DependencyInjection.cs:25`). `AppDbContext` gọi `ApplyConfigurationsFromAssembly` cho mọi assembly đã Register (`AppDbContext.cs:27`) — **AppDbContext KHÔNG khai `DbSet<T>`**, chỉ auto-discover `IEntityTypeConfiguration<T>`. Migration nằm ở `src/FreshFlow.Infrastructure.Persistence/Migrations/`. (SCRUM-327 đã áp dụng đúng pattern này.)
- **Cursor pagination:** `Pricing.Application/Queries/GetPriceChangeHistory/` (`GetPriceChangeHistoryQueryHandler.cs`) + repo `GetPageAsync(cursor, pageSize, from, to)` lấy `PageSize+1` để tính `NextCursor`; DTO `...PageDto(Items, PageSize, NextCursor)`; controller trả `Ok(ApiResponse.OkPaged(items, pageSize, nextCursor))`. Cursor = base64 của `(CreatedAt, Id)`, order `CreatedAt DESC, Id DESC`.
- **RBAC "chỉ của mình":** controller resolve `ResolveUserId()` = `User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "sub"` và `User.IsInRole("admin")`, truyền vào query; handler enforce ownership (xem `RestaurantCreditController.cs:145-153`, `ListCreditStatementsQuery`). Với notifications thì **đơn giản hơn**: filter thẳng `user_id == currentUserId` (mỗi user chỉ thấy noti của chính mình) — xem DEC-NOT-05.
- **Background job (retry, SCRUM-334):** `ScheduledOrderGenerationHostedService` (`Orders.Infrastructure/Jobs/`) — `BackgroundService`, config-gated enable + interval, mỗi tick idempotent. `MonthlyCreditStatementHostedService` là ví dụ thứ 2. **`PartitionMaintenanceJob` trong CLAUDE.md KHÔNG tồn tại** — đừng copy nó.
- **Cross-module read seam (map RestaurantId → owner UserId):** `Orders.Infrastructure/CrossModule/RestaurantReader.cs` (`IRestaurantReader`) dùng `ToSqlQuery`/keyless read model đọc bảng của module khác **không** cần project ref. Notifications sẽ cần seam tương tự để biết gửi noti cho `user_id` nào (xem DEC-NOT-10).
- **Không model FK cross-module:** `RestaurantCreditConfiguration.cs` (Orders) — `restaurant_id` là plain `Guid` PK, KHÔNG `HasOne`/nav sang module khác. Đây là pattern chuẩn cho tham chiếu aggregate của module khác (xem DEC-NOT-12).
- **Domain event → Integration event:** CLAUDE.md; cross-module CHỈ qua `FreshFlow.Contracts`, MediatR `INotificationHandler<T>` hai đầu.

---

## 2. Các quyết định đã chốt (DEC-NOT-xx — KHÔNG hỏi lại)

- **DEC-NOT-01 — Bootstrap persistence THẬT cho module Notifications (không còn stub-only).** Đợt này biến Notifications thành module có persistence đầy đủ theo pattern chuẩn: entity trong `Notifications.Domain`, `IEntityTypeConfiguration<T>` trong `Notifications.Infrastructure/Persistence/Configurations/`, `EfAssemblyRegistry.Register(Assembly.GetExecutingAssembly())` trong `AddNotificationsModule()`, repository `INotificationRepository`/`INotificationDeviceRepository`, và migration trong `FreshFlow.Infrastructure.Persistence/Migrations/`. Migration **tạo nhưng KHÔNG apply** lên DB thật; verify bằng `dotnet ef migrations has-pending-model-changes` = no drift. *(Đã hiện thực ở SCRUM-327.)*

- **DEC-NOT-02 — `notification_type` = subset buildable + `credit_alert`.** Enum domain `NotificationType` khởi tạo với các value **có producer thật đợt này**: `order_status`, `credit_alert`, `system`. Các value `price_change`, `delivery_update` giữ trong docs như `[PLANNED]`, **CHƯA** thêm consumer (không có integration event — xem DEC-NOT-03). Lưu varchar snake_case qua `HasConversion<string>` (nhất quán `credit_transactions.type`). Enum để mở rộng dễ.

- **DEC-NOT-03 — SCRUM-330 CHỈ wire consumer cho integration event ĐÃ TỒN TẠI trong `FreshFlow.Contracts`.** Đây là quyết định scope quan trọng nhất của epic:

  | Nguồn sự kiện | Integration event có sẵn? | Trạng thái đợt này |
  |---|---|---|
  | **Order status** (confirmed/cancelled) | ✅ `OrderConfirmedIntegrationEvent`, `OrderCancelledIntegrationEvent` | **BUILDABLE** — thêm consumer persist `type=order_status` |
  | **Credit limit alert** | ✅ `CreditLimitThresholdReachedIntegrationEvent` | **BUILDABLE** — thay log stub SCRUM-266 bằng persist writer `type=credit_alert` |
  | **Price update** | ❌ Pricing chỉ SignalR, không publish integration event | ⛔ BLOCKED-BY Pricing epic (cần Pricing emit integration event trước) |
  | **Hub discrepancy** | ❌ Module Hub chưa implement | ⛔ BLOCKED-BY Hub epic |
  | **Delivery update** | ❌ Module Logistics chưa implement | ⛔ BLOCKED-BY Logistics epic |

  ⇒ SCRUM-330 đợt này = **2 consumer buildable** (order_status + credit_alert). Ba nguồn còn lại ghi rõ trong doc là "blocked-by-their-epic", KHÔNG cố tạo integration event ở producer (ngoài scope epic NOT). Khi các epic kia phát integration event, chỉ cần thêm consumer mới ở `Notifications.Application/EventHandlers/` — persist writer đã sẵn seam.

- **DEC-NOT-04 — SCRUM-327 entity `notification_devices` (push token registry).** Bảng mới (docs/03 chưa có → bổ sung docs sau). Cột đề xuất: `id UUID PK`, `user_id UUID NOT NULL` (plain Guid, KHÔNG model FK — DEC-NOT-12), `token TEXT NOT NULL` (FCM/APNs/web-push token), `platform VARCHAR` (`ios`/`android`/`web`), `device_id TEXT NULL` (client-supplied, optional), `created_at TIMESTAMPTZ`, `updated_at TIMESTAMPTZ`, `revoked_at TIMESTAMPTZ NULL` (soft-unregister). **Dedup: unique `(user_id, token)` filter `revoked_at IS NULL`.** Register lại token đã tồn tại = **upsert idempotent** (reactivate nếu đang revoked, cập nhật `platform`/`updated_at`), KHÔNG tạo bản trùng. Unregister = set `revoked_at` (soft-delete), KHÔNG xoá cứng (giữ audit + tránh reuse token cũ). *(Đã hiện thực & PASS ở SCRUM-327.)*

- **DEC-NOT-05 — RBAC: user chỉ thấy/thao tác noti CỦA CHÍNH MÌNH.** Không có "admin xem hộ" ở v1. Mọi query/command lấy `userId` từ JWT (`ResolveUserId()`), filter `notifications.user_id == userId`. Mark-as-read phải re-check ownership → nếu noti không thuộc user → **404** (không lộ tồn tại). Device register: `user_id` LẤY TỪ JWT, **BỎ QUA** mọi `userId` trong request body (chống IDOR/gán token cho user khác). Đây là bề mặt bảo mật chính của epic.

- **DEC-NOT-06 — Push provider (FCM/APNs) DEFER; đợt này chỉ dựng SEAM.** Chưa có provider nào được cấu hình trong repo (không có FCM key/APNs cert). Quyết định: định nghĩa interface `IPushSender` trong `Notifications.Application/Abstractions/` với method `SendAsync(deviceTokens, notification, ct) -> Result`; implementation đợt này = **no-op/log** (`LogPushSender`) trong `Notifications.Infrastructure`. Việc tích hợp provider thật = **follow-up ticket riêng** (ngoài 4 task này). SCRUM-334 xây retry infra dựa trên seam này (test được mà không cần provider). Ghi rõ trong doc để reviewer không bắt lỗi "chưa gửi push thật".

- **DEC-NOT-07 — SCRUM-334 mô hình status + retry.** Persist trạng thái gửi để có "history" và retry:
  - Thêm cột trạng thái gửi cho notification (hoặc bảng phụ `notification_deliveries` nếu cần tách 1-noti-nhiều-device — **khuyến nghị cột trên `notifications`** cho v1, YAGNI): `send_status VARCHAR` (`pending`/`sent`/`failed`), `attempt_count INT DEFAULT 0`, `last_attempt_at TIMESTAMPTZ NULL`, `failed_reason TEXT NULL`. (Lưu ý: các cột này mutable → không vi phạm "append-only" của noti nội dung; ghi chú rõ read/is_read + send_status là mutable metadata, còn title/body/payload bất biến.)
  - Retry = `BackgroundService` (mẫu `ScheduledOrderGenerationHostedService`): config-gated enable + interval; mỗi tick quét noti `send_status='failed' AND attempt_count < MAX` (backoff theo `last_attempt_at`), gọi `IPushSender.SendAsync` lại; đạt MAX → để `failed` (dead-letter, không loop vô hạn). Idempotent theo notification id.
  - Vì provider là no-op seam (DEC-NOT-06), "send" luôn success ở v1 → retry infra được test bằng cách inject `IPushSender` giả (fail N lần rồi success) trong unit test.

- **DEC-NOT-08 — List = cursor pagination** (mẫu `GetPriceChangeHistory` + `ApiResponse.OkPaged`), order `created_at DESC, id DESC`, `pageSize` default 50, **validate/reject ngoài `[1,200]` → 400** (dùng FluentValidation `InclusiveBetween(1,200)` y hệt `GetPriceChangeHistoryQueryValidator` của Pricing — KHÔNG âm thầm clamp; nhất quán convention repo). Không offset. *(Đính chính 2026-07-09: bản nháp trước ghi nhầm "clamp" — mâu thuẫn với chính pattern Pricing mà DEC này trích dẫn; hành vi đúng & đã impl ở SCRUM-331 là reject/400.)*

- **DEC-NOT-09 — Ranh giới module.** `Notifications.Application` chỉ ref `FreshFlow.Contracts` + `SharedKernel` + `Notifications.Domain`; consume integration event qua MediatR `INotificationHandler<T>`. KHÔNG project-ref sang Orders/Pricing/Auth. Persist writer map integration event → `Notification` entity.

- **DEC-NOT-10 — Map RestaurantId → recipient UserId qua READ SEAM (không sửa Contracts).** Vấn đề: `notifications.user_id` FK `users(id)`, nhưng integration event order/credit mang `RestaurantId`, KHÔNG mang `UserId`. Để persist noti đúng người nhận cần resolve restaurant → owner user. **Quyết định: thêm read seam `INotificationRecipientResolver` trong `Notifications.Infrastructure/CrossModule/`** dùng `ToSqlQuery`/keyless read model đọc `restaurants.user_id` — **giống hệt `RestaurantReader` của Orders** — thay vì enrich integration event (giữ Contracts ổn định, không retrofit producer). Nếu resolve ra null (restaurant không có owner) → log + skip persist (không crash consumer). Đây là điểm thiết kế then chốt của SCRUM-330; reviewer chú ý cả correctness lẫn việc KHÔNG tạo project-ref cross-module.

- **DEC-NOT-11 — `notifications` append-only về NỘI DUNG, mutable ở metadata đọc/gửi.** `title/body/payload/type/user_id/created_at` bất biến sau tạo. `is_read`/`read_at` (SCRUM-331) và `send_status`/`attempt_count`/... (SCRUM-334) là metadata mutable — chấp nhận, KHÔNG dùng `updated_at`/`deleted_at` toàn cục cho bảng này (theo docs/03). Concurrency mark-as-read: idempotent (đã read → no-op, vẫn 200).

- **DEC-NOT-12 (chốt 2026-07-08 sau review SCRUM-327) — KHÔNG model FK cross-module trong EF; cột id của module khác lưu là plain indexed `Guid`.** Cột `notification_devices.user_id` (và sau này `notifications.user_id`) tham chiếu `Auth.User` phải là **plain `Guid` có index**, KHÔNG dùng `builder.HasOne(...)`/navigation, KHÔNG dùng reflection (`Type.GetType("...Auth...User...")`). Lý do: (1) nhất quán pattern chuẩn repo — `RestaurantCreditConfiguration` model `restaurant_id` là plain Guid PK không HasOne; (2) **giữ ranh giới modular monolith** — Notifications KHÔNG được phụ thuộc Auth. Cách reflection-HasOne mà SCRUM-327 (bản codex đầu) dùng đã buộc phải thêm `ProjectReference` `FreshFlow.Auth.Infrastructure` vào project test + hack force-load assembly Auth trước khi build model — đây chính là vi phạm ranh giới, chỉ "chạy được nhờ" thứ tự đăng ký trong `Program.cs` (fragile, fail ở model-build nếu Auth build trước/entity đổi tên). Referential integrity ở tầng app (resolver DEC-NOT-10 + validation), KHÔNG ở DB-level FK cross-module. FK trong DDL docs/03 là aspirational; nếu thực sự muốn DB-level FK thì là quyết định cross-cutting riêng, áp dụng đồng bộ toàn repo — KHÔNG nhét ad-hoc vào 1 config. **Fix SCRUM-327:** bỏ `HasOne`/reflection → plain Guid + index; bỏ `ProjectReference` Auth.Infrastructure + hack force-load khỏi project test. *(Đã fix & PASS ở re-review SCRUM-327.)*

---

## 3. Rủi ro & bề mặt bảo mật

- **RBAC/IDOR (CRITICAL):** mọi endpoint lấy `userId` từ JWT, KHÔNG từ body/route cho quyền sở hữu. List filter `user_id==caller`. Mark-as-read re-check ownership → 404 nếu không thuộc. Device register bỏ qua body userId.
- **Recipient resolution (DEC-NOT-10):** map sai restaurant→user = gửi noti cho nhầm người (lộ dữ liệu). Cần test kỹ resolver + xử lý null an toàn.
- **Consumer resilience (SCRUM-330):** handler persist noti KHÔNG được ném lỗi làm hỏng luồng phát integration event của producer (order/credit). Lỗi persist → log + nuốt an toàn (hoặc để retry infra 334 xử lý), không lan ngược.
- **Idempotency:** (a) device register trùng token = upsert, không nhân bản; **race đồng thời 2 register cùng token mới** = read-then-write không có catch → `DbUpdateException` unique-violation (SCRUM-327 đã fix: catch unique-violation → re-query → reactivate). (b) consumer nhận lại cùng integration event (nếu redelivery) — cân nhắc dedup theo (user, type, source-id) hoặc chấp nhận at-least-once cho v1 (ghi rõ). (c) mark-as-read lặp = no-op.
- **Retry loop (SCRUM-334):** phải bounded (MAX attempts + backoff) → tránh gửi vô hạn / bão log.
- **Payload JSONB:** chỉ lưu field không nhạy cảm; không nhét token/secret vào payload.
- **Migration:** tạo nhưng KHÔNG apply lên DB thật; no-drift check. KHÔNG commit khi chưa có key.
- **Coverage gate:** ≥80% cho code mới, tính CẢ tầng Application (handler/validator/DTO mapper) — không chỉ repository. Test controller mock `ISender` KHÔNG tính là phủ handler (bài học review 327 vòng 1).

---

## 4. Phân rã task

### SCRUM-327 — [BE] Register Device for Push Notification (NET-NEW + bootstrap module) — ✅ HOÀN THÀNH
**Mục tiêu:** API cho phép user đăng ký device token để nhận push; là task ĐẦU tiên nên gánh luôn **bootstrap persistence** cho module (DEC-NOT-01).
- **Bootstrap (một lần):** `EfAssemblyRegistry.Register(...)` trong `AddNotificationsModule()`; thêm MediatR ValidationBehavior + `AddValidatorsFromAssembly` (mẫu Orders DI); tạo `Notifications.Infrastructure/Persistence/Configurations/`.
- **Domain:** entity `NotificationDevice` (fields DEC-NOT-04) trong `Notifications.Domain/Entities/`.
- **Application:** `Commands/RegisterDevice/` (command + handler + FluentValidation validator co-located: token required/non-empty, platform ∈ {ios,android,web}); `Commands/UnregisterDevice/` (command + handler + validator); `Abstractions/INotificationDeviceRepository`. `UserId` từ command (controller bơm từ JWT).
- **Infrastructure:** `NotificationDeviceConfiguration` (unique `(user_id, token)` filter `revoked_at IS NULL`; `user_id` = plain Guid + index, KHÔNG HasOne — DEC-NOT-12), `NotificationDeviceRepository` (upsert idempotent), DI đăng ký repo; migration `AddNotificationDevices` (create-only, no drift).
- **API:** `NotificationDeviceController` — `POST /api/v1/notifications/devices` (register), `DELETE .../devices/{token}` (unregister → soft revoke). `[Authorize]`, userId từ `ResolveUserId()`, bỏ qua body userId (DEC-NOT-05).
- **Acceptance:** register token mới → 201/200 + bản ghi active; register lại cùng token → không nhân bản (upsert); unregister → `revoked_at` set, không xoá cứng; unregister token không tồn tại → 404; user A không thao tác được token gắn user B; validator chặn platform sai; TDD ≥80% **kể cả handler/validator/mapper**; build 0 lỗi; `has-pending-model-changes` no drift; format sạch.
- **Reuse:** Orders DI pattern, EfAssemblyRegistry, repository/config pattern.
- **Review vòng 1 (reviewer-not, 2026-07-08) — CHANGES-NEEDED:** 2 HIGH — (1) coverage tầng Application 16.32% < 80% (test controller mock `ISender` không chạy handler thật; thiếu test handler/UnregisterValidator/UnregisterAsync/null→404); (2) FK reflection cross-module (`Type.GetType` Auth.User + HasOne) buộc thêm ref Auth.Infrastructure + hack force-load. MEDIUM: race register đồng thời. Chốt DEC-NOT-12.
- **Review vòng 2 (reviewer-not, 2026-07-08) — ✅ PASS.** Cả 3 finding fix & re-verify độc lập: (1) FK → plain Guid + 2 index, migration `20260708143833_AddNotificationDevices` không có FK constraint, test csproj bỏ ref Auth.Infrastructure + bỏ hack force-load; (2) coverage Application **94.89% line / 90% branch**, các class handler/validator/mapper/ValidationBehavior/controller = 100%, có test `Handle_MissingToken_ReturnsNotFoundAsync` (assert `NOTIFICATIONDEVICE_NOT_FOUND`) đóng đúng gap null→404; (3) race → catch `DbUpdateException` gated `isInsert && IsUniqueViolation` (SqlState UniqueViolation) → detach → re-query → reactivate, test dùng `SaveChangesInterceptor` mô phỏng competing insert thật. Gates: build 0 lỗi, 30/30 GREEN, format sạch, no drift. Không commit (chưa có key).

### SCRUM-330 — [BE] Send Domain Event Notifications (persist writer — THAY log stub CRE-266) — ✅ DONE (committed `5514025`)
**Mục tiêu:** consume integration event ĐÃ CÓ và **persist** noti; thay handler stub SCRUM-266 bằng writer thật.
- **Domain:** entity `Notification` (fields §1.3 + `send_status/attempt_count/...` chuẩn bị cho 334 — có thể để 334 thêm cột, nhưng khuyến nghị tạo entity đủ field ngay để tránh 2 migration liền kề; **quyết định:** 330 tạo `notifications` với is_read/read_at; các cột send_status để 334 thêm — giữ mỗi task một migration rõ ràng). Enum `NotificationType` (DEC-NOT-02). `user_id` = plain Guid + index (DEC-NOT-12).
- **Application:**
  - `Abstractions/INotificationRepository` (`AddAsync`, `GetPageAsync`, `FindByIdForUserAsync`, `MarkReadAsync`...).
  - Persist writer/service `INotificationWriter` + impl: nhận (userId, type, title, body, payload) → tạo `Notification`.
  - `EventHandlers/`:
    - **THAY** `CreditLimitThresholdReachedIntegrationEventHandler`: bỏ log-only, resolve recipient (restaurant owner qua DEC-NOT-10) → persist noti `type=credit_alert`, title/body VI (vd "Cảnh báo hạn mức tín dụng"), payload = {level, utilization, outstanding, limit}.
    - **THÊM** `OrderConfirmedIntegrationEventHandler` + `OrderCancelledIntegrationEventHandler`: resolve owner → persist `type=order_status`, payload theo docs/04.2 (order_id, new_status...).
  - KHÔNG thêm consumer cho price/hub/delivery (DEC-NOT-03).
- **Infrastructure:** `NotificationConfiguration` (varchar type snake_case, jsonb payload), `NotificationRepository`, `NotificationRecipientResolver : INotificationRecipientResolver` (`CrossModule/`, ToSqlQuery đọc `restaurants.user_id`), DI đăng ký; `AddNotificationsModule` giờ scan cả consumer + repo + resolver; migration `AddNotifications` (create-only, no drift).
- **Acceptance:** publish `CreditLimitThresholdReachedIntegrationEvent` → 1 row `notifications` đúng user/type/payload (thay vì chỉ log); order confirmed/cancelled → 1 row `type=order_status`; recipient resolver map đúng restaurant→user, null-safe (skip + log, không crash); consumer nuốt lỗi persist an toàn; KHÔNG project-ref cross-module; TDD ≥80% (gồm test resolver + 3 handler); no drift; format sạch.
- **Reuse:** `RestaurantReader` (mẫu resolver), MediatR `INotificationHandler`, EF config/repo.
- **Liên kết:** đóng phần "Notifications persists a notification" đã defer ở SCRUM-266.

### SCRUM-331 — [BE] View & Read Notifications  (blockedBy 330 ✅) — ✅ DONE (committed `ede04e0`)
**Mục tiêu:** list noti của user (cursor paged, filter `is_read` optional) + mark-as-read (single, idempotent, RBAC-own).

> **⚠️ Scope đã CO LẠI so với plan gốc — phần lớn tầng data đã có sẵn từ SCRUM-330.** Trước khi viết code, đọc lại các file dưới; 331 **KHÔNG** tạo lại repo/cursor/entity.
>
> **ĐÃ CÓ SẴN (330 build, đừng làm lại):**
> - `INotificationRepository` (`Application/Abstractions/INotificationRepository.cs`) đã có đủ 3 method 331 cần: `GetPageAsync(userId, cursor, pageSize, bool? isRead, ct) → (Items, NextCursor?)`, `FindByIdForUserAsync(userId, id, ct)`, `MarkReadAsync(userId, id, ct) → Notification?` (trả `null` nếu không thuộc user).
> - `NotificationRepository` (Infrastructure) đã impl đầy đủ: cursor base64 `(CreatedAt, Id)`, order `CreatedAt DESC, Id DESC`, lấy `pageSize+1`, filter `IsRead`, ownership filter `UserId==userId`. **KHÔNG cần đụng repo trừ khi thêm method mới.**
> - `Notification.MarkRead()` (Domain) đã idempotent (đã read → no-op, không đổi `ReadAt`).
>
> **CÒN LẠI cho 331 = chỉ tầng Application + API (mỏng):**

- **Application:**
  - `Queries/ListNotifications/`: `ListNotificationsQuery(Guid UserId, string? Cursor, int PageSize, bool? IsRead)` + handler (gọi `GetPageAsync`, map entity→`NotificationDto`, build `NotificationPageDto`) + FluentValidation validator **co-located** (**validate/reject `PageSize` ngoài `[1,200]` → 400** (`InclusiveBetween(1,200)` y hệt `GetPriceChangeHistoryQueryValidator`), default 50 — nhất quán DEC-NOT-08; cursor để repo tự `TryDecode`, cursor rác → coi như trang đầu, KHÔNG 400).
  - `Commands/MarkNotificationRead/`: `MarkNotificationReadCommand(Guid UserId, Guid NotificationId)` + handler (gọi `MarkReadAsync`; `null` → `Result.Failure` mã `NOTIFICATION_NOT_FOUND` → map 404) + validator (`NotificationId` không empty). **Mark-all NGOÀI scope v1** (YAGNI — thêm sau nếu FE cần; ghi rõ để reviewer không bắt thiếu).
  - DTO (record, immutable): `NotificationDto(Guid Id, string Type, string Title, string Body, string? Payload, bool IsRead, DateTime? ReadAt, DateTime CreatedAt)` — `Type` serialize snake_case (enum `.ToString()` đã snake_case theo DEC-NOT-02); `NotificationPageDto(IReadOnlyList<NotificationDto> Items, int PageSize, string? NextCursor)` (mẫu `PriceHistoryPageDto`/`CreditStatementPageDto`, map qua `ApiResponse.OkPaged`).
- **Infrastructure:** **KHÔNG có việc mới** (repo đã xong ở 330). Chỉ đảm bảo DI đã đăng ký `INotificationRepository` (330 đã làm) — 331 không thêm.
- **API:** `NotificationController` mới `[ApiController][Route("api/v1/notifications")][Authorize]` (tách khỏi `NotificationDeviceController` ở `.../devices`), dùng `ResolveUserId()` y hệt device controller (lấy `ClaimTypes.NameIdentifier ?? "sub"`, parse Guid, malformed → 401):
  - `GET /api/v1/notifications?cursor=&page_size=&is_read=` → `Ok(ApiResponse.OkPaged(page.Items, page.PageSize, page.NextCursor))`.
  - `PATCH /api/v1/notifications/{id}/read` → success `Ok(ApiResponse.Ok(dto))`; not-found/không thuộc user → `result.Error.ToActionResult()` (404). **userId LUÔN từ JWT, KHÔNG từ body/route** (DEC-NOT-05).
- **Acceptance:**
  - List chỉ trả noti của caller — test user A gọi list KHÔNG thấy noti của user B.
  - Cursor pagination đúng: trang 1 trả `pageSize` item + `NextCursor` khi còn dữ liệu; dùng `NextCursor` trả trang kế không trùng/không sót; hết dữ liệu → `NextCursor=null`. Order `created_at DESC, id DESC`. Cursor rác → trang đầu (không 400/500).
  - Filter `?is_read=false` chỉ trả chưa đọc; `?is_read=true` chỉ trả đã đọc; bỏ trống → tất cả.
  - `page_size` ngoài `[1,200]` → **400** (validate, KHÔNG clamp — nhất quán Pricing; test biên: 0/`-1`/`201` → Fails validation), default 50.
  - Mark-as-read: set `is_read=true` + `read_at` (không null) và trả DTO đã cập nhật; gọi lại lần 2 trên cùng noti → **vẫn 200, no-op** (idempotent, `read_at` KHÔNG đổi).
  - Mark noti KHÔNG thuộc caller (hoặc id không tồn tại) → **404** `NOTIFICATION_NOT_FOUND`, KHÔNG lộ tồn tại (DEC-NOT-05/11).
  - `[Authorize]` — thiếu/hết JWT → 401.
  - **TDD ≥80% tính CẢ tầng Application** (handler + validator + mapper), KHÔNG chỉ controller mock `ISender` (bài học review 327 vòng 1 — §3): phải có unit test chạy handler thật với repo (fake/in-memory) cho list (ownership + cursor + filter + pageSize-validate) và mark-read (success + idempotent + null→failure).
  - Build 0 lỗi; `dotnet ef migrations has-pending-model-changes` **no drift** (331 KHÔNG thêm migration — không đổi schema); `dotnet format --verify-no-changes` sạch.
- **Reuse:** `INotificationRepository`/`NotificationRepository` (đã có từ 330), `GetPriceChangeHistory` cursor pattern, `ApiResponse.OkPaged`, `NotificationDeviceController.ResolveUserId()`, RBAC-own pattern (`RestaurantCreditController`).
- **Tiện thể (opportunistic, KHÔNG bắt buộc để PASS nhưng nên làm vì đang chạm `Notification`):** thêm 3 test guard-clause constructor `Notification` — `userId==Guid.Empty` → `ArgumentException`, `title` rỗng/whitespace → `ArgumentException`, `body` rỗng/whitespace → `ArgumentException` (mẫu test constructor `NotificationDevice` đã có) để kéo `Domain` branch từ 79.41% lên ≥80% (note reviewer-not vòng 2 SCRUM-330). Đặt trong `Domain` test của Notifications.
- **KHÔNG làm:** mark-all, admin-xem-hộ, push-gửi-lại (334), thêm cột schema, sửa repo trừ khi thật sự thiếu method.

### SCRUM-334 — [BE] Store Notification History & Retry Failed Notification  (blockedBy 330 ✅) — ✅ DONE (committed `916311f`)
**Mục tiêu:** lưu trạng thái gửi (history) cho mỗi noti + retry gửi thất bại có bound; provider push thật DEFER (chỉ dựng seam no-op).

> **⚠️ Khác 330/331: 334 ĐỔI SCHEMA → CẦN 1 MIGRATION MỚI.** 330 tạo bảng `notifications`, 331 không đổi schema; 334 **thêm 4 cột** vào `notifications`. Đây là task DUY NHẤT của phần list/retry có migration. Migration create-only, KHÔNG apply DB thật, verify `has-pending-model-changes` = no drift.
>
> **Hiện trạng code (đã verify 2026-07-09):**
> - Entity `Notification` (`Domain/Entities/Notification.cs`) hiện có: `Id, UserId, Type, Title, Body, Payload, IsRead, ReadAt, CreatedAt` + `MarkRead()`. **CHƯA có** field send-status nào.
> - `INotificationWriter.WriteAsync(userId, type, title, body, IReadOnlyDictionary<string,object?>? payload, ct) → Notification`; impl `NotificationWriter` chỉ `new Notification(...)` + `repo.AddAsync`. 3 handler (Credit/OrderConfirmed/OrderCancelled) gọi writer này. **CHƯA** có `IPushSender`, chưa hook gửi push.
> - `AddNotificationsModule` (DI) hiện đăng ký MediatR + ValidationBehavior + `INotificationRepository`/`INotificationDeviceRepository`/`INotificationWriter`/`INotificationRecipientResolver`. **CHƯA** có `AddHostedService` nào trong module Notifications.
> - Mẫu hosted service để COPY: `ScheduledOrderGenerationHostedService` (`Orders.Infrastructure/Jobs/`) — `BackgroundService`, `IServiceScopeFactory` tạo scope mỗi tick, `ReadBool`/`ReadInt` đọc config gate + interval (`PeriodicTimer`, `MinimumIntervalSeconds` floor), `try/catch` nuốt lỗi trừ `OperationCanceledException`. Đăng ký `services.AddHostedService<...>()` cuối `AddNotificationsModule` (mẫu `Orders.Infrastructure/DependencyInjection.cs:51`).

- **Domain:** thêm vào entity `Notification` (DEC-NOT-07, DEC-NOT-11 — metadata mutable, KHÔNG đụng title/body/payload bất biến):
  - enum `NotificationSendStatus { pending, sent, failed }` (`Domain/Enums/`, lưu varchar snake_case như `NotificationType`).
  - property `SendStatus` (default `pending` khi tạo), `AttemptCount` (int, default 0), `LastAttemptAt` (DateTime?), `FailedReason` (string?).
  - method domain: `MarkSent()` (set `sent`, tăng `AttemptCount`, set `LastAttemptAt`, clear `FailedReason`), `MarkFailed(string reason)` (set `failed`, tăng `AttemptCount`, set `LastAttemptAt`, set `FailedReason` — truncate reason hợp lý). Idempotent-safe, giữ pattern private setter + constructor init `SendStatus=pending`.
- **Application:**
  - `Abstractions/IPushSender` (seam, DEC-NOT-06): `Task<Result> SendAsync(Notification notification, CancellationToken ct)` (hoặc nhận (userId, title, body) — chọn shape đủ để resolve device tokens ở impl; v1 impl no-op nên tối giản). Trả `Result` (thành công/thất bại) — KHÔNG throw cho lỗi gửi.
  - Hook gửi vào luồng writer: sau `repo.AddAsync`, `NotificationWriter.WriteAsync` gọi `IPushSender.SendAsync` → `MarkSent()`/`MarkFailed(reason)` + `repo` lưu cập nhật. **Lỗi gửi KHÔNG được ném ngược làm hỏng consumer** (DEC-NOT-07/§3) — catch → `MarkFailed` → để retry job xử lý. (Cân nhắc: tách logic gửi khỏi writer thành 1 coordinator để writer đơn nhiệm — nhưng YAGNI, có thể để trong writer nếu gọn.)
  - `Abstractions/INotificationRepository`: thêm method `GetRetryablePageAsync(int maxAttempts, DateTime backoffThreshold, int batchSize, ct)` (quét `send_status='failed' AND attempt_count<MAX AND (last_attempt_at IS NULL OR last_attempt_at < threshold)`), + `UpdateAsync`/`SaveChangesAsync` seam để lưu status sau retry.
  - Service `INotificationRetryService` + impl (`Services/`): 1 tick = lấy batch retryable → mỗi cái `IPushSender.SendAsync` → `MarkSent`/`MarkFailed` → lưu; đạt MAX → để `failed` (dead-letter, không quét lại). Idempotent theo notification id. Trả count để job log.
- **Infrastructure:**
  - `LogPushSender : IPushSender` (`Infrastructure/Push/` hoặc `Services/`): no-op — `logger.LogInformation` + `return Result.Success()`. Ghi rõ XML/comment 1 dòng: provider thật (FCM/APNs) = follow-up ticket.
  - `NotificationRepository`: impl `GetRetryablePageAsync` + lưu status (AsNoTracking KHÔNG dùng cho path cần update — dùng tracking query cho retry batch).
  - `NotificationConfiguration`: map 4 cột mới (`send_status` varchar(20) HasConversion<string> default `pending`, `attempt_count` int default 0, `last_attempt_at` timestamptz nullable, `failed_reason` text nullable). Cân nhắc index `(send_status, attempt_count, last_attempt_at)` filtered `send_status='failed'` cho retry scan (partial index — mẫu `notification_devices` unique filtered).
  - `NotificationRetryHostedService : BackgroundService` (COPY `ScheduledOrderGenerationHostedService`): config gate `Notifications:Retry:Enabled` (default true/false — chọn default an toàn), `Notifications:Retry:IntervalSeconds`, `Notifications:Retry:MaxAttempts`, `Notifications:Retry:BackoffSeconds`; mỗi tick tạo scope → `INotificationRetryService`. DI: `services.AddScoped<IPushSender, LogPushSender>()` + `services.AddScoped<INotificationRetryService, ...>()` + `services.AddHostedService<NotificationRetryHostedService>()` trong `AddNotificationsModule`.
  - **Migration `AddNotificationSendStatus`** (create-only, no drift): thêm 4 cột + index retry. `dotnet ef migrations add AddNotificationSendStatus --project src/FreshFlow.Infrastructure.Persistence --startup-project src/FreshFlow.API`.
- **Acceptance:**
  - Noti tạo mới có `send_status` set đúng (`sent` nếu push no-op success ở v1, hoặc `pending`→`sent` trong cùng flow) + `attempt_count`/`last_attempt_at` cập nhật.
  - Retry: inject `IPushSender` GIẢ (fail N lần rồi success) → job/service cập nhật `attempt_count` tăng dần, cuối cùng `sent`; chứng minh **tôn trọng MAX** (fail mãi → dừng ở `failed` khi `attempt_count==MAX`, KHÔNG loop vô hạn); backoff theo `last_attempt_at` (noti vừa thử chưa tới threshold → bỏ qua tick này).
  - Consumer resilience: `IPushSender` throw/fail KHÔNG làm `WriteAsync` ném ngược (persist noti vẫn thành công, chỉ `send_status=failed`).
  - Job config-gated: `Enabled=false` → `ExecuteAsync` return sớm, không quét.
  - Provider thật ghi rõ là follow-up (reviewer không bắt "chưa gửi push thật").
  - **TDD ≥80% CẢ Application** (retry service + writer-with-push + domain MarkSent/MarkFailed): test service với repo fake + `IPushSender` giả; test domain method trực tiếp.
  - Build 0 lỗi; migration tạo & `has-pending-model-changes` **no drift**; format sạch.
- **Kèm dọn 2 điểm LOW treo từ review SCRUM-331** (nếu không tốn nhiều công — supervisor yêu cầu):
  - `NotificationController.ListAsync` nhánh `Result.Failure` chưa test qua controller → thêm 1 test controller cho nhánh lỗi (mẫu `MarkReadAsync_NotFound_Returns404`).
  - `NotificationDto` positional-record coverage artifact → **KHÔNG cần** (reviewer xác nhận là hạn chế coverage-tool, không phải logic thiếu test); bỏ qua.
- **Reuse:** `ScheduledOrderGenerationHostedService` (hosted service + config gate + PeriodicTimer + scope-per-tick), retry/backoff pattern, `NotificationWriter` (hook push), `NotificationConfiguration`/`NotificationRepository` (thêm cột + query).
- **Follow-up (NGOÀI epic):** tích hợp FCM/APNs thật vào `IPushSender` — ticket riêng; resolve device tokens qua `INotificationDeviceRepository` (đã có từ 327).
- **Commit khi PASS:** `feat(notifications): SCRUM-334 store notification history & retry failed` (một commit).

---

## 5. Build order khuyến nghị (đã justify)

**327 → 330 → 331 → 334.** (Khớp kỳ vọng supervisor.)

1. **SCRUM-327 trước** vì: self-contained (bảng `notification_devices` độc lập với `Notification`), rủi ro thấp, và **gánh việc bootstrap persistence** cho module (EfAssemblyRegistry.Register, config đầu tiên, migration đầu tiên, repository pattern, ValidationBehavior) — thứ mà 330/331/334 đều tái dùng. Làm nó trước để "khai hoang" module một lần, các task sau chỉ thêm entity/handler. **✅ ĐÃ XONG (PASS).**
2. **SCRUM-330** kế: tạo entity `Notification` + `notifications` + persist writer + resolver, thay log stub 266. Đây là lõi giá trị của epic; 331/334 phụ thuộc entity này. **← KẾ TIẾP.**
3. **SCRUM-331** (list + mark-read) **blockedBy 330** — cần `Notification` entity/table + repo.
4. **SCRUM-334** (history/retry) **blockedBy 330** — cần entity để thêm cột send_status + writer để hook `IPushSender`. Làm cuối vì phụ thuộc nhiều nhất và giá trị nghiệp vụ thấp hơn (infra).

**Phụ thuộc chéo cần biết trước khi code:**
- 330 phụ thuộc **soft** vào bootstrap của 327 (EfAssemblyRegistry). 327 đã xong nên 330 chỉ thêm entity/handler/resolver.
- **SCRUM-330 consumer buildable NGAY:** `OrderConfirmed`, `OrderCancelled`, `CreditLimitThresholdReached`. **BLOCKED-BY epic khác** (không làm đợt này): price_change (Pricing chưa emit integration event), delivery_update (Logistics chưa implement), hub discrepancy (Hub chưa implement). Xem DEC-NOT-03.

---

## 6. Nhật ký tiến độ

| Ngày | Sự kiện |
|---|---|
| 2026-07-08 | Khảo sát module Notifications (stub từ CRE-266 Option B) + `FreshFlow.Contracts` (3 integration event) + docs/03 schema `[PLANNED]` + pattern tái dùng (EfAssemblyRegistry, GetPriceChangeHistory cursor, ScheduledOrderGenerationHostedService, RestaurantReader). Chốt DEC-NOT-01…11. Backlog 4 task BE (327/330/331/334), FE 328/329/332/333 ngoài scope. Build order 327→330→331→334. Chốt scope 330: chỉ 2 consumer buildable (order_status + credit_alert), 3 nguồn còn lại blocked-by epic. Viết plan này. Chưa code, chưa commit. |
| 2026-07-08 | **SCRUM-327 — codex code xong → reviewer-not CHANGES-NEEDED.** Gates OK (build 0 lỗi, 12/12 GREEN, format sạch) nhưng 2 HIGH: (1) coverage tầng Application 16.32% < 80% — handler/validator Unregister/mapper/ValidationBehavior ~0% vì test controller mock `ISender` không chạy handler thật, thiếu test handler + UnregisterValidator + UnregisterAsync + null→404; (2) FK reflection cross-module (`Type.GetType` Auth.User + HasOne) buộc thêm ProjectReference Auth.Infrastructure vào test + hack force-load → vi phạm ranh giới module + fragile. MEDIUM (không chặn): race 2 register đồng thời cùng token mới → DbUpdateException không catch. **Leader chốt DEC-NOT-12** (không model FK cross-module — plain Guid + index, giống `RestaurantCreditConfiguration`; bỏ ref Auth + hack). Chuyển verdict + 3 fix cho supervisor để codex sửa (leader KHÔNG dispatch codex — coding external). |
| 2026-07-08 | **SCRUM-327 ✅ reviewer-not PASS (sau 1 vòng fix).** Cả 3 finding fix & re-verify độc lập: (1) FK → plain Guid + index, migration không FK constraint, test bỏ ref Auth.Infrastructure + bỏ hack force-load (đúng DEC-NOT-12); (2) coverage Application **94.89% line / 90% branch** (handler/validator/mapper/ValidationBehavior/controller = 100%), có test null→404 (`NOTIFICATIONDEVICE_NOT_FOUND`); (3) race → catch `DbUpdateException` gated unique-violation → re-query → reactivate, test mô phỏng competing insert bằng `SaveChangesInterceptor`. Gates: build 0 lỗi, 30/30 GREEN, format sạch, no EF drift. RBAC/IDOR + upsert idempotency + soft-revoke re-check vẫn đúng. Chưa commit (chưa có key). **Kế tiếp: SCRUM-330.** |
| 2026-07-08 | **SCRUM-327 đã COMMIT `a4b5d46`** — `feat(notifications): SCRUM-327 register push device + module persistence bootstrap`. Supervisor cấp key & commit sau khi reviewer-not PASS. |
| 2026-07-09 | **SCRUM-330 — codex implement xong, đang chờ reviewer-not verdict.** Diff (working tree, CHƯA commit): entity `Notification` + enum `NotificationType`; `INotificationRepository`/`INotificationWriter`/`INotificationRecipientResolver` (Application/Abstractions); persist writer trong `Application/Services/`; handler THAY `CreditLimitThresholdReachedIntegrationEventHandler` (bỏ log stub → persist `credit_alert`) + THÊM `OrderConfirmedIntegrationEventHandler`/`OrderCancelledIntegrationEventHandler` (`order_status`); `NotificationConfiguration` + `NotificationRepository` + `NotificationRecipientResolver` (`Infrastructure/CrossModule/`, ToSqlQuery `restaurants.user_id`); DI update; migration `20260708151450_AddNotifications`. Supervisor chạy gate sơ bộ: build/test/format/migration pass, **44/44 test GREEN**. Supervisor spawn lại `reviewer-not` (instance mới) review sâu — **verdict CHƯA về**. Leader KHÔNG commit, KHÔNG đánh dấu PASS; chờ reviewer-not báo verdict trực tiếp cho supervisor. |
| 2026-07-09 | **SCRUM-330 — reviewer-not verdict vòng 1: CHANGES-NEEDED** (KHÔNG có CRITICAL/security; kiến trúc/DEC-NOT-12/10/03/migration/IDOR đều verify ĐÚNG plan — KHÔNG cần đổi code, chỉ bổ sung test). **1 HIGH:** Infrastructure package coverage 76.66% line / 40.9% branch < 80% — `NotificationRepository.GetPageAsync` (cursor paging + isRead filter) và `NotificationCursor` encode/decode hoàn toàn chưa có test. **1 MEDIUM:** `OrderConfirmedIntegrationEventHandler`/`OrderCancelledIntegrationEventHandler` thiếu test nhánh "recipient null → skip" (thiếu ở OrderCancelled) và "writer throws → không throw/nuốt lỗi" (thiếu ở cả 2). **1 LOW:** `NotificationRecipientResolver` guard `Guid.Empty` chưa có test. Supervisor tự soạn & gửi prompt fix cho codex (coding ngoài team). Leader KHÔNG commit, KHÔNG đánh dấu PASS; tiếp tục chuẩn bị plan SCRUM-331, chờ supervisor báo khi 330 PASS. |
| 2026-07-09 | **SCRUM-330 ✅ reviewer-not PASS vòng 2 (sau 1 vòng fix).** Cả 3 finding đóng, re-verify độc lập (coverage class-level, không tin số codex): (HIGH) `GetPageAsync` + `NotificationCursor` 0%→100%/100% — 8 test mới trong `NotificationRepositoryTests.cs` (boundary `pageSize+1`, cursor 2 trang liên tiếp đúng thứ tự, filter isRead true/false, `pageSize<=0` throw, malformed-cursor→trang đầu, cross-user isolation; deterministic `CreatedAt` qua EF Entry thay vì Task.Delay — không flaky); (MEDIUM) 2 order handler line/branch=100% — 3 test mới null-skip (OrderCancelled) + writer-throws-không-throw (cả 2), assertion thật `DidNotReceive()`/`NotThrowAsync()`; (LOW) resolver `Guid.Empty` guard 100% qua SQLite in-memory thật. Gates: build 0 lỗi, **57/57 GREEN**, format sạch, no EF drift. Diff vs vòng 1 = chỉ 4 file test + Sqlite package, KHÔNG đụng production. **Không chặn:** `Domain` branch 79.41% (guard-clause constructor, giữ nguyên từ vòng 1, không phải path mới) — dọn khi chạm lại entity ở 331/334. Verdict gửi thẳng leader; leader relay supervisor, KHÔNG tự commit. **Kế tiếp: supervisor commit 330 → sinh prompt SCRUM-331.** |
| 2026-07-09 | **SCRUM-330 ✅ committed `5514025`** — supervisor commit sau khi reviewer-not PASS vòng 2. Ghi nhận note reviewer về `Domain` branch 79.41%: đồng ý dọn tiện thể khi SCRUM-331 chạm entity `Notification` (thêm test guard-clause constructor), KHÔNG tạo task riêng. **Kế tiếp: SCRUM-331 (list + mark-read), plan §4 sẵn sàng — supervisor sinh prompt codex.** |
| 2026-07-09 | **SCRUM-331 — codex implement xong → reviewer-not review vòng 1: CHANGES-NEEDED do PLAN TỰ MÂU THUẪN (không phải lỗi code).** Gates độc lập: build 0 lỗi, **84/84 GREEN**, format sạch, no drift; coverage App 97.2%/87.5% · Infra 98.57%/90.9% · **Domain 93.81%/88.23%** (branch tăng từ 79.41% nhờ 3 test guard-clause `Notification` constructor — đúng note reviewer vòng 2 SCRUM-330); mọi class 331 100% trừ 2 artifact LOW (positional-record `NotificationDto`, nhánh `Result.Failure` của List controller chưa test). IDOR/404-đồng-nhất/DTO-không-leak-UserId/route-không-conflict đều PASS. **Finding chính:** `page_size` ngoài `[1,200]` → code REJECT (400) qua `InclusiveBetween(1,200)`, KHÔNG clamp như acceptance criteria plan viết. Đối chiếu: code làm ĐÚNG pattern `GetPriceChangeHistoryQueryValidator` (Pricing) mà DEC-NOT-08 trích dẫn làm mẫu — chính chữ "clamp" trong plan sai. **Leader chốt hướng (a):** giữ code reject/400 (nhất quán toàn repo), sửa chữ DEC-NOT-08 + §4 SCRUM-331 "clamp"→"validate/reject [1,200]→400", KHÔNG đổi code/test. Đã đính chính doc. Chờ reviewer-not xác nhận doc khớp → PASS (không cần chạy lại gate). |
| 2026-07-09 | **SCRUM-331 ✅ reviewer-not PASS (sau đính chính doc, KHÔNG đổi code).** reviewer verify `git status`: không `.cs` nào đổi; DEC-NOT-08 + §4 acceptance/validator bullet + §6 đã sửa "clamp"→"reject/400" khớp thực tế + trích đúng lý do (mâu thuẫn pattern Pricing). 2 điểm LOW không chặn (đồng thuận): (1) `NotificationDto` positional-record coverage artifact — không cần test thêm; (2) nhánh `Result.Failure` của `NotificationController.ListAsync` chưa test qua controller — rủi ro rất thấp vì logic reject đã test đủ ở `ListNotificationsQueryValidatorTests` + cơ chế `result.Error.ToActionResult()` cùng controller đã chứng minh qua `MarkReadAsync_NotFound_Returns404Async`; để dành dọn tiện ở SCRUM-334 nếu tiện, không task riêng. Gates (verify vòng trước, code không đổi): build 0 lỗi, 84/84 GREEN, format sạch, no drift, coverage 3 pkg ≥80%. **Kế tiếp: supervisor commit 331 → SCRUM-334 (history + retry, task cuối epic).** |
| 2026-07-09 | **SCRUM-331 ✅ committed `ede04e0`.** Kế tiếp: SCRUM-334 (Store History + Retry Failed) — task CUỐI epic NOT. Leader rà lại plan §4 SCRUM-334 cho khớp code hiện tại (entity `Notification` đã tồn tại từ 330; 334 sẽ THÊM cột send_status → **cần 1 migration mới**, khác 330/331). Kèm dọn 2 điểm LOW treo từ 331 nếu không tốn công. |
| 2026-07-09 | **SCRUM-334 ✅ reviewer-not PASS (round sạch nhất, không finding chặn) — TASK CUỐI epic NOT.** Gates độc lập: build 0 lỗi, **106/106 GREEN**, format sạch, no drift; coverage App 94.44/92.85 · Infra 97.67/86.84 · Domain 94.78/88.23 (cả 3 pkg ≥80% line+branch). 3 điểm verify sâu: (1) `GetRetryablePageAsync` filter đúng `failed && attempt<max && (last_attempt null || <backoff)` — test seed 6 case (Due/Due-null/Recent/Exhausted/Pending/Sent) assert CHỈ 2 Due lọt batch, loại cả pending/sent + test batchSize; (2) `UpdateAsync` trên entity đã tracked = set state Modified, không race/throw; (3) migration `send_status default 'pending' NOT NULL` + `attempt_count default 0` → Postgres backfill row cũ an toàn, không NULL-violation. Consumer resilience 3 tầng (writer try/catch không throw · retry service catch/item · hosted service catch/job). Index filtered `idx_notifications_retry_scan (attempt_count,last_attempt_at) WHERE send_status='failed'`. **SCRUM-331 LOW đã dọn:** `ListAsync_Failure_ReturnsMappedError` test controller nhánh lỗi. Không vi phạm ranh giới (chỉ thêm package `Hosting.Abstractions`). **2 LOW follow-up (không chặn, ngoài epic):** `NotificationRetryService` outer catch chưa log (thêm `ILogger` lần chạm sau) + tick-2 của PeriodicTimer chưa test (khó test không flaky). **EPIC NOT 4/4 PASS (327/330/331/334).** CHỜ supervisor commit 334. |
| 2026-07-09 | **🎉 SCRUM-334 committed `916311f` — EPIC NOT BACKEND HOÀN TẤT 4/4.** Toàn bộ commit epic: SCRUM-327 `a4b5d46` (register device + bootstrap module persistence) → SCRUM-330 `5514025` (persist writer + 3 consumer + recipient resolver, thay stub CRE-266) → SCRUM-331 `ede04e0` (list cursor-paged + mark-read) → SCRUM-334 `916311f` (send-status history + retry hosted service + IPushSender no-op seam). Mỗi task review độc lập bởi reviewer-not (327/330/331 mỗi task 2 vòng; 334 PASS 1 vòng), không còn CRITICAL/HIGH tồn đọng. **Follow-up ngoài epic (không task):** tích hợp FCM/APNs thật vào `IPushSender`; thêm `ILogger` cho `NotificationRetryService` outer catch; consumer price_change/delivery_update/hub-discrepancy chờ epic Pricing/Logistics/Hub phát integration event (DEC-NOT-03). **Doc còn lại chưa commit:** audit doc này + `docs/features/README.md` index (supervisor sẽ hỏi user commit riêng/gộp). |
