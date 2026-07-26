# Khảo sát & kế hoạch triển khai: Market–Hub Mapping và Daily Procurement Plan cho Hub

| | |
|---|---|
| **Ngày khảo sát** | 2026-07-26 |
| **Branch khảo sát** | `SCRUM-370-invoicing` |
| **HEAD khảo sát** | `b4250ff` |
| **Epic/Task key** | Chưa gán |
| **Phạm vi** | Một Market có một Hub active; tự resolve Hub khi batching; Hub xem đơn và danh sách thu mua theo ngày |
| **Trạng thái** | Implemented locally — migration generated, build/unit/integration checks passed |

> Business invariant đã chốt: mỗi chợ có đúng một Hub FreshFlow đang hoạt động. Sản phẩm thuộc
> Market nào thì Market Agent mua tại Market đó và mang về Hub của chính Market đó. Vì vậy client
> không được chọn `hubId` lúc bàn giao; hệ thống phải suy ra Hub từ `marketId`.

## 1. Kết luận khảo sát hiện trạng

### 1.1 Procurement đã biết Market từ lúc batching

- `BatchConfirmedOrdersService` resolve `MarketProductId → MarketId`, sau đó group các order line theo
  `MarketId`.
- Mỗi `ProcurementBatch` đã có `MarketId`, `BatchDate`, danh sách order nguồn và danh sách item tổng hợp.
- `ProcurementBatchItem` đã lưu `ProductNameSnapshot`, `TotalQuantity`, `ActualQuantity`,
  `ActualUnitPrice`, `PurchasedAt`; đủ dữ liệu để Hub biết cần mua gì và thực tế đã mua gì.
- `ProcurementBatchOrder` đã lưu `OrderId`; không cần đọc toàn bộ Order chỉ để trả danh sách ID.

### 1.2 Hub chưa có quan hệ với Market

- `Hub` hiện không có `MarketId`; schema `hubs` cũng không có `market_id`.
- API create/update Hub chỉ nhận thông tin địa điểm, sức chứa và `ManagedBy`.
- Không có repository/read seam nào resolve `MarketId → HubId`.
- Vì thiếu mapping này, hệ thống hiện không thể tự biết batch của Market phải về Hub nào.

### 1.3 `HubId` đang được nhập quá muộn

- `ProcurementBatch.HubId` hiện là nullable và chỉ được set trong
  `HandoverToHub(Guid? hubId, DateTime capturedAtUtc)`.
- `PATCH /api/v1/procurement/tasks/{batchId}/handover` nhận body `{ "hubId": ... }`.
- Đây là trust-boundary sai với invariant mới: agent có thể gửi nhầm Hub hoặc cố tình chọn Hub khác.
- Hub chỉ biết batch sau handover, trong khi nhu cầu là xem kế hoạch ngay khi batch được tạo.

### 1.4 API Hub hiện có không trả procurement plan

- `GET /api/v1/hubs/{hubId}/pending-inbound` chỉ đọc `hub_inbound_events`.
- `HubInboundItemDto` chỉ có `MarketProductId`, `ProductId`, `QuantityKg`; không có `OrderId`,
  `ProductNameSnapshot`, target/actual purchase quantity hoặc price.
- Handover Procurement hiện không tự tạo `HubInboundEvent`, nên `pending-inbound` không thể dùng làm
  màn hình kế hoạch thu mua.
- `GET /api/v1/admin/order-groups?date=` có gần đủ dữ liệu nhưng chỉ cho `admin`, không enforce
  Hub Staff assignment và không filter theo Hub.

## 2. Quyết định thiết kế đã chốt

