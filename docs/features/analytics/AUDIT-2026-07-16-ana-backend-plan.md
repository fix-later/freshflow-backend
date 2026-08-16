# Báo cáo khảo sát & kế hoạch triển khai: ANA — Operations Dashboard & Analytics

| | |
|---|---|
| **Ngày khảo sát** | 2026-07-16 |
| **Branch** | hiện tại `SCRUM-255-procurement-batching` — đề xuất tách nhánh mới từ `dev`: `SCRUM-301-ANA-Analytics` |
| **Epic** | SCRUM-301 — Operations Dashboard & Analytics |
| **Phạm vi** | **9 task backend** phủ **10 UC** (SCRUM-337 gộp UC-ANA-02+07 — xem §0.1). Nằm TRONG module **Analytics** (đang RỖNG — bootstrap từ đầu, giống Hub trước epic HUB). Analytics là module **CHỈ ĐỌC**: tổng hợp dữ liệu từ Orders/Procurement/Hub/Logistics/Pricing đã có sẵn. |
| **Tác giả** | Leader agent (team `backend-dev`) — CHỈ nghiên cứu + lập plan. Code do "codex" implement theo prompt supervisor; "reviewer" review độc lập. |
| **Mục đích** | Cung cấp đủ ngữ cảnh + quyết định thiết kế để codex implement không cần hỏi lại. Nguồn tham chiếu chính, KHÔNG chỉ TaskList. |
| **Trạng thái** | Khảo sát xong. Điểm mở đã chốt (DEC-ANA-01…14) theo code THẬT. 9 task chưa bắt đầu, sẵn sàng breakdown bắt đầu SCRUM-335. |

> **Nguồn Jira:** breakdown 9 task BE do supervisor cung cấp (335/337/339/341/343/345/347/349/351). **CHỈ 4/10 UC có FR chống lưng** (`docs/01-requirements-spec.md` §1.6 dòng 113-121: FR-ANA-001..004). 6 UC còn lại (Overview, Order Metrics, Procurement Metrics, Hub Throughput, Recent Activities) **KHÔNG có FR** — scope suy ra từ tiêu đề UC + dữ liệu THẬT sẵn có. Xem §0.2 + DEC-ANA-14.

> **Quy ước cập nhật:** mỗi khi 1 task PASS hoặc có quyết định giữa chừng → cập nhật §0 (bảng trạng thái), §4 (breakdown), §6 (nhật ký). Đồng bộ TaskList + memory `project_ana_epic`, không thay thế.

---

## 0. Bảng SCRUM key theo task

### 0.1 Về "10 task" — KHÔNG thiếu key

Supervisor ghi "10 task" nhưng liệt kê 9 key. **Không thiếu key**: có **10 UC** (UC-ANA-01…10) nhưng **9 task BE**, vì **SCRUM-337 gộp UC-ANA-02 + UC-ANA-07**. Đối chiếu: 01→335, 02+07→337, 03→339, 04→341, 05→343, 06→345, 08→347, 09→349, 10→351 = **10 UC / 9 key**. Đủ.

### 0.2 Bảng trạng thái

| Thứ tự build | SCRUM Key | UC | Tên | FR chống lưng | Loại | Trạng thái |
|---|---|---|---|---|---|---|
| 1 | SCRUM-335 | UC-ANA-01 | Operations Dashboard Overview | ❌ không có FR | Net-new (**bootstrap module**) | ⬜ Chưa bắt đầu |
| 2 | SCRUM-345 | UC-ANA-06 | Product Price Trend Summary | ✅ FR-ANA-001 | Net-new (query-only) | ⬜ Chưa bắt đầu |
| 3 | SCRUM-337 | UC-ANA-02+07 | Order Metrics | ❌ không có FR | Net-new (query-only) | ⬜ Chưa bắt đầu |
| 4 | SCRUM-339 | UC-ANA-03 | Procurement Metrics | ❌ không có FR | Net-new (query-only) — **trùng MỘT PHẦN** `/admin/order-groups/progress`, KHÔNG đóng (DEC-ANA-09) | ⬜ Chưa bắt đầu |
| 5 | SCRUM-341 | UC-ANA-04 | Hub Throughput Metrics | ❌ không có FR | Net-new (query-only) | ⬜ Chưa bắt đầu |
| 6 | SCRUM-343 | UC-ANA-05 | Delivery Performance Metrics | ✅ FR-ANA-003 | Net-new (query-only) — 2/6 chỉ số **degrade** (DEC-ANA-08) | ⬜ Chưa bắt đầu |
| 7 | SCRUM-347 | UC-ANA-08 | Demand Heatmap by Area | ✅ FR-ANA-002 | Net-new (query-only) | ⬜ Chưa bắt đầu |
| 8 | SCRUM-351 | UC-ANA-10 | Recent Activities | ❌ không có FR | **Gần như đã cover** — `audit_logs` + `GET /admin/audit-logs` đã có. Đề xuất **Option B-lite** (DEC-ANA-07) | ⬜ Chờ supervisor chọn scope |
| 9 | SCRUM-349 | UC-ANA-09 | Export Dashboard Data | ✅ FR-ANA-004 | Net-new — **làm cuối** (tái dùng query 337-347) | ⬜ Chưa bắt đầu |

**Không có task nào đề xuất đóng hoàn toàn kiểu Option A (như SCRUM-296 HUB).** Ứng viên duy nhất là **351** (audit_logs đã có sẵn) nhưng feed hiện chỉ phủ 4/12 loại sự kiện → đề xuất Option B-lite thay vì đóng hẳn. Xem DEC-ANA-07.

**Quy ước commit:** `feat(analytics): SCRUM-XXX <description>` — 1 commit/task, đúng key của task. KHÔNG commit khi chưa được supervisor cấp/xác nhận key (memory `feedback_no_commit_without_jira`).

---

## 1. Hiện trạng repo tại thời điểm khảo sát

### 1.1 Module Analytics — RỖNG, nhưng đã được nối dây MỘT PHẦN

`find src/Modules/Analytics -name "*.cs"` (ngoài `obj/`) → **0 file**. Chỉ có 3 `.csproj`. 18 file `.cs` mà supervisor thấy đều nằm trong `obj/` = artifact build của project rỗng.

**⚠️ KHÁC Hub lúc bootstrap — 2 việc ĐÃ LÀM SẴN, đừng làm lại:**

| Bước bootstrap | Trạng thái THẬT | Ghi chú |
|---|---|---|
| Đăng ký `.slnx` | ✅ **ĐÃ CÓ** | `FreshFlow.slnx` dòng 13-16 đã có cả 3 project Analytics |
| `FreshFlow.API.csproj` → ProjectReference | ✅ **ĐÃ CÓ** | dòng 35 `..\Modules\Analytics\FreshFlow.Analytics.Infrastructure\...` |
| Package refs trong `.csproj` | ❌ **THIẾU** | xem 1.2 |
| `AddAnalyticsModule()` | ❌ **THIẾU** | `Program.cs` dòng 188-196 chain 9 module, KHÔNG có Analytics |
| `ForceLoadModuleAssemblies` | ❌ **THIẾU** | `DesignTimeDbContextFactory.cs:77` list 8 assembly, KHÔNG có Analytics |
| `tests/Unit/FreshFlow.Analytics.UnitTests` | ❌ **THIẾU** | 11 test project khác đã có, không có Analytics |
| `ValidationBehavior` | ❌ **THIẾU** | mỗi module có bản COPY riêng (9 bản, xem 1.5) |

### 1.2 `.csproj` Analytics — thiếu toàn bộ PackageReference

Hiện trạng THẬT:
- `FreshFlow.Analytics.Domain.csproj` → chỉ ref `SharedKernel`. **Đúng luật, giữ nguyên.** (Domain sẽ RỖNG — DEC-ANA-02.)
- `FreshFlow.Analytics.Application.csproj` → ref `Domain` + `SharedKernel` + `Contracts`. **Đúng luật** nhưng **THIẾU** `PackageReference` MediatR + FluentValidation.
- `FreshFlow.Analytics.Infrastructure.csproj` → chỉ ref `Application`. **THIẾU** `FreshFlow.Infrastructure.Persistence` (bắt buộc để chạm `AppDbContext`) + 6 package.

Đối chiếu mẫu chuẩn `FreshFlow.Hub.Infrastructure.csproj` (bản sao 1-1 cần đạt tới):
```xml
<PackageReference Include="FluentValidation" />
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
<PackageReference Include="MediatR" />
<PackageReference Include="Microsoft.EntityFrameworkCore" />
<PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
```
+ `<ProjectReference Include="..\..\..\FreshFlow.Infrastructure.Persistence\FreshFlow.Infrastructure.Persistence.csproj" />`
(package version quản lý tập trung — KHÔNG ghi `Version=` trong csproj.)

`FreshFlow.Hub.Application.csproj` chỉ thêm `FluentValidation` + `MediatR` → Analytics.Application copy y hệt.

### 1.3 Nguồn dữ liệu — đã verify code THẬT (đây là phần quan trọng nhất)

**Mọi epic khác đã xong ⇒ Analytics KHÔNG chờ ai.** Bảng nguồn + casing (đã verify từng file Configuration):

