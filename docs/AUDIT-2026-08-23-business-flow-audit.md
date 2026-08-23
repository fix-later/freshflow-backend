# AUDIT 2026-08-23 — Rà soát luồng nghiệp vụ toàn hệ thống

> **Loại:** audit đọc code, không sửa gì.
> **Phạm vi:** 10/10 module + host. Trace từ `Draft` đến `Delivered`, cộng credit ledger,
> phiên chợ → batch → hub → giao hàng → hóa đơn VAT, auth, và 5 module vòng ngoài
> (Pricing, Catalog, Analytics, Notifications, Assistant).
> **Nguồn sự thật:** code hiện tại trên `dev-bao`. Doc thiết kế **không** được dùng làm căn cứ —
> chỗ nào doc khác code thì code thắng.

## Cách đọc

| Mức | Nghĩa |
|---|---|
| 🔴 **CRITICAL** | Sai tiền, mất tiền của khách, hoặc đơn kẹt vĩnh viễn không có đường code nào gỡ |
| 🟠 **HIGH** | Lỗ hổng bảo mật khai thác được, hoặc sai số liệu pháp lý (thuế) |
| 🟡 **MEDIUM** | Sai số liệu báo cáo, ràng buộc vận hành, mìn chờ nổ |

Tổng: **6 CRITICAL · 5 HIGH · 7 MEDIUM**.

---

# 🔴 CRITICAL

## C1 — Mua thiếu hàng nhưng khách vẫn bị trừ đủ tiền

**Chỗ:** `Order.cs:374` · `ProcurementBatchHandedOffIntegrationEventHandler.cs:108` (Orders) ·
`ProcurementBatchHandedOffIntegrationEventHandler.cs` (Hub)

`ApplyProcurementActuals` ghi `ActualQuantity` / `ActualUnitPrice` vào từng dòng đơn nhưng
**không tính lại `TotalAmount`**, và không phát bất kỳ lệnh hoàn tiền nào.

```
Nhà hàng đặt 10kg cải  → Confirm → CreditService.ChargeAsync(10kg × giá)   ← trừ ĐỦ
Agent đi chợ chỉ mua được 6kg → HandoverBatch
  → ApplyProcurementActuals(6kg)   ← chỉ ghi số, không đụng tiền
  → Hub tạo HubInboundEvent 6kg
  → HẾT. Không sinh HubDiscrepancy nào.
```

Chỉ `RecordDiscrepancy` (hub staff bấm tay, **từng order-item một**) mới kích hoạt refund.
Không ai bấm → khách trả tiền 10kg, nhận 6kg. Sao kê cuối tháng
(`CreditStatementGenerationService`) dựng thuần từ ledger nên ghi lại đúng số đã trừ sai.

**Sửa:** trong `ProcurementBatchHandedOffIntegrationEventHandler` (Hub), so `item.Quantity`
với `item.ActualQuantity`; chênh thì tự `HubDiscrepancy.Create(... ConditionPartial)`.
Đường refund đã có sẵn và đã được test — chỉ thiếu người bấm nút.

---

## C2 — Giao hàng thất bại → đơn kẹt vĩnh viễn, tiền không đòi lại được

**Chỗ:** `UpdateDeliveryStatusCommandHandler.cs:69` · `Order.cs:11` · `FileClaimCommandHandler.cs:31`

Comment trong code nói thẳng: *"Orders has no failed-delivery state"*. Tài xế bấm FAILED →
`deliveries.status = 'failed'`, còn `orders."Status"` đứng nguyên ở `Delivering`. Từ đó:

| Muốn làm gì | Bị chặn ở đâu |
|---|---|
| Hủy đơn | `Order.cs:11` — `AllowedTransitions[Delivering] = [Delivered]`, không có `Cancelled` → `ORDER_NOT_CANCELLABLE` |
| Khiếu nại đòi tiền | `FileClaimCommandHandler.cs:31` — chỉ cho `AtHub` / `Delivered` → `CLAIM_ORDER_NOT_CLAIMABLE` |
| Xuất hóa đơn | seam lọc `o."Status" = 'Delivered'` → không bao giờ xuất |
| Giao lại hôm sau | không có lệnh nào đưa `Delivering` → `AtHub` |