| Quyết định | Chốt |
|---|---|
| Market–Hub cardinality | Một Market có tối đa một Hub active; một Hub thuộc đúng một Market |
| Nơi lưu mapping | `hubs.market_id`; Hub module sở hữu quan hệ |
| Cross-module FK | Không tạo FK DB sang `markets`; validate qua keyless read seam |
| Thời điểm resolve Hub | Khi tạo `ProcurementBatch`, không chờ handover |
| `ProcurementBatch.HubId` | Giữ làm snapshot nội bộ, nhưng không nhận từ client |
| Handover request | Không body; chỉ dùng `batchId` + agent ID từ JWT |
| API Hub | Endpoint read-only mới theo `hubId + date` |
| `pending-inbound` | Giữ cho nhận hàng/scan vật lý; không đổi semantics trong task này |
| Pagination | Chưa cần: một Hub gắn một Market và dữ liệu được giới hạn theo một ngày |
| Realtime | Không thêm SignalR; client gọi REST |

`ProcurementBatch.HubId` vẫn đáng giữ vì:

1. Repo đã có cột và contract này; xoá đi tạo breaking change không cần thiết.
2. Nó giữ lịch sử batch đã được route tới Hub nào nếu mapping Market–Hub thay đổi sau này.
3. API Hub filter trực tiếp bằng `(hub_id, batch_date)` mà không phải join lại `hubs`.

## 3. Flow mục tiêu

```text
OrderItem.MarketProductId
        ↓
MarketProduct.MarketId
        ↓
Hub active có hubs.market_id = MarketId
        ↓
ProcurementBatch(MarketId, HubId, BatchDate)
        ↓
GET /api/v1/hubs/{hubId}/procurement-plan?date=YYYY-MM-DD
```

Handover sau khi agent mua xong:

```text
PATCH /api/v1/procurement/tasks/{batchId}/handover
        ↓
Load batch + ownership guard
        ↓
Giữ nguyên batch.HubId đã resolve từ batching
        ↓
Purchasing → HandedOff
```

## 4. Public API contract

### 4.1 Create Hub gắn với Market

`POST /api/v1/hubs`

RBAC giữ nguyên: `admin,operations_manager`.

Request bổ sung `marketId` bắt buộc:

```json
{
  "marketId": "00000000-0000-0000-0000-000000000010",
  "name": "Hub Bình Điền",
  "address": "Chợ đầu mối Bình Điền",
  "latitude": 10.705,
  "longitude": 106.607,
  "capacityKg": 3000,
  "managedBy": null
}
```

Rules:

- Market phải tồn tại, active và chưa soft-delete.
- Market đã có Hub active → `409 HUB_ALREADY_CONFIGURED_FOR_MARKET`.
- `MarketId` là immutable sau khi tạo Hub; `PATCH /hubs/{id}` không đổi Market để tránh chuyển nhầm
  inventory/batch lịch sử sang Market khác.
- `HubDto` trả thêm `marketId`.

### 4.2 Hub xem đơn và danh sách thu mua trong ngày

`GET /api/v1/hubs/{hubId}/procurement-plan?date=2026-07-26`

RBAC:

- `hub_staff`: chỉ Hub được assign qua `HubAccessBehavior`.
- `admin,operations_manager`: bypass assignment theo behavior hiện có.
- Hub inactive hoặc staff không được assign → `403 HUB_ACCESS_DENIED`.

`date` là bắt buộc và là `DateOnly` business date; không convert timezone.

Response:

```json
{
  "success": true,
  "data": {
    "hubId": "00000000-0000-0000-0000-000000000001",
    "date": "2026-07-26",
    "batches": [
      {
        "batchId": "00000000-0000-0000-0000-000000000101",
        "marketId": "00000000-0000-0000-0000-000000000010",
        "status": "Purchasing",
        "handedOffAt": null,
        "orderIds": [
          "00000000-0000-0000-0000-000000000201",
          "00000000-0000-0000-0000-000000000202"
        ],
        "items": [
          {
            "marketProductId": "00000000-0000-0000-0000-000000000301",
            "productName": "Cà chua",
            "targetQuantity": 50,
            "actualQuantity": 48,
            "actualUnitPrice": 18000,
            "purchasedAt": "2026-07-26T02:30:00Z"
          }
        ]
      }
    ]
  }
}
```

