# PLAN — Gắn xe (Vehicle) vào Hub, ranh giới cứng khi dispatch

**Ngày:** 2026-08-12 · **Module:** Logistics · **Epic:** LOG
**Trạng thái:** DRAFT

## Vấn đề

Bảng `vehicles` không biết xe thuộc đâu (`Vehicle.cs` không có `HubId`). Fleet dùng
chung toàn hệ thống — cố ý khi 1 hub/chợ. Khi mở rộng nhiều hub, cần gắn xe vào hub
để quản lý theo hub và **chặn gán xe của hub này cho route của hub khác**.

## Vì sao Hub (không phải Market)

- **Route đã có sẵn `HubId`** (`DeliveryRoute.cs:32`). Mọi route thật đều là hub route:
  `PlanRoutesCommandHandler` và `CalculateRouteCommandHandler` đều gọi `CreateHubRoute` +
  bắt buộc `HubId`. `CreateDirect` chỉ còn trong domain/test. → check dispatch = so 1 field,
  **0 đọc cross-module**.
- **Code đã tính trước:** `AssignVehicleCommandHandler.cs:17-20` ghi thẳng "add `Vehicle.HubId`
  + hub-assignment guard here (see HubAccessChecker)". Đây đúng là lộ trình đã dự trù.
- **Nhóm theo chợ vẫn free:** hub thuộc 1 chợ (`Hub.MarketId`). Cần báo cáo theo chợ thì
  roll-up hub→chợ; `HubCoordinateReader` đã trả sẵn `MarketId`. Khỏi lưu market trên xe.

## Đã có sẵn — không phải viết mới

- Pattern `Guid? MarketId` + `AssignMarket()` nullable-rollout của `Hub.cs:8` — copy y cho `Vehicle.HubId`.
- Validate hub tồn tại: `IHubCoordinateReader.FindByIdAsync(hubId, ct)` — null nếu không có. KHÔNG seam mới, KHÔNG EF FK.
- Bảng `vehicles` full snake_case, soft-delete `deleted_at`.

## Các bước

### 1. Domain — `Vehicle.cs`
- Thêm `public Guid? HubId { get; private set; }` (nullable, copy `Hub.MarketId`).
- Ctor nhận thêm `Guid? hubId`; gán trong ctor.
- Thêm `public void AssignHub(Guid hubId)` (copy `Hub.AssignMarket`: guard `Guid.Empty`, set `UpdatedAt`).
- `Update(...)` KHÔNG đụng `HubId` (đổi hub đi qua `AssignHub` riêng).

### 2. EF config — `VehicleConfiguration.cs`
- `builder.Property(v => v.HubId).HasColumnName("hub_id");`
- `builder.HasIndex(v => v.HubId).HasFilter("deleted_at IS NULL").HasDatabaseName("idx_vehicles_hub_id");`

### 3. Migration
```
dotnet ef migrations add AddVehicleHubId \
  --project src/FreshFlow.Infrastructure.Persistence --startup-project src/FreshFlow.API
```
- `hub_id uuid NULL` + index. **Nullable** — xe cũ chưa có hub.
- ⚠️ `localhost:5433 = PRODUCTION` — KHÔNG chạy `database update`; chỉ tạo file migration, để user apply.

### 4. Register / Update — nhận + validate hub
- `RegisterVehicleCommand` + `UpdateVehicleCommand`: thêm `Guid? HubId`.
- Handler: nếu `HubId != null` → `hubReader.FindByIdAsync`, null thì
  `Result.Failure(Error.Validation("HUB_NOT_FOUND", "..."))`. Inject `IHubCoordinateReader`.
- `VehicleDto`: thêm `Guid? HubId`. Cập nhật `VehicleMappings.ToDto`.
- `ListVehiclesQuery`: thêm `Guid? HubId` filter → `.Where(v => v.HubId == hubId)` trong handler.

### 5. Backfill xe cũ — endpoint gán hub
- `AssignVehicleToHubCommand(VehicleId, HubId)` → validate hub tồn tại → `vehicle.AssignHub()`.
- Endpoint `PUT /api/vehicles/{id}/hub` body `{ hubId }`, `[Authorize(Roles="admin,operations_manager")]`, route qua `ISender`.

### 6. RANH GIỚI CỨNG — chặn dispatch chéo hub
Trong `AssignVehicleCommandHandler` (thay comment fleet-wide ở dòng 17-20 bằng guard thật),
sau khi load `route`, TRƯỚC khi check eligibility:
- Load `vehicle` (cần method repo `IVehicleRepository.FindByIdAsync` — kiểm tra đã có chưa; nếu chưa, thêm).
- Nếu `vehicle.HubId is null` → `Result.Failure(Error.Validation("VEHICLE_HUB_UNASSIGNED", "Vehicle chưa gán hub."))`.
- Nếu `route.HubId is null` (route trực tiếp legacy) → cho qua (không chặn) HOẶC từ chối — mặc định **cho qua** vì không có route thật nào null.
- Nếu `vehicle.HubId != route.HubId` → `Result.Failure(Error.Validation("VEHICLE_HUB_MISMATCH", "Xe không thuộc hub của route."))`.
- Code string mới `VEHICLE_HUB_UNASSIGNED` / `VEHICLE_HUB_MISMATCH` map về 400 (đuôi `_ERROR`? — dùng `Error.Validation` để chắc chắn ra 400; xem `ErrorExtensions.ToActionResult`).

### 7. Test (TDD, viết trước)
- Unit: register có/không hub; hub không tồn tại → `HUB_NOT_FOUND`; list filter theo hub; `AssignHub` guard empty.
- Unit dispatch: xe null hub → `VEHICLE_HUB_UNASSIGNED`; xe khác hub route → `VEHICLE_HUB_MISMATCH`; xe cùng hub → pass tới eligibility.
- Integration Postgres cho seam `HubCoordinateReader` (ToSqlQuery không chạy trên InMemory).

## Verify
```
dotnet build FreshFlow.slnx
dotnet test tests/Unit/FreshFlow.Logistics.UnitTests/
dotnet format FreshFlow.slnx --verify-no-changes
dotnet build-server shutdown
```

## Không làm (YAGNI)
- Không tạo module/entity mới, không EF FK cross-module, không lưu `MarketId` trên xe (suy từ hub).
- Không chuyển `hub_id` sang NOT NULL trong migration này (đợi backfill xong — theo lộ trình Hub).
- Không đổi RBAC hub_staff (ai được dispatch giữ nguyên); chỉ chặn xe↔route theo hub.

## Rủi ro
- Xe `hub_id = NULL` sẽ bị từ chối dispatch sau khi bật §6 → **phải backfill (§5) trước khi deploy §6**. Thứ tự: 1-5 trước, migration apply, backfill dữ liệu, rồi mới bật guard §6.
- `DeliveryRouteVehicleRow` (Analytics) đọc `vehicles` — thêm `hub_id` vào Row đó nếu Analytics cần lọc theo hub (hiện chưa, để sau).