**Đơn đã trừ tiền, hàng nằm lại hub, không có bất kỳ đường code nào hoàn tiền hay đóng đơn.**
Chỉ sửa được bằng `UPDATE` tay vào DB.

**Sửa tối thiểu:** cho phép `Delivering → AtHub` khi delivery FAILED, và thêm `Delivering`
vào danh sách trạng thái được claim.

---

## C3 — Đã thanh toán rồi thì không hoàn được nữa, và hệ thống im lặng

**Chỗ:** `HubDiscrepancyRecordedIntegrationEventHandler.cs:51` · `RestaurantCredit.cs` (`Refund`)

```csharp
var refundAmount = Math.Min(desiredRefund, account?.OutstandingBalance ?? 0m);
if (refundAmount <= 0m)
    return;   // ← không log, không ghi ledger, không báo ai
```

`RestaurantCredit.Refund()` ném lỗi nếu `amount > OutstandingBalance`, nên handler phải cắt.
Mô hình công nợ B2B chốt sao kê theo tháng: phát hiện thiếu hàng **sau khi** nhà hàng đã trả
hết nợ tháng đó → dư nợ = 0 → refund bị cắt về 0 → `return` lặng lẽ. Tiền của khách bốc hơi,
không có dòng ledger nào, không cả một warning log.

**Gốc rễ:** ledger không cho phép **số dư âm** (ghi có cho khách). Cần cho `OutstandingBalance`
xuống âm (khấu trừ kỳ sau) hoặc thêm `CreditTransactionType.CreditNote`.

---

## C4 — Hóa đơn VAT không có đường điều chỉnh / hủy

**Chỗ:** `Invoice.cs` · `OrderInvoiceRowConfiguration.cs`

`Invoice` chỉ có `MarkIssued` / `MarkIssuanceFailed` / `MarkAwaitingBuyerInfo` / `MarkFailed`.
**Không có `Cancel`, không có `Adjust`, không có hóa đơn thay thế.** Trong khi:

- Seam xuất hóa đơn theo `oi."Quantity"` và `LockedUnitPrice` (số **đặt**), có comment ghi rõ
  đây là chủ ý: *"Procurement actuals are internal costs; buyer invoices use terms locked at confirmation."*
- Refund do discrepancy/claim chỉ chạm ledger công nợ, **không hề chạm Invoicing**.

→ Khách được hoàn tiền 4kg, nhưng hóa đơn VAT đã phát hành mã CQT vẫn ghi 10kg. Doanh thu khai
thuế ≠ tiền thực thu, và không có nghiệp vụ điều chỉnh theo NĐ 123 / TT 78.
**Đây là blocker go-live với NCC hóa đơn thật.**

---

## C5 — Mật khẩu DB production nằm trong repo

**Chỗ:** `src/FreshFlow.API/appsettings.json:3`

```
"DefaultConnection": "Host=localhost;Port=5433;Database=freshflow;Username=ffx;Password=<đang hardcode trong file>"
```

File **đang được git track**. `localhost:5433` là DB production. Cloudinary / Resend / JWT đã
chuyển hết sang `__SET_VIA_USER_SECRETS__` — riêng connection string thì bị bỏ sót.

**Sửa:** đổi mật khẩu ngay, chuyển sang env var / user-secrets, và `git filter-repo` nếu repo
từng ở chế độ public.

---

## C6 — Xóa sản phẩm khi đơn đang chạy làm chết 3 luồng cùng lúc

**Chỗ:** `DeleteMarketProductCommandHandler.cs:17` · `DeactivateProductCommandHandler.cs:18` ·
`OrderRepository.cs:205,212,220` · `OrderPackingLineRowConfiguration.cs:26` ·
`RoutePlanningInputBuilder.cs:69`

Cả hai handler xóa mềm với **zero kiểm tra**: không xem `ReservedQuantity > 0`, không xem có đơn
nào đang chạy. Và **không có endpoint khôi phục nào** trong cả Catalog lẫn Pricing.