Semantics:

- Trả batch ngay từ trạng thái `Built`; Hub không phải chờ agent handover.
- Trả tất cả status, kể cả `Cancelled`, để UI không hiểu nhầm batch biến mất; UI dùng `status` để
  hiển thị.
- `targetQuantity`/`actualQuantity` dùng đơn vị hiện có của Procurement, không đổi thành Kg.
- Ngày không có batch trả `batches: []`, không phải 404.
- Response chỉ cần `orderIds`; chi tiết restaurant/order item per-order nằm ngoài nhu cầu hiện tại.

### 4.3 Handover không nhận HubId

`PATCH /api/v1/procurement/tasks/{batchId}/handover`

- RBAC giữ nguyên: `market_agent`.
- Không request body.
- Agent ID chỉ lấy từ JWT.
- Handler dùng `batch.HubId` đã có.
- Batch legacy chưa có Hub mapping → `422 HUB_NOT_CONFIGURED_FOR_MARKET`, không cho handover mơ hồ.
- Success tiếp tục trả `ProcurementBatchDto`.

Breaking change có chủ đích:

```diff
- PATCH .../handover { "hubId": "..." }
+ PATCH .../handover
```

Frontend Market Agent phải bỏ body `hubId`.

## 5. Domain và application plan

### 5.1 Hub module

`Hub`:

- Thêm `Guid MarketId`.
- `Hub.Create(...)` nhận `marketId` bắt buộc và guard `Guid.Empty`.
- Không thêm setter/update cho `MarketId`.
- `HubDto`/`HubMappings` trả `MarketId`.

Create flow:

- Thêm `IMarketReader` ở Hub Application.
- Hub Infrastructure thêm keyless `MarketRow` đọc bảng Catalog `markets` với đúng quoted PascalCase
  columns: `"Id"`, `"IsActive"`, `"DeletedAt"`.
- `CreateHubCommandHandler` validate Market active trước khi tạo.
- Repository kiểm tra Hub active đã tồn tại cho Market; unique index vẫn là guard cuối chống race.
- Map conflict thành `HUB_ALREADY_CONFIGURED_FOR_MARKET`/409.

### 5.2 Procurement resolve Hub khi batching

Thêm `IHubByMarketReader` ở Procurement Application và keyless row/reader/config ở
Procurement Infrastructure:

```sql
SELECT
    id AS "HubId",
    market_id AS "MarketId"
FROM hubs
WHERE deleted_at IS NULL
  AND is_active = TRUE
  AND market_id IS NOT NULL
```

`BatchConfirmedOrdersService`:

1. Resolve `MarketProductId → MarketId` như hiện tại.
2. Lấy danh sách `MarketId` distinct.
3. Resolve toàn bộ Hub trong một query, không query từng market.
4. Nếu bất kỳ Market nào thiếu Hub active, trả failure trước `AddRangeAsync`; không tạo partial batch.
5. Gọi `ProcurementBatch.Build(batchDate, marketId, hubId, lines)`.

`ProcurementBatch`:

- `Build(...)` nhận `hubId` non-empty và set `HubId` ngay lúc tạo.
- Giữ property nullable ở mức persistence trong rollout để đọc được row legacy, nhưng mọi batch mới
  phải có Hub.
- `HandoverToHub(...)` bỏ tham số `hubId`; chỉ nhận timestamp và không sửa `HubId`.

`HandoverBatchCommand`/handler/controller:

- Bỏ `HubId` khỏi command.
- Bỏ `HandoverRequest`.
- Controller action không có `[FromBody]`.
- Handler reject batch legacy `HubId == null` bằng error rõ ràng trước khi transition.
- Integration event handover tiếp tục mang `batch.HubId`; không đổi consumer Orders ngoài nullability
  nếu chưa siết được schema.

## 6. Hub procurement-plan read model

