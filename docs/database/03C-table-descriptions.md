# FreshFlow — Chức năng từng bảng dữ liệu

> Mô tả nghiệp vụ ngắn gọn của từng bảng, nhóm theo domain. Companion của bộ tài liệu schema:
>
> | File | Vai trò |
> |------|---------|
> | `03-database-schema.conceptual.md` / `.conceptual.dbml` | Mức khái niệm: entity + quan hệ |
> | `03-database-schema.logical.dbml` | Mức logic: thuộc tính, khóa, mọi quan hệ nghiệp vụ |
> | `03-database-schema.dbml` + `../03-database-schema.md` | Mức vật lý PostgreSQL (kèm archive các bảng đã loại bỏ) |
> | `03B-database-erd.md` | Mermaid ERD của phần đã triển khai |
> | **`03C-table-descriptions.md` (file này)** | Chức năng nghiệp vụ của từng bảng |
>
> Rà soát theo implementation ngày **2026-07-03**. Part A = đã triển khai (22 bảng),
> Part B = lộ trình, chưa có trong database (21 bảng).

---

## Part A — Đã triển khai

### Định danh & Truy cập (Auth)

| Bảng | Chức năng |
|---|---|
| `roles` | Danh mục vai trò toàn hệ thống (admin, market_agent, hub_staff, driver, restaurant). Mỗi user có đúng một vai trò. |
| `users` | Tài khoản gốc: email/phone đăng nhập (đều unique), trạng thái kích hoạt, liên kết vai trò. |
| `refresh_tokens` | Refresh token xoay vòng (rotation): lưu lineage qua `replaced_by_token_id` và `family_id` để phát hiện token bị dùng lại — khi phát hiện, thu hồi cả họ token. |
| `password_reset_tokens` | Token đặt lại mật khẩu dùng một lần, có hạn (`expires_at`, `used_at`). |
| `verification_codes` | Mã xác minh email/phone khi kích hoạt tài khoản, dùng một lần. |
| `user_market_assignments` | Bảng cầu nối nhiều-nhiều: market agent nào phụ trách chợ nào (unique theo cặp user–market). |
| `driver_profiles` | Hồ sơ mở rộng 1:1 cho user vai trò tài xế (biển số xe, SĐT liên lạc). |

### Hồ sơ nhà hàng

| Bảng | Chức năng |
|---|---|
| `restaurants` | Hồ sơ doanh nghiệp bên mua, 1:1 với một user; vòng đời duyệt Pending → Active; khung giờ nhận hàng (`pickup_start/end`). |
| `delivery_addresses` | Các địa chỉ giao hàng của một nhà hàng (kèm tọa độ, cờ mặc định). |

### Danh mục & Giá (Catalog / Pricing)

| Bảng | Chức năng |
|---|---|
| `product_categories` | Danh mục phân loại sản phẩm (lookup, tên unique). |
| `units_of_measurement` | Đơn vị tính (kg, bó, thùng…) — lookup, tên unique. |
| `products` | Catalog sản phẩm toàn hệ thống: tên, mô tả, thuộc danh mục nào, tính bằng đơn vị nào. |
| `markets` | Chợ đầu mối — điểm nguồn hàng, có tọa độ và trạng thái hoạt động. |
| `market_products` | "Bảng giá sống": một sản phẩm tại một chợ cụ thể với giá hiện tại, tồn kho hiện tại và lượng đã giữ chỗ (`reserved_quantity`). Unique theo cặp (chợ, sản phẩm). |
| `price_snapshots` | Lịch sử giá/tồn kho dạng append-only của từng `market_product` — ai ghi nhận, lúc nào. Nguồn dữ liệu cho biểu đồ giá và phân tích. Partition theo tháng trên `recorded_at`. |

### Đơn hàng & Công nợ (Orders / Credit)

| Bảng | Chức năng |
|---|---|
| `orders` | Đơn mua của nhà hàng: vòng đời Draft → Confirmed → … → Delivered, trạng thái thanh toán công nợ, tổng tiền, lịch giao, thông tin hủy. |
| `order_items` | Dòng hàng của đơn: số lượng đặt, giá tại thời điểm đặt, **giá khóa lúc xác nhận** (`locked_unit_price/locked_total` — DEC-007), số lượng thực nhận (`actual_quantity`). Tên sản phẩm được snapshot để bất biến với thay đổi catalog. |
| `scheduled_orders` | Mẫu đơn định kỳ (ngày/tuần) tự sinh `orders`; lưu lần chạy gần nhất và trạng thái hủy. |
| `restaurant_credit` | Tài khoản công nợ B2B 1:1 với nhà hàng: hạn mức (`credit_limit`) và dư nợ hiện tại (`outstanding_balance`) — DEC-002. |
| `credit_transactions` | Sổ cái công nợ append-only: mỗi biến động (charge khi xác nhận đơn / settlement khi thanh toán / refund / adjustment) kèm số dư sau giao dịch. |
| `order_issues` | Báo cáo sự cố sau giao nhận (thiếu / sai / hỏng) trên toàn đơn hoặc từng dòng hàng; ai báo, số lượng ảnh hưởng, trạng thái xử lý. |

