# FreshFlow — Activity / Flowchart Diagrams

| | |
|---|---|
| **Ngày** | 2026-06-29 |
| **Phạm vi** | Các luồng nghiệp vụ chính của toàn hệ thống, **có Actor tham gia**, ở mức nghiệp vụ (không mô tả xử lý nội bộ chi tiết) |
| **Nguồn sự thật** | Thiết kế nghiệp vụ + module đã implement; phần chưa code đánh dấu `[PLANNED]` |
| **Draw.io source** | [`02C-activity-diagrams.drawio`](./02C-activity-diagrams.drawio) (mỗi nghiệp vụ là 1 page, dạng swimlane theo Actor) |

> Mỗi diagram là **activity/flowchart dạng swimlane**: mỗi cột (lane) là một **Actor**; các hộp là bước nghiệp vụ;
> hình thoi là điểm quyết định. Mục tiêu là thể hiện *ai làm gì để hoàn thành nghiệp vụ*, không đi vào chi tiết code.

**Quy ước (draw.io):** xanh dương = bước xử lý · cam (hình thoi) = quyết định ·
xám + chữ nghiêng = bước thuộc phần `[PLANNED]` · ghi chú vàng = lưu ý.

### Actor trong hệ thống

| Actor | Vai trò |
|---|---|
| Restaurant (Manager/Staff) | Nhà hàng đặt hàng, xác nhận nhận hàng, theo dõi công nợ |
| Market Agent | Cập nhật giá tại chợ; (kế hoạch) thu mua |
| Ops Manager / Admin | Duyệt nhà hàng, điều phối, xử lý sự cố, tất toán công nợ |
| Hub Staff | (kế hoạch) Nhận & đối chiếu hàng tại hub |
| Driver | (kế hoạch) Giao hàng theo tuyến |
| System | Tiến trình nền, validate, realtime, sinh đơn định kỳ |
| Assistant Orchestrator / LLM | Trợ lý AI điều phối lệnh sẵn có |

---

## Tổng hợp các page

| # | Nghiệp vụ | Actor chính | Trạng thái |
|---|-----------|-------------|-----------|
| 01 | Đăng ký & duyệt nhà hàng | Restaurant Mgr, System, Email, Admin | Implemented |
| 02 | Đặt hàng trong ngày (Draft → Confirm → công nợ) | Restaurant, System | Implemented |
| 03 | Đơn đặt định kỳ tự sinh | Restaurant, System (Scheduler/Orders) | Implemented |
| 04 | Cập nhật giá & bảng giá realtime | Market Agent, System, Restaurant | Implemented |
| 05 | Fulfillment end-to-end (thu mua → hub → giao) | Ops, Agent, Hub Staff, Driver, Restaurant | `[PLANNED]` |
| 06 | Xác nhận nhận hàng & xử lý sự cố | Restaurant, System, Ops/Admin | Implemented |
| 07 | Công nợ & tất toán B2B | System, Restaurant, Admin/Ops | Implemented |
| 08 | Trợ lý AI đặt hàng + Confirmation Gate | Restaurant, Assistant, LLM, System | Implemented |
| 09 | Đăng nhập / refresh / quên mật khẩu | User, System, Email | Implemented |

---

## 01 — Đăng ký & duyệt nhà hàng
Restaurant Manager đăng ký → System tạo `user` + `restaurant` (status **Pending**) và gửi email xác thực →
NH bấm link xác thực → Admin duyệt → `restaurant.status = Active`.
**Lưu ý:** chưa duyệt thì không tạo/confirm được đơn.

## 02 — Đặt hàng trong ngày
Restaurant chọn chợ + sản phẩm → System validate `market_products`/giá → lưu **Draft** + `order_items` →
Restaurant confirm → khóa giá → kiểm tra hạn mức công nợ → nếu đủ thì **ghi nợ + Confirmed**, nếu vượt thì từ chối.
Liên kết state: `payment_status` NotApplicable → Outstanding; thêm `credit_transactions` (charge).

## 03 — Đơn đặt định kỳ
Restaurant tạo lịch (`daily`/`weekly`) → `ScheduledOrderGenerationHostedService` chạy theo chu kỳ →
khi tới hạn & chưa sinh thì **sinh order Draft** từ template và cập nhật `last_executed_at` →
sau đó đi tiếp theo luồng **02**.

## 04 — Cập nhật giá & bảng giá realtime
Market Agent cập nhật giá/lượng → System kiểm tra quyền (`user_market_assignments`) → nếu hợp lệ thì
update `market_products` + insert `price_snapshots` (append-only) → cache Redis + broadcast SignalR `market:{id}` →
Restaurant nhận bảng giá realtime. Lỗi cache không rollback việc ghi giá.

## 05 — Fulfillment end-to-end `[PLANNED]`
System cutoff & gom đơn → Ops tạo batch + phân công Agent → Agent thu mua → Hub Staff nhận & đối chiếu
(thiếu/hỏng → `order_issue`) → Ops lập tuyến + gán xe/tài xế → Driver giao → Restaurant nhận hàng.
Đây là luồng tương ứng các state Order **Batched → Delivered** (`[PLANNED]`).

## 06 — Xác nhận nhận hàng & xử lý sự cố
Restaurant nhận hàng → nếu đúng/đủ thì **xác nhận** (`confirmed_receipt_at`); nếu có vấn đề thì
**báo sự cố** → tạo `order_issue` (Open) → Ops/Admin xử lý → **điều chỉnh công nợ** (adjustment/refund) → đóng (Resolved).

## 07 — Công nợ & tất toán B2B
Mỗi đơn confirm → **charge** (`outstanding_balance += total`). Restaurant xem công nợ và thanh toán (ngoài hệ thống) →
Admin/Ops ghi nhận → **settlement** (`balance -= amount`) hoặc **waive/adjust** → ghi `credit_transactions` (sổ cái append-only).

## 08 — Trợ lý AI đặt hàng
Restaurant chat → Orchestrator load phiên (`assistant_conversations`) + gọi LLM (ZenMux) kèm tool definitions →
nếu LLM gọi tool thì thực thi **command/query sẵn có** qua MediatR (áp business rules + RBAC) →
hành động thay đổi dữ liệu bị chặn bởi **ConfirmationGate** cho tới khi người dùng xác nhận → tổng hợp & lưu state.
Trợ lý **không ghi DB trực tiếp**.

## 09 — Đăng nhập / refresh / quên mật khẩu
Login → kiểm tra mật khẩu + lockout → phát JWT + refresh token (xoay vòng theo family).
Quên mật khẩu → tạo reset token → gửi email (Resend) → đặt mật khẩu mới qua link → xác thực token + cập nhật hash.
Phát hiện reuse refresh token → thu hồi cả family; nhóm endpoint auth có rate-limit.