Sau khi xóa, ba chỗ gãy:

**1. Không hủy được đơn → không hoàn được tiền.**
`UpdateStockAsync` có `AND deleted_at IS NULL` ở cả ba nhánh Reserve/Release/Consume.
Product bị xóa → `UPDATE` khớp 0 dòng → `affected != 1` → `false` →
`CancelOrderCommandHandler` trả `STOCK_RESERVATION_CONFLICT`. Đơn không hủy được, tiền
không trả lại được.

**2. Phiên chợ không bàn giao được nữa.**
`ConsumeStockAsync` gãy y hệt → `ProcurementHandoverRejectedException` → agent nhận 409
vĩnh viễn.

**3. Cả ngày giao hàng của hub đó chết, không riêng đơn lỗi.**
Seam packing lọc `WHERE mp."deleted_at" IS NULL AND p."DeletedAt" IS NULL`, nên dòng của đơn
đó biến mất. Rồi `RoutePlanningInputBuilder.cs:69`:

```csharp
if (routableOrders.Any(order => !packingByOrder.TryGetValue(order.OrderId, out var lines)
        || lines.Count == 0 || lines.Any(line => line.CapacityKg is null or <= 0m)))
    return Error.Validation("ROUTE_WEIGHT_INCOMPLETE", ...);
```

**Một** đơn thiếu dữ liệu → **toàn bộ** kế hoạch tuyến của hub ngày hôm đó bị từ chối.
Mọi nhà hàng khác cũng không được giao.

**Sửa:** chặn xóa khi `ReservedQuantity > 0` hoặc còn đơn ở trạng thái
`Confirmed/Batched/PickedUp/AtHub/Delivering`. Và tách `ROUTE_WEIGHT_INCOMPLETE` thành cảnh báo
theo từng đơn (bỏ đơn lỗi ra khỏi lượt plan) thay vì đánh sập cả mẻ.

---

# 🟠 HIGH

## H1 — Rate limit auth bypass được bằng một HTTP header

**Chỗ:** `Program.cs:107-108` · `PasswordResetToken.cs` · `ResetPasswordCommandHandler.cs`

```csharp
options.KnownNetworks.Clear();
options.KnownProxies.Clear();
```

Chấp nhận `X-Forwarded-For` từ **bất kỳ ai**. Attacker đổi header mỗi request → mỗi request một
partition key → giới hạn 10 req/phút thành vô hạn.

Ghép với: OTP reset password 6 chữ số, TTL 15 phút, và `PasswordResetToken` **không có bộ đếm
số lần thử sai** — `IsValid` chỉ kiểm `!IsExpired && !IsUsed`. Không rate limit thật + không
giới hạn số lần thử = quét hết 1.000.000 mã trong cửa sổ 15 phút là khả thi →
**chiếm tài khoản**.

**Sửa:** thêm `AttemptCount` vào `PasswordResetToken`, khóa token sau 5 lần sai; khai báo
`KnownProxies` đúng CIDR của reverse proxy khi deploy.

---

## H2 — Không có outbox → một lỗi giữa chừng chặn bàn giao vĩnh viễn

**Chỗ:** `ProcurementBatchBuiltIntegrationEventHandler.cs` ·
`ProcurementBatchHandedOffIntegrationEventHandler.cs` (Orders)

```csharp
foreach (var orderId in ...) {
    try { order.AdvanceStatus(Batched); await orders.SaveChangesAsync(ct); }
    catch (Exception ex) { logger.LogError(...); }   // ← rơi đơn ở đây là mất luôn
}
```

`SaveChanges` từng đơn một, nuốt exception, không retry, không outbox. Batch 40 đơn, lỗi ở đơn
thứ 20 → 20 đơn `Batched`, 20 đơn `Confirmed`. `session.BatchingCompletedAt` đã set nên không
batch lại được. Rồi `HandoverBatch` bắt buộc **tất cả** đơn phải `Batched`:

```csharp
if (batchedOrders.Count != coveredOrders.Count)
    return Error.Conflict("PROCUREMENT_ORDER_STATE_CONFLICT", ...);
```