Không reference trực tiếp Procurement project từ Hub Application. Dùng pattern keyless Row seam hiện
có của repo.

### 6.1 Application

Thêm:

- `IHubProcurementPlanReader`.
- `HubProcurementPlanDto`.
- `HubProcurementBatchDto`.
- `HubProcurementItemDto`.
- `GetHubProcurementPlanQuery(HubId, Date, ActorUserId, BypassHubAssignment)`.
- Validator: `HubId`/`ActorUserId` non-empty, `Date != default`.
- Query implement `IHubAccessRequest` để tái sử dụng `HubAccessBehavior`; không copy authorization vào
  controller/reader.

### 6.2 Infrastructure

Một reader, ba keyless row:

1. `ProcurementPlanBatchRow`: batch ID/date/market/hub/status/handover timestamp.
2. `ProcurementPlanItemRow`: batch ID, market product, product snapshot, target/actual quantity,
   actual price, purchased timestamp.
3. `ProcurementPlanOrderRow`: batch ID và order ID.

Reader:

- Filter batch bằng `HubId`, `BatchDate` và `deleted_at IS NULL`.
- Lấy item/order cho tập `BatchId` đã chọn; filter soft-delete ở static `ToSqlQuery`.
- Group trong memory thành DTO; sort ổn định theo batch ID, item ID và order ID.
- Không dùng `FromSqlRaw`, không SQL động.
- Không đọc Orders table vì chỉ cần `OrderId`.

`ToSqlQuery` rows phải được integration-test bằng PostgreSQL; EF InMemory không chứng minh được SQL/casing.

### 6.3 Controller

Thêm action vào `HubInboundController` hiện có để tái sử dụng:

- Base route `/api/v1/hubs`.
- Class RBAC `hub_staff,admin,operations_manager`.
- `ResolveUserId()` và `BypassHubAssignment()` hiện có.
- `ApiResponse.Ok(...)` hiện có.

Không tạo controller mới chỉ cho một GET.

## 7. Persistence và rollout

### 7.1 Schema

`hubs`:

- Thêm `market_id UUID`.
- Unique partial index:

```sql
CREATE UNIQUE INDEX ux_hubs_active_market
ON hubs (market_id)
WHERE deleted_at IS NULL
  AND is_active = TRUE
  AND market_id IS NOT NULL;
```

- Không FK sang `markets` vì đây là cross-module relation.

`procurement_batches`:

- Cột `hub_id` đã tồn tại; không tạo lại.
- Thêm index phục vụ endpoint:

```sql
CREATE INDEX idx_procurement_batches_hub_date
ON procurement_batches (hub_id, batch_date)
WHERE deleted_at IS NULL;
```

### 7.2 Backfill an toàn

Không thể tự đoán Hub hiện có thuộc Market nào. Rollout theo hai bước:

1. Migration thêm `hubs.market_id` nullable + indexes.
2. Cung cấp mapping thật cho từng environment và backfill `hubs.market_id`.
3. Backfill batch cũ theo mapping:

```sql
UPDATE procurement_batches AS batch
SET hub_id = hub.id
FROM hubs AS hub
WHERE batch.market_id = hub.market_id
  AND batch.hub_id IS NULL
  AND batch.deleted_at IS NULL
  AND hub.deleted_at IS NULL;
```

4. Verify không còn Hub/batch active thiếu mapping.
5. Follow-up migration mới siết `hubs.market_id NOT NULL` và, nếu dữ liệu cho phép,
   `procurement_batches.hub_id NOT NULL`.

App-level create/batching guard có hiệu lực ngay ở bước 1: Hub mới bắt buộc có Market; batch mới bắt buộc
resolve được Hub. Row legacy chưa backfill không được handover mơ hồ.

**Release gate:** phải hoàn tất bước 2–4 trước khi deploy application code; nếu không, batching của
Market hiện hữu sẽ trả `HUB_NOT_CONFIGURED_FOR_MARKET`.