### AI Assistant

| Bảng | Chức năng |
|---|---|
| `assistant_conversations` | Phiên hội thoại trợ lý AI lưu DB (thay vì Redis): state JSON, `session_id` unique, TTL qua `expires_at`; gắn với user và tùy chọn ngữ cảnh một chợ. |

---

## Part B — Trong lộ trình, chưa triển khai

### Nhà hàng & Cấu hình

| Bảng | Chức năng |
|---|---|
| `price_alert_subscriptions` | Nhà hàng đăng ký nhận cảnh báo khi giá một sản phẩm biến động vượt ngưỡng %. |
| `system_config` | Tham số vận hành Admin chỉnh được (giờ cutoff 22:00, dung sai price-band 10%…) dạng key–value. |

### Đơn hàng mở rộng

| Bảng | Chức năng |
|---|---|
| `order_groups` | Lô các đơn Confirmed được gom để đi mua/giao cùng nhau (auto-batch lúc 22:00 — DEC-006). `orders.order_group_id` đã chừa sẵn. |
| `order_status_history` | Vết audit mọi lần chuyển trạng thái đơn: từ đâu sang đâu, ai đổi, lý do. |

### Thu mua (Procurement)

| Bảng | Chức năng |
|---|---|
| `procurement_batches` | Một đợt đi chợ, sinh từ một order group; theo dõi tiến độ tổng. |
| `procurement_orders` | Nhiệm vụ mua của một agent tại một chợ trong đợt đó. |
| `procurement_items` | Từng dòng cần mua: số lượng mục tiêu vs thực mua, giá thực tế (đầu vào cho cơ chế price-band ±10%), kèm danh sách đơn hàng nguồn. |

### Vận hành Hub

| Bảng | Chức năng |
|---|---|
| `hubs` | Điểm trung chuyển/cross-dock: mã, địa chỉ, tọa độ, trạng thái. |
| `hub_inventory` | Số dư tồn theo từng market_product tại hub (vào − ra = khả dụng). |
| `hub_receivings` | Phiếu nhận hàng tại hub từ một procurement order — bước QC gate trước khi giao (DEC-008). |
| `hub_receiving_items` | Chi tiết từng dòng nhận: kỳ vọng vs thực nhận, tình trạng (OK/hỏng/thiếu/một phần). |
| `hub_outbound_events` | Sự kiện xuất hàng khỏi hub lên tuyến giao. |
| `cross_dock_transfers` | Luồng chuyển thẳng nhận-vào → xuất-ra không lưu kho. |

### Giao vận (Logistics)

| Bảng | Chức năng |
|---|---|
| `vehicles` | Đội xe: biển số, tải trọng, trạng thái (rảnh / đang chạy / bảo trì). |
| `delivery_routes` | Tuyến giao đã hoạch định: tài xế, xe, ngày chạy, số điểm dừng, ước lượng thời gian/quãng đường. |
| `route_stops` | Điểm dừng có thứ tự trên tuyến — mỗi điểm ứng với một nhà hàng/đơn, giờ đến dự kiến vs thực tế. |
| `deliveries` | Kết quả giao của một đơn tại một điểm dừng: mốc thời gian khởi hành/đến/giao xong, nhà hàng xác nhận, lý do thất bại nếu có. |

### Thông báo & Phân tích

| Bảng | Chức năng |
|---|---|
| `notifications` | Thông báo tới từng user (đa kênh: push/SMS/in-app/email), trạng thái đã đọc. |
| `notification_templates` | Mẫu nội dung thông báo theo loại sự kiện và kênh gửi. |
| `analytics_aggregations` | Số liệu báo cáo tính sẵn theo kỳ (JSON) để dashboard đọc nhanh không đụng bảng giao dịch. |
| `export_jobs` | Yêu cầu xuất CSV bất đồng bộ: tham số, trạng thái xử lý, hạn tải file. |

---

## Đã loại bỏ theo quyết định (không thuộc mô hình)

| Bảng | Lý do loại bỏ |
|---|---|
| `invoices`, `invoice_orders`, `payments`, `refunds` | Superseded bởi mô hình công nợ B2B (`restaurant_credit` + `credit_transactions`) — **DEC-002** trong `06-context-decisions.md`. Chỉ còn trong file physical làm archive. |
| `restaurant_members` | v1 chỉ có một actor Restaurant duy nhất, 1:1 với User — **DEC-003**. |
