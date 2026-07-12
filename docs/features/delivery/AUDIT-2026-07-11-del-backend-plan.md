# Báo cáo khảo sát & kế hoạch triển khai: DEL — Delivery Execution (driver last-mile)

| | |
|---|---|
| **Ngày khảo sát** | 2026-07-11 |
| **Branch** | tách MỚI từ `dev` (đề xuất `SCRUM-299-DEL-Delivery-Execution`) — KHÔNG code trên branch HUB (`SCRUM-256-HUB-Operations`) |
| **Epic** | DEL — Delivery Execution (driver nhận route tại hub → khởi hành → giao từng điểm → cập nhật trạng thái Order). SCRUM key epic + breakdown **CHỜ supervisor** (Jira MCP không khả dụng trong session leader). |
| **Phạm vi** | Feature **driver execution** = **FR-LOG-007** (bị defer khỏi epic LOG SCRUM-298 — xem memory `project_log_epic`) + hook enforcement **FR-HUB-NEW-002 AC2** (block DELIVERING khi có discrepancy mở, defer từ HUB DEC-HUB-07) + realtime giao hàng **FR-NOT-003**. Nằm TRONG module **Logistics** (ĐÃ bootstrap ở epic LOG — KHÔNG bootstrap lại). |
| **Tác giả** | Leader agent (team `backend-dev`) — CHỈ nghiên cứu + lập plan. Code do "codex" (tool ngoài) implement theo prompt supervisor; "reviewer" review độc lập diff của codex. |
| **Mục đích** | Cung cấp đủ ngữ cảnh + quyết định thiết kế để codex implement không cần hỏi lại. Nguồn tham chiếu chính, KHÔNG chỉ TaskList. |
| **Trạng thái** | Khảo sát xong. **4 câu hỏi scope ĐÃ được supervisor chốt 2026-07-11** (đều theo đề xuất leader — xem §6). Prompt codex task #1 sẵn sàng. Chờ SCRUM key thật (Jira MCP timeout) trước khi commit. |

> **Nguồn Jira:** breakdown chưa có (Jira MCP unavailable cho leader session này — giống LOG/HUB). Scope suy ra từ **FR-LOG-007** (`docs/01-requirements-spec.md` dòng 92) + **FR-HUB-NEW-002** (dòng 104) + **FR-NOT-003** (dòng 130) + **FR-LOG-005** (dòng 93, có thể ngoài scope) + DDL `deliveries`/`delivery_routes` (`docs/03-database-schema.md` dòng 876-920) + GA-012/CONFLICT-001 (driver là role thật, order → DELIVERING khi delivery schedule khởi hành).

> **Quy ước cập nhật:** mỗi khi 1 task PASS hoặc có quyết định giữa chừng → cập nhật §0 (bảng trạng thái), §4 (breakdown), §6 (nhật ký). Đồng bộ TaskList + memory `project_del_epic`, không thay thế.

---

## 0. Bảng SCRUM key theo task (JIRA THẬT — supervisor cấp 2026-07-12)

> **6 task BE** (mỗi task kèm 1 FE 316/318/320/322/324/326 — NGOÀI scope BE). **KHÔNG có UC-DEL trong `docs/01`** — cả feature driver chỉ được mô tả bởi **FR-LOG-007** (view routes + update stop ARRIVED/DELIVERED/FAILED + SignalR DeliveryStopUpdated + IDOR). UC-DEL-xx là mã Jira, AC suy từ tiêu đề task + FR-LOG-007 + DEC-DEL.

| SCRUM Key | UC | Tên | Map việc đã làm | Trạng thái code (uncommitted trên branch) |
|---|---|---|---|---|
| **SCRUM-315** | UC-DEL-01+02 | View Assigned Route & Stop Details | = "Driver view routes today" (FR-LOG-007 AC1) | ✅ CODED + reviewer2 PASS sạch. Chờ key→commit |
| **SCRUM-317** | UC-DEL-03 | Confirm Pickup from Hub | ⚠️ liên quan "DispatchRoute" (tạo Delivery records) — actor lệch | 🟡 CODED phần tạo-Delivery (admin DispatchRoute, reviewer2 PASS+DEFER note) NHƯNG scope/actor cần chốt (xem §4) |
| **SCRUM-319** | UC-DEL-04+05+10 | Execute Delivery Route Lifecycle | = "Start route" + "Update stop status" + "Complete route" | 🟡 CODED phần START (StartRoute→Delivering, block discrepancy) — CÒN THIẾU update-stop-status + complete |
| **SCRUM-321** | UC-DEL-06 | Capture Proof of Delivery | MỚI — chưa có | ⬜ chưa làm (tái dùng Cloudinary signed-upload SCRUM-363) |
| **SCRUM-323** | UC-DEL-07 | Report Delivery Issue | MỚI — chưa có | ⬜ chưa làm (khảo sát tái dùng OrderIssue) |
| **SCRUM-325** | UC-DEL-08+09 | Sync Delivery Status | = realtime DeliveryStopUpdated (FR-LOG-007 AC3 + FR-NOT-003) | ⬜ chưa làm — **realtime KHÔNG còn defer** (có key riêng, đảo DEC-DEL-10) |

**Quy ước commit:** `feat(logistics): SCRUM-XXX <desc>` — 1 commit/task đúng key. KHÔNG commit khi chưa xác nhận key (memory `feedback_no_commit_without_jira`).

