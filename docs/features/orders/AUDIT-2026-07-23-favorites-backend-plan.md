# AUDIT / PLAN — Restaurant Favorites (server-backed wishlist)

- **Jira**: SCRUM-368 (task) dưới epic **SCRUM-179 – ORD Order Management**
- **Ngày**: 2026-07-23
- **Module**: Orders
- **Trạng thái**: ✅ Implemented (coder), chờ reviewer

### Implementation notes (2026-07-23)

- Casing/error-code/reader-reuse decisions in this doc matched the code as-verified — no
  redesign needed.
- File layout: `FavoriteRepository.cs` landed under `Infrastructure/Repositories/` (not
  `Infrastructure/Persistence/`) to match every other repository in the module.
- `FavoriteItemDto.cs` landed in the flat `Application/Dtos/` folder (not nested under
  `Queries/GetFavorites/`) — that's where every other query DTO in this module already lives.
- Idempotent add reuses `CreditRepository.IsUniqueViolation(ex)` rather than re-declaring the
  Postgres unique-violation check.
- Migration `AddRestaurantFavorites` (20260723145614) — table + `FavoriteListItemRow` seam both
  confirmed in `AppDbContextModelSnapshot`.
- Tests: 3 unit test classes (Add/Remove/Get handlers, 9 cases) + 1 integration class
  (`RestaurantFavoritesEndpointTests`, 5 cases covering all 4 mandatory scenarios). Full solution
  suite green (unit + integration), `dotnet format` clean.

---

## 1. Vì sao cần

Đối chiếu `freshflow-app` (mobile) với backend hiện tại: **mọi endpoint app gọi đều đã có sẵn ở BE**. Gap thật sự **duy nhất** là **favorites**:

- App có `FavoritesScreen.tsx` + `favoritesStore.ts`, nhưng store chỉ là **React Context local-only** — không persist, không gọi API. Danh sách yêu thích **mất khi tắt app / đổi thiết bị**.
- Backend: `grep -ri "favorite|favourite|wishlist"` trên toàn `src/` + migrations → **0 kết quả**. Không entity, không bảng, không endpoint.

## 2. Quyết định kiến trúc: đặt trong **Orders module**

Không phải Pricing/Auth. Lý do: Orders **đã có sẵn 2 mảnh khó nhất**, chỉ cần tái sử dụng:

| Mảnh cần | Có sẵn ở Orders |
|---|---|
| Resolve JWT userId → restaurantId (+ check active) | `CrossModule/RestaurantReader.FindByUserIdAsync` (`IRestaurantReader`) |
| Validate marketProductId tồn tại (chống favorite rác) | `CrossModule/MarketProductReader.FindAsync` (`IMarketProductReader`) |

JWT chỉ chứa `sub/email/role` (xác nhận trong `JwtTokenService`) → **bắt buộc** resolve userId→restaurant, không có claim restaurantId.

FE cũng đặt `FavoritesScreen.tsx` dưới `features/orders/` → domain-consistent.

## 3. Data model — 1 bảng owned mới

`restaurant_favorites` (Orders.Domain owns, giống cách Auth owns `delivery_addresses`):

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `id` | UUID PK | `gen_random_uuid()` |
| `restaurant_id` | UUID | FK → restaurants, index |
| `market_product_id` | UUID | không FK cứng (bảng thuộc Pricing — cross-module) |
| `created_at` | timestamptz | UTC |

- **Unique index** `(restaurant_id, market_product_id)` → POST idempotent, chống trùng.
- **Hard delete** — KHÔNG soft-delete. Favorite là toggle on/off; xoá thật sạch hơn soft-delete (khỏi phải "hồi sinh" row khi favorite lại). Đây là ngoại lệ có chủ đích so với convention "mọi bảng mutable có deleted_at" — ghi comment rõ trong Configuration cho reviewer.
- Casing: `restaurant_favorites` là bảng mới → dùng **snake_case** nhất quán (`restaurant_id`, `market_product_id`, `created_at`), tránh kiểu mixed của `orders`.

## 4. Endpoints — `[Authorize(Roles = "restaurant")]`

Path bám theo `me/*` pattern của `RestaurantProfileController`:

```
GET    /api/v1/restaurants/me/favorites                       → list (enriched)
POST   /api/v1/restaurants/me/favorites   { marketProductId } → add (idempotent)
DELETE /api/v1/restaurants/me/favorites/{marketProductId}     → remove (idempotent)
```

- DELETE key theo **`marketProductId`** (không phải favorite row id) — khớp cách FE nghĩ: `toggleFavorite(product)`.
- Controller **riêng** `RestaurantFavoritesController` (không nhồi vào `RestaurantProfileController` để tránh trộn import DTO Auth + Orders trong 1 file).
- Route qua `ISender` (để `ValidationBehavior` chạy).

## 5. GET trả về gì (enriched — FE render card không cần gọi thêm)

FE card cần: `name, imageUrl, marketName, category, unit, currentPrice, availableQuantity`. DTO:

```
FavoriteItemDto(
    Guid   MarketProductId,
    Guid   ProductId,
    string ProductName,
    string? ImageUrl,
    Guid   MarketId,
    string MarketName,
    string? Category,
    string Unit,
    decimal CurrentPrice,
    int    AvailableQuantity,   // CurrentQuantity - ReservedQuantity
    DateTime CreatedAt)
```

**Read seam mới** `FavoriteListItemRow` (`ToSqlQuery`, HasNoKey) join:
```
restaurant_favorites rf
  JOIN market_products mp ON rf.market_product_id = mp."Id"
  JOIN products p         ON mp."ProductId" = p."Id"
  LEFT JOIN units_of_measurement u ON p."UnitId" = u."Id"
  LEFT JOIN product_categories  c ON p."CategoryId" = c."Id"
  JOIN markets m           ON mp."MarketId" = m."Id"
WHERE mp."deleted_at" IS NULL AND p."DeletedAt" IS NULL
```
- ⚠️ **Casing không đồng nhất** — verify từng identifier với `*Configuration.cs` của Pricing/Catalog trước khi viết SQL (đừng đoán). Copy đúng casing từ `MarketProductRowConfiguration` (Orders) + `ProductDetailRowConfiguration` (Pricing) đã có.
- Filter theo `restaurant_id` **ngoài** SQL bằng LINQ `.Where(rf => rf.RestaurantId == ...)` (giữ ToSqlQuery static, không tham số — đúng SQL policy ladder rung 2).
- **Prefer-extend rule**: CLAUDE.md ưu tiên extend seam sẵn có. Ở đây `MarketProductRow` (Orders) là projection gọn cho order-validation hot-path; nhồi thêm 5 cột + join markets/units/categories là scope creep, và query favorites phải JOIN chính bảng `restaurant_favorites` (shape khác hẳn). → Seam riêng hợp lý; note lý do trong code.

## 6. Handler logic (reuse error path của CreateOrderCommandHandler)

- **Add**: resolve restaurant từ userId → 404 `RESTAURANT_NOT_FOUND` nếu null. Validate marketProductId qua `IMarketProductReader.FindAsync` → 404 nếu null (deleted/không tồn tại). Insert; unique-violation → coi như success (idempotent). Reuse đúng error code mà `CreateOrderCommandHandler` đang dùng.
- **Remove**: xoá theo (restaurantId, marketProductId); không tồn tại → `NoContent` (idempotent).
- **Get**: resolve restaurant → list qua `IFavoriteReader`.

## 7. Files (layout module chuẩn)

**Domain** (`Orders.Domain`)
- `Entities/RestaurantFavorite.cs`

**Application** (`Orders.Application`)
- `Abstractions/IFavoriteRepository.cs`, `Abstractions/IFavoriteReader.cs`
- `Commands/Favorites/Add/{AddFavoriteCommand,Handler,Validator,Response}.cs`
- `Commands/Favorites/Remove/{RemoveFavoriteCommand,Handler}.cs`
- `Queries/GetFavorites/{GetFavoritesQuery,Handler}.cs` + `Dtos/FavoriteItemDto.cs`

**Infrastructure** (`Orders.Infrastructure`)
- `Persistence/Configurations/RestaurantFavoriteConfiguration.cs` (ToTable + unique index + FK restaurant_id)
- `Persistence/FavoriteRepository.cs`
- `CrossModule/FavoriteListItemRow.cs` + `FavoriteListItemRowConfiguration.cs` + `FavoriteReader.cs`
- Wire vào `DependencyInjection.cs`

**Persistence**
- `dotnet ef migrations add AddRestaurantFavorites` — verify `DesignTimeDbContextFactory.ForceLoadModuleAssemblies` đã list Orders.Infrastructure (đã có).

**API host**
- `Controllers/RestaurantFavoritesController.cs`

## 8. Tests

- **Unit** (`FreshFlow.Orders.UnitTests`): 3 handler — restaurant-not-found, product-not-found, idempotent add, list mapping đúng.
- **Integration** (`FreshFlow.IntegrationTests`, **BẮT BUỘC** — `ToSqlQuery` seam chỉ proven trên Postgres thật):
  1. favorite → GET thấy enriched item đúng giá/tồn kho/tên/đơn vị/chợ
  2. un-favorite → biến mất khỏi GET
  3. favorite 2 lần cùng sản phẩm → 1 row (unique)
  4. favorite marketProductId không tồn tại → 404

## 9. Ngoài scope (follow-up, repo `freshflow-app`)

FE `favoritesStore.ts` hiện là Context local-only. Cần wiring riêng: đổi `toggleFavorite/isFavorite` sang gọi 3 endpoint + rehydrate khi mở app. **Repo khác — không thuộc SCRUM-368.**

## 10. Ước lượng

~1 task vừa. Mô hình ghép sẵn từ delivery-addresses (owned table + me/* endpoints) + order-validation seams (restaurant + market_product readers).