| Bảng | Module chủ | Casing cột | Cột hữu ích cho ANA |
|---|---|---|---|
| `orders` | Orders | **HỖN HỢP** ⚠️ | `"Id"`, `"RestaurantId"`, `"Status"`, `"TotalAmount"`, `"CreatedAt"`, `"OrderGroupId"`, `"CancelledAt"` = **PascalCase**; nhưng `deleted_at`, `confirmed_receipt_at` = **snake_case** |
| `order_items` | Orders | **PascalCase** | `"Id"`, `"OrderId"`, `"MarketProductId"`, `"Quantity"` (**int**), `"UnitPrice"`, `"LockedUnitPrice"`, `"LockedTotal"`, `"ActualQuantity"` |
| `price_snapshots` | Pricing | **PascalCase** | `"Id"`, `"MarketProductId"`, `"Price"` numeric(12,2), `"Quantity"`, `"RecordedAt"`, `"RecordedBy"` — **append-only**, range-partition theo tháng trên `RecordedAt`, có index `idx_price_snapshots_recorded_at` |
| `market_products` | Catalog | PascalCase + `"deleted_at"` | `"Id"`, `"ProductId"`, `"MarketId"`, `"CurrentPrice"` |
| `products` / `markets` / `restaurants` | Catalog/Auth | PascalCase, **NHƯNG** `restaurants.status` = **lowercase** ⚠️ | verify qua seam THẬT trong snapshot: `... WHERE da."IsDefault" = true AND da."DeletedAt" IS NULL AND r.status = 'active'` |
| `delivery_addresses` | Auth | PascalCase | `"RestaurantId"`, `"Latitude"`, `"Longitude"`, `"IsDefault"` |
| `procurement_batches` | Procurement | **snake_case** | `batch_date`, `market_id`, `status`, `manifested_at`, `assigned_at`, `handed_off_at`, `hub_id`, `total_item_count`, `deleted_at` |
| `procurement_batch_items` | Procurement | **snake_case** | `total_quantity`, `reference_unit_price`, `actual_quantity`, `actual_unit_price`, `purchased_at` |
| `procurement_exceptions` | Procurement | **snake_case** | `type`, `reported_quantity`, `reported_at`, `procurement_batch_id` |
| `hub_inbound_events` | Hub | **snake_case** | `hub_id`, `source_market_id`, `total_quantity_kg`, `arrived_at`, `status`, `condition_status`, `deleted_at` |
| `hub_outbound_events` | Hub | **snake_case** | `hub_id`, **`destination_route_id`**, `total_quantity_kg`, `dispatched_at`, `deleted_at` |
| `hub_handover_events` | Hub | **snake_case** | `delivery_route_id`, `handed_over_at`, **`driver_confirmed_at`**, `status` |
| `deliveries` | Logistics | **snake_case** | `delivery_route_id`, `order_id`, **`estimated_arrival`**, **`actual_arrival`**, `status` ∈ (pending/arrived/delivered/failed), `failure_reason`, `deleted_at` |
| `delivery_routes` | Logistics | **snake_case** | `route_type`, `status`, `service_date`, `total_distance_km`, `estimated_duration_minutes`, `vehicle_id`, `driver_user_id`, `order_group_id` — **KHÔNG có started_at/completed_at** ⚠️ (DEC-ANA-08) |
| `vehicles` | Logistics | snake_case | `capacity_kg`, `plate_number`, `vehicle_type` |
| `audit_logs` | Infrastructure.Persistence | **snake_case** | `actor_id`, `action`, `entity_type`, `entity_id`, `details` jsonb, `occurred_at` — append-only, có index trên cả 4 cột filter |

> **⚠️ CASING GOTCHA — nguồn lỗi số 1 của epic này.** 2 convention cùng tồn tại (memory `project_ef_column_casing`) và **`orders` trộn CẢ HAI trong cùng 1 bảng**. Mọi `ToSqlQuery` PHẢI alias về PascalCase khớp property của Row. Mẫu THẬT đã chạy (trích `AppDbContextModelSnapshot.cs`):
> - Orders (hỗn hợp): `SELECT "Id" AS "OrderId", "Status" AS "Status", "RestaurantId" AS "RestaurantId" FROM orders WHERE "deleted_at" IS NULL`
> - Logistics (snake): `SELECT id AS "RouteId", status AS "Status", driver_user_id AS "DriverUserId" FROM delivery_routes WHERE deleted_at IS NULL`
> - Hub (snake): `SELECT order_id AS "OrderId" FROM hub_discrepancies WHERE status = 'OPEN' AND deleted_at IS NULL`
>
> **KHÔNG đoán casing. Mở file `*Configuration.cs` của bảng đó xác nhận trước khi viết SQL.**

### 1.4 Cross-module read-seam pattern (TÁI DÙNG — KHÔNG viết mới)

Repo có **~25 seam** đang chạy. Mẫu chuẩn mới nhất = `Hub.Infrastructure/CrossModule/` và `Procurement.Infrastructure/CrossModule/`. Bộ 3 file:
1. `XxxRow.cs` — POCO, init props, PascalCase.
2. `XxxRowConfiguration.cs` — `IEntityTypeConfiguration<XxxRow>` với `builder.HasNoKey(); builder.ToSqlQuery("""SELECT ... FROM <bảng module khác> WHERE deleted_at IS NULL""");` — **KHÔNG `ToTable`** (tránh model-conflict với module chủ).
3. `XxxReader.cs` — `internal sealed class XxxReader(AppDbContext db) : IXxxReader` → `db.Set<XxxRow>().AsNoTracking()...`

Interface + DTO ở `*.Application/Abstractions`, impl ở `*.Infrastructure/CrossModule`, đăng ký DI trong `AddAnalyticsModule`.

**Verify quan trọng:** keyless Row **CÓ vào `AppDbContextModelSnapshot.cs`** (đã kiểm chứng: `DeliveryRouteRow` dòng 1163, `OrderLookupRow` dòng 1180, `ConfirmedOrderRow` dòng 2860). ⇒ **BẮT BUỘC** thêm `"FreshFlow.Analytics.Infrastructure"` vào `ForceLoadModuleAssemblies`, nếu không model design-time ≠ runtime → migration kế tiếp của module khác sẽ **âm thầm xoá Row Analytics khỏi snapshot** (bài học LOG/NOT/HUB). `ToSqlQuery` không sinh DDL nên migration sẽ rỗng — lỗi này **không nổ ngay**, chỉ drift snapshot.

### 1.5 Plumbing dùng chung

- **`ValidationBehavior`**: KHÔNG có bản dùng chung — mỗi module **copy riêng** (9 bản: Hub/Procurement/Catalog/Pricing/Logistics/Auth/Orders/Notifications + Persistence). Analytics **copy 1 bản** vào `Analytics.Application/Behaviors/ValidationBehavior.cs` (theo mẫu Hub). Đây là duplication CÓ CHỦ Ý của repo — KHÔNG refactor gộp trong epic này (ngoài scope).
- **`Result<T>` / `Error` / `IQuery<T>`**: `src/Shared/FreshFlow.SharedKernel/Application/`. Analytics chỉ có QUERY (không command) → dùng `IQuery<T>`.
- **`ErrorExtensions.ToActionResult()`** (`src/FreshFlow.API/Extensions/ErrorExtensions.cs`): map **theo CODE STRING**, không theo type. Code kết thúc `_NOT_FOUND` → 404 tự động; `VALIDATION_ERROR` → 400; `FORBIDDEN` → 403. **Analytics dự kiến KHÔNG cần thêm code mới** (chỉ 400/403/404 sẵn có) — nếu cần thì phải đăng ký tường minh, nếu không rơi về **500**.
- **DTO** = `record` PascalCase; JSON casing do config global, KHÔNG đặt tên property snake_case.
- **Cursor pagination**: private record trong TỪNG repository (mẫu `HubRepository.HubCursor`: base64 JSON, `Take(pageSize+1)`). Nhưng `GetAuditLogsQuery` dùng **offset paging** (`Page`/`PageSize`/`Total`). Analytics = dashboard → offset paging OK (DEC-ANA-12).

### 1.6 ⚠️ Phát hiện lớn: repo có **ZERO** `FromSqlRaw`

`grep -rn "FromSqlRaw\|FromSqlInterpolated\|SqlQuery<" --include="*.cs" src` → **0 kết quả.**

CLAUDE.md nói *"No raw SQL except in `Analytics.Infrastructure` for complex aggregations"* — tức Analytics là nơi **được phép**, nhưng **KHÔNG có tiền lệ nào trong repo**. Toàn bộ raw SQL hiện tại là **static, không tham số** (`ToSqlQuery` của seam). ⇒ Analytics sẽ là module ĐẦU TIÊN dùng raw SQL có tham số nếu cần. Đây là **điểm reviewer soi kỹ nhất** → DEC-ANA-04 chốt chặt chính sách.

### 1.7 Endpoint đã có, liên quan trực tiếp

| Endpoint | Handler | Liên quan task |
|---|---|---|
| `GET /api/v1/admin/order-groups/progress` | `GetProcurementProgressQueryHandler` | **339** — trùng MỘT PHẦN, xem DEC-ANA-09 |
| `GET /api/v1/admin/audit-logs` | `GetAuditLogsQueryHandler` (offset paged, filter actor/action/entity/from/to) | **351** — gần như cover, xem DEC-ANA-07 |
| `GET /api/v1/markets/{marketId}/products/{productId}/price-history` | `GetPriceChangeHistoryQueryHandler` (cursor paged, from/to, **raw list KHÔNG có summary stats**) | **345** — KHÔNG cover (thiếu min/max/avg/stddev + multi-product + bucketing), xem DEC-ANA-11 |

`AdminController` = `[Route("api/v1/admin")]`, `[Authorize]` mức class + `[Authorize(Roles=...)]` **mức action** (đa số `admin`, vài cái `admin,operations_manager`). 22 controller hiện có, **KHÔNG có `AnalyticsController`**.

### 1.8 Docs vs code — lệch cần biết

- `docs/04-api-design.md` dòng 764: *"Logistics (4.3), Hub (4.4), and Analytics (4.5) are entirely `[PLANNED]`"* → **Logistics + Hub nay ĐÃ XONG**, docs chưa cập nhật. Chỉ Analytics còn đúng là `[PLANNED]`.
- `docs/04` §4.5 (dòng 2257-2267) chỉ spec **5 endpoint** (price-trends, demand-heatmap, delivery-performance, export, export status) = phủ FR-ANA-001..004. **KHÔNG spec** Overview / Order Metrics / Procurement Metrics / Hub Throughput / Recent Activities.
- RBAC matrix `docs/04` dòng 3272-3276: `price-trends` = Admin **+ Restaurant**; 4 endpoint còn lại = **Admin only**.
- CLAUDE.md nhắc `PartitionMaintenanceJob` tạo partition `price_snapshots` — **job này KHÔNG TỒN TẠI** (memory `project_background_job_pattern`). Không ảnh hưởng ANA (chỉ đọc), nhưng **nếu partition tháng chưa được tạo thì `price_snapshots` không có dữ liệu mới** → ghi nhận rủi ro, KHÔNG fix trong epic này.