**⚠️ 2 điểm CẦN SUPERVISOR CHỐT trước khi commit/tiếp (xem §4 + §6):**
1. **SCRUM-317 vs DispatchRoute:** code đã làm là admin-dispatch (`POST /api/v1/logistics/routes/{id}/deliveries`, RBAC admin/ops) tạo Delivery records. "Confirm Pickup from Hub" là **driver-facing**. Hoặc (a) reframe → driver confirm-pickup tự tạo deliveries (đổi actor), hoặc (b) giữ admin-dispatch là bước prep + 317 là layer driver xác nhận trên. HUB-294 đã có driver checkout tại hub (HubHandoverEvent) — cân nhắc trùng lặp.
2. **SCRUM-319 rộng hơn "Start route" đã code:** lifecycle = start (DONE) + update-stop-status ARRIVED/DELIVERED/FAILED (chưa) + complete route (chưa). Gộp cả 3 vào SCRUM-319 (1 commit lớn) hay tách? Đề xuất: 1 task 319 gồm cả 3 (đúng Jira).


## 1. Hiện trạng repo tại thời điểm khảo sát (đã verify code THẬT)

### 1.1 Order state machine (Orders module) — pipeline post-confirm CHƯA có command nào kích hoạt
`Order.cs` (`Orders.Domain/Entities/Order.cs`): enum `Draft/Confirmed/Batched/PickedUp/AtHub/Delivering/Delivered/Cancelled`. `AllowedTransitions` map ĐÃ định nghĩa `AtHub→Delivering→Delivered` (dòng 17-19). Method `AdvanceStatus(next)` (dòng 230) validate transition + `TransitionTo` raise `OrderStatusChangedDomainEvent`.
- **⚠️ KHÔNG có command/handler nào GỌI `AdvanceStatus`** (grep toàn repo: 0 caller ngoài `Order.cs`). Nghĩa là **Batched→PickedUp→AtHub→Delivering→Delivered hoàn toàn CHƯA được wire** — chỉ có enum + method chờ sẵn. DEL phải build command trigger cho ít nhất `AtHub→Delivering` và `Delivering→Delivered`. (Batched/PickedUp/AtHub upstream thuộc batching/hub — ngoài scope DEL; xem DEC-DEL-03.)
- Đã wire sẵn: `RecordActualQuantity` (OrdersController dòng 298 — hub flag shortage), `ConfirmReceipt` (Delivered→confirm receipt). `AdvanceStatus` thì chưa.
- **`OrderStatusChangedDomainEventHandler`** (`Orders.Application/EventHandlers`) ĐÃ broadcast realtime qua `IOrderBroadcastService` → `OrderHub`. ⇒ **realtime trạng thái Order (AC FR-ORD-003, FR-NOT-002) TỰ ĐỘNG có sẵn** khi DEL gọi `AdvanceStatus`. DEL KHÔNG cần thêm SignalR cho order status.

### 1.2 Bảng `deliveries` — `[PLANNED]`, CHƯA có trong DB (docs/03 dòng 898-920)
- KHÔNG có entity `Delivery` trong Logistics (`find` = 0). DEL tạo MỚI.
- DDL roadmap: `deliveries(id, delivery_route_id FK routes, order_id UNIQUE, sequence_number, status delivery_status DEFAULT pending, estimated_arrival, actual_arrival, failure_reason, created_at, updated_at)`. **Ta điều chỉnh:** `order_id` = cross-module (Orders) → **Guid + unique index, KHÔNG FK** (DEC-DEL-02); `delivery_route_id` = cùng module Logistics → **GIỮ FK nội bộ**. `delivery_status` = **VARCHAR + CHECK** (pending/arrived/delivered/failed), KHÔNG PG enum (convention repo, giống DeliveryRoute/HubInboundEvent).
- `delivery_status` enum VALUES không được khai tường minh trong docs/03 (chỉ tham chiếu + payload ví dụ `"delivered"`). Suy từ FR-LOG-007 AC2: `ARRIVED`, `DELIVERED`, `FAILED` (+ `pending` khởi tạo).

### 1.3 `DeliveryRoute` (Logistics, epic LOG xong) — có driver, KHÔNG có in-progress/completed status
`DeliveryRoute.cs`: `Id, RouteType, Status(planned/selected/reviewed/assigned/cancelled), ServiceDate, Stops(JSONB List<RouteStop>), VehicleId?, DriverUserId?, OrderGroupId?, actual_start_at/actual_end_at (docs/03 có cột, entity CHƯA map), ...`.
- `Assign(vehicleId, driverUserId)` (dòng 143, SCRUM-307) set `DriverUserId` + `Status=assigned`. ⇒ **route đã mang driver khi assigned** — driver execution đọc từ đây.
- **⚠️ RouteStatus KHÔNG có `in_progress`/`completed`.** DEL "start route" cần thêm trạng thái khởi hành/hoàn tất (hoặc dùng `actual_start_at`/`actual_end_at` — docs/03 dòng 884-885 có cột nhưng entity chưa map). Đây là thay đổi domain NỘI BỘ Logistics (được phép, cùng module).
- `RouteStop` (value object): `(StopOrder, EntityType{market|restaurant}, EntityId, EntityName, Lat, Lng, EstimatedArrivalAt, EstimatedDepartureAt)`. **`EntityId` của restaurant stop = `restaurantId`, KHÔNG phải `orderId`.** ⇒ route KHÔNG tự suy ra được danh sách order (xem DEC-DEL-03).