→ Phiên chợ đó **không bao giờ bàn giao được**, không có endpoint sửa.

Đây là chỗ outbox thực sự cần: 1 bảng `outbox_messages` ghi cùng transaction + 1
`BackgroundService` poll & publish lại (copy `NotificationRetryHostedService`). ~150 dòng,
không thêm hạ tầng. **Không phải chỗ cần Kafka.**

---

## H3 — Sửa tồn kho không kiểm tra hàng đã được giữ chỗ

**Chỗ:** `MarketProduct.cs:109-127` · `UpdateAvailableQuantityCommandHandler.cs`

`ApplyUpdate` gán thẳng `CurrentQuantity = newQuantity.Value`, **không so với `ReservedQuantity`**.
Handler cũng chỉ chặn `quantity < 0`.

Market agent hạ tồn kho xuống dưới số đã giữ chỗ → `ReservedQuantity > CurrentQuantity` →
`AvailableQuantity` **âm**. Sau đó `ConsumeStockAsync` (yêu cầu
`CurrentQuantity >= qty AND ReservedQuantity >= qty`) khớp 0 dòng → bàn giao phiên chợ bị chặn
vĩnh viễn, cùng ngõ cụt với C6.

**Sửa:** trong `ApplyUpdate`, từ chối `newQuantity < ReservedQuantity` với lỗi
`QUANTITY_BELOW_RESERVED`.

---

## H4 — Sản phẩm chưa cấu hình VAT bị âm thầm xuất hóa đơn 0%

**Chỗ:** `OrderPricingCalculator.cs:80` · `VatRateResolver.cs:17` · `Product.cs:107`

`products.VatRate` nullable. Cả hai bộ giải mã đều rơi vào 0% khi null:

```csharp
// Orders — OrderPricingCalculator.ResolveTax
var code = string.IsNullOrWhiteSpace(rate) ? "KCT" : ...;
var percent = code switch { "5" => 5m, "8" => 8m, "10" => 10m, _ => 0m };
```

Admin quên set VAT cho một sản phẩm → đơn được tính **0% VAT**, và hóa đơn điện tử phát hành
cho cơ quan thuế cũng **0% VAT**, mã `KCT`. Không cảnh báo, không báo cáo, không ai biết.
Comment trong `VatRateResolver` đã tự nhận: *"Tighten to fail-closed once per-category rates
are signed off."*

Thêm nữa: **hai bảng VAT trùng lặp ở hai module** (`ResolveTax` và `VatRateResolver.ToPercent`).
Hôm nay chúng cho kết quả giống nhau; ngày mai sửa một bên là số tiền charge lệch số tiền
trên hóa đơn.

**Sửa:** fail-closed — `VatRate` null thì từ chối confirm với `VAT_RATE_MISSING` (domain đã có
sẵn error code này ở `Order.cs`). Và gộp bảng VAT về một chỗ trong `SharedKernel` hoặc `Contracts`.

---

## H5 — Giá bán công khai bị ghi đè bằng giá agent gõ tay, không có chặn nào

**Chỗ:** `ProcurementPurchaseConfirmedIntegrationEventHandler.cs:33`

```csharp
marketProduct.UpdatePrice(actualUnitPrice, notification.AgentUserId);
```

Agent xác nhận giá mua thực tế → giá niêm yết trên bảng giá (Redis + SignalR broadcast tới mọi
nhà hàng) **bị ghi đè ngay lập tức**. Không kiểm biên độ, không cần duyệt, không so với giá cũ.

Agent gõ nhầm `500000` thay vì `50000` → bảng giá công khai nhảy 10× cho tới khi có người sửa tay.
Guard duy nhất là `ValidatePrice(price > 0)`.

**Sửa:** chặn biến động vượt ngưỡng (ví dụ ±50% so với giá hiện tại) → ghi
`ProcurementException` chờ admin duyệt thay vì áp thẳng.

---

# 🟡 MEDIUM