---

## 2. Quyết định thiết kế (DEC-ANA) — leader tự chốt theo code THẬT + convention

- **DEC-ANA-01 — Bootstrap ở SCRUM-335, checklist RÚT GỌN so với DEC-HUB-01.** Vì `.slnx` + `API.csproj` **đã có sẵn**, task 335 chỉ cần: (a) thêm PackageReference vào `Analytics.Application.csproj` (FluentValidation, MediatR) + `Analytics.Infrastructure.csproj` (6 package như Hub) + ProjectReference `FreshFlow.Infrastructure.Persistence`; (b) tạo `Analytics.Application/Behaviors/ValidationBehavior.cs` (copy từ Hub); (c) `AddAnalyticsModule(config)` trong `Analytics.Infrastructure/DependencyInjection.cs` — gọi `EfAssemblyRegistry.Register(Assembly.GetExecutingAssembly())` + `services.TryAddSingleton(config)` + `AddMediatR(RegisterServicesFromAssembly(applicationAssembly) + AddOpenBehavior(ValidationBehavior<,>))` + `AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true)` + đăng ký reader; (d) `builder.Services.AddAnalyticsModule(builder.Configuration);` vào `Program.cs` **sau dòng 195 `AddHubModule`**; (e) thêm `"FreshFlow.Analytics.Infrastructure"` vào `ForceLoadModuleAssemblies` (`DesignTimeDbContextFactory.cs:77`); (f) tạo `tests/Unit/FreshFlow.Analytics.UnitTests` (mẫu `FreshFlow.Hub.UnitTests`) **+ đăng ký vào `FreshFlow.slnx`** (memory `project_solution_file_slnx` — **KHÔNG** `.sln`, file đó không tồn tại). **KHÔNG lặp lại (a) `.slnx` project src và (b) API.csproj ref — đã có.**

- **DEC-ANA-02 — Analytics KHÔNG có bảng riêng, KHÔNG migration, Domain project để RỖNG.** Toàn bộ 9 task đọc thẳng bảng nguồn qua seam. Không có aggregate/entity/domain event/integration event nào. `FreshFlow.Analytics.Domain.csproj` compile rỗng (hợp lệ) — **KHÔNG tạo entity giả cho đủ bộ**. Lý do: (a) YAGNI — không UC nào yêu cầu ghi; (b) bảng pre-aggregate = dữ liệu trùng lặp cần job đồng bộ, chi phí >> lợi ích ở quy mô hiện tại; (c) `audit_logs` đã là "bảng analytics" duy nhất cần thiết và **đã tồn tại**. **Hệ quả: epic ANA có 0 migration.** Nếu 1 query nào đó chậm → thêm **index** (migration additive, 1 dòng) chứ KHÔNG thêm bảng. **Override** FR-ANA-003 AC4 ("served from a pre-aggregated analytics table") — xem DEC-ANA-10.

- **DEC-ANA-03 — Đọc cross-module CHỈ qua keyless Row + `HasNoKey()` + `ToSqlQuery`; TUYỆT ĐỐI không project-reference chéo module.** `Analytics.Application` chỉ được ref Domain + SharedKernel + Contracts (đúng luật CLAUDE.md). Mỗi bảng nguồn = 1 bộ 3 file trong `Analytics.Infrastructure/CrossModule/` (mẫu §1.4). **Alias mọi cột về PascalCase** khớp property Row. **Bắt buộc `WHERE deleted_at IS NULL`** cho bảng có soft-delete (`orders` dùng `"deleted_at"` có quote, `deliveries`/`procurement_batches`/`hub_*` dùng `deleted_at` trần — xem bảng casing §1.3). `price_snapshots` + `audit_logs` **append-only, KHÔNG có deleted_at** → không filter.

- **DEC-ANA-04 — Chính sách raw SQL (điểm reviewer soi kỹ nhất).** Repo hiện có **0 `FromSqlRaw`** (§1.6). Thứ tự ưu tiên **bắt buộc**:
  1. **LINQ + `GroupBy`/`Count`/`Sum`/`Average` trên keyless Row** — MẶC ĐỊNH. Npgsql dịch được `date_trunc` qua `.Date`, `EF.Functions.DateDiffMinute`, `d.CreatedAt.Hour`, `(int)d.CreatedAt.DayOfWeek`. **Dùng cho 337/339/341/347/351 và phần lớn 343/345.**
  2. **Aggregate trong SQL của chính `ToSqlQuery`** (static, không tham số) — nếu cần `stddev_samp`/`percentile_cont` mà LINQ không dịch. **An toàn tuyệt đối vì không nhận input người dùng**; filter `from`/`to` áp ở tầng LINQ bên ngoài → EF tự tham số hoá. **Đây là lựa chọn ưu tiên cho `priceVolatility` (345).**
  3. **`FromSql($"...")` (FormattableString, EF tự parameterize)** — CHỈ khi 1+2 không đủ. **KHÔNG dùng `FromSqlRaw` + nội suy chuỗi.**

  **CẤM tuyệt đối:** `FromSqlRaw($"... WHERE x = {userInput}")`, `string.Format`, `+` nối chuỗi vào SQL, `ORDER BY {sortColumn}` từ input. **Cột sort/nhóm do client chọn** (nếu có) PHẢI qua **allow-list `switch` map sang biểu thức LINQ**, không bao giờ ghép vào SQL. Mọi giá trị lọc (`from`/`to`/`marketId`/`productId`) là **tham số**, không bao giờ là text SQL.

- **DEC-ANA-05 — `AnalyticsController` MỚI tại `/api/v1/analytics`, KHÔNG nhét vào `AdminController`.** Lý do: (a) `docs/04` §4.5 + RBAC matrix spec rõ prefix `/analytics`; (b) `AdminController` đã 358 dòng / 20+ action — thêm 9 endpoint nữa là vi phạm giới hạn file của repo; (c) RBAC khác nhau (`price-trends` cho cả Restaurant, phần còn lại admin-only) → không thể để `[Authorize(Roles="admin")]` mức class. **Pattern:** `[Route("api/v1/analytics")]` + `[Authorize]` mức class, `[Authorize(Roles=...)]` **mức action** (y hệt `AdminController` dòng 34-36). Mặc định `admin,operations_manager` cho mọi endpoint ops; **RIÊNG `price-trends`** = `admin,operations_manager,restaurant_manager,restaurant_staff` (đúng RBAC matrix docs/04 dòng 3272). ⚠️ **Verify tên role THẬT trong seed** (`migration AddRolesTable`) trước khi ghi chuỗi — nếu không khớp thì `[Authorize]` fail câm.

- **DEC-ANA-06 — SCRUM-349 Export = CSV **ĐỒNG BỘ**, KHÔNG job async, KHÔNG dependency mới. (OVERRIDE FR-ANA-004 AC3 + docs/04 §4.5.)** Docs yêu cầu `POST /analytics/export` → 202 + `jobId` → poll status → download, file sống 24h. **Từ chối** vì: (a) cần bảng `export_jobs` (vi phạm DEC-ANA-02) + background worker + blob storage + job dọn file — hạ tầng KHÔNG tồn tại; (b) `IBackgroundJobService`/Hangfire/Quartz **không có trong repo** (memory `project_background_job_pattern`: chỉ có `ScheduledOrderGenerationHostedService` tự viết); (c) Cloudinary chỉ dùng cho ảnh, không phải file export. **Quyết định:** `GET /api/v1/analytics/export?dataset=...&from=...&to=...&format=csv` → trả **200 + `text/csv`** + `Content-Disposition: attachment` (thoả FR-ANA-004 AC1+AC2). **Sinh CSV thủ công bằng `StringBuilder`** (~30 dòng, escape RFC4180: bọc `"` khi field chứa `,`/`"`/newline, double-up `"` bên trong) — **KHÔNG thêm CsvHelper**. **Chặn export lớn:** hard cap **50 000 dòng**; vượt → **400 `EXPORT_RANGE_TOO_LARGE`** kèm gợi ý thu hẹp `from`/`to` (thay cho async của AC3). `dataset` qua **allow-list enum** (`price-history` | `order-history` | `delivery-performance` — đúng FR-ANA-004), giá trị lạ → 400. **Mở lại async khi có hạ tầng job thật** (ghi rõ là simplification).