### 1.4 ⚠️ Linkage route → orders BỊ ĐỨT (gap lớn nhất của epic)
- **KHÔNG có entity `OrderGroup`** (grep `class OrderGroup` = 0). **Batching FR-ORD-005 CHƯA build.** `Order.OrderGroupId` + `DeliveryRoute.OrderGroupId` = nullable Guid nhưng **KHÔNG có code nào populate** chúng.
- `deliveries.order_id` cần biết đơn hàng nào thuộc route. Route stops chỉ có restaurantId. ⇒ **không có đường tự động route → orders**. Đây là điều kiện tiên quyết để tạo `deliveries`. Xem **DEC-DEL-03** (✅ CHỐT = option (a) explicit orderIds).

### 1.5 Hub handover + discrepancy (epic HUB SCRUM-256, gần xong)
- `HubHandoverEvent` (Hub.Domain): handover tại hub cho driver 1 route, `ConfirmCheckout(driverUserId)` (driver-only). Đây là **điểm bắt đầu journey của driver** — sau checkout, driver khởi hành (DEL bắt đầu từ đây).
- **`IHubDiscrepancyReader.HasOpenDiscrepanciesForOrderAsync(orderId, ct)`** (Hub.Application/Abstractions) ĐÃ tồn tại + DI-registered (`Hub.Infrastructure/DependencyInjection.cs:38`). Dùng cho DEC-HUB-07 (block DELIVERING). **⚠️ Đây ở Hub.Application — Logistics/Orders KHÔNG được reference (cross-module).** ⇒ DEL phải tạo **read-seam RIÊNG** vào `hub_discrepancies` (ToSqlQuery `WHERE status='OPEN'`), KHÔNG dùng interface của Hub. Xem DEC-DEL-05.
- Bảng `hub_discrepancies` đã có trong DB (migration `20260710161631_AddHubDiscrepancies`), cột `order_id`, `status` ('OPEN'/'ACKNOWLEDGED') snake_case.

### 1.6 SignalR / realtime — as-is
`Program.cs` map CHỈ `PricingHub` (`/hubs/pricing`) + `OrderHub` (`/hubs/orders`). **KHÔNG có `DeliveryHub`.** `OrderHub` + `IOrderBroadcastService` đã broadcast OrderStatusChanged. FR-NOT-003 (DeliveryStarted/DeliveryCompleted per-restaurant, <500ms) = realtime **delivery-schedule-level** — CHƯA có hub/consumer. Nhất quán DEC-HUB-08 + NOT epic → **defer live SignalR, persist DB** (DEC-DEL-10).

### 1.7 Cross-module patterns (TÁI DÙNG — KHÔNG viết mới)
- **Read-seam:** `Logistics.Infrastructure/CrossModule/` (`DriverReader`/`RestaurantCoordinateReader`...) = mẫu chuẩn (`HasNoKey()` + `ToSqlQuery` + `Reader(AppDbContext db)`, interface ở `*.Application/Abstractions`). DEL thêm seam `hub_discrepancies` (snake_case) + có thể seam orders (đọc order status/restaurant).
- **Cross-module state change = integration event, KHÔNG gọi trực tiếp.** Logistics KHÔNG được gọi `Order.AdvanceStatus` (Orders-owned). Mẫu: raise/publish integration event ở Contracts → Orders consume qua `INotificationHandler`. Xem DEC-DEL-04.
- **Contracts hiện có:** `OrderConfirmed/OrderCancelled/CreditLimitThresholdReached/HubDiscrepancyRecorded/RestaurantRefundIssued`. DEL thêm event mới (DEC-DEL-04).
- **Casing:** bảng Logistics tự sở hữu (`deliveries`, `delivery_routes`) = **snake_case explicit `HasColumnName`** (memory `project_ef_column_casing`). `hub_discrepancies` seam = snake_case (Hub tự sở hữu). `orders`/`order_items` seam (nếu cần) = **PascalCase EF default**.

---

## 2. Quyết định thiết kế (DEC-DEL) — leader chốt theo code THẬT + convention

