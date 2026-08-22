# PLAN — Bỏ số lượng sản phẩm trong chợ → cờ `IsAvailable`

**Ngày:** 2026-08-07
**Module chính:** Pricing (lan sang Orders, Analytics, Persistence)
**Trạng thái:** PLAN — chưa code
**Quyết định business:** thay `int` tồn kho bằng `bool IsAvailable` (còn bán / tạm hết), agent bật tắt tay.

---

## 1. Vì sao bỏ

Truy vết vòng đời số lượng cho thấy nó **gần như là con số ma**, không phản ánh thực tế:

- `ReservedQuantity` **không bao giờ được ghi** — không có `Reserve()`/`Release()`, luôn = 0. Cả cơ chế giữ chỗ là code chết. Comment TODO trong `LivePriceEntry` và `RedisPriceBoardCache` tự xác nhận reservation "chưa từng build".
- `CurrentQuantity` chỉ set ở constructor (`InitialQuantity`) và `UpdateAvailableQuantity` (agent gõ tay). **Đặt hàng KHÔNG trừ số lượng** — Pricing không có handler nào cho `OrderCreated/Confirmed`. Bán 100 đơn số vẫn nguyên tới khi agent tự sửa.
- Nó chỉ có 2 tác dụng thật: (a) chặn đơn khi `requestedQuantity > AvailableQuantity` ở 4 handler Orders — tức **một con số gõ tay đang chặn đơn hợp lệ**; (b) filter `InStockOnly` + badge `IsOutOfStock`.

Khớp business model: procurement batching — agent ra chợ mua **sau khi** gom đơn (credit/công nợ). Tồn kho định sẵn về bản chất sai. `IsAvailable` giữ đúng thứ có nghĩa (còn bán hay không) và lần đầu tiên phản ánh đúng thực tế.

---

## 2. Thiết kế

### Domain `MarketProduct`
- **Xoá:** `CurrentQuantity`, `ReservedQuantity`, `AvailableQuantity`, `IsOutOfStock`, `ValidateQuantity`, param `initialQuantity` ở constructor, nhánh quantity trong `ApplyUpdate`.
- **Thêm:** `bool IsAvailable` (mặc định `true`) + `SetAvailability(bool value, Guid? actor)` — copy pattern `SetFeatured` (no-op nếu không đổi, không bump concurrency vô ích).
- `ApplyUpdate` → thu về price-only (đổi tên `UpdatePrice` là đủ; giữ tên cũ nếu ngại lan).

### Pricing.Application
- Folder command `UpdateAvailableQuantity/` (UC-PRI-04) → đổi thành `SetAvailability/` (command + validator + handler + result DTO). Bool nên nhẹ hơn hẳn.
- `CreateMarketProduct`: bỏ `InitialQuantity` khỏi command/validator/handler.
- `PriceUpdatedDomainEvent`: đang mang `CurrentQuantity` → thay bằng `IsAvailable`, hoặc bỏ hẳn nếu handler broadcast/Redis không cần (xác nhận khi làm).
- DTO đụng: `LivePriceEntry`, `MarketProductItemDto`, `MarketProductSearchItemDto`, `PriceUpdateBroadcastDto`, `UpdateAvailableQuantityResultDto`.

### Pricing.Infrastructure
- `RedisPriceBoardCache`: bỏ field `quantity` khỏi hash. **Phải flush Redis khi deploy** (schema cache đổi).
- `DbPriceBoardReader`: bỏ tính `AvailableQuantity`.
- `MarketProductRepository` / `MarketProductReader`: filter `InStockOnly` `(CurrentQuantity - ReservedQuantity > 0)` → `IsAvailable == true`; bỏ projection quantity.
- `PriceSnapshot`: bỏ `Quantity` (int, required) → snapshot thành **price-only**. Sửa `PriceSnapshot.For` + `PriceSnapshotConfiguration`.

### Orders — ĐỔI NGỮ NGHĨA, chú ý
4 handler chặn `requestedQuantity > AvailableQuantity` → chặn khi **`IsAvailable == false`**:
- `AddOrderItem`, `UpdateOrderItem`, `CreateDraftOrder`, `ReorderFromHistory`.
- `IMarketProductReader` + seam `FavoriteReader/Row/Configuration`: SQL `(mp."CurrentQuantity" - mp."ReservedQuantity") AS "AvailableQuantity"` → `mp."IsAvailable"`.
- `FavoriteItemDto.AvailableQuantity` (int) → `IsAvailable` (bool). **BREAKING API — báo FE.**

### Analytics
- `MarketProductDetailRow` / config: bỏ cột quantity nếu có tham chiếu.

### DB migration
- Drop `market_products."CurrentQuantity"`, `"ReservedQuantity"`.
- Add `market_products."IsAvailable" boolean NOT NULL DEFAULT true`.
- Drop `price_snapshots."Quantity"`.
- ⚠️ Casing: `market_products` **mixed** — cột mới theo **PascalCase** (`"IsAvailable"`) như các cột nghiệp vụ khác cùng bảng.
- ⚠️ Sau khi đổi seam: verify `ForceLoadModuleAssemblies()` liệt kê đủ + cập nhật `AppDbContextModelSnapshot`.

---

## 3. Đã verify (2026-08-07)

- `ReservedQuantity` không có nơi ghi → `AvailableQuantity == CurrentQuantity` luôn.
- Không có Order integration event handler nào trong Pricing → đặt hàng không trừ số lượng.
- `price_snapshots.Quantity`: int required, append-only, tạo qua `PriceSnapshot.For`.
- Redis hash có field `quantity`; comment TODO xác nhận reservation chưa build.
- `InStockOnly` là filter user-facing thật → map sang `IsAvailable == true`.

---

## 4. Rủi ro

1. **Breaking API**: `FavoriteItemDto` + các DTO đổi `int → bool`. Đồng bộ FE trước khi merge.
2. **Flush Redis** price-board khi deploy (schema cache đổi).
3. **Lịch sử `price_snapshots.Quantity`**: drop = mất số liệu cũ. Nếu cần giữ audit, thay bằng "archive rồi drop" — cân nhắc lúc làm.

---

## 5. Thứ tự làm (khi được duyệt)

1. Domain `MarketProduct` + unit test (đổi invariant).
2. `PriceSnapshot` price-only.
3. Pricing.Application: command `SetAvailability`, bỏ `InitialQuantity`, sửa event + DTO.
4. Pricing.Infrastructure: cache, readers, repository, snapshot config.
5. Orders: 4 handler + seam Favorite + DTO.
6. Analytics row.
7. Migration (drop 2 + add 1 + drop snapshot qty) → `AppDbContextModelSnapshot`.
8. Integration test seam Favorite/InStock trên Postgres thật (seam chỉ chứng minh được bằng integration test).
9. Format gate + báo FE breaking DTO.