- **DEC-ANA-07 — SCRUM-351 Recent Activities = **Option B-lite**: tái dùng `audit_logs` + bù handler còn thiếu. KHÔNG bảng mới, KHÔNG đóng hẳn.** Hiện trạng THẬT: `audit_logs` + `IAuditLogWriter` + `GET /admin/audit-logs` (filter actor/action/entity/from/to, offset paged, index đủ) **đã chạy**. NHƯNG feed chỉ được nuôi bởi **4 handler**: `PriceUpdated`, `OrderCancelled`, `CreditLimitThresholdReached`, `RestaurantRefundIssued`. Trong khi `FreshFlow.Contracts` **đã có sẵn 8 event chưa ai audit**: `OrderConfirmed`, `ProcurementBatchBuilt`, `ProcurementManifestGenerated`, `ProcurementAgentAssigned`, `ProcurementBatchHandedOff`, `DeliveryStarted`, `DeliveryCompleted`, `HubDiscrepancyRecorded`. ⇒ "Recent Activities" của dashboard ops mà **thiếu order confirmed / procurement / delivery** thì vô dụng. **Quyết định:** 351 = (a) thêm **7-8 audit handler** vào `src/FreshFlow.Infrastructure.Persistence/Audit/` — mỗi cái ~15 dòng, **copy nguyên mẫu `OrderCancelledAuditLogHandler`**, tái dùng `IAuditLogWriter` sẵn có, **KHÔNG event mới, KHÔNG bảng mới**; (b) `GET /api/v1/analytics/recent-activities` = **wrapper mỏng delegate thẳng `GetAuditLogsQuery` đã có** (không viết query mới), default `PageSize=20`, sort `occurred_at DESC`. **Ghi chú:** `AuditLogWriter` cố tình **nuốt exception** (`catch (Exception)` → chỉ log) để audit-fail không làm hỏng business flow → activity feed là **best-effort, có thể thiếu dòng**; đây là hành vi CÓ CHỦ Ý của repo, KHÔNG "sửa" trong epic này. **Nếu supervisor muốn cực lười → Option A: đóng 351, xài `GET /admin/audit-logs`, chấp nhận feed thiếu 8 loại sự kiện.** Leader **khuyến nghị B-lite** (diff nhỏ, giá trị thật). **Cần supervisor chốt.**

- **DEC-ANA-08 — SCRUM-343: 4/6 chỉ số tính được ĐÚNG, 2/6 phải degrade. (Điều chỉnh FR-ANA-003 AC2.)** Verify code THẬT `Delivery.cs` + `DeliveryRoute.cs`:
  - ✅ `totalDeliveries` / `onTimeCount` / `lateCount` / `onTimeRatePercent`: `deliveries` có **`estimated_arrival`** + **`actual_arrival`**. **LATE ⟺ `actual_arrival > estimated_arrival + 15 phút`.** **Điều chỉnh AC2** (docs nói `actualArrivalAt > plannedDepartureAt + estimated route duration + 15m`): `estimated_arrival` **chính là** kết quả của phép tính đó, đã được route planner ghi sẵn lúc tạo delivery ⇒ dùng thẳng, **tương đương về ngữ nghĩa, đơn giản hơn nhiều**. Chỉ đếm `status = 'delivered'` và `actual_arrival IS NOT NULL`; `failed` đếm riêng, KHÔNG tính vào on-time rate.
  - ⚠️ `avgDeliveryDurationMinutes`: **`delivery_routes` KHÔNG có cột thời điểm khởi hành thật.** `DeliveryRoute.Start()` (dòng 163-170) và `.Complete()` chỉ set `Status` + `UpdatedAt`; **không có `started_at`/`completed_at`**, và `updated_at` bị ghi đè bởi mọi update sau đó ⇒ **không dùng được**. **Quyết định (tái dùng, không migration):** dùng **`hub_handover_events.driver_confirmed_at`** (driver checkout tại hub = thời điểm rời hub THẬT, có `delivery_route_id`) làm mốc khởi hành ⇒ `duration = deliveries.actual_arrival − hub_handover_events.driver_confirmed_at`, join theo `delivery_route_id`. **Route KHÔNG có handover đã checkout thì LOẠI khỏi chỉ số này** (không tính là 0!) + trả kèm `durationSampleCount` để FE biết mẫu nhỏ. **KHÔNG thêm cột vào `delivery_routes`** — đó là sửa module Logistics = mở scope cross-epic. **Nếu supervisor muốn chỉ số chính xác 100% cho MỌI route** → cần task riêng ở LOG epic thêm `started_at`/`completed_at` (2 cột + 2 dòng trong `Start()`/`Complete()`). **Flag để supervisor quyết.**
  - ⚠️ `avgVehicleUtilizationPercent`: `delivery_routes` **không mang tải trọng**; `RouteStop` cũng không (chỉ toạ độ + ETA); `order_items.Quantity` là **`int`** không phải kg ⇒ không suy ra kg từ Orders. **Quyết định (tái dùng):** dùng **`hub_outbound_events.total_quantity_kg`** — bảng này CÓ **`destination_route_id`** và **đo bằng kg thật** (DEC-HUB-04 đã thống nhất mọi thứ đo kg) ⇒ `utilization = SUM(hub_outbound_events.total_quantity_kg) per route ÷ vehicles.capacity_kg × 100`, join `delivery_routes.vehicle_id → vehicles`. **Loại** route có `vehicle_id IS NULL` hoặc không có outbound event; **clamp về 100** nếu >100 (dữ liệu bẩn) và trả `utilizationSampleCount`.
  - **KHÔNG có** `GET /analytics/delivery-performance/by-route` ở MVP (FR-ANA-003 AC3) trừ khi supervisor yêu cầu — cùng query, thêm `GROUP BY route`. Ưu tiên ship overall trước.

- **DEC-ANA-09 — SCRUM-339 KHÔNG đóng: chồng lấn `/admin/order-groups/progress` là NHỎ và khác mục đích.** Đọc code THẬT `GetProcurementProgressQueryHandler`: nó lấy **MỘT chu kỳ** (`Date` hoặc `GetLatestCycleDateAsync()`), trả trạng thái **live** của các batch trong ngày đó (`statusCounts`, `itemsPurchased/Pending`, `exceptionCount`) — **công cụ VẬN HÀNH theo dõi hôm nay**, không nhận `from`/`to`, không tính chi phí/độ lệch giá. **SCRUM-339 = metrics THEO KHOẢNG** (`from`..`to`, nhiều chu kỳ): tỉ lệ hoàn thành batch, tổng chi phí thu mua thực tế, **độ lệch giá thực tế vs tham chiếu** (`actual_unit_price` vs `reference_unit_price` — dữ liệu `/progress` KHÔNG đụng tới), tỉ lệ exception theo `type`, lead time (`manifested_at → handed_off_at`). ⇒ **Trùng lặp thực tế chỉ ở `exceptionCount` + đếm status.** Khác biệt đủ lớn (period vs single-cycle, có chiều tiền vs không) ⇒ **giữ 339, build mới trong Analytics**, KHÔNG sửa/không gọi lại endpoint Procurement (không cross-module ref). **Chấp nhận** 2 endpoint cùng đếm status theo 2 trục thời gian khác nhau — đúng ý đồ.

- **DEC-ANA-10 — KHÔNG Redis cache, KHÔNG pre-aggregate ở MVP. (OVERRIDE FR-ANA-003 AC4 "<800ms từ bảng pre-aggregate/Redis" + docs/04 "15-minute TTL cache".)** Lý do: (a) YAGNI — chưa có số đo nào chứng minh LINQ + index sẵn có là chậm; (b) cache = invalidation bug + dữ liệu ops cũ 15 phút gây hiểu nhầm; (c) mọi bảng nguồn **đã có index** trên đúng cột lọc (`idx_price_snapshots_recorded_at`, `idx_deliveries_delivery_route_id`, `audit_logs` index cả 4 cột filter, `orders."CreatedAt"`) — verify lại khi đo. **Cách đạt 800ms mà không cache:** bắt buộc `from`/`to` + cap độ rộng khoảng (DEC-ANA-12) + `AsNoTracking()` + chỉ `SELECT` cột cần. **Mở lại cache khi có số đo thật** vượt ngưỡng (đo trước, tối ưu sau). Nếu chậm → **thêm index trước** (migration additive), pre-aggregate là phương án cuối.

- **DEC-ANA-11 — SCRUM-345 KHÔNG tái dùng `GetPriceChangeHistory`; build mới trong Analytics.** Endpoint Pricing hiện có (`GET /markets/{marketId}/products/{productId}/price-history`) trả **danh sách thô, cursor-paged, 1 sản phẩm/1 chợ, KHÔNG summary stats**. FR-ANA-001 cần: **multi-product (≤10)**, **summary `minPrice`/`maxPrice`/`avgPrice`/`priceVolatility` (stddev)**, **bucket theo ngày khi khoảng >12 tháng** (AC4). Khác biệt lớn ⇒ query mới. **KHÔNG sửa/không gọi module Pricing** (cấm cross-module ref) — Analytics đọc thẳng `price_snapshots` qua seam riêng. **Tham số:** theo `docs/04` §4.5 dùng **`marketProductId` (lặp ≤10 lần)**, KHÔNG phải `productId`+`marketId` như FR-ANA-001 — **chọn `marketProductId`** vì đó là khoá thật trên `price_snapshots` (khỏi resolve 2 bước) và là spec API mới hơn. **`priceVolatility` = `stddev_samp`**: LINQ **không dịch được** ⇒ đây là chỗ **duy nhất** dùng aggregate SQL, ưu tiên **rung 2 của DEC-ANA-04** (`ToSqlQuery` static có `stddev_samp`, filter tham số áp ngoài bằng LINQ). Nếu mẫu <2 điểm → `priceVolatility = null` (KHÔNG 0 — `stddev_samp` trả NULL, đừng che). **Bucketing:** khoảng >12 tháng → **ép** `interval=daily`, group `RecordedAt.Date`, lấy `avg(Price)` (AC4). Giới hạn **≤10 `marketProductId`** ở validator → vượt = 400.

- **DEC-ANA-12 — Mọi endpoint: `from`/`to` BẮT BUỘC + cap khoảng + paging. (Chống unbounded query — điểm reviewer soi.)** Ngoại lệ: `335` overview (mặc định "hôm nay"/7 ngày gần nhất) và `351` recent-activities (paged, không cần range). Quy tắc:
  - `from`/`to` **required** cho 337/339/341/343/345/347/349; thiếu → 400 `VALIDATION_ERROR`.
  - `from <= to` (mẫu `GetPriceChangeHistoryQueryValidator` — copy nguyên rule).
  - **Cap độ rộng: 366 ngày** (400 `VALIDATION_ERROR`), trừ 345 cho phép tới 24 tháng nhưng **ép daily** khi >12 tháng (AC4). 349 cap thêm bằng 50k dòng (DEC-ANA-06).
  - Endpoint trả **list** (347 heatmap, 351 activities, 349 export) **PHẢI paged/capped** — KHÔNG `ToListAsync()` trần trên bảng nguồn. Heatmap gom theo restaurant (bounded ~số nhà hàng) nhưng vẫn cap + `PageSize` `InclusiveBetween(1,200)` (mẫu Hub).
  - **Timezone:** lưu UTC; `from`/`to` là **ngày business `Asia/Ho_Chi_Minh`** → handler convert sang UTC ở **BIÊN** trước khi query (mẫu Orders). Ghi rõ trong DTO là ngày VN. **Đừng để lệch 7h âm thầm** — đây là bug hay gặp nhất của dashboard.