- **DEC-DEL-01 — Module = Logistics, KHÔNG bootstrap.** Logistics đã có DI/`AddLogisticsModule`, EF config, entry `ForceLoadModuleAssemblies`, `tests/Unit/FreshFlow.Logistics.UnitTests`. DEL chỉ THÊM entity `Delivery` + configs + commands/queries driver-facing + migration. KHÔNG tạo module/test-project mới.
- **DEC-DEL-02 — Bảng MỚI `deliveries` (override [PLANNED] + bỏ FK cross-module).** `id, delivery_route_id (FK delivery_routes — GIỮ, cùng module), order_id (Guid + UNIQUE index, KHÔNG FK cross-module), sequence_number INT CHECK>0, status VARCHAR(20) CHECK(pending/arrived/delivered/failed) DEFAULT pending, estimated_arrival TIMESTAMPTZ?, actual_arrival TIMESTAMPTZ?, failure_reason TEXT?, created_at, updated_at`. snake_case `HasColumnName`. Entity `Delivery` = **plain `sealed class`** (mẫu `DeliveryRoute`/`HubHandoverEvent` — Logistics KHÔNG dùng `AggregateRoot`), `Create` factory + `MarkArrived/MarkDelivered/MarkFailed` methods, string status consts.
- **DEC-DEL-03 (✅ CHỐT supervisor 2026-07-11 = option (a)) — Linkage route → orders (tạo `deliveries`).** Batching/OrderGroup (FR-ORD-005) CHƯA build → route không tự suy order list. 3 lựa chọn:
  - **(a) [leader đề xuất, MVP]** Command admin "dispatch route" nhận **`orderIds[]` tường minh**, validate mỗi order ở trạng thái `AtHub` + restaurant khớp 1 stop của route; tạo 1 `delivery`/order (sequence từ stop order). Defer auto-batch. Đơn giản, không đụng Orders batching.
  - **(b)** Build tối thiểu linkage: DEL populate `route.OrderGroupId`/`Order.OrderGroupId` + query orders theo group. Mở rộng scope sang Orders.
  - **(c)** Defer toàn bộ tạo-delivery sang epic batching riêng; DEL chỉ build driver-update trên deliveries giả định đã tồn tại (rủi ro: không test được E2E).
  - → **CHỐT (a):** command `DispatchRouteCommand(routeId, orderIds[])`, validate mỗi order `AtHub` + chưa-có-delivery, tạo 1 delivery/order. Defer auto-batch/OrderGroup. **⚠️ restaurant-khớp-stop DEFER ở MVP task #1** (supervisor chốt 2026-07-11): chỉ enforce `AtHub`, KHÔNG check `Order.RestaurantId ∈ stop route. Lý do: rủi ro thấp — endpoint chỉ `admin,operations_manager` (nội bộ, không driver-facing); fix sau nếu cần (mở rộng seam trả `RestaurantId`).
- **DEC-DEL-04 — Cross-module Order transition = integration event, KHÔNG direct call.** Driver "start" / "mark delivered" xảy ra ở Logistics; Order status đổi ở Orders. Luồng:
  - Start route → Logistics command → publish `DeliveryStartedIntegrationEvent(OrderId, RouteId, OccurredAt)` (mỗi order) → **Orders** consume (`INotificationHandler`) → `order.AdvanceStatus(Delivering)` (đã raise OrderStatusChanged → OrderHub broadcast sẵn).
  - Mark DELIVERED → Logistics update delivery → publish `DeliveryCompletedIntegrationEvent(OrderId, RouteId, ActualArrivalAt, OccurredAt)` → Orders → `order.AdvanceStatus(Delivered)`.
  - `Delivery` = plain class (không AggregateRoot) → **handler publish integration event TRỰC TIẾP qua `IPublisher`** sau SaveChanges (interceptor chỉ dispatch AggregateRoot; đơn giản hơn, giống 292 không domain event). Events MỚI vào `FreshFlow.Contracts`.
- **DEC-DEL-05 (✅ CHỐT supervisor 2026-07-11 = chặn CẢ route) — Block `AtHub→Delivering` khi có discrepancy mở → 409 `PENDING_HUB_DISCREPANCY` (FR-HUB-NEW-002 AC2 / DEC-HUB-07).** Enforce ĐỒNG BỘ tại Logistics "start delivery" command (driver nhận 409 ngay), KHÔNG chờ async ở Orders. Logistics tạo **read-seam riêng** `IHubDiscrepancyStatusReader` (ToSqlQuery `SELECT order_id FROM hub_discrepancies WHERE status='OPEN' AND deleted_at IS NULL AND order_id = ANY(:ids)`) — KHÔNG reference `IHubDiscrepancyReader` của Hub.Application. **Câu hỏi granularity (cần chốt):** start route có nhiều order → chặn TOÀN route nếu BẤT KỲ order nào có discrepancy mở, hay chỉ skip order đó? **CHỐT: chặn CẢ route** — start route trả 409 `PENDING_HUB_DISCREPANCY` nếu BẤT KỲ order nào của route có discrepancy mở (kèm danh sách orderId vướng).
- **DEC-DEL-06 — Driver-only + own-route IDOR.** `GET /api/v1/driver/routes/today` + `PATCH /api/v1/driver/deliveries/{id}/status` + start command = RBAC `driver` (+ admin/ops nếu cần giám sát — mặc định driver-only cho self-service, giống DEC-HUB-11 sửa checkout). Verify `route.DriverUserId == JWT sub` (delivery → route → driver), lệch → 403 `FORBIDDEN`. Mẫu identity check = `DriverCheckoutCommandHandler`.
- **DEC-DEL-07 — API namespace `/api/v1/...`.** FR viết `/api/driver/...` → chuẩn hoá `/api/v1/driver/routes/today`, `/api/v1/driver/deliveries/{deliveryId}/status`, `POST /api/v1/driver/routes/{routeId}/start` (hoặc admin dispatch `/api/v1/logistics/routes/{routeId}/deliveries`). Giữ shape path AC.
- **DEC-DEL-08 — Route lifecycle mở rộng nội bộ Logistics.** Thêm `RouteStatus` `in_progress`/`completed` HOẶC map `actual_start_at`/`actual_end_at` (docs/03 đã có cột). Đề xuất: map 2 cột thời gian + method `DeliveryRoute.Start()`/`Complete()` (guard `assigned`→start, mọi delivery done→complete). Cùng module Logistics, không cross-module.
- **DEC-DEL-09 (✅ CHỐT supervisor 2026-07-11 = NGOÀI scope) — FR-LOG-005 (delivery schedules: plannedDepartureAt + capacity check) NGOÀI scope DEL MVP.** "Schedule" là artifact PLANNING của admin (giờ khởi hành dự kiến, check tải trọng vs order group). DEL là EXECUTION. Route đã mang assignment; MVP coi "route assigned + driver Start" là trigger execution, KHÔNG cần entity schedule riêng. Supervisor chốt loại khỏi DEL — thuộc epic logistics-planning riêng sau này. Task 5 huỷ.
- **DEC-DEL-10 (⚠️ ĐẢO 2026-07-12) — Realtime KHÔNG còn defer: thuộc SCRUM-325 (Sync Delivery Status).** Trước chốt defer, nhưng Jira thật có key riêng cho realtime ⇒ build trong SCRUM-325 (DeliveryHub hoặc OrderHub + persist qua Notifications). Order-status realtime (AtHub→Delivering→Delivered) vẫn FREE qua OrderHub sẵn có. Order status realtime đã có qua OrderHub (DEC-DEL-04). Delivery-level `DeliveryStarted`/`DeliveryCompleted`/`DeliveryStopUpdated` (FR-NOT-003, FR-LOG-007 AC3) → **Notifications consume integration event + `INotificationWriter.WriteAsync` persist DB** (mẫu NOT epic). DeliveryHub live SignalR = follow-up khi backplane sẵn sàng. Nhất quán DEC-HUB-08 + memory `project_not_epic` (delivery_update blocked-by epic này — nay unblock phần persist).