Khi deactivate/thay Hub mapping, vận hành phải pause batching scheduler. Nếu hai thao tác phải chạy
đồng thời tuyệt đối, bổ sung transaction + PostgreSQL row lock chung cho Hub routing.

## 8. Error contract

| Code | HTTP | Khi nào |
|---|---:|---|
| `MARKET_NOT_FOUND` | 404 | Market không tồn tại/đã soft-delete |
| `MARKET_INACTIVE` | 422 | Market tồn tại nhưng inactive |
| `HUB_ALREADY_CONFIGURED_FOR_MARKET` | 409 | Market đã có Hub active |
| `HUB_NOT_CONFIGURED_FOR_MARKET` | 422 | Batching/handover không resolve được Hub |
| `HUB_INACTIVE` | 422 | Batch snapshot tới Hub đã inactive; không cho handover |
| `HUB_HAS_ACTIVE_PROCUREMENT` | 409 | Không deactivate Hub khi còn batch chưa handed-off/cancelled |
| `HUB_ACCESS_DENIED` | 403 | Hub Staff không được assign hoặc Hub inactive |
| `VALIDATION_ERROR` | 400 | `marketId`, `hubId`, `date` rỗng/không hợp lệ |

Các code mới phải được map trong `ErrorExtensions`; không để rơi xuống 500.

## 9. File-level implementation breakdown

### Phase 1 — Market–Hub invariant

- `Hub.Domain/Entities/Hub.cs`: thêm immutable `MarketId`.
- `Hub.Application/Dtos/HubDto.cs`, `Mappings/HubMappings.cs`: expose field.
- `CreateHubCommand*` + `HubsController.CreateHubRequest`: nhận/validate MarketId.
- `Hub.Application/Abstractions/IMarketReader.cs`: cross-module contract.
- `Hub.Infrastructure/CrossModule/MarketRow*`, `MarketReader.cs`: validate Catalog Market.
- `IHubRepository`/`HubRepository`: lookup active Hub by Market.
- `HubConfiguration`: map/index.
- `Hub.Infrastructure/DependencyInjection.cs`: đăng ký reader.

### Phase 2 — Resolve Hub tại batching và sửa handover

- `Procurement.Application/Abstractions/IHubByMarketReader.cs`.
- `Procurement.Infrastructure/CrossModule/HubMarketRow*`, `HubByMarketReader.cs`.
- `Procurement.Infrastructure/DependencyInjection.cs`.
- `BatchConfirmedOrdersService.cs`: resolve Hub một lần trước build.
- `ProcurementBatch.cs`: `Build(..., hubId, ...)`, `HandoverToHub(at)`.
- `HandoverBatchCommand*` + `ProcurementController`: bỏ request HubId/body.
- Update domain/application/integration tests và mọi caller `ProcurementBatch.Build`.

### Phase 3 — Hub daily procurement-plan endpoint

- `Hub.Application/Dtos/HubProcurementPlanDtos.cs`.
- `Hub.Application/Abstractions/IHubProcurementPlanReader.cs`.
- `Hub.Application/Queries/GetHubProcurementPlan/*`.
- `Hub.Infrastructure/CrossModule/ProcurementPlan*Row*` + reader.
- `Hub.Infrastructure/DependencyInjection.cs`.
- `HubInboundController`: thêm GET action.

### Phase 4 — Persistence và contract

- `HubConfiguration` + `ProcurementBatchConfiguration`: indexes.
- Một migration additive cho `hubs.market_id`, indexes và model snapshot.
- `ErrorExtensions`: map error mới.
- Cập nhật API docs/feature index nếu task được gán Jira key.

## 10. Test plan

### 10.1 Hub domain/application

- `Hub.Create` reject `MarketId == Guid.Empty`.
- Create Hub với Market active → success và DTO có MarketId.
- Market missing/deleted → `MARKET_NOT_FOUND`.
- Market inactive → `MARKET_INACTIVE`.
- Market đã có Hub active → 409; concurrent create vẫn bị unique index chặn.
- Update Hub không thể đổi MarketId.

