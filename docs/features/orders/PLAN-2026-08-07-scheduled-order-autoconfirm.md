# PLAN — Lịch định kỳ chọn sản phẩm + tự động đặt đơn (bỏ draft rỗng)

- **Ngày:** 2026-08-07
- **Epic:** ORD (Orders)
- **Trạng thái:** Implemented 2026-08-07 (build + 611 unit + 14 Postgres integration tests GREEN,
  `dotnet format` clean, migration `AddScheduledOrderItemsAndDeliveryAddress` created — NOT applied
  to prod). Chưa commit — chờ Jira key. Chi tiết: xem báo cáo coder gửi leader cùng ngày.

---

## 1. Vấn đề hiện tại

Luồng lịch định kỳ (`ScheduledOrder`) hôm nay:

1. Nhà hàng tạo lịch: chỉ có `recurrenceType` + `firstRunAt` + `notes`. **Không có sản phẩm.**
   (`CreateScheduledOrderCommand` → `ScheduledOrder` không mang line item nào.)
2. Job nền `ScheduledOrderGenerationHostedService` (60s/lần) → tới hạn thì
   `ScheduledOrderGenerationService.GenerateDueAsync` tạo một `Order` **rỗng ở trạng thái Draft**
   (`new Order(...)` không add item).
3. Nhà hàng phải tự mở draft, thêm sản phẩm, rồi bấm confirm.

→ Đúng như user mô tả: "tới giờ nó tạo bản draft không có sản phẩm, phải tự thêm vào".

**Mong muốn:** chọn sản phẩm (kèm số lượng) **ngay khi tạo lịch** → tới ngày job **tự đặt đơn thật
(confirmed)**, không còn bản draft phải sửa tay.

---

## 2. Điều kiện để "đặt đơn thật" tự động

Confirm một đơn hiện nay (`ConfirmOrderCommandHandler`) cần đủ:

| Yêu cầu | Nguồn khi confirm thủ công | Nguồn khi tự động |
|---|---|---|
| Danh sách item + số lượng | Draft đã có item | **Template item trên lịch (mới)** |
| Delivery address | `body.DeliveryAddressId` từ request | **`DeliveryAddressId` lưu trên lịch (mới)** |
| Giá sản phẩm | snapshot live lúc confirm (`marketProductReader`) | y hệt — snapshot lúc job chạy |
| Credit đủ hạn mức | `creditService.CanChargeAsync` | y hệt |
| Còn hàng | `TryReserveStockAsync` | y hệt |
| Cutoff / delivery window | `OrderConfirmationEvaluator` | y hệt |

→ Chỉ thiếu **2 dữ liệu phải lưu sẵn trên lịch: item template + delivery address**. Phần còn lại
tái dùng nguyên pipeline confirm.

---

## 3. Thay đổi đề xuất

### 3.1 Domain — `ScheduledOrder` mang item template + địa chỉ

- Thêm entity con `ScheduledOrderItem` (`MarketProductId`, `Quantity`) — chỉ lưu id + qty, **không
  lưu giá** (giá luôn snapshot lúc chạy, giống flow tay).
- `ScheduledOrder` thành aggregate root gồm `_items` + `DeliveryAddressId` (bắt buộc).
- Ctor nhận `deliveryAddressId` + danh sách item; validate ≥ 1 item.

### 3.2 Persistence — bảng mới + cột mới + migration

- Bảng `scheduled_order_items` (`id`, `scheduled_order_id`, `market_product_id`, `quantity`,
  `deleted_at`, timestamps). Casing: theo cụm `orders`/`order_items` — xác nhận lại
  `OrderItemConfiguration` trước khi đặt tên cột (⚠️ casing không nhất quán, không đoán).
- Cột `DeliveryAddressId` trên `scheduled_orders`.
- Migration mới (`--project FreshFlow.Infrastructure.Persistence`). ⚠️ **`localhost:5433` = PROD** —
  không `database update` vào prod, chỉ tạo migration; apply do người vận hành quyết định.

### 3.3 Application — tách pipeline confirm để tái dùng

`ConfirmOrderCommandHandler.ConfirmAsync` hiện đang trộn (a) resolve restaurant theo JWT userId với
(b) toàn bộ logic pricing/credit/stock/charge. Tách (b) ra service dùng chung:

- `OrderConfirmationService.ConfirmAsync(Order order, Guid restaurantId, Guid deliveryAddressId, DateTime nowUtc, ct)`
  → chứa toàn bộ đoạn pricing → credit check → evaluator → stock reserve → capture address →
  `order.Confirm()` → charge (dòng 61–153 hiện tại).
- `ConfirmOrderCommandHandler` chỉ còn: resolve+authorize restaurant theo userId rồi gọi service.
- Job tự động: đã biết `restaurantId` trên lịch (không có JWT) → gọi thẳng service.

