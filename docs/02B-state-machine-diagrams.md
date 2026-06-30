# FreshFlow — State Machine Diagrams

| | |
|---|---|
| **Ngày** | 2026-06-29 |
| **Phạm vi** | Vòng đời trạng thái của các entity chính (toàn bộ thiết kế; phần chưa code đánh dấu `[PLANNED]`) |
| **Nguồn sự thật** | Các enum domain trong `src/Modules/**/Domain/Enums` + ERD `03-database-schema.dbml` |
| **Draw.io source** | [`02B-state-machine-diagrams.drawio`](./02B-state-machine-diagrams.drawio) (mỗi entity là 1 page) |

> Mô tả vòng đời (state machine) của từng entity: trạng thái, sự kiện/điều kiện kích hoạt chuyển trạng thái.
> Đây là góc nhìn **hành vi của dữ liệu**, bổ sung cho ERD (cấu trúc) và Activity diagrams (luồng nghiệp vụ).

**Quy ước màu (draw.io):** xanh dương = trạng thái thường · xanh lá = trạng thái kết thúc thành công ·
đỏ = hủy/thất bại · xám + chữ nghiêng = `[PLANNED]` (chưa implement).

---

## Tổng hợp các page

| # | Entity | Trạng thái | Trạng thái |
|---|--------|-----------|-----------|
| 01 | **Order** (`orders.status`) | Draft, Confirmed, `[PLANNED]` Batched, PickedUp, AtHub, Delivering, Delivered, Cancelled | Implemented (lõi) |
| 02 | **Order Payment** (`orders.payment_status`) | NotApplicable, Outstanding, Settled, Waived | Implemented |
| 03 | **Restaurant** (`restaurants.status`) | Pending, Active, Suspended | Implemented |
| 04 | **User account** (suy ra từ cờ) | Active, Locked, Inactive, Deleted | Implemented |
| 05 | **RefreshToken** | Active, Rotated, Revoked, Expired | Implemented |
| 06 | **ScheduledOrder** | Active, Cancelled/Deleted | Implemented |
| 07 | **OrderIssue** (`order_issues.status`) | Open, Resolved | Implemented |
| 08 | **AssistantConversation** | Active, Expired (TTL) | Implemented |
| 09 | **Payment** `[PLANNED]` | Pending, Succeeded, Failed, Cancelled | Planned |
| 10 | **Delivery** `[PLANNED]` | Pending, InTransit, Delivered, Failed | Planned |
| 11 | **DeliveryRoute** `[PLANNED]` | Planned, Dispatched, Completed, Cancelled | Planned |
| 12 | **ProcurementOrder** `[PLANNED]` | Assigned, InProgress, Completed, Cancelled | Planned |

---

## 01 — Order

Lõi đã implement là **Draft → Confirmed**. Các trạng thái `Batched → PickedUp → AtHub → Delivering → Delivered`
đã khai báo trong `enum OrderStatus` nhưng **luồng chuyển tiếp thuộc phần fulfillment chưa code** (`[PLANNED]`).

| Từ | Đến | Kích hoạt |
|---|---|---|
| _(start)_ | Draft | Nhà hàng tạo đơn nháp |
| Draft | Confirmed | Xác nhận đơn, còn đủ hạn mức công nợ |
| Draft | Cancelled | Hủy / bỏ giỏ |
| Confirmed | Cancelled | Hủy trước cutoff |
| Confirmed | Batched `[PLANNED]` | Cutoff hằng ngày + gom đơn |
| Batched → … → Delivering `[PLANNED]` | | Thu mua → về hub → điều phối giao |
| Delivering | Delivered `[PLANNED]` | Nhà hàng xác nhận nhận hàng |

## 02 — Order Payment Status (công nợ)

Mô hình **công nợ B2B** (thay cho cổng thanh toán per-order).

| Từ | Đến | Kích hoạt |
|---|---|---|
| _(start)_ | NotApplicable | Đơn nháp, chưa phát sinh nợ |
| NotApplicable | Outstanding | Confirm → ghi nợ (`credit charge`) |
| Outstanding | Settled | Thanh toán đủ (`settlement`) |
| Outstanding | Waived | Admin miễn / điều chỉnh (`waive`) |

## 03 — Restaurant

| Từ | Đến | Kích hoạt |
|---|---|---|
| _(start)_ | Pending | Đăng ký hồ sơ nhà hàng |
| Pending | Active | Admin duyệt |
| Active | Suspended | Admin khóa |
| Suspended | Active | Admin mở lại |

Chỉ trạng thái **Active** mới được tạo/confirm đơn.

## 04 — User account

Không có cột `status` enum; trạng thái **suy ra** từ `is_active`, `locked_until`, `deleted_at`.

| Từ | Đến | Kích hoạt |
|---|---|---|
| _(start)_ | Active | Tạo tài khoản + xác thực email |
| Active | Locked | Sai mật khẩu quá ngưỡng (`locked_until`) |
| Locked | Active | Hết hạn khóa / Admin mở |
| Active ↔ Inactive | | Admin bật/tắt `is_active` |
| Active/Inactive | Deleted | Xóa mềm (`deleted_at`) |

## 05 — RefreshToken

Append-only, xoay vòng theo `family_id`; phát hiện reuse → thu hồi **cả family**.

| Từ | Đến | Kích hoạt |
|---|---|---|
| _(start)_ | Active | Phát hành khi đăng nhập |
| Active | Rotated | Dùng để refresh → xoay vòng |
| Active | Revoked | Logout / phát hiện reuse |
| Active | Expired | Quá `expires_at` |

## 06 — ScheduledOrder

| Từ | Đến | Kích hoạt |
|---|---|---|
| _(start)_ | Active | Tạo lịch định kỳ |
| Active | Active (self) | Tới hạn → sinh order (cập nhật `last_executed_at`) |
| Active | Cancelled/Deleted | Nhà hàng/Admin hủy lịch |

## 07 — OrderIssue

| Từ | Đến | Kích hoạt |
|---|---|---|
| _(start)_ | Open | Nhà hàng báo sự cố (`missing`/`wrong`/`damaged`) |
| Open | Resolved | Ops/Admin xử lý → điều chỉnh công nợ |

## 08 — AssistantConversation

| Từ | Đến | Kích hoạt |
|---|---|---|
| _(start)_ | Active | Bắt đầu phiên chat |
| Active | Active (self) | Mỗi lượt chat → cập nhật `state` |
| Active | Expired | Quá `expires_at` (TTL) |

## 09–12 — `[PLANNED]`

`Payment`, `Delivery`, `DeliveryRoute`, `ProcurementOrder` là vòng đời thuộc các module
**Payment / Logistics / Hub / Procurement chưa được implement**. Giữ ở đây làm thiết kế tham chiếu;
các enum trạng thái lấy từ `03-database-schema.dbml` (Part B — Planned).