---

## 3. Rủi ro & bề mặt bảo mật

- **RBAC/IDOR:** driver chỉ thấy/đổi route+delivery của CHÍNH mình — verify `route.DriverUserId == JWT sub` mọi endpoint (403). KHÔNG tin `deliveryId`/`routeId` client → load + check owner. Mẫu chống IDOR = `DriverCheckoutCommandHandler`.
- **Cross-module integrity:** `order_id`/`driver_user_id` = Guid trần, validate tồn tại qua seam khi tạo delivery (order phải AtHub); KHÔNG FK cross-module (DEC-DEL-02).
- **State machine an toàn:** `AdvanceStatus` đã guard transition (Conflict `ORDER_INVALID_TRANSITION`) — Orders handler map lỗi hợp lý; delivery status chỉ tiến (pending→arrived→delivered/failed), guard ở domain method.
- **Discrepancy block (DEC-DEL-05):** đọc `hub_discrepancies` status='OPEN' trước khi start; race (discrepancy tạo sau khi start) = chấp nhận MVP (ghi rõ). KHÔNG lộ nội dung discrepancy ra driver — chỉ 409 + orderId.
- **Idempotency:** start route 2 lần / mark delivered 2 lần → domain guard (route đã in_progress → 409; delivery đã delivered → 409). Integration event re-deliver → Orders `AdvanceStatus` tự no-op/409 (transition không hợp lệ) — an toàn.
- **Timezone:** `routes/today` filter theo `Asia/Ho_Chi_Minh` service_date; lưu UTC (mẫu Orders).
- **Migration an toàn:** create-only, no-drift (`dotnet ef migrations has-pending-model-changes`), KHÔNG apply DB thật, KHÔNG commit khi chưa có key. `dotnet build-server shutdown` sau mỗi lệnh dotnet (memory `feedback_dotnet_cleanup`).

---

## 4. Phân rã task theo SCRUM key THẬT

> Giữ nguyên toàn bộ DEC-DEL kỹ thuật ở §2 (vẫn đúng). Code đã làm cho 315/317-partial/319-partial nằm uncommitted trên branch, reviewer2 đã PASS phần tương ứng.

### SCRUM-315 — View Assigned Route & Stop Details (UC-DEL-01+02) — ✅ CODED, PASS
= query `GetDriverRoutesTodayQuery` + `GET /api/v1/driver/routes/today` (RBAC driver, IDOR bằng JWT). Trả route assigned/in_progress hôm nay (VN tz) + stops sequence + deliveries. reviewer2 PASS sạch 0 finding (318/318 Logistics). **Chỉ chờ SCRUM key để commit** `feat(logistics): SCRUM-315 ...`.

### SCRUM-317 — Confirm Pickup from Hub (UC-DEL-03) — ✅ CHỐT (a): reframe driver-facing
**Supervisor chốt 2026-07-12 = option (a).** Rework code đã có (admin DispatchRoute → driver confirm-pickup):
- Endpoint MỚI `POST /api/v1/driver/routes/{routeId}/confirm-pickup` trên `DriverController`, RBAC `driver`. **BỎ** endpoint admin-dispatch (`POST /api/v1/logistics/routes/{id}/deliveries`) + RoutesController action.
- Command `ConfirmPickupCommand(routeId, driverUserId (JWT), orderIds[])`. Tái dùng gần nguyên logic tạo Delivery (AtHub + chưa-có-delivery + all-or-nothing). Thêm **IDOR**: `route.DriverUserId == driverUserId` else 403.
- **⚠️ restaurant-stop-match RE-ENABLE (đảo DEFER cũ):** DEFER trước dựa trên "admin-only rủi ro thấp". Nay actor = **driver** (thấp tin cậy) tự truyền `orderIds` → driver có thể gắn order AtHub bất kỳ vào route mình. ⇒ **bật lại check** `order.RestaurantId ∈ {stop.EntityId | stop.EntityType=restaurant}` else 422 `ORDER_NOT_ON_ROUTE` (seam trả thêm `RestaurantId`). Guard toàn vẹn trust-boundary — KHÔNG defer cho driver-facing.
- HUB-294 `HubHandoverEvent.ConfirmCheckout` = driver checkout góc hub-inventory; 317 = góc order/delivery (tạo delivery records). Coexist.