Giữ nguyên `ExecuteInSerializableTransactionAsync` bao ngoài (job bao mỗi occurrence trong 1 tx).

### 3.4 Generation service — tạo đơn có item rồi tự confirm

`ScheduledOrderGenerationService.GenerateDueAsync`, mỗi occurrence:

1. `new Order(restaurantId, scheduledFor: occurrence, notes, scheduledOrderId)`.
2. Add item từ template (snapshot giá + tên qua `marketProductReader`, giống `CreateDraftOrder`).
3. Gọi `OrderConfirmationService.ConfirmAsync(...)`.
4. **Thành công** → đơn Confirmed, `RecordExecution(occurrence)`.
5. **Thất bại** (hết credit / hết hàng / sản phẩm bị gỡ / địa chỉ không còn) →
   **để đơn ở Draft** (đã tạo ở bước 1) + `RecordExecution` (giữ idempotency) + phát
   notification cho nhà hàng "đơn định kỳ ngày X cần xử lý tay: <lý do>".
   → Draft giờ chỉ là *lối thoát khi lỗi*, không còn là mặc định.

> Ponytail: bước 5 gần như không thêm code — đơn Draft vốn đã được tạo; lỗi thì chỉ là *không*
> gọi confirm. Không dựng cơ chế retry riêng; occurrence sau vẫn chạy như thường.

Service cần thêm dep: `IMarketProductReader`, `OrderConfirmationService` (và các dep con của nó).
Notification: phát integration event sẵn có hoặc tạo mới — xác nhận Contracts hiện có event phù hợp
trước khi thêm.

### 3.5 API / DTO

- `CreateScheduledOrderRequest` / `CreateScheduledOrderCommand` + validator: thêm `Items[]`
  (marketProductId, quantity ≥ 1, ≥ 1 item) + `DeliveryAddressId` (bắt buộc).
- `UpdateScheduledOrderRequest` / command: cho sửa items + address (thay cả template).
- `ScheduledOrderDto` + mapper + `GetScheduledOrder` / `ListScheduledOrders`: trả kèm items + address.
- Validate địa chỉ thuộc nhà hàng ngay lúc tạo lịch (fail sớm, tránh lỗi lặp mỗi lần job chạy).

---

## 4. Tests

- Unit `ScheduledOrderGenerationService`: (a) tạo + auto-confirm thành công; (b) hết credit → Draft +
  notify; (c) hết hàng → Draft; (d) idempotency (chạy 2 lần không tạo trùng); (e) bù nhiều occurrence.
- Unit validator: thiếu item / thiếu address / quantity ≤ 0.
- Unit `OrderConfirmationService` sau khi tách: hành vi confirm không đổi (regression).
- Integration (Postgres, Testcontainers): tạo lịch có item → chạy job → đơn Confirmed + credit charged
  + stock giảm. Seam `ToSqlQuery` chỉ chứng minh được trên Postgres thật.
- **Cập nhật test cũ** đang assert "job tạo Draft rỗng" → nay là Confirmed (hoặc Draft-khi-lỗi).

---

## 5. Rủi ro / điểm cần quyết

1. **Địa chỉ bị xoá sau khi lập lịch** → confirm fail → degrade Draft. Chấp nhận (đã có notify).
2. **Giá tăng vượt credit** giữa lúc lập lịch và lúc chạy → fail → Draft. Đúng ý (không tự đặt đơn
   vượt hạn mức).
3. **Migration trên prod (5433)**: chỉ tạo file, không tự apply.
4. **`ForceLoadModuleAssemblies`**: không thêm seam mới ở đây nên snapshot không bị drift, nhưng
   kiểm tra lại nếu có Row mới.
5. **Backward compat — QUYẾT ĐỊNH: backfill.**
   - `DeliveryAddressId`: cột thêm dạng nullable, migration backfill = địa chỉ mặc định
     (`IsDefault`) của nhà hàng nếu có.
   - Lịch cũ **không có item** (không có nguồn để bịa ra sp): job vẫn **tạo Draft như trước**
     (chính là nhánh degrade ở 3.4 khi template rỗng). Không confirm tự động cho tới khi nhà hàng
     thêm sp vào lịch. → không đơn nào bị vỡ.

---

## 6. Thứ tự làm

1. Domain: `ScheduledOrderItem` + sửa `ScheduledOrder` (+ unit test đỏ).
2. Persistence: config + migration (không apply prod).
3. Tách `OrderConfirmationService` khỏi `ConfirmOrderCommandHandler` (regression test xanh).
4. `GenerateDueAsync`: tạo item + auto-confirm + degrade-to-draft + notify.
5. API/DTO/validator (create + update + get + list).
6. Test đầy đủ + sửa test cũ + `dotnet format`.