- **DEC-ANA-13 — Analytics KHÔNG raise/consume domain event, KHÔNG integration event, KHÔNG SignalR.** Module chỉ đọc. **Ngoại lệ DUY NHẤT:** 8 audit handler của DEC-ANA-07 — nhưng chúng **CONSUME event ĐÃ CÓ** và đặt trong `FreshFlow.Infrastructure.Persistence/Audit/` (nơi 4 handler hiện tại đang ở), **KHÔNG** trong module Analytics. **KHÔNG thêm event mới vào `FreshFlow.Contracts`** trong toàn epic. Dashboard realtime (nếu ai hỏi) = FE poll REST, **KHÔNG** thêm hub SignalR (3 hub hiện có: pricing/orders/delivery — `Program.cs` 307-309).

- **DEC-ANA-14 — 6/10 UC KHÔNG có FR ⇒ scope do leader chốt theo dữ liệu THẬT sẵn có; KHÔNG bịa chỉ số.** Nguyên tắc: **chỉ số nào tính được từ cột đã tồn tại thì làm; không tồn tại thì BỎ, ghi rõ, KHÔNG thêm cột/bảng để có nó.** Cụ thể: 335 Overview = **thẻ KPI mỏng** gom từ các nguồn có sẵn (đơn hôm nay, doanh thu hôm nay, batch đang chạy, delivery on-time hôm nay, tồn hub) — **KHÔNG** phát minh chỉ số mới; 337 Order Metrics = đếm/tổng/AOV/tỉ lệ huỷ theo status + theo ngày; 339 = §DEC-ANA-09; 341 Hub Throughput = kg in/out + số event theo hub/ngày; 351 = §DEC-ANA-07. **Nếu supervisor có wireframe FE cho dashboard → GỬI TRƯỚC KHI CODE 335**, vì tập KPI của Overview là điểm mơ hồ nhất của epic (xem §7).

---

## 3. Rủi ro & bề mặt bảo mật (điểm reviewer sẽ soi)

- **🔴 SQL injection (rủi ro số 1 — module đầu tiên của repo dùng raw SQL có tham số).** Tuân thủ **DEC-ANA-04** tuyệt đối: LINQ trước, `ToSqlQuery` static thứ hai, `FromSql($"")` cuối. **KHÔNG BAO GIỜ** ghép `from`/`to`/`marketProductId`/`dataset`/`sort` vào chuỗi SQL. Cột động → allow-list `switch` → biểu thức LINQ. **Test bắt buộc:** 1 test/endpoint truyền chuỗi ác ý (`'; DROP TABLE orders;--`) vào **mọi** tham số string → assert không lỗi + không đổi dữ liệu (hoặc 400 tại validator). `dataset` của 349 = enum allow-list, KHÔNG free-text.
- **🔴 RBAC / rò rỉ dữ liệu chéo nhà hàng.** `price-trends` là endpoint ANA **duy nhất** mở cho Restaurant (docs/04 dòng 3272) — dữ liệu giá chợ là **công khai với mọi nhà hàng**, không nhạy cảm ⇒ chấp nhận. **NHƯNG: 347 demand-heatmap chứa `restaurantId` + toạ độ + doanh thu từng nhà hàng, 337 order metrics chứa doanh thu toàn hệ thống ⇒ admin-only, tuyệt đối KHÔNG mở cho `restaurant_*`** (rò rỉ dữ liệu kinh doanh của đối thủ cho nhau + lộ vị trí). FR-ANA-002 AC4 nói rõ Restaurant token → **403**. **Test RBAC cho TỪNG endpoint** (mẫu reflection test của HUB epic). Verify tên role khớp seed trước khi ghi chuỗi (`[Authorize]` sai tên role = fail câm, không báo lỗi build).
- **🟠 Unbounded query / OOM.** Không có `from`/`to` bắt buộc thì `price_snapshots` (append-only, partition theo tháng, tăng vô hạn) sẽ được quét toàn bộ. Tuân thủ **DEC-ANA-12**: required + cap 366 ngày + paging + `AsNoTracking()`. 349 cap 50k dòng. **Không `ToListAsync()` trần trên bảng nguồn.**
- **🟠 N+1.** Nguy cơ cao ở 347 (heatmap: mỗi restaurant 1 query lấy `dominantProductCategory`) và 343 (mỗi route 1 query lấy handover/outbound). **Bắt buộc:** 1 query gom + `GROUP BY`, hoặc JOIN sẵn trong `ToSqlQuery`. **Test:** assert số câu SQL hoặc review thủ công — **KHÔNG lặp `await` trong `foreach`**.
- **🟠 Chia cho 0 / mẫu rỗng.** `onTimeRatePercent` khi `totalDeliveries=0`; `avgVehicleUtilizationPercent` khi `capacity_kg=0` hoặc không route nào có vehicle; `priceVolatility` khi <2 điểm (→ **null**, không phải 0); AOV khi 0 đơn. **Mỗi chỉ số phải có test mẫu-rỗng** → trả `0`/`null` tường minh, KHÔNG `NaN`, KHÔNG 500.
- **🟠 Timezone lệch 7h.** `from`/`to` = ngày VN, DB lưu UTC. Convert ở biên (DEC-ANA-12). **Test:** đơn lúc 23:30 VN ngày N phải thuộc ngày N, không phải N+1. Đặc biệt nguy hiểm ở 347 (heatmap **hour-of-day / day-of-week 7×24** — nếu group theo UTC thì biểu đồ lệch 7 giờ, sai hoàn toàn mà **nhìn vẫn "hợp lý"**).
- **🟡 Soft-delete bỏ sót.** Quên `WHERE deleted_at IS NULL` → đếm cả đơn/batch đã xoá. Casing khác nhau giữa bảng (`"deleted_at"` vs `deleted_at` — §1.3). `price_snapshots`/`audit_logs` **không có** cột này.
- **🟡 Drift snapshot EF.** Quên `ForceLoadModuleAssemblies` → migration sau của module khác âm thầm xoá Row Analytics khỏi snapshot (§1.4). Sau task 335 chạy `dotnet ef migrations has-pending-model-changes` = **no drift**. Chạy `dotnet build-server shutdown` sau mỗi lệnh dotnet one-shot (memory `feedback_dotnet_cleanup`; nếu còn node treo → `pkill -f MSBuild`).
- **🟡 `price_snapshots` partition.** CLAUDE.md nói `PartitionMaintenanceJob` tạo partition tháng kế — **job KHÔNG tồn tại**. Nếu partition tháng hiện tại chưa tạo, INSERT snapshot mới sẽ fail ⇒ 345 trả khoảng trống. **Ngoài scope ANA** (chỉ đọc) nhưng ghi nhận để supervisor biết.
- **🟢 Không secret mới, không dependency mới, 0 migration** (DEC-ANA-02/06). Nếu ai đề xuất thêm CsvHelper/Hangfire/Redis → **từ chối**, quay lại DEC tương ứng.

---

## 4. Phân rã task (theo thứ tự build)

> **Quy ước chung mọi task:** query `internal sealed class XxxQueryHandler(IXxxReader reader) : IRequestHandler<XxxQuery, Result<XxxDto>>`; Validator **co-located** cùng thư mục query; DTO = `record` PascalCase; seam = bộ 3 file trong `Infrastructure/CrossModule/`; đăng ký DI trong `AddAnalyticsModule`; `AsNoTracking()` mọi nơi; **KHÔNG** migration, **KHÔNG** entity, **KHÔNG** event.

### SCRUM-335 — Operations Dashboard Overview (UC-ANA-01) — *bootstrap module*

**Mục tiêu:** khởi tạo hạ tầng module Analytics + 1 endpoint KPI tổng quan. **Không FR** → scope theo DEC-ANA-14.

- **Bootstrap (CHỈ 1 lần, task này — DEC-ANA-01):** package refs 2 csproj + ProjectReference Persistence; `ValidationBehavior` copy từ Hub; `AddAnalyticsModule(config)`; `Program.cs` sau dòng 195; `ForceLoadModuleAssemblies` += `"FreshFlow.Analytics.Infrastructure"`; tạo `tests/Unit/FreshFlow.Analytics.UnitTests` + **đăng ký `.slnx`**. **KHÔNG** đụng `.slnx` phần src + `API.csproj` (đã có).
- **Domain:** ❌ **KHÔNG CÓ GÌ** (DEC-ANA-02). Project rỗng, không tạo file.
- **Application:** `GetDashboardOverviewQuery(DateOnly? Date)` + Validator + Handler → `DashboardOverviewDto`. Mặc định `Date = hôm nay (VN)`. KPI **chỉ từ dữ liệu có sẵn**:
  - `ordersToday` (count `orders` theo `"CreatedAt"` trong ngày VN, `"deleted_at" IS NULL`), `revenueToday` (sum `"TotalAmount"` các đơn **không huỷ**), `pendingOrders` (`"Status"` ∈ Draft/Confirmed), `cancelledToday`.
  - `activeProcurementBatches` (count `procurement_batches` `batch_date = date` + `status` chưa hoàn tất).
  - `deliveriesToday` / `onTimeRatePercent` (từ `deliveries`, công thức DEC-ANA-08).
  - `hubInboundKgToday` / `hubOutboundKgToday` (sum `total_quantity_kg`).
  - **KHÔNG bịa thêm KPI.** Thiếu dữ liệu → bỏ, đừng chế.