| # | Mô tả | Chỗ |
|---|---|---|
| **M1** | `Order.Confirm()` có nhánh dự phòng tự gán VAT `KCT` **0%** và phí ship **0đ** khi item chưa lock giá. Hiện chỉ `OrderConfirmationService` gọi `Confirm()` và luôn lock giá trước nên chưa nổ — nhưng bất kỳ code mới nào gọi thẳng sẽ âm thầm xuất đơn 0 VAT, 0 ship. Nên đổi thành `Result.Failure`. | `Order.cs:269-273` |
| **M2** | `CancelOrderCommand` nhận `User.IsInRole("admin")`, trong khi `GetOrder` dùng `CanReadAllOrders()` (gồm `operations_manager`). Ops manager được controller cho vào nhưng hủy đơn luôn `FORBIDDEN`. Fail-closed nên không nguy hiểm, nhưng RBAC không nhất quán. | `OrdersController.cs:304` vs `:156` |
| **M3** | `OrderItem.Quantity` là `int` nhưng ngữ nghĩa thực tế là **kg** (`RoutePlanningInputBuilder.LineLoad` cộng thẳng vào tải trọng kg). Không đặt được 0.5kg. `ActualQuantity` lại là `decimal` → agent mua 6.5kg thì phần lẻ không bao giờ khớp lại số đặt. | `OrderItem.cs` |
| **M4** | Analytics: giá trị đơn trung bình = `totalRevenue / totalOrders`, nhưng `totalRevenue` **loại** đơn Cancelled còn `totalOrders` **tính cả** đơn Cancelled. AOV luôn bị báo thấp hơn thực tế. | `GetOrderMetricsQueryHandler.cs:61` |
| **M5** | Analytics gom nhóm theo `orders."CreatedAt"` — thời điểm tạo **giỏ hàng**, không phải `ConfirmedAt`. Đơn tạo 01/01 xác nhận 05/01 rơi vào rổ 01/01. Ngoài ra doanh thu **không bao giờ trừ refund** (hub discrepancy, claim), nên dashboard luôn cao hơn tiền thực thu. | `OrderMetricsReader.cs` · `OrderSummaryRowConfiguration.cs` |
| **M6** | **6 background job không có leader election / distributed lock** (`ScheduledOrderGeneration`, `MonthlyCreditStatement`, `ProcurementBatching`, `NotificationRetry`, `InvoiceIssuanceRetry`, `HubInboundBackfill`). Cộng với SignalR `AddSignalR()` in-memory không backplane → **hệ thống chỉ chạy được 1 instance**. Rủi ro chạy trùng hiện được che bởi các guard idempotent (`BatchingCompletedAt`, `ExistsForOrderAsync`, khóa unique theo kỳ) chứ không phải bằng khóa. Cần ghi rõ đây là ràng buộc deploy. | `*/Jobs/*HostedService.cs` |
| **M7** | `ROUTE_WEIGHT_INCOMPLETE` đánh sập cả mẻ: một đơn thiếu packing code làm hỏng kế hoạch tuyến của toàn hub trong ngày. Nên loại riêng đơn lỗi và báo cáo, thay vì từ chối tất cả. | `RoutePlanningInputBuilder.cs:69` |

---

# Soi sâu 5 module vòng ngoài

## Pricing

| Hạng mục | Kết quả |
|---|---|
| RBAC | ✅ `UpdatePrice` / `UpdateAvailableQuantity` giới hạn `admin,market_agent` + kiểm `HasAssignmentAsync` theo chợ. `DeleteMarketProduct` admin-only. |
| Concurrency | ✅ Optimistic qua `ExpectedVersion` (so ticks, xử lý đúng `DateTimeKind`) + `ConcurrencyConflictException`. |
| Giá đã chốt | ✅ Đổi giá **không** ảnh hưởng đơn đã confirm (`LockedUnitPrice`). Đúng. |
| Redis | ✅ Ghi cache post-commit, lỗi Redis chỉ log Warning, không rollback DB. Trường `reserved` cố ý do Orders sở hữu. |
| Tồn kho | ❌ **H3** — không guard `ReservedQuantity`. |
| Xóa listing | ❌ **C6** — xóa mềm không guard, không có đường khôi phục. |
| Giá tự động | ❌ **H5** — giá mua thực tế ghi đè giá niêm yết, không chặn biên độ. |