### 10.2 Procurement

- Nhiều order line cùng Market → một batch với đúng HubId.
- Nhiều Market → mỗi batch nhận đúng Hub của Market.
- Một Market thiếu Hub → không persist bất kỳ batch nào.
- Batch mới luôn có HubId.
- Handover success không cần body và không đổi HubId.
- Agent khác → 404 như hiện tại.
- Batch legacy `HubId == null` → `HUB_NOT_CONFIGURED_FOR_MARKET`.

### 10.3 Hub procurement plan

- Filter đúng `hubId + date`.
- Trả order IDs và target/actual item fields đúng.
- Date không có dữ liệu → `batches: []`.
- Không trả batch Hub khác, ngày khác hoặc soft-deleted row.
- Giữ và expose status `Built`, `Manifested`, `Purchasing`, `HandedOff`, `Cancelled`.
- Hub Staff assigned → 200.
- Hub Staff không assigned/Hub khác → 403.
- Admin/operations manager → 200 không cần assignment.
- Missing/invalid date → 400.

### 10.4 Integration/PostgreSQL

- Prove unique partial index Market–Hub.
- Prove keyless Market reader đúng casing Catalog.
- Prove keyless Procurement Plan rows đúng snake_case và soft-delete filter.
- End-to-end:
  1. tạo Market + Hub mapping;
  2. tạo confirmed orders của Market;
  3. chạy batching;
  4. gọi procurement-plan trước handover và thấy batch;
  5. agent purchase + handover không body;
  6. gọi lại và thấy status/actual purchase cập nhật.

## 11. Gates trước khi handoff

```bash
dotnet test tests/Unit/FreshFlow.Hub.UnitTests/
dotnet test tests/Unit/FreshFlow.Procurement.UnitTests/
dotnet test tests/Integration/FreshFlow.IntegrationTests/ --filter "FullyQualifiedName~HubProcurement"
dotnet build FreshFlow.slnx
dotnet format FreshFlow.slnx --verify-no-changes
dotnet ef migrations has-pending-model-changes \
  --project src/FreshFlow.Infrastructure.Persistence \
  --startup-project src/FreshFlow.API
dotnet build-server shutdown
```

Không apply migration vào DB thật và không commit nếu chưa có Jira key/user approval.

## 12. Ngoài scope có chủ đích

- Không auto-create `HubInboundEvent` khi Procurement handover.
- Không thay đổi QR/scan/inventory/capacity flow.
- Không convert procurement quantity sang Kg.
- Không trả restaurant/order item detail theo từng order.
- Không SignalR/realtime push procurement plan.
- Không cho đổi Market của Hub sau khi tạo.
- Không xoá `ProcurementBatch.HubId`.

`pending-inbound` chỉ nên được nối tự động với handover ở task riêng khi đã chốt cách tạo expected inbound
và mapping quantity unit → Kg. Endpoint procurement-plan hiện tại đủ cho nhu cầu Hub biết hôm đó có đơn
nào và cần/thực tế đã thu mua gì.

## 13. Acceptance criteria tổng

1. Admin tạo Hub phải chọn một Market active; không có hai Hub active cùng Market.
2. Batch được gắn đúng Hub ngay khi tạo từ Market, trước manifest/purchase/handover.
3. Market Agent handover không gửi hoặc chọn `hubId`.
4. Hub Staff gọi API theo ngày và thấy đúng batch, order IDs, target/actual purchased items của Hub mình.
5. Hub Staff không đọc được dữ liệu Hub khác.
6. Market thiếu Hub không tạo partial batch và trả lỗi rõ ràng.
7. Existing inbound/scan/inventory/order-status flow không regression.
8. Hub còn procurement batch active không thể deactivate; Hub inactive không nhận handover.