- **Infrastructure:** `IDashboardOverviewReader` + seam Row cho từng nguồn (`OrderSummaryRow`, `ProcurementBatchRow`, `DeliveryRow`, `HubEventRow`) — **casing theo §1.3**, alias PascalCase. **Gom bằng LINQ `CountAsync`/`SumAsync` song song hoặc tuần tự — KHÔNG N+1, KHÔNG raw SQL** (DEC-ANA-04 rung 1).
- **API:** `AnalyticsController` MỚI (DEC-ANA-05), `GET /api/v1/analytics/overview?date=`, RBAC `admin,operations_manager`.
- **Test ≥80%:** bootstrap smoke (`AddAnalyticsModule` resolve được handler); overview happy path; **ngày rỗng → tất cả 0, KHÔNG NaN/500**; timezone (đơn 23:30 VN thuộc đúng ngày); soft-delete bị loại; RBAC (restaurant → 403); seam SQL chạy được (**integration test — `ToSqlQuery` KHÔNG chạy trên InMemory**); `has-pending-model-changes` = no drift.

### SCRUM-345 — Product Price Trend Summary (UC-ANA-06) — *FR-ANA-001*

**Mục tiêu:** time-series giá + summary stats. **Làm sớm (thứ 2)** vì đây là UC có FR **rõ nhất**, nguồn `price_snapshots` **độc lập** (không phụ thuộc task khác) → khoá sớm pattern seam + stats cho các task sau.

- **Application:** `GetPriceTrendsQuery(IReadOnlyList<Guid> MarketProductIds, DateOnly From, DateOnly To, string? Interval)` + Validator + Handler → `PriceTrendsDto(IReadOnlyList<PriceTrendSeriesDto> Series)`; `PriceTrendSeriesDto(Guid MarketProductId, string ProductName, string MarketName, string Interval, PriceTrendSummaryDto Summary, IReadOnlyList<PriceTrendPointDto> Points)`; `PriceTrendSummaryDto(decimal MinPrice, decimal MaxPrice, decimal AvgPrice, decimal? PriceVolatility)`.
- **Validator:** `MarketProductIds` **NotEmpty + tối đa 10** (FR-ANA-001 AC3 / docs/04); `From <= To` (copy rule `GetPriceChangeHistoryQueryValidator`); cap 24 tháng; `Interval` ∈ {`hourly`,`daily`} allow-list, default `daily`.
- **Logic:** khoảng **>12 tháng ⇒ ÉP `daily`** (AC4), bỏ qua `hourly` client gửi. Group theo `RecordedAt.Date` (daily) / `.Date + .Hour` (hourly), `avg(Price)` mỗi bucket. `PriceVolatility` = **`stddev_samp`** → **DEC-ANA-04 rung 2** (`ToSqlQuery` static). **<2 điểm ⇒ `null`** (KHÔNG 0).
- **Infrastructure:** seam `PriceSnapshotRow` (`price_snapshots` **PascalCase**, **KHÔNG** `deleted_at` — append-only) + `MarketProductDetailRow` (join `market_products`→`products`→`markets` lấy `ProductName`/`MarketName`; **`market_products` có `"deleted_at"`**). `IPriceTrendReader`.
- **API:** `GET /api/v1/analytics/price-trends?marketProductId=..&marketProductId=..&from=&to=&interval=`, RBAC **`admin,operations_manager,restaurant_manager,restaurant_staff`** (DEC-ANA-05 — endpoint ANA **duy nhất** mở cho Restaurant; **verify tên role thật trong seed**).
- **Test ≥80%:** series đúng thứ tự thời gian; summary min/max/avg đúng; **stddev đúng (so số tính tay)**; **1 điểm → volatility null**; **0 điểm → series rỗng, không 500**; 11 marketProductId → 400; `from > to` → 400; **>12 tháng ép daily** (assert `Interval="daily"` dù client gửi `hourly`); RBAC restaurant **200** (khác các endpoint kia); **SQL injection vào interval/id → 400**; seam integration test.

### SCRUM-337 — Order Metrics (UC-ANA-02 + UC-ANA-07)

**Mục tiêu:** metrics đơn hàng theo khoảng. **Không FR** → DEC-ANA-14.

- **Application:** `GetOrderMetricsQuery(DateOnly From, DateOnly To, Guid? RestaurantId, string? GroupBy)` + Validator + Handler → `OrderMetricsDto(OrderMetricsSummaryDto Summary, IReadOnlyList<OrderMetricsBucketDto> Buckets)`.
  - `Summary`: `totalOrders`, `totalRevenueVND` (sum `"TotalAmount"`, **loại đơn Cancelled**), `avgOrderValueVND` (**0 khi totalOrders=0**), `cancelledCount`, `cancellationRatePercent`, `deliveredCount`, `statusCounts` (Dictionary<string,int> — **mẫu `ProcurementProgressSummaryDto`**, tái dùng ý tưởng `Enum.GetValues` để status 0 vẫn xuất hiện).
  - `Buckets`: theo ngày (`GroupBy=day`, default) — `date`, `orderCount`, `revenueVND`.
- **Validator:** `From <= To`; cap 366 ngày; `GroupBy` ∈ {`day`,`week`,`month`} **allow-list** (DEC-ANA-04 — map sang biểu thức LINQ qua `switch`, **KHÔNG ghép vào SQL**).
- **Infrastructure:** seam `OrderMetricsRow` — ⚠️ **`orders` casing HỖN HỢP**: `SELECT "Id" AS "OrderId", "RestaurantId", "Status", "TotalAmount", "CreatedAt", "CancelledAt" FROM orders WHERE "deleted_at" IS NULL`. `IOrderMetricsReader`.
- **API:** `GET /api/v1/analytics/order-metrics?from=&to=&restaurantId=&groupBy=`, RBAC **`admin,operations_manager`** (doanh thu toàn hệ thống — **KHÔNG mở Restaurant**, §3).
- **Test ≥80%:** metrics đúng; **0 đơn → AOV=0 không chia 0**; đơn Cancelled không tính doanh thu nhưng có trong `cancelledCount`; soft-delete loại; timezone biên 23:30; `groupBy` lạ → 400; **injection vào groupBy → 400**; cap 366 ngày → 400; RBAC; seam integration.

### SCRUM-339 — Procurement Metrics (UC-ANA-03) — *KHÔNG đóng, xem DEC-ANA-09*

**Mục tiêu:** metrics thu mua **theo khoảng** (khác `/admin/order-groups/progress` = 1 chu kỳ live).

- **Application:** `GetProcurementMetricsQuery(DateOnly From, DateOnly To, Guid? MarketId)` + Validator + Handler → `ProcurementMetricsDto`:
  - `totalBatches`, `statusCounts`, `completionRatePercent`.
  - `itemsTotal` / `itemsPurchased` (`actual_quantity IS NOT NULL`) / `itemsPending`.
  - **`totalActualCostVND`** = `SUM(actual_quantity × actual_unit_price)` — **chiều "tiền" mà `/progress` KHÔNG có**.
  - **`priceVariancePercent`** = lệch `actual_unit_price` vs `reference_unit_price` — **giá trị khác biệt lớn nhất của task này**. **NULL-safe:** cả 2 cột nullable; chỉ tính trên dòng có **đủ cả hai** và `reference_unit_price > 0`; mẫu rỗng → `null`.
  - `exceptionCount` + `exceptionsByType` (group `procurement_exceptions.type`).
  - `avgLeadTimeMinutes` = `handed_off_at − manifested_at`, **chỉ trên batch có đủ 2 mốc** (cả 2 nullable) → mẫu rỗng = `null`.
- **Infrastructure:** seam `ProcurementBatchRow` / `ProcurementBatchItemRow` / `ProcurementExceptionRow` — **snake_case → alias PascalCase**, `WHERE deleted_at IS NULL` (`procurement_batches` có soft-delete). `IProcurementMetricsReader`. **Group 1 query, KHÔNG N+1 theo batch.**
- **API:** `GET /api/v1/analytics/procurement-metrics?from=&to=&marketId=`, RBAC `admin,operations_manager`.
- **KHÔNG được làm:** sửa/gọi lại `GetProcurementProgressQueryHandler`; project-ref sang Procurement; đụng module Procurement.
- **Test ≥80%:** metrics đúng; **batch chưa mua xong → itemsPending đúng**; `actual_unit_price` null → **không crash, loại khỏi variance**; `reference_unit_price = 0` → loại (không chia 0); lead time chỉ tính batch đủ 2 mốc; **0 batch → mọi số 0/null, không 500**; exceptionsByType; RBAC; seam integration.

### SCRUM-341 — Hub Throughput Metrics (UC-ANA-04)

**Mục tiêu:** thông lượng hub theo khoảng. **Không FR** → DEC-ANA-14. **Task đơn giản nhất của epic.**

- **Application:** `GetHubThroughputQuery(DateOnly From, DateOnly To, Guid? HubId)` + Validator + Handler → `HubThroughputDto(IReadOnlyList<HubThroughputRowDto> Hubs, HubThroughputSummaryDto Summary)`:
  - Per hub: `hubId`, `hubName`, `inboundEventCount`, `inboundKg` (sum `total_quantity_kg` theo `arrived_at`), `outboundEventCount`, `outboundKg` (theo `dispatched_at`), `netKg`, `currentOccupiedKg` + `capacityKg` + `utilizationPercent` (từ `hubs`).
  - `Buckets` theo ngày (in/out kg) — cho biểu đồ.
  - **`discrepancyCount`** (từ `hub_discrepancies`) nếu rẻ; nếu làm phình query → **bỏ** (không FR).