## Catalog

| Hạng mục | Kết quả |
|---|---|
| VAT | ❌ **H4** — `VatRate` null → 0% âm thầm. Whitelist `KCT/KKKNT/0/5/8/10` thì đúng, nhưng null lọt. |
| Xóa sản phẩm | ❌ **C6** — `Deactivate` xóa mềm vô điều kiện; không có `Reactivate`. |
| Xóa chợ | ⚠️ `DeleteMarketCommandHandler` cũng xóa mềm vô điều kiện — chưa trace hết hệ quả, nhưng cùng dạng rủi ro với C6. |
| Packing code | ✅ `CapacityKg` là nguồn kg/thùng cho cả ước tính thùng lẫn tải trọng tuyến, dùng nhất quán. |
| Validate | ⚠️ `ValidateCommercialTerms` **ném `ArgumentException` từ domain** thay vì trả `Result` → nếu validator ở tầng trên không chặn trước, request admin sai VAT sẽ thành **500** chứ không phải 400. |

## Analytics

| Hạng mục | Kết quả |
|---|---|
| SQL an toàn | ✅ Toàn bộ đọc qua keyless Row + LINQ. **0 chỗ** `FromSqlRaw`, 0 chỗ nối chuỗi, 0 `ORDER BY {input}`. |
| Loại Draft | ✅ Seam lọc `"Status" <> 'Draft'` — giỏ hàng không làm phồng số đơn. |
| Doanh thu | ✅ Loại Cancelled + Draft khi cộng doanh thu. |
| Múi giờ | ✅ Quy đổi VN (`+7h`) trước khi gom nhóm ngày/tuần/tháng. |
| AOV | ❌ **M4** — tử số và mẫu số dùng hai tập đơn khác nhau. |
| Mốc thời gian | ❌ **M5** — gom theo `CreatedAt` (tạo giỏ), không phải `ConfirmedAt`. |
| Refund | ❌ **M5** — doanh thu không trừ refund. |

## Notifications

| Hạng mục | Kết quả |
|---|---|
| IDOR | ✅ `MarkReadAsync(userId, notificationId)` — lọc theo user ngay ở repository, không đọc trước rồi mới kiểm. |
| Cô lập lỗi | ✅ In-app và email độc lập; một kênh hỏng không chặn kênh kia; không ném ngược về publisher. |
| Retry | ✅ Có `AttemptCount`, `MaxAttempts` (mặc định 5), backoff, batch size, bật/tắt qua config. |
| Múi giờ | ✅ Quy đổi VN trước khi format kỳ sao kê và hạn thanh toán. |
| ⚠️ | `CreditStatementGeneratedIntegrationEvent` mang **nguyên byte[] file PDF** trong payload event. Chạy in-process thì không sao, nhưng đây là thứ sẽ nổ đầu tiên nếu sau này chuyển event ra broker. |
| ⚠️ | Không leader election → xem **M6**. |

## Assistant (host, không phải module)

| Hạng mục | Kết quả |
|---|---|
| IDOR phiên chat | ✅ Đã vá. `sessionId` do client sinh nên được coi là không tin cậy: `existing.UserId != userId` → trả **404** (không phải 403) để không lộ sự tồn tại của phiên. |
| Bỏ qua validation | ✅ Đã vá. Endpoint không đi qua `ISender` nên `ValidationBehavior` không chạy — controller gọi `requestValidator.ValidateAsync` thủ công. |
| Xác nhận đơn 2 pha | ✅ `ConfirmationGate` là hàm thuần: chỉ `Allow` khi `confirmOrderIdFlag` **từ body client** khớp đúng `orderId` mà LLM nêu, **và** có `deliveryAddressId`. LLM không điều khiển được request body → model "tự quyết định" xác nhận đơn sẽ bị chặn trước khi `ConfirmOrderCommand` được gửi đi. Thiết kế tốt. |
| Phạm vi tool | ✅ Cả 11 tool đều bind `ctx.UserId` lấy từ JWT và đi qua `ISender`. Không tool nào nhận userId/restaurantId do LLM cung cấp. |
| Rate limit | ✅ Policy `assistant` riêng, 15 req/phút theo user, chặt hơn `orders`. |
| Failover | ✅ `FailoverChatClient` chuyển provider trên mọi lỗi trừ `AuthenticationFailed` — đúng, không loop vô ích khi sai key. |

