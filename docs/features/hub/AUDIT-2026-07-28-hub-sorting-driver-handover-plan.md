# Khảo sát & kế hoạch triển khai: Hub Sorting Detail, Loading Manifest linkage & Driver Handover (FE P1 mục 10–14)

| | |
|---|---|
| **Ngày khảo sát** | 2026-07-28 |
| **Branch khảo sát** | `SCRUM-256-hub-update-market` |
| **HEAD khảo sát** | `e4ab009` |
| **Epic/Task key** | Chưa gán (đề xuất tách 4 ticket con dưới epic SCRUM-256) |
| **Phạm vi** | Các mục P1 còn lại trong spec FE gửi: (10) chi tiết đơn trong inbound task, (11) loading-manifest gắn orderId/orderItemId + trạng thái phân loại, (12) lưu/đọc tiến độ phân loại, (13–14) orchestration bàn giao Driver + danh sách tài xế đủ điều kiện |
| **Trạng thái** | Planning only — chưa code |

> P0 (mục 1–9) đã DONE + verified + committed `e4ab009` (auto-create HubInboundEvent PENDING lúc
> handover + backfill job + scan gắn hubStaffUserId + pending-inbound trả productName). Xem
> `docs/features/hub/AUDIT-2026-07-26-market-hub-procurement-plan.md` và commit `e4ab009`.
> Tài liệu này chỉ lên kế hoạch cho phần còn lại.

---

## 1. Kết luận khảo sát hiện trạng

### 1.1 Inbound task hiện không truy vết được về Order (mục 10)

- `HubInboundEvent` được tạo từ `ProcurementBatch` (seam `HubProcurementPlanReader`), item lấy từ
  `procurement_batch_items` — **không có `order_id`/`order_item_id`** ở tầng này.
- Liên kết batch → order nằm ở Procurement: `ProcurementBatchOrder.OrderId`. Chi tiết dòng đơn
  (`order_items`) thì thuộc module Orders.
- Hub hiện **không có seam nào** đọc "một inbound/batch phục vụ những đơn nào, mỗi đơn cần bao nhiêu".
  Muốn hiển thị màn "phân loại theo đơn" thì phải bắc seam mới: batch → orders → order_items.

### 1.2 Loading manifest chỉ có ProductName, không có khóa đơn (mục 11)

- `GetLoadingManifestQueryHandler` (Logistics) dựng manifest từ các order `AtHub` qua seam
  `OrderPackingReader.GetLinesByOrdersAsync`.
- `OrderPackingLineRow` / `OrderPackingLine` / `LoadingLineDto` chỉ mang `ProductName`, `Quantity`,
  `CapacityKg` — **thiếu `orderId` và `orderItemId`** nên FE không map được dòng manifest về đơn/khách,
  và không gắn được trạng thái phân loại.
- Bảng `order_items` có sẵn `"Id"` (PascalCase) + `order_id` → seam mở rộng projection được, không cần
  đổi schema Orders.

### 1.3 Chưa có nơi lưu tiến độ phân loại (mục 12)

- Không có bảng/entity nào ghi "dòng đơn X đã được soạn/phân loại tại hub". `hub_inbound_*`,
  `hub_outbound_*`, `hub_cross_dock_*` đều không có khái niệm này.
- Đây là **state mới** thuộc Hub → cần entity + bảng + migration + command ghi + query đọc.

### 1.4 Handover đã có, nhưng thiếu "danh sách tài xế đủ điều kiện" (mục 13–14)

- `POST /hubs/{hubId}/handover` (`CreateHandoverCommand`) đã nhận sẵn `DriverUserId`,
  `DeliveryRouteId`, `OutboundEventId`; `POST .../handover/{id}/checkout` cho driver đã có.
- Seam `IDriverReader` hiện **chỉ có** `FindByUserIdAsync(userId)` — dùng để validate 1 tài xế.
  **Không có** phương thức liệt kê tài xế active để FE cho hub staff chọn.
- Vậy phần "orchestration" phần lớn đã xong; việc còn thiếu thực chất là **`GET /hubs/{hubId}/drivers/eligible`**
  + (tùy chọn) validate route/driver chặt hơn khi tạo handover.

---

## 2. Kế hoạch triển khai (4 task, theo thứ tự phụ thuộc)