- **Infrastructure:** seam `HubInboundRow` / `HubOutboundRow` / `HubRow` — **toàn bộ snake_case**, `WHERE deleted_at IS NULL`. `IHubThroughputReader`. **⚠️ `hub_inbound_events` chỉ tính `status = 'ARRIVED_AT_HUB'`** (`PENDING` = mới record, **CHƯA thực nhận, CHƯA vào kho** — DEC-HUB-04/10). Đếm cả PENDING = **thổi phồng thông lượng**.
- **API:** `GET /api/v1/analytics/hub-throughput?from=&to=&hubId=`, RBAC `admin,operations_manager` (+ `hub_staff` nếu supervisor muốn — mặc định **KHÔNG**, tránh lộ số liệu hub khác).
- **Test ≥80%:** in/out kg đúng (**decimal kg, KHÔNG int** — DEC-HUB-04); **inbound PENDING KHÔNG được tính**; hub không hoạt động → 0 không 500; `capacity_kg=0` → utilization không chia 0; filter hubId; soft-delete loại; RBAC; seam integration.

### SCRUM-343 — Delivery Performance Metrics (UC-ANA-05) — *FR-ANA-003; 2/6 chỉ số degrade*

**Mục tiêu:** KPI giao hàng. **Đọc kỹ DEC-ANA-08 trước khi code — đây là task nhiều bẫy nhất.**

- **Application:** `GetDeliveryPerformanceQuery(DateOnly From, DateOnly To)` + Validator + Handler → `DeliveryPerformanceDto`:
  - ✅ `totalDeliveries`, `onTimeCount`, `lateCount`, `onTimeRatePercent`, `failedCount` — từ `deliveries`. **LATE ⟺ `actual_arrival > estimated_arrival + 15 phút`** (hằng số `LateThresholdMinutes = 15`, **KHÔNG magic number**). Chỉ tính `status='delivered'` + `actual_arrival IS NOT NULL`; `failed` đếm riêng, **KHÔNG vào on-time rate**.
  - ⚠️ `avgDeliveryDurationMinutes` + **`durationSampleCount`** — `actual_arrival − hub_handover_events.driver_confirmed_at` join `delivery_route_id`. **Route không có handover CHECKED_OUT ⇒ LOẠI khỏi mẫu (KHÔNG tính 0!)**. Mẫu rỗng → `null`.
  - ⚠️ `avgVehicleUtilizationPercent` + **`utilizationSampleCount`** — `SUM(hub_outbound_events.total_quantity_kg) per destination_route_id ÷ vehicles.capacity_kg × 100`. Loại route `vehicle_id IS NULL` / không outbound / `capacity_kg = 0`. **Clamp 100**. Mẫu rỗng → `null`.
  - **KHÔNG** làm `/by-route` (AC3) ở MVP trừ khi supervisor yêu cầu.
- **Infrastructure:** seam `DeliveryRow` (`deliveries` snake_case) / `DeliveryRouteVehicleRow` (join `delivery_routes`→`vehicles`) / `HandoverDepartureRow` (`hub_handover_events`, **chỉ `status='CHECKED_OUT'` + `driver_confirmed_at IS NOT NULL`**) / tái dùng `HubOutboundRow` của 341 nếu khớp. `IDeliveryPerformanceReader`. **JOIN sẵn trong `ToSqlQuery` hoặc 1 query gom — TUYỆT ĐỐI không loop per-route** (§3 N+1).
- **API:** `GET /api/v1/analytics/delivery-performance?from=&to=`, RBAC **`admin,operations_manager`** (FR-ANA-003 admin-only).
- **KHÔNG được làm:** thêm `started_at`/`completed_at` vào `delivery_routes` (mở scope Logistics — DEC-ANA-08, cần task LOG riêng); pre-aggregate/Redis cho "800ms" (DEC-ANA-10); coi route thiếu handover là duration 0.
- **Test ≥80%:** on-time đúng; **đúng biên 15 phút** (14:59 → on-time, 15:01 → late — **test cả 2 phía**); `failed` không vào rate; **0 delivery → rate 0 không chia 0**; **route không handover → loại khỏi duration, durationSampleCount đúng**; **route không vehicle → loại khỏi utilization**; `capacity_kg=0` → loại; utilization >100 → clamp 100; **KHÔNG N+1** (assert số query hoặc review); seam integration.

### SCRUM-347 — Demand Heatmap by Area (UC-ANA-08) — *FR-ANA-002*

**Mục tiêu:** heatmap nhu cầu theo nhà hàng + phân bố giờ/thứ.

- **Application:** 2 query:
  1. `GetDemandHeatmapQuery(DateOnly From, DateOnly To)` → `IReadOnlyList<DemandHeatmapPointDto(Guid RestaurantId, string RestaurantName, decimal Latitude, decimal Longitude, int TotalOrderCount, decimal TotalOrderValueVND, string? DominantProductCategory)>`. **Nhà hàng 0 đơn trong kỳ ⇒ LOẠI** (AC3).
  2. `GetDemandTimeDistributionQuery(DateOnly From, DateOnly To)` → **ma trận 7×24** `IReadOnlyList<TimeDistributionCellDto(int DayOfWeek, int HourOfDay, int OrderCount)>` (AC2).
- **⚠️ Timezone (bẫy chí tử):** `hour-of-day`/`day-of-week` **PHẢI theo giờ VN**, không phải UTC — nếu không biểu đồ **lệch 7 giờ mà vẫn trông hợp lý**. Convert trong SQL (`"CreatedAt" AT TIME ZONE 'Asia/Ho_Chi_Minh'`) **hoặc** LINQ sau khi shift. **Test bắt buộc.**
- **`DominantProductCategory`:** join `order_items`→`market_products`→`products`→`product_categories`, lấy category có **tổng `Quantity` lớn nhất** mỗi restaurant. **1 query gom + window function/GROUP BY — TUYỆT ĐỐI không query per-restaurant (N+1)**. Nếu quá phức tạp → **DEC-ANA-04 rung 2** (`ToSqlQuery` static có `DISTINCT ON`/`row_number()`, filter tham số áp ngoài). **Nếu vẫn cồng kềnh → trả `null` + báo supervisor**, đừng đánh đổi N+1 lấy 1 field phụ.
- **Infrastructure:** seam `RestaurantDemandRow` — join `orders` (**casing HỖN HỢP**) + `delivery_addresses`/`restaurants` (⚠️ **`restaurants.status` lowercase**, PascalCase phần còn lại; **mẫu THẬT có sẵn**: `Logistics.Infrastructure/CrossModule/RestaurantCoordinateRow` — `SELECT da."RestaurantId", r."Name" AS "Name", da."Latitude", da."Longitude" FROM delivery_addresses da JOIN restaurants r ON r."Id" = da."RestaurantId" WHERE da."IsDefault" = true AND da."DeletedAt" IS NULL AND r.status = 'active'` → **COPY nguyên, đừng viết lại từ đầu**). `IDemandHeatmapReader`.
- **API:** `GET /api/v1/analytics/demand-heatmap?from=&to=` + `GET /api/v1/analytics/demand-heatmap/time-distribution?from=&to=`, RBAC **`admin,operations_manager`** — **FR-ANA-002 AC4: Restaurant token → 403 (test bắt buộc)**; dữ liệu vị trí + doanh thu từng nhà hàng (§3).
- **Test ≥80%:** heatmap đúng; **nhà hàng 0 đơn bị loại (AC3)**; nhà hàng không có địa chỉ mặc định → loại (không crash); **7×24 = tối đa 168 cell, giờ theo VN (test đơn 23:30 VN → hour=23 KHÔNG phải 16)**; **Restaurant → 403 (AC4)**; **KHÔNG N+1** cho dominantCategory; soft-delete loại; seam integration.

### SCRUM-351 — Recent Activities (UC-ANA-10) — *Option B-lite, xem DEC-ANA-07; CẦN SUPERVISOR CHỐT*

**Mục tiêu:** feed hoạt động gần đây. **PONYTAIL: phần lớn ĐÃ CÓ.** `audit_logs` + `IAuditLogWriter` + `GetAuditLogsQuery` (filter + offset paging + index) **đã chạy**. Thiếu **duy nhất**: feed chỉ được nuôi bởi 4/12 loại sự kiện.

- **(a) Bù audit handler (phần net-new thật):** thêm vào `src/FreshFlow.Infrastructure.Persistence/Audit/` (**KHÔNG** vào module Analytics — DEC-ANA-13), **copy nguyên mẫu `OrderCancelledAuditLogHandler`** (~15 dòng/file), consume **event ĐÃ CÓ trong Contracts**: `OrderConfirmed`, `ProcurementBatchBuilt`, `ProcurementManifestGenerated`, `ProcurementAgentAssigned`, `ProcurementBatchHandedOff`, `DeliveryStarted`, `DeliveryCompleted`, `HubDiscrepancyRecorded`. Tái dùng `IAuditLogWriter` sẵn có. **KHÔNG event mới, KHÔNG bảng mới, KHÔNG migration.**
- **(b) Endpoint:** `GET /api/v1/analytics/recent-activities?page=&pageSize=&entityType=&action=` → **wrapper mỏng `sender.Send(new GetAuditLogsQuery(...))` đã có** — **KHÔNG viết query mới**. Default `PageSize=20`, sort `occurred_at DESC` (handler hiện tại đã vậy). RBAC `admin,operations_manager`.
- **Ghi rõ (không phải bug):** `AuditLogWriter` **cố tình nuốt exception** → feed **best-effort, có thể thiếu dòng**. Đây là contract có chủ ý của repo ("audit-write failure must not fail the originating business operation"). **KHÔNG "sửa".**
- **Option A (lười hơn, cần supervisor duyệt):** đóng 351, dùng thẳng `GET /admin/audit-logs`, **chấp nhận feed thiếu order-confirmed/procurement/delivery**. Leader **KHÔNG khuyến nghị** (feed ops thiếu 3 domain quan trọng nhất = vô dụng), nhưng là lựa chọn hợp lệ nếu muốn cắt scope.
- **Test ≥80%:** mỗi handler mới → assert `IAuditLogWriter.WriteAsync` được gọi đúng `action`/`entityType`/`entityId`/`occurredAt` (mock); endpoint paging/filter; **writer ném exception → handler KHÔNG ném ra ngoài** (giữ contract best-effort); RBAC.