### SCRUM-319 — Execute Delivery Route Lifecycle (UC-DEL-04+05+10) — ✅ CHỐT gộp 3-trong-1 (supervisor 2026-07-12)
1 task/1 commit gồm cả vòng đời (start đã code + bổ sung update-stop + complete):
- **START (✅ CODED, = "task #3"):** `StartRouteCommand` `POST /api/v1/driver/routes/{routeId}/start` → `route.Start()` assigned→in_progress; **block discrepancy** qua seam Logistics-owned `hub_discrepancies` (any OPEN → 409 `PENDING_HUB_DISCREPANCY`, all-or-nothing); publish `DeliveryStartedIntegrationEvent(RouteId, OrderIds[])`; Orders consumer `AdvanceStatus(Delivering)`. RBAC driver + IDOR. `RouteStatus.in_progress` (no migration). Chờ reviewer2 review + gate.
- **UPDATE STOP STATUS (⬜ CHƯA — FR-LOG-007 AC2/AC3):** `PATCH /api/v1/driver/deliveries/{deliveryId}/status` `{status: ARRIVED|DELIVERED|FAILED, failureReason?}` RBAC driver own-route (403). `Delivery.MarkArrived/MarkDelivered/MarkFailed` (đã có ở entity task#1). DELIVERED → publish `DeliveryCompletedIntegrationEvent(OrderId,...)` → Orders `AdvanceStatus(Delivered)`. **⚠️ idempotency `MarkArrived` gọi 2 lần** (reviewer2 note): chặn 409 ở handler nếu status đã qua.
- **COMPLETE ROUTE (⬜ CHƯA):** khi mọi delivery của route = delivered/failed → `route.Complete()` (in_progress→`completed`, thêm enum value — no migration). 
- **Realtime** `DeliveryStopUpdated` (AC3) → thuộc **SCRUM-325**, KHÔNG làm ở đây (chỉ persist Order status qua OrderHub sẵn có khi advance).

### SCRUM-321 — Capture Proof of Delivery (UC-DEL-06) — ⬜ PLANNED (prompt codex sẵn sàng)
Driver chụp ảnh bằng chứng khi giao xong. **Chốt thiết kế (DEC-DEL-11), prompt codex `codex-prompt-scrum321.md` đã soạn:**
- **POD OPTIONAL, độc lập DELIVERED** (KHÔNG bắt buộc trước mark-delivered, KHÔNG chặn luồng giao). Cơ sở: FR-LOG-007 (docs/01 dòng 92) chỉ có view route + update stop status + realtime + IDOR — KHÔNG AC nào bắt buộc POD; không lock luồng chính khi upload lỗi mạng. Driver upload trước/trong/sau đều được.
- **Lưu 1 cột `proof_url VARCHAR(512)` nullable** trên `deliveries` (migration `AddDeliveryProofUrl`, ADD COLUMN duy nhất). KHÔNG bảng riêng (1 ảnh/delivery, YAGNI). **KHÔNG** `proof_signature_url` (chữ ký riêng DEFER — thêm chỉ khi FE có yêu cầu cụ thể). `docs/03` chưa có cột proof → thêm mới.
- **Tái dùng Cloudinary signed-upload SCRUM-363** (`ICloudinarySignatureService.Sign(folder)`, mẫu `CreateAvatarUploadSignatureCommandHandler`) — folder `freshflow/proof-of-delivery`. **2-step** (mẫu avatar): (1) `POST /api/v1/driver/deliveries/{id}/proof-of-delivery/upload-signature` cấp signature (IDOR delivery→route→driver, 404/403); (2) `PUT /api/v1/driver/deliveries/{id}/proof-of-delivery` `{proofUrl}` persist (IDOR + validator: https, ≤512, **host `res.cloudinary.com`** — chống lưu URL tùy ý → stored-XSS/SSRF). Domain `Delivery.AttachProof(url)` cho phép mọi status.
- Thêm `IDeliveryRepository.FindByIdAsync` (tracking). KHÔNG integration event / cross-module / realtime / đổi Order status (realtime = SCRUM-325).

### SCRUM-323 — Report Delivery Issue (UC-DEL-07) — ⬜ MỚI
Driver báo vấn đề khi giao (không giao được/hư hỏng/khách từ chối...). **Khác** `HubDiscrepancy` (hub-side, trước dispatch) và `OrderIssue` (Orders, reported by hub/ops). Khảo sát: `OrderIssue` (Orders.Domain, IssueType Missing/Wrong/Damaged, Status Open/Resolved) CÓ THỂ tái dùng nếu cho phép driver là reporter + thêm issue type delivery — HOẶC bảng `delivery_issues` riêng (Logistics-owned). `deliveries.failure_reason` (đã có) chỉ là text thô — issue là record có cấu trúc + trạng thái. Đề xuất khảo sát kỹ khi tới task; nghiêng về bảng Logistics riêng để không cross-module-write OrderIssue.

### SCRUM-325 — Sync Delivery Status (UC-DEL-08+09) — ⬜ MỚI (realtime, KHÔNG defer)
= FR-LOG-007 AC3 `DeliveryStopUpdated` SignalR tới restaurant group + FR-NOT-003 (DeliveryStarted/Completed) + có thể offline-sync (AC4 "persist, no retry"). **Đảo DEC-DEL-10** (không còn defer vì có key riêng). Cần chốt: build `DeliveryHub` thật (map trong Program.cs, backplane Redis chưa cấu hình — có thể vẫn chỉ single-node) hay tái dùng OrderHub. Persist notification qua Notifications (mẫu NOT epic). Đây là task realtime — làm SAU 319 (cần các integration event Delivery* tồn tại).

## 5. Thứ tự phụ thuộc & lý do
```
SCRUM-315 (view) ✅ + SCRUM-317 (confirm pickup → tạo deliveries) 🟡
   └─> SCRUM-319 (lifecycle: start ✅ / update-stop ⬜ / complete ⬜)
          ├─> SCRUM-321 (POD: chụp ảnh khi DELIVERED)
          ├─> SCRUM-323 (report issue khi giao lỗi)
          └─> SCRUM-325 (sync realtime — cần Delivery* integration events từ 319)
```
Ghi chú: 315/317/319-start đã code trên branch (uncommitted, chờ key + review). 319-còn-lại/321/323/325 chưa làm.


## 6. Nhật ký khảo sát & quyết định
- **2026-07-11** — Khảo sát code THẬT xong. Xác nhận: (1) Order pipeline post-confirm CHƯA có command trigger nào (`AdvanceStatus` 0 caller) — DEL build `AtHub→Delivering→Delivered`; (2) bảng `deliveries` [PLANNED] chưa có, entity Delivery chưa có; (3) **linkage route→orders BỊ ĐỨT** (OrderGroup/batching chưa build, route stops chỉ có restaurantId) — DEC-DEL-03 ⚠️OPEN, đề xuất option (a) explicit orderIds; (4) `IHubDiscrepancyReader` ở Hub.Application cross-module → DEL cần seam riêng vào `hub_discrepancies` (DEC-DEL-05); (5) OrderHub đã broadcast OrderStatusChanged → realtime order status FREE; (6) cross-module Order transition PHẢI qua integration event (DEC-DEL-04), không direct call. **4 câu hỏi scope mở gửi supervisor: DEC-DEL-03 (linkage), DEC-DEL-05 (block granularity), DEC-DEL-09 (FR-LOG-005 in/out), DEC-DEL-10 (realtime live in/out).** Module = Logistics (không bootstrap). Chờ SCRUM key + chốt scope trước khi phát task #1.
- **2026-07-11 (supervisor chốt 4 câu scope)** — Cả 4 theo đề xuất leader: (1) DEC-DEL-03 = option (a) explicit `orderIds[]`, defer auto-batch; (2) DEC-DEL-05 = chặn CẢ route nếu bất kỳ order có discrepancy mở; (3) DEC-DEL-09 = FR-LOG-005 NGOÀI scope; (4) DEC-DEL-10 = DeliveryHub live DEFER, persist DB. Build order chốt 4 task core (5/6 huỷ). Prompt codex task #1 soạn xong, gửi main. **Chưa tạo branch `SCRUM-299-DEL-Delivery-Execution`:** working tree đang có HUB SCRUM-294 uncommitted (HubHandoverController + handlers + tests + appsettings) — tạo branch lúc này sẽ kéo work HUB sang DEL. Đề xuất tạo branch sau khi HUB 294 commit. Jira MCP timeout → key task #1 = placeholder `SCRUM-XXX`, main điền khi có.
- **2026-07-11 (task #1 review — reviewer2 MEDIUM)** — Reviewer PASS 6/6 + gate (2064/2064, no drift, format sạch) NHƯNG bắt 1 MEDIUM: prompt/breakdown task #1 lược mất điều kiện **restaurant-khớp-stop** của DEC-DEL-03 gốc (chỉ enforce AtHub). Đây là guard toàn vẹn tại trust-boundary (admin ghép order↔route; thiếu check → tạo delivery cho order mà route không hề ghé restaurant đó). ~~Đề xuất ban đầu: bổ sung ngay~~ → **SUPERSEDED bởi supervisor 2026-07-11 = DEFER** (xem log dưới). Note phụ (không chặn): `MarkArrived()` gọi 2 lần không bị chặn (Arrived không terminal) → xử lý idempotency ở handler task #4 nếu cần 409.
- **2026-07-11 (supervisor override — DEFER restaurant-match)** — Crossed message: supervisor chốt **DEFER có ghi chú**, KHÔNG fix ngay. Lý do: rủi ro thấp — `DispatchRouteCommand` chỉ `admin,operations_manager` gọi (nội bộ, không driver-facing), ưu tiên tiến độ. Patch seam+stop-match ĐÃ HUỶ, KHÔNG gửi codex. Task #1 giữ nguyên như codex làm (enforce `AtHub` + chưa-có-delivery), sẵn sàng PASS + commit khi có key. Audit doc §4/DEC-DEL-03 sửa lại theo hướng DEFER. Fix sau (mở rộng seam trả `RestaurantId` + check `order.RestaurantId ∈` restaurant-stops → 422 `ORDER_NOT_ON_ROUTE`) nếu/khi cần.
- **2026-07-11 (task #3 prep — verify code thật Orders)** — Chốt cơ chế cross-module cho task #3 (Start route): (1) Orders KHÔNG có command AdvanceStatus (0 caller) → dùng **integration-event consumer** mẫu `HubDiscrepancyRecordedIntegrationEventHandler` (FindByIdAsync→AdvanceStatus→SaveChanges, try/catch log-swallow), KHÔNG tạo command. (2) Event mới `DeliveryStartedIntegrationEvent(RouteId, OrderIds[], OccurredAt)`; Logistics publish TRỰC TIẾP qua IPublisher sau save (route/Delivery là plain class, không domain-event). (3) Discrepancy block ĐỒNG BỘ trong Logistics command (409 cho driver ngay) qua **seam Logistics-owned** đọc `hub_discrepancies` (ToSqlQuery, status='OPEN') — KHÔNG ref `IHubDiscrepancyReader` của Hub (cross-module). (4) `RouteStatus.in_progress` thêm vào enum — **KHÔNG migration** (status=varchar(20) không check-constraint, HasConversion<string>). (5) OrderHub đã broadcast OrderStatusChanged → **KHÔNG thêm Notifications consumer** task #3 (persisted delivery-notif = follow-up). (6) best-effort không atomic route↔order (giống refund precedent). Prompt task #3 gửi main.
- **2026-07-12 (REMAP sang Jira key thật)** — Supervisor cấp 6 BE key: 315 View Route+Stops / 317 Confirm Pickup / 319 Execute Lifecycle / 321 Proof of Delivery / 323 Report Issue / 325 Sync Status. **`docs/01` KHÔNG có UC-DEL** — scope suy từ FR-LOG-007 + tiêu đề. **Phát hiện: task #1/#2/#3 (build order cũ) ĐÃ code sẵn uncommitted trên branch** (Delivery+DispatchRoute, GetDriverRoutesToday, StartRoute+DeliveryStartedIntegrationEvent+Orders consumer+RouteStatus.in_progress). Remap: #2→**315** (PASS), #1→**317** (nhưng actor admin vs driver — CẦN CHỐT), #3→**319 phần START** (319 còn thiếu update-stop-status + complete). 321/323/325 MỚI. **Đảo DEC-DEL-10** (realtime = 325, hết defer). §0/§4/§5 viết lại theo key thật. **2 câu hỏi gửi supervisor:** (1) 317 = reframe DispatchRoute thành driver confirm-pickup hay giữ admin-dispatch + layer driver; (2) 319 gộp start+update+complete thành 1 task. KHÔNG chạy codex tiếp 319-remainder tới khi chốt.
- **2026-07-12 (chốt Q1+Q2)** — Supervisor: (Q1) SCRUM-317 = option (a) driver `POST /api/v1/driver/routes/{routeId}/confirm-pickup`, bỏ admin-dispatch, rework code cũ. **Leader bật lại restaurant-stop-match** cho 317 (actor driver → hết cơ sở DEFER 'admin-only', là guard trust-boundary). (Q2) SCRUM-319 gộp start+update-stop+complete vào 1 task, phần start đã code → prompt BỔ SUNG update-stop (ARRIVED/DELIVERED/FAILED, DELIVERED→DeliveryCompletedIntegrationEvent→Orders AdvanceStatus(Delivered); FAILED lưu reason, order KHÔNG advance vì state machine không có failed) + complete route (mọi delivery terminal → route.Complete() in_progress→completed, thêm enum no-migration). Realtime DeliveryStopUpdated = SCRUM-325, KHÔNG ở 319. SCRUM-315 commit riêng ngay. Soạn prompt 317-rework + 319-remainder.

- **2026-07-12 (SCRUM-321 plan — POD)** — Khảo sát Cloudinary signed-upload (SCRUM-363) code THẬT: `ICloudinarySignatureService.Sign(CloudinarySignatureRequest(folder))`→`CloudinarySignatureResult`, handler chỉ gọi `signer.Sign(folder)` + map `UploadSignatureResponse` (BE KHÔNG lưu file). Persist URL = bước 2 riêng (mẫu avatar: `POST me/avatar/upload-signature` cấp chữ ký + `PUT me` lưu `AvatarUrl` với validator https/≤512). **DEC-DEL-11 chốt (leader, theo YAGNI/ponytail):** (1) POD **OPTIONAL độc lập DELIVERED** — FR-LOG-007 KHÔNG có AC bắt buộc POD (verify docs/01 dòng 92), không chặn luồng chính; (2) **1 cột `proof_url VARCHAR(512)` nullable** trên `deliveries` (migration ADD COLUMN), KHÔNG bảng riêng, KHÔNG `proof_signature_url` (chữ ký DEFER); (3) tái dùng Cloudinary 2-step, folder `freshflow/proof-of-delivery`, IDOR delivery→route→driver ở CẢ 2 endpoint (403), validator ép host `res.cloudinary.com` (chống lưu URL tùy ý → stored-XSS/SSRF); (4) thêm `IDeliveryRepository.FindByIdAsync`; KHÔNG event/cross-module/realtime. Prompt `codex-prompt-scrum321.md` gửi main. **Không cần escalate câu POD-bắt-buộc:** đề xuất optional đủ cơ sở FR + đơn giản hơn, main đã gợi ý cùng hướng.