**Kết luận Assistant: không tìm thấy lỗi mới.** Hai lỗi từng ghi nhận (IDOR resume phiên, bypass
validation) đã được vá đúng cách và comment lại lý do trong code.

---

# Đã kiểm tra và SẠCH — đừng "sửa" nhầm

- **SQL injection:** toàn repo **0 chỗ** `FromSqlRaw`, 0 nối chuỗi/`string.Format` vào SQL,
  0 `ORDER BY {input}`. Cập nhật tồn kho dùng `ExecuteSqlInterpolated` → tham số hóa đúng.
- **Trần tải phiên chợ:** `MarketSessionGate` so `SUM(oi."Quantity")` với `planned_capacity_kg`.
  Thoạt nhìn như so *số lượng* với *kg*, nhưng `Quantity` vốn **là kg** (xác nhận qua
  `RoutePlanningInputBuilder.LineLoad`). Không phải bug.
- **Hoàn tiền trùng:** chặn bởi `GetRefundableAmountForOrderAsync` (tổng charge − tổng refund
  theo từng đơn). Không hoàn được quá số đã thu.
- **JWT:** validate đủ issuer / audience / lifetime / signing key, `ClockSkew = 0`,
  `MapInboundClaims = false`, role claim khai báo tường minh. Refresh token có xoay vòng +
  phát hiện tái sử dụng theo family. Tốt.
- **OTP:** sinh bằng `RandomNumberGenerator.GetInt32` (CSPRNG), hash bằng BCrypt, TTL 15 phút,
  dùng một lần. Điểm yếu duy nhất là thiếu bộ đếm lần thử (**H1**).
- **Forgot password:** luôn trả 202 kể cả email không tồn tại — không lộ user enumeration.
- **Xác nhận đơn:** chạy trong transaction `SERIALIZABLE`, giữ đơn + tồn kho + trừ tiền nguyên tử,
  có xử lý `SERIALIZATION_CONFLICT` và `DbUpdateConcurrencyException`.
- **Proof of delivery:** kiểm `route.DriverUserId == request.DriverUserId`, và URL bắt buộc
  HTTPS + host `res.cloudinary.com`.
- **Sao kê:** số dư đầu kỳ luôn tính lại từ ledger, không nối chuỗi từ `ClosingBalance` kỳ trước
  — miễn nhiễm với việc bỏ sót kỳ. Idempotent theo kỳ, có xử lý race.

---

# Thứ tự sửa đề xuất

| Bước | Việc | Ước lượng | Vì sao trước |
|---|---|---|---|
| 1 | **C5** đổi mật khẩu DB + chuyển sang secrets | 5 phút | Đang lộ |
| 2 | **H3** guard `ReservedQuantity` + **C6** guard xóa | ~40 dòng | Chặn thêm đơn bị kẹt mới |
| 3 | **C2** cho `Delivering → AtHub` + mở claim | ~30 dòng | Gỡ đơn đang kẹt |
| 4 | **C1** tự sinh discrepancy khi thiếu hàng | ~40 dòng | Ngừng thu sai tiền |
| 5 | **H1** `AttemptCount` cho OTP + `KnownProxies` | ~20 dòng | Chặn chiếm tài khoản |
| 6 | **H4** VAT fail-closed + gộp một bảng VAT | ~30 dòng | Trước khi phát hành hóa đơn thật |
| 7 | **H2** outbox | ~150 dòng | Việc lớn hơn, làm sau khi hết cháy |
| 8 | **C3**, **C4** | cần quyết định nghiệp vụ trước | Đụng mô hình công nợ và quy trình thuế |

C3 và C4 **không phải bug code đơn thuần** — chúng đòi một quyết định nghiệp vụ (cho phép số dư
âm hay không; quy trình điều chỉnh hóa đơn ra sao) trước khi viết dòng code nào.
