# AUDIT / PLAN — Route Suggestions (gợi ý chợ + nhà hàng theo ngày)

**Ngày:** 2026-07-28
**Epic:** SCRUM-256 (Hub) — liên quan Logistics route creation
**Trạng thái:** PLAN — chờ chốt build owner
**Diagram:** `docs/features/hub/order-route-flow.drawio`

---

## Vấn đề

Khi admin tạo route (`POST /api/v1/logistics/routes/calculate`) phải **gõ tay** danh sách
`SourceMarketIds` + `DestinationRestaurantIds`. Không có gì suy ngược từ "ngày X có đơn nào" ra
tập chợ/nhà hàng cần đưa vào route.

Hệ quả (ô đỏ trong diagram): admin quên một nhà hàng có đơn `AtHub` → đơn đó **không lên
loading-manifest, không được xếp xe, không cảnh báo**.

## Quyết định

- Hướng: **endpoint gợi ý, admin xác nhận** (không tự tạo route — giữ admin trong vòng kiểm soát).
- Phạm vi: **cả nhà hàng + chợ**.
- `ScheduledFor` (DateTime? trên `Order`, có index `idx_orders_scheduled_for`) = ngày giao dự kiến →
  lọc theo ngày khả thi.
- Trạng thái routable: `AtHub` (hàng đã ở hub, chắc chắn lên xe). Tùy chọn kèm `Batched` (đang về hub)
  để admin thấy trước — **mặc định chỉ `AtHub`** cho khớp đúng với loading-manifest.

## Endpoint

```
GET /api/v1/logistics/routes/suggestions?service_date=YYYY-MM-DD
[Authorize(Roles = "admin,operations_manager")]

200 →
{
  "serviceDate": "2026-07-29",
  "markets":     [ { "id": "...", "name": "Chợ Đầu Mối", "orderCount": 12 } ],
  "restaurants": [ { "id": "...", "name": "Nhà hàng A",  "orderCount": 3  } ]
}
```

`orderCount` = số đơn routable của ngày đó gắn với chợ/nhà hàng (giúp admin ưu tiên).

## Nguồn dữ liệu & seam (cross-module read)

Tất cả là **keyless Row + ToSqlQuery** trong `Logistics.Infrastructure/CrossModule/` (repo có ZERO
`FromSqlRaw` — giữ nguyên). Filter `service_date`/status áp ở LINQ, KHÔNG nội suy vào SQL.

### 1. Nhà hàng — mở rộng seam `OrderStatusRow` sẵn có
`OrderStatusRow` hiện chỉ có `OrderId, Status, RestaurantId`. **Thêm cột `ScheduledFor`**:

```sql
-- OrderStatusRowConfiguration (mở rộng)
SELECT "Id" AS "OrderId", "Status" AS "Status", "RestaurantId" AS "RestaurantId",
       "ScheduledFor" AS "ScheduledFor"
FROM orders WHERE "deleted_at" IS NULL
```
- Reader mới: `ListRoutableByServiceDateAsync(DateOnly date, IReadOnlyCollection<string> statuses)`
  → group theo `RestaurantId`, đếm, join tên qua `RestaurantCoordinateRow` (đã có `Name`).
- ⚠️ Row này đã xuất hiện trong `AppDbContextModelSnapshot` — thêm cột phải regenerate snapshot;
  không sinh migration mới (keyless, không map bảng thật).

### 2. Chợ — seam mới `OrderMarketRow` (join order_items → market_products → markets)
Market KHÔNG nằm thẳng trên order. Chuỗi:
`order_items."MarketProductId"` → `market_products."Id"` → `market_products."MarketId"` → `markets`.

```sql
-- OrderMarketRowConfiguration (static ToSqlQuery, không tham số)
SELECT o."Id"           AS "OrderId",
       o."Status"       AS "Status",
       o."ScheduledFor" AS "ScheduledFor",
       mp."MarketId"    AS "MarketId",
       m."Name"         AS "MarketName"
FROM order_items oi
JOIN orders o           ON o."Id" = oi."OrderId" AND o."deleted_at" IS NULL
JOIN market_products mp ON mp."Id" = oi."MarketProductId" AND mp."deleted_at" IS NULL
JOIN markets m          ON m."Id" = mp."MarketId" AND m."DeletedAt" IS NULL
```
> ⚠️ `order_items` KHÔNG có `deleted_at` (OrderItemConfiguration.Ignore) — không filter cột này ở oi.
> Casing: orders/`ScheduledFor`/`RestaurantId`, market_products `"Id"`/`"MarketId"`+`deleted_at`,
> markets `"Id"`/`"Name"`/`"DeletedAt"` — đã verify với *Configuration.cs.

- Reader: `ListMarketsByServiceDateAsync(date, statuses)` → distinct `MarketId`, đếm distinct `OrderId`.

## CQRS

```
Queries/GetRouteSuggestions/
  GetRouteSuggestionsQuery.cs           (DateOnly ServiceDate, bool IncludeBatched=false)
  GetRouteSuggestionsQueryValidator.cs  (ServiceDate NotEmpty)
  GetRouteSuggestionsQueryHandler.cs    (gộp 2 reader → RouteSuggestionsDto)
Dtos/RouteSuggestionsDto.cs             (record: ServiceDate, Markets[], Restaurants[])
Abstractions/  → thêm method vào IOrderStatusReader + IOrderMarketReader (mới)
```
Controller: thêm 1 action `GetSuggestionsAsync` vào `RoutesController`, route qua `ISender`.

## Test
- Integration (Postgres, Testcontainers) — BẮT BUỘC: `ToSqlQuery` không chạy trên EF InMemory nên
  seam chỉ chứng minh được trên Postgres thật. Seed: 1 chợ, 2 nhà hàng, đơn `AtHub`/`Batched`/`Draft`
  với `ScheduledFor` khác ngày → assert chỉ ngày+status đúng lọt, orderCount đúng, đơn `Draft`/khác
  ngày bị loại.
- Unit: validator (thiếu service_date → fail).

## Ước lượng
~8 file mới + 2 sửa (OrderStatusRow/Config/Reader/interface, RoutesController, snapshot). Nhỏ–vừa.
Rủi ro chính: snapshot drift khi mở rộng OrderStatusRow — chạy `dotnet ef migrations add` dry hoặc
kiểm `AppDbContextModelSnapshot` sau khi build.

## Ngoài phạm vi (YAGNI)
- Không tự tạo route. Không HUB_RELAY (vẫn `HUB_RELAY_NOT_SUPPORTED`).
- Không validate lúc `calculate` (đã có gợi ý rồi thì cảnh báo là thừa) — làm sau nếu vẫn sót đơn.