### Task A — `GET /hubs/{hubId}/drivers/eligible` (mục 13–14) — ĐỘC LẬP, làm trước

**Mục tiêu:** trả danh sách tài xế đủ điều kiện nhận route để FE render dropdown khi tạo handover.

- **Seam:** thêm `Task<IReadOnlyList<DriverDto>> ListEligibleAsync(CancellationToken ct)` vào
  `IDriverReader`; implement trong `DriverReader` (Logistics.Infrastructure/CrossModule) — LINQ trên
  row hiện có, lọc `role = 'driver'` + active + `deleted_at IS NULL`.
- **Endpoint:** đặt trong Logistics (nơi `IDriverReader` sống) hoặc thêm route
  `/hubs/{hubId}/drivers/eligible` ở `RoutesController`/controller Logistics; RBAC
  `hub_staff,admin,operations_manager`. `hubId` chỉ để phân quyền theo hub (fleet dùng chung — không có
  `Vehicle.HubId`, xem [[project_hub_staff_logistics_rbac]]).
- **Tests:** unit query handler (lọc đúng active/role); **integration Postgres** (seam `ToSqlQuery`
  không chạy trên InMemory — bắt buộc theo CLAUDE.md).
- **Không** cần migration.

### Task B — Loading manifest gắn `orderId` + `orderItemId` (mục 11, phần khóa đơn)

**Phụ thuộc:** không (làm song song A). Là tiền đề cho phần "sorting state" của mục 11.

- Mở rộng `OrderPackingLineRow` + `ToSqlQuery` trong `OrderPackingLineRowConfiguration`: project thêm
  `order_item_id AS "OrderItemId"` (từ `order_items."Id"`) và giữ `OrderId`.
- Thêm `OrderId`, `OrderItemId` vào `OrderPackingLine` và `LoadingLineDto`; thread qua
  `GetLoadingManifestQueryHandler`.
- **Lưu ý casing:** `order_items` dùng cột quoted PascalCase cho `"Id"`, snake cho `deleted_at`
  (xem [[project_ef_column_casing]]) — copy đúng seam hiện có, không đoán.
- **Tests:** cập nhật integration test loading-manifest hiện có để assert `orderItemId` không null;
  đây là seam nên phải chạy Postgres.
- **Không** cần migration (chỉ đọc thêm cột sẵn có).

### Task C — Lưu/đọc tiến độ phân loại — sorting-progress (mục 12)

**Phụ thuộc:** B (cần `orderItemId` làm khóa dòng phân loại).

- **Entity mới** `HubSortingProgress` (Hub.Domain): khóa nghiệp vụ `(RouteId hoặc InboundId, OrderItemId)`,
  các cột `SortedQuantityKg`, `Status` (`PENDING`/`SORTED`), `SortedByUserId`, `SortedAt`, `deleted_at`.
  Chốt phạm vi khóa (theo route hay theo inbound) là **quyết định mở** — xem §3.
- **Persistence:** `HubSortingProgressConfiguration` (snake_case explicit theo DEC-HUB-03) + **migration mới**
  `AddHubSortingProgress`. Cập nhật `AppDbContextModelSnapshot` (đã tự sinh). Đảm bảo
  `FreshFlow.Hub.Infrastructure` vẫn nằm trong `ForceLoadModuleAssemblies` (đã có từ 286).
- **Command:** `MarkLineSortedCommand` (idempotent theo unique index trên khóa nghiệp vụ) — chuyển
  `PENDING → SORTED`, ghi user + thời điểm. Route qua `ISender` để chạy `ValidationBehavior`.
- **Query:** `GetSortingProgressQuery` trả tiến độ theo route/inbound.
- **Endpoints (Hub):**
  - `POST /hubs/{hubId}/routes/{routeId}/sorting` (hoặc `.../inbound/{inboundId}/sorting`) — mark sorted.
  - `GET  /hubs/{hubId}/routes/{routeId}/sorting-progress` — đọc tiến độ.
  - RBAC `hub_staff,admin,operations_manager`.
- **Loading manifest (nối tiếp mục 11):** manifest handler đọc thêm sorting-progress để gắn `sortingState`
  cho mỗi dòng. Vì manifest sống ở Logistics còn progress ở Hub → **bắc seam đọc ngược** từ Logistics
  sang bảng `hub_sorting_progress` (keyless row), HOẶC để FE gọi 2 endpoint và tự merge. Đề xuất
  **FE merge** (rung thang lười, tránh seam chéo mới) trừ khi FE yêu cầu gộp — xem §3.
