# PLAN — VAT e-Invoicing (Hóa đơn điện tử)

- **Jira**: epic **SCRUM-370 – INV Hóa đơn điện tử VAT**; tasks SCRUM-371…378 (#1…#8)
- **Ngày**: 2026-07-25
- **Module**: `Invoicing` (mới) + sửa nhỏ Catalog / Orders / Auth
- **Trạng thái**: 📐 Blueprint chờ duyệt

---

## 1. Vì sao cần

Đối chiếu với sàn B2B foodservice (Kamereo): FreshFlow **chưa có dòng code nào** về hóa đơn VAT
(`grep -i 'invoice|vat|tax'` trên `src/` = 0). Sao kê công nợ (`RestaurantCreditController`,
PDF) **không phải** hóa đơn VAT hợp lệ.

Ở VN, hóa đơn phải có **mã cơ quan thuế** do Tổng cục Thuế cấp, phát hành qua **nhà cung cấp
HĐĐT được cấp phép** — không thể tự sinh PDF (NĐ 123/2020, TT 78/2021, NĐ 70/2025 sửa đổi).

**Phần khó KHÔNG phải code** mà là điều kiện pháp lý (MST, chữ ký số, đăng ký HĐĐT với thuế, hợp
đồng NCC). Các điều kiện này **không chặn dev** — dev tích hợp với **sandbox NCC**, pháp lý làm
song song, chỉ bắt buộc trước khi phát hành hóa đơn thật (go-live).

## 2. Quyết định kiến trúc: module `Invoicing` riêng

Là concern cross-cutting (đọc Orders + Restaurant, kích bởi event của Logistics) → **module riêng**,
đúng luật phụ thuộc. KHÔNG nhét vào Orders (Orders.Application không được để module khác tham chiếu).

```
src/Modules/Invoicing/
  FreshFlow.Invoicing.Domain/          Invoice aggregate + enums
  FreshFlow.Invoicing.Application/      IEInvoiceProvider, EventHandlers, Commands, Queries, CrossModule interfaces, Dtos
  FreshFlow.Invoicing.Infrastructure/   provider adapter, seam Rows, persistence, retry job, DI
```

`AddInvoicingModule` chèn vào chuỗi trong `Program.cs`. Nhớ liệt kê Infrastructure assembly trong
`DesignTimeDbContextFactory.ForceLoadModuleAssemblies()` (nếu không snapshot drift → migration
module khác xoá seam).

## 3. Luồng — phát hành **per-delivery** (đúng luật, KHÔNG theo tháng)

> Luật: hóa đơn xuất tại **thời điểm giao hàng**, không được gộp cuối tháng theo chu kỳ công nợ.
> Sao kê công nợ vẫn theo tháng như hiện tại — độc lập với hóa đơn.

```
Driver giao xong → UpdateDeliveryStatus → DeliveryCompletedIntegrationEvent(OrderId, RouteId, ActualArrivalAt)
    ├─ (đã có) Orders: mark receipt / credit
    ├─ (đã có) Notifications
    └─ (MỚI) Invoicing.DeliveryCompletedIntegrationEventHandler
          1. đọc order + lines qua seam — dùng ActualQuantity ?? Quantity (giao thực, KHÔNG phải đặt)
          2. đọc hồ sơ thuế người mua qua seam (MST, tên, địa chỉ, email)
          3. tạo Invoice(Draft) → lưu
          4. IEInvoiceProvider.IssueAsync(...) → NCC ký số + trả mã CQT + link tra cứu
          5. Issued (lưu mã/link/XML/PDF ref)  |  lỗi → PendingIssuance
    InvoiceIssuanceRetryHostedService (copy NotificationRetryHostedService) gom PendingIssuance thử lại
```

`DeliveryCompletedIntegrationEvent` hiện đã có (`OrderId, RouteId, ActualArrivalAt, OccurredAt`) và
đã được Orders + Notifications tiêu thụ → Invoicing chỉ **thêm handler thứ 3**, không đổi event.

## 4. Data model — 3 sửa nhỏ + 1 bảng mới

| Nơi | Thêm | Lý do |
|---|---|---|
| Catalog `products` | `vat_rate` (nullable/enum) | **Thực phẩm tươi có thuế suất đặc biệt** — nhiều mặt 5% hoặc *không chịu thuế* (KCT), không phải 10%. Phải model được KCT, đừng hardcode 10% |
| Orders `order_items` | `VatRate` (snapshot lúc `LockPrice`) | Đúng pattern đã có (`ProductNameSnapshot`, `LockedUnitPrice`, `LockedTotal`). Hóa đơn đọc snapshot → miễn nhiễm đổi giá/thuế sau này |
| `restaurants` (Auth — bảng tạo ở InitAuth; nơi `me/profile` ghi) | `tax_code`, `invoice_legal_name`, `invoice_address`, `invoice_email` | Người mua tự nhập; hiện **chưa có trường nào** |
| **`invoices` (mới, Invoicing owns)** | xem dưới | Bản ghi hóa đơn — **immutable snapshot** như credit statement |

### Bảng `invoices` (snake_case nhất quán — bảng mới)

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `id` | UUID PK | `gen_random_uuid()` |
| `order_id` | UUID | không FK cứng (cross-module) |
| `restaurant_id` | UUID | index |
| `serial` | text | ký hiệu HĐ (NCC cấp) |
| `number` | text | số HĐ (NCC cấp) |
| `tax_authority_code` | text | mã CQT |
| `issued_at` | timestamptz | thời điểm cấp mã |
| `status` | text | Draft/PendingIssuance/Issued/Failed (+ Adjusted/Cancelled v2) |
| `lookup_url` | text | link tra cứu HĐ |
| `pdf_ref` / `xml_ref` | text | tham chiếu file NCC |
| `sub_total` / `vat_amount` / `total` | numeric | tiền |
| `provider_name` | text | NCC đã dùng |
| `error_reason` | text | lý do fail (retry) |
| `retry_count` | int | số lần thử |
| `created_at` | timestamptz | UTC |

Line snapshot: nhúng JSON `lines` hoặc bảng `invoice_lines` (Product, ActualQuantity, UnitPrice,
VatRate, LineTotal). **Append-only sau khi Issued** — không sửa (điều chỉnh = hóa đơn mới ở v2).

## 5. Adapter — che lock-in NCC (1 interface, 1 impl)

Template = `ICloudinarySignatureService` (interface ở SharedKernel, impl ở infra project riêng).
Ở đây giữ **interface trong `Invoicing.Application`**, impl trong `Invoicing.Infrastructure` (chỉ 1
module dùng — không cần đẩy lên SharedKernel).

```csharp
// Invoicing.Application/Abstractions/IEInvoiceProvider.cs
public interface IEInvoiceProvider
{
    // Gửi dữ liệu HĐ → NCC ký số + xin mã CQT; trả kết quả đã cấp mã.
    public Task<Result<IssuedInvoice>> IssueAsync(InvoiceIssueRequest request, CancellationToken ct);
    public Task<Result<IssuedInvoice>> AdjustAsync(InvoiceAdjustRequest request, CancellationToken ct); // v2
}

public sealed record IssuedInvoice(
    string Serial, string Number, string TaxAuthorityCode,
    string LookupUrl, string XmlRef, DateTime IssuedAt);
```

Impl `MisaEInvoiceProvider` (hoặc VNPT) → config `Invoicing:EInvoice:*` (endpoint/appId/taxCode).
**Dev trỏ sandbox NCC, prod trỏ thật — không đổi code.** Đăng ký trong `AddInvoicingModule`.

## 6. Seam đọc cross-module (keyless Row — Invoicing.Infrastructure/CrossModule)

- `OrderInvoiceRow` — OrderId → lines (ProductName, `ActualQuantity ?? Quantity`, LockedUnitPrice,
  VatRate, LineTotal). Đọc `orders` + `order_items`. ⚠️ **casing lẫn lộn** (`orders."Id"` PascalCase,
  `order_items` không có soft-delete) — copy seam có sẵn, đừng đoán.
- `RestaurantTaxProfileRow` — RestaurantId → MST, tên, địa chỉ, email. Đọc `restaurants`.

> `ToSqlQuery` **không chạy trên EF InMemory** → mỗi seam chỉ chứng minh được bằng **integration
> test trên Postgres thật** (Testcontainers). Ưu tiên mở rộng seam có sẵn hơn thêm seam mới lên
> cùng bảng.

## 7. Máy trạng thái Invoice

```
Draft ─issue→ PendingIssuance ─ok→ Issued(có mã CQT)   ← cuối, bất biến
                     └─lỗi(retryable)→ PendingIssuance (retry job) → Failed(hết lượt)
Issued ─(v2: trả hàng/điều chỉnh)→ Adjusted / Cancelled (hóa đơn thay thế/điều chỉnh)
```

## 8. Endpoints (v1)

```
# Người mua nhập hồ sơ thuế (mở rộng RestaurantProfileController me/*)
PUT  /api/v1/restaurants/me/tax-profile   { taxCode, legalName, address, email }

# Xem / tải hóa đơn
GET  /api/v1/invoices?restaurantId=&status=&page=      → list (admin: all; restaurant: của mình)
GET  /api/v1/invoices/{id}                             → chi tiết + lookup_url
GET  /api/v1/invoices/{id}/pdf                         → tải PDF (proxy từ NCC / ref)
```

RBAC: `restaurant` chỉ thấy HĐ của mình (resolve userId→restaurant như favorites); `admin`,
`operations_manager` xem tất cả.

## 9. Cần chốt trước khi code (nghiệp vụ, không phải kỹ thuật)

1. **Số lượng trên HĐ = giao thực (`ActualQuantity`)**, không phải đặt — Hub có thể báo thiếu. Xuất
   HĐ sau khi giao là đúng vì lúc đó mới biết actual.
2. **Chữ ký số server-side** cần cert **HSM/remote signing**, không USB token (không tự động hóa
   trên server) — quyết định hạ tầng lúc chọn NCC.
3. **Thuế suất thực phẩm tươi** — rà từng nhóm hàng (5% / KCT / 10%), quyết cách nhập `vat_rate`.
4. **Chọn NCC** (VNPT-Invoice / Viettel S-Invoice / MISA meInvoice / EasyInvoice) — quyết định
   thương mại; ảnh hưởng adapter impl (nhưng interface không đổi).

## 10. Scope

- **v1**: schema (4 mục) → module scaffold → Invoice aggregate + persistence → adapter 1 NCC
  sandbox → seam + integration test → handler DeliveryCompleted (issue, dùng ActualQuantity) →
  retry job → tax-profile endpoint → query xem/tải HĐ.
- **Sau (v2)**: hóa đơn điều chỉnh/hủy (hook = `RestaurantRefundIssuedIntegrationEvent` khi refund
  ở Hub), HĐ tổng hợp, nhiều NCC.

**Bỏ qua có chủ đích**: hóa đơn điều chỉnh (add khi có refund thật), NCC thứ 2 (add khi cần đa nhà
cung cấp), online payment (mô hình công nợ — ngoài scope hóa đơn).

## 11. Điều kiện pháp lý (công ty lo — song song, không chặn dev)

| Điều kiện | Ai | Chặn dev? |
|---|---|---|
| Pháp nhân + MST | Công ty | Không (dev dùng sandbox) |
| Chữ ký số (VNPT-CA/Viettel-CA/FPT-CA…) | Công ty | Không |
| Đăng ký sử dụng HĐĐT với thuế (Mẫu 01/ĐKTĐ-HĐĐT) | Công ty | Không |
| Hợp đồng NCC HĐĐT | Công ty | Không |

→ Chỉ bắt buộc **trước go-live** phát hành hóa đơn thật.