### SCRUM-349 — Export Dashboard Data (UC-ANA-09) — *FR-ANA-004; làm CUỐI*

**Mục tiêu:** xuất CSV. **Làm cuối vì tái dùng query của 337/343/345.** Xem DEC-ANA-06 (đồng bộ, không job async).

- **Application:** `ExportAnalyticsQuery(string Dataset, DateOnly From, DateOnly To)` + Validator + Handler → `Result<CsvExportDto(string FileName, string ContentType, byte[] Content)>` (hoặc trả `string Csv`).
  - `Dataset` = **allow-list enum**: `price-history` | `order-history` | `delivery-performance` (đúng FR-ANA-004). Giá trị lạ → **400** (KHÔNG free-text vào SQL — DEC-ANA-04).
  - **TÁI DÙNG reader/query có sẵn** (345/337/343) → map sang hàng CSV. **KHÔNG viết lại query.**
  - **Cap 50 000 dòng** → vượt = **400 `EXPORT_RANGE_TOO_LARGE`** (thay async của AC3). ⚠️ **`EXPORT_RANGE_TOO_LARGE` KHÔNG kết thúc `_NOT_FOUND` ⇒ PHẢI đăng ký tường minh vào `ErrorExtensions.cs`** nhánh BadRequest, **nếu không rơi về 500** (bài học SCRUM-288 `SCAN_NO_MATCH`). **Hoặc** dùng thẳng `VALIDATION_ERROR` (đã map 400) — **lười hơn, ưu tiên cách này**.
  - **Sinh CSV thủ công** `StringBuilder`, ~30 dòng, **escape RFC4180**: field chứa `,`/`"`/`\n` → bọc `"`, `"` bên trong → `""`. Header row = tên field API (AC2). **KHÔNG thêm CsvHelper.**
- **API:** `GET /api/v1/analytics/export?dataset=&from=&to=&format=csv` → `200` + `Content-Type: text/csv` + `Content-Disposition: attachment; filename="..."` (AC1). `format` chỉ nhận `csv` (allow-list), khác → 400. RBAC `admin,operations_manager`.
- **KHÔNG được làm:** bảng `export_jobs`; background worker; upload Cloudinary; `POST` + 202 + jobId + poll + download (**DEC-ANA-06 override**); thêm package CSV.
- **Test ≥80%:** CSV header đúng tên field API (AC2); **escape đúng (field chứa dấu phẩy / dấu ngoặc kép / xuống dòng)** — **test riêng, đây là chỗ hay sai nhất**; `Content-Disposition` + `Content-Type` (AC1); `dataset` lạ → 400; **injection vào dataset → 400**; **>50k dòng → 400** (không OOM); kỳ rỗng → CSV chỉ có header (không 500); RBAC.

---

## 5. Thứ tự phụ thuộc & lý do

```
335 (BOOTSTRAP: module wiring + AnalyticsController + overview)
 │   ← BẮT BUỘC đầu tiên. Không có nó, mọi task khác không compile/không có controller.
 ├─> 345 (price-trends; nguồn price_snapshots ĐỘC LẬP)
 │      ← làm ngay sau bootstrap: FR rõ nhất + khoá pattern seam & summary-stats cho các task sau
 ├─> 337 (order-metrics; seam orders — casing hỗn hợp, khoá pattern cho 347)
 ├─> 339 (procurement-metrics; seam procurement)
 ├─> 341 (hub-throughput; seam hub_inbound/outbound)
 │      └─> 343 (delivery-performance; TÁI DÙNG HubOutboundRow của 341 + seam deliveries/handover/vehicles)
 ├─> 347 (demand-heatmap; TÁI DÙNG seam orders của 337 + restaurant coords)
 ├─> 351 (recent-activities; ĐỘC LẬP — chỉ đụng Persistence/Audit + 1 endpoint wrapper)
 └─> 349 (export; LÀM CUỐI — tái dùng query 345/337/343)
```

**Ghi chú thứ tự:** sau 335, các task **337/339/341/345/351 độc lập nhau** → có thể làm song song nếu supervisor muốn chạy nhiều codex. **Ràng buộc thật chỉ có 3:** `343` sau `341` (tái dùng `HubOutboundRow`), `347` sau `337` (tái dùng seam `orders`), `349` sau `345`+`337`+`343` (tái dùng cả 3 query). Thứ tự đề xuất ở §0.2 là **tuần tự an toàn** — ưu tiên khoá pattern sớm (345) rồi mới tới task nhiều bẫy nhất (343).

---

## 6. Nhật ký khảo sát & quyết định

- **2026-07-16** — Khảo sát xong (leader). Xác nhận module Analytics rỗng **nhưng `.slnx` + `API.csproj` ĐÃ nối sẵn** ⇒ checklist bootstrap **ngắn hơn** DEC-HUB-01 (bỏ 2 bước, thêm bước package refs vì 3 csproj Analytics **thiếu toàn bộ PackageReference**). Xác nhận **10 UC / 9 key, không thiếu key** (337 gộp UC-02+07). Chốt DEC-ANA-01…14.
  - **Phát hiện quan trọng nhất: repo có ZERO `FromSqlRaw`** — CLAUDE.md cho phép Analytics dùng raw SQL nhưng **không có tiền lệ**; mọi raw SQL hiện tại là `ToSqlQuery` **static không tham số**. ⇒ **DEC-ANA-04** chốt thang 3 rung (LINQ → ToSqlQuery static → FromSql FormattableString), cấm nội suy chuỗi. Đây là điểm reviewer soi kỹ nhất.
  - **Phát hiện lớn thứ 2: `delivery_routes` KHÔNG có thời điểm khởi hành/hoàn thành thật** (`Start()`/`Complete()` chỉ set Status + UpdatedAt) ⇒ `avgDeliveryDurationMinutes` (FR-ANA-003) **không tính được trực tiếp**. **DEC-ANA-08**: tái dùng `hub_handover_events.driver_confirmed_at` làm mốc khởi hành + `hub_outbound_events.total_quantity_kg` (có `destination_route_id`, đo kg thật) cho vehicle utilization ⇒ **0 migration, 0 sửa module khác**, đổi lại mẫu nhỏ hơn (trả kèm `sampleCount`). Phương án "đúng 100%" = thêm 2 cột ở LOG epic → **flag supervisor**.
  - **DEC-ANA-02:** Analytics **KHÔNG có bảng/entity/migration nào** — Domain project để rỗng. **DEC-ANA-06:** export CSV **đồng bộ** + cap 50k (override FR-ANA-004 AC3 async job — không có hạ tầng job/blob). **DEC-ANA-10:** không Redis/pre-aggregate (override FR-ANA-003 AC4) — đo trước, tối ưu sau.
  - **DEC-ANA-09:** 339 **KHÔNG đóng** — `/admin/order-groups/progress` là single-cycle live, 339 là period metrics + chiều tiền (`actual_unit_price` vs `reference_unit_price`), trùng thật chỉ ở đếm status/exception.
  - **DEC-ANA-07:** 351 **gần như đã cover** bởi `audit_logs` + `GET /admin/audit-logs`; đề xuất **Option B-lite** (bù 8 audit handler cho event ĐÃ CÓ trong Contracts + endpoint wrapper) thay vì đóng hẳn — **chờ supervisor chốt**.
  - **Casing:** ghi nhận `orders` **trộn PascalCase + snake_case trong cùng bảng** (`"Status"`/`"TotalAmount"` vs `deleted_at`/`confirmed_receipt_at`) và `restaurants.status` lowercase — bổ sung cho memory `project_ef_column_casing`.
  - **Docs lệch code:** `docs/04` dòng 764 vẫn ghi Logistics/Hub `[PLANNED]` (đã xong); `PartitionMaintenanceJob` trong CLAUDE.md **không tồn tại**.
  - Sẵn sàng breakdown codex bắt đầu **SCRUM-335**. Chờ supervisor chốt 4 điểm ở §7.

---

## 7. Điểm cần supervisor chốt TRƯỚC KHI CODE

1. **🔴 SCRUM-335 — tập KPI của Overview** (điểm mơ hồ nhất epic). Không FR, không wireframe. Leader đề xuất 9 KPI ở §4 dựa trên dữ liệu có sẵn. **Nếu FE đã có design → gửi trước khi code 335**, tránh làm lại. Nếu không có → duyệt danh sách đề xuất.
2. **🔴 SCRUM-351 — Option B-lite hay Option A?** B-lite = bù 8 audit handler (~120 dòng, thuần copy mẫu) + endpoint wrapper → feed đầy đủ. A = đóng luôn, xài `/admin/audit-logs`, feed thiếu order-confirmed/procurement/delivery. **Leader khuyến nghị B-lite.**
3. **🟠 SCRUM-343 — chấp nhận `avgDeliveryDurationMinutes` mẫu nhỏ (chỉ route có hub handover) không?** Nếu cần chính xác 100% mọi route → phải mở **task LOG riêng** thêm `started_at`/`completed_at` vào `delivery_routes` (2 cột + 2 dòng + 1 migration) — **cross-epic, ngoài scope ANA**. Leader đề xuất **chấp nhận mẫu nhỏ** + trả `durationSampleCount`.
4. **🟡 SCRUM-349 — xác nhận export đồng bộ + cap 50k** (override FR-ANA-004 AC3 async job). Nếu bắt buộc async thật → phải mở scope hạ tầng (bảng job + worker + storage) = **1 epic riêng**, không nhét vào 349.
5. **🟡 SCRUM-343 — có cần `/delivery-performance/by-route` (FR-ANA-003 AC3) ở MVP không?** Leader đề xuất **bỏ**, thêm sau nếu FE cần (cùng query + `GROUP BY route`).