- **Tests:** unit domain (idempotent, chuyển trạng thái) + unit handler; **integration Postgres** cho
  persistence + query.

### Task D — Chi tiết đơn trong inbound task (mục 10)

**Phụ thuộc:** không bắt buộc, nhưng nên làm sau B để dùng chung khóa order_item.

- **Seam mới (Hub):** đọc batch → orders → order_items cho một `inboundId` (qua
  `deliveryScheduleId = batchId`). Có 2 hướng:
  1. Mở rộng seam Procurement hiện có (`ProcurementBatchOrder`) + seam Orders để lấy dòng đơn.
  2. Nếu chỉ cần "đơn nào + số lượng theo sản phẩm", tái dùng dữ liệu loading-manifest theo route.
- **Endpoint:** `GET /hubs/{hubId}/inbound/{inboundId}/orders` → trả danh sách order + order_items
  (restaurantId, orderId, orderItemId, productName, quantity). RBAC như trên.
- **Tests:** integration Postgres (seam).
- **Quyết định mở:** xác nhận với FE inbound-detail theo **batch/inbound** hay theo **route** (§3).

---

## 3. Quyết định mở cần chốt với FE/supervisor

1. **Khóa của sorting-progress:** theo `routeId` (soạn hàng lên xe) hay theo `inboundId` (phân loại lúc
   nhận)? Ảnh hưởng schema Task C và endpoint. → Đề xuất **theo routeId** vì phân loại thực tế gắn với
   chuyến giao.
2. **Gắn `sortingState` vào loading-manifest:** BE gộp (thêm seam chéo Logistics→Hub) hay **FE merge**
   2 response? → Đề xuất FE merge để tránh seam chéo mới; đổi nếu FE cần một call.
3. **Inbound-detail (mục 10) trục theo batch hay theo route** — cần FE xác nhận màn hình dùng để làm gì
   (đối soát lúc nhận vs soạn hàng lúc giao).
4. **Đặt endpoint `drivers/eligible` ở đâu:** controller Logistics (đúng chủ sở hữu `IDriverReader`) vs
   thêm vào nhóm route `/hubs/...`. → Đề xuất theo Logistics, đường dẫn `/hubs/{hubId}/drivers/eligible`.

---

## 4. Bảng task & thứ tự

| # | Task | Phụ thuộc | Migration | Seam mới | Test bắt buộc |
|---|------|-----------|-----------|----------|----------------|
| A | `drivers/eligible` | — | Không | Mở rộng `IDriverReader` | Unit + **Postgres** |
| B | Manifest + orderId/orderItemId | — | Không | Mở rộng `OrderPackingLineRow` | **Postgres** (sửa test có sẵn) |
| C | Sorting-progress (bảng + API) | B | **Có** (`AddHubSortingProgress`) | Bảng Hub mới | Unit domain/handler + **Postgres** |
| D | Inbound-detail `inbound/{id}/orders` | (B) | Không | Seam batch→orders (Hub) | **Postgres** |

**Thứ tự đề xuất:** A và B song song (độc lập) → C (cần B) → D. Chốt §3 trước khi bắt đầu C và D.

---

## 5. Ràng buộc kỹ thuật (nhắc lại, áp cho mọi task)

- Cross-module read **chỉ** qua keyless Row seam (`HasNoKey` + `ToSqlQuery`), không project reference.
  `ToSqlQuery` không chạy trên InMemory → **mọi seam phải có integration test Postgres** (Testcontainers).
- Không `FromSqlRaw`; sort do client chọn phải qua allow-list switch → LINQ.
- Column casing không đồng nhất — copy seam hiện có, không đoán (xem [[project_ef_column_casing]]).
- Route qua `ISender` để `ValidationBehavior` chạy; interface members cần `public` tường minh (IDE0040).
- RBAC dùng đúng tên seed: `hub_staff` · `admin` · `operations_manager` · `driver` (xem [[project_rbac_role_names]]).
- Commit chỉ sau khi có Jira key (xem [[feedback_no_commit_without_jira]]); loại
  `appsettings.Development.json` khỏi mọi commit (secret Cloudinary, xem [[project_cloudinary_secret_leak]]).
