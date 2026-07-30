# FreshFlow — Luồng thao tác theo Role

| | |
|---|---|
| Cập nhật | 2026-07-29 |
| Nguồn sự thật | Controller, command/query handler và domain hiện tại |
| Role | `admin`, `operations_manager`, `restaurant`, `market_agent`, `hub_staff`, `driver` |
| Luồng giao hàng | **Hub → Restaurant** |

> Sơ đồ này mô tả hành vi đang có trong code, kể cả các giới hạn hiện tại. Các tài liệu kế hoạch cũ
> không được xem là chức năng đã triển khai.

## 1. Bản đồ trách nhiệm

| Role | Phạm vi dữ liệu | Trách nhiệm chính |
|---|---|---|
| Admin | Toàn hệ thống | Người dùng, nhà hàng, catalog, cấu hình, procurement, công nợ, Hub và logistics |
| Operations Manager | Toàn hệ thống trong các API được mở | Điều phối Hub, route, xe, đơn, analytics; không quản trị user/catalog/procurement admin |
| Restaurant | Chỉ hồ sơ và dữ liệu nhà hàng của mình | Đặt hàng, theo dõi đơn, xác nhận nhận hàng, sự cố, công nợ, hóa đơn |
| Market Agent | Chỉ Market và batch được phân công | Giá/tồn khả dụng, thu mua, báo ngoại lệ, bàn giao về Hub |
| Hub Staff | Chỉ Hub được phân công | Nhận hàng, đối soát, sorting, outbound, xem và dispatch route của Hub |
| Driver | Chỉ route/delivery được gán cho mình | Nhận bàn giao, pickup, chạy route, giao hàng, POD và sự cố |

Mọi role đã đăng nhập đều có thể dùng hồ sơ cá nhân, thông báo và một số catalog đọc công khai nội bộ.
Quyền nghiệp vụ vẫn được kiểm tra thêm theo ownership, Market assignment, Hub assignment hoặc Driver assignment.

## 2. Luồng end-to-end

```mermaid
sequenceDiagram
    autonumber
    actor Restaurant
    participant System
    actor Admin
    actor Agent as Market Agent
    actor Hub as Hub Staff
    actor Ops as Admin / Operations Manager
    actor Driver

    Restaurant->>System: Tạo Draft, thêm/sửa/xóa item
    Restaurant->>System: Preview và Confirm order
    System->>System: Kiểm tra nhà hàng active, cutoff, credit
    System->>System: Khóa giá, ghi nợ, Order = Confirmed

    System->>System: Auto-batch sau cutoff
    Admin->>System: Hoặc chạy auto-batch thủ công
    System->>System: Gom theo ngày + Market, resolve Hub
    System->>System: Batch = Built, Order = Batched

    Admin->>System: Generate manifest
    Admin->>System: Assign Market Agent
    Agent->>System: Confirm purchase đủ các dòng bắt buộc
    opt Thiếu hàng / hỏng / thay thế
        Agent->>System: Report procurement exception + proof
    end
    Agent->>System: Handover batch về Hub
    System->>System: Batch = HandedOff
    System->>System: Order Batched → PickedUp → AtHub
    System->>System: Tạo Hub inbound PENDING

    Hub->>System: Scan inbound
    System->>System: Kiểm tra Hub assignment + capacity
    System->>System: Inbound = ARRIVED_AT_HUB, tăng inventory
    opt Có lệch số lượng / chất lượng
        Hub->>System: Record discrepancy
        Ops->>System: Acknowledge discrepancy
    end
    Hub->>System: Sort từng order item

    Ops->>System: Calculate Hub → Restaurants
    Ops->>System: Select → Optimize → Review route
    Hub->>System: Xem route/manifest của Hub
    Hub->>System: Check eligibility và assign xe + driver
    System->>System: Kiểm tra driver, xe, lịch xe, tải trọng
    System->>System: Route = assigned

    Hub->>System: Record outbound
    Hub->>System: Create driver handover
    Driver->>System: Checkout handover
    Driver->>System: Confirm pickup với toàn bộ OrderId
    System->>System: Tạo Delivery pending cho từng order

    Driver->>System: Start route
    System->>System: Chặn nếu còn discrepancy OPEN
    System->>System: Route = in_progress, Order = Delivering

    loop Mỗi điểm Restaurant
        Driver->>System: Delivery = ARRIVED
        Driver->>System: Gắn POD nếu có
        alt Giao thành công
            Driver->>System: Delivery = DELIVERED
            System->>System: Order = Delivered
            System->>System: Sinh/xử lý invoice + notification
        else Giao thất bại
            Driver->>System: Delivery = FAILED + reason
            System->>System: Order vẫn Delivering
        end
    end

    System->>System: Mọi delivery terminal → Route = completed
    Restaurant->>System: Confirm receipt
    opt Có vấn đề sau nhận
        Restaurant->>System: Report order issue + ảnh
        Admin->>System: Điều chỉnh số lượng/công nợ khi cần
    end
```

## 3. State machine chính

### 3.1 Order

```mermaid
stateDiagram-v2
    [*] --> Draft: Restaurant tạo đơn
    Draft --> Confirmed: Restaurant confirm
    Draft --> Cancelled: Restaurant/Admin cancel
    Confirmed --> Batched: Procurement batch được build
    Confirmed --> Cancelled: Restaurant/Admin cancel
    Batched --> PickedUp: Market Agent handover
    PickedUp --> AtHub: Cùng integration event handover
    AtHub --> Delivering: Driver start route
    Delivering --> Delivered: Delivery thành công
    Delivered --> [*]
    Cancelled --> [*]
```

Order đã `Batched` không được hủy riêng. Admin chỉ có thể hủy cả batch trước khi Agent mua hàng.

### 3.2 Procurement batch

```mermaid
stateDiagram-v2
    [*] --> Built: Auto-batch
    Built --> Manifested: Admin generate manifest
    Built --> Cancelled: Admin cancel batch
    Manifested --> Manifested: Admin assign/reassign Agent
    Manifested --> Purchasing: Agent confirm purchase
    Manifested --> Cancelled: Admin cancel batch
    Purchasing --> HandedOff: Agent handover về Hub
    HandedOff --> [*]
    Cancelled --> [*]
```

`Confirm purchase` phải chứa đúng một dòng cho mỗi batch item không được miễn bởi exception
`Unavailable`; actual quantity và actual unit price phải lớn hơn 0.

### 3.3 Route

```mermaid
stateDiagram-v2
    [*] --> planned: Admin/Ops calculate Hub → Restaurants
    planned --> planned: Optimize
    planned --> selected: Select
    selected --> selected: Optimize / chỉnh stop order
    selected --> reviewed: Review, bắt buộc đã optimize
    reviewed --> assigned: Assign xe + driver
    assigned --> assigned: Driver reorder trước khi chạy
    assigned --> in_progress: Driver start
    in_progress --> completed: Mọi delivery terminal
    completed --> [*]
```

Route luôn có đúng một Hub stop khớp `HubId`, ít nhất một Restaurant stop và tối đa 20 stop.

### 3.4 Delivery

```mermaid
stateDiagram-v2
    [*] --> pending: Driver confirm pickup đủ đơn
    pending --> arrived: Driver tới Restaurant
    pending --> failed: Driver báo thất bại + reason
    arrived --> delivered: Driver giao thành công
    arrived --> failed: Driver báo thất bại + reason
    delivered --> [*]
    failed --> [*]
```

## 4. Luồng từng Role

### 4.1 Admin

```mermaid
flowchart TD
    A[Đăng nhập Admin] --> B{Nhóm công việc}
    B --> U[User và Restaurant]
    U --> U1[Tạo user / đổi role / activate / unlock]
    U --> U2[Approve / suspend / reactivate Restaurant]
    U --> U3[Gán Market Agent ↔ Market]
    U --> U4[Gán Hub Staff ↔ Hub]

    B --> C[Catalog và cấu hình]
    C --> C1[Market / Product / Category / Unit]
    C --> C2[Market product / Packing code]
    C --> C3[Operational settings / Pricing settings]

    B --> P[Procurement]
    P --> P1[Chạy auto-batch / xem progress]
    P --> P2[Generate manifest]
    P --> P3[Assign Agent hoặc cancel batch]

    B --> L[Hub và Logistics]
    L --> L1[Tạo/sửa/deactivate Hub, Vehicle]
    L --> L2[Calculate → Select → Optimize → Review route]
    L --> L3[Eligibility → assign xe + driver]
    L --> L4[Thực hiện/giám sát Hub inbound, sorting, outbound, handover]

    B --> F[Tài chính và giám sát]
    F --> F1[Credit limit / settlement]
    F --> F2[Orders / invoices / analytics]
    F --> F3[Audit logs]
```

Điểm riêng của Admin:

- Chỉ Admin quản lý user, role, duyệt nhà hàng, catalog ghi, packing code, operational/pricing settings.
- Chỉ Admin điều khiển procurement batch: auto-batch, manifest, assign Agent, cancel batch.
- Admin được bypass Hub assignment khi thao tác Hub/route.

### 4.2 Operations Manager

```mermaid
flowchart TD
    A[Đăng nhập Operations Manager] --> B[Điều phối hằng ngày]
    B --> H[Hub]
    H --> H1[CRUD Hub]
    H --> H2[Gán Hub Staff]
    H --> H3[Inbound / discrepancy / sorting / outbound / handover]

    B --> L[Logistics]
    L --> L1[CRUD vehicle]
    L --> L2[Route suggestion / calculate]
    L --> L3[Select → optimize → review]
    L --> L4[Eligibility → assign xe + driver]

    B --> O[Order operations]
    O --> O1[Xem order toàn hệ thống]
    O --> O2[Ghi actual quantity]
    O --> O3[Advance Batched / PickedUp / AtHub]

    B --> R[Báo cáo]
    R --> R1[Analytics vận hành]
    R --> R2[Invoices]
```

Operations Manager **không** có quyền Admin-only: quản lý user/role, duyệt Restaurant, catalog ghi,
packing code, credit settlement/limit, operational settings và điều khiển procurement batch.

### 4.3 Restaurant

```mermaid
flowchart TD
    A[Đăng ký Restaurant] --> V[Xác thực email]
    V --> P[Chờ Admin approve]
    P --> L[Đăng nhập; response có approvalStatus]
    L --> C[Xem Market, product, giá và tồn]
    C --> D[Tạo Draft]
    D --> I[Thêm / sửa / xóa item]
    I --> Q[Preview confirm]
    Q --> G{Đủ điều kiện?}
    G -- Không --> E[Hiện lỗi approval / cutoff / credit / order]
    E --> I
    G -- Có --> O[Confirm: khóa giá + ghi nợ]
    O --> T[Theo dõi trạng thái và realtime]
    T --> X{Kết quả giao}
    X -- Delivered --> R[Confirm receipt]
    X -- Có vấn đề --> S[Report issue + ảnh]
    R --> H[Xem lịch sử / reorder]

    L --> SO[Scheduled order]
    SO --> SD[System sinh Draft theo lịch]
    SD --> Q

    L --> AI[Assistant]
    AI --> AG[Tool dùng cùng command/query và confirmation gate]
    AG --> D

    L --> F[Tài chính]
    F --> F1[Xem credit và giao dịch]
    F --> F2[Statement / PDF]
    F --> F3[Invoice]
```

Các cổng chặn khi confirm:

- Order phải thuộc Restaurant đang đăng nhập, còn `Draft` và có item.
- Restaurant phải ở trạng thái `Active`; tài khoản pending/suspended vẫn có thể login nhưng không confirm.
- Không vượt hạn mức công nợ; giá được khóa khi confirm.
- Sau cutoff, ngày giao có thể được đẩy sang chu kỳ kế tiếp theo operational settings.

Restaurant chỉ xem/sửa dữ liệu của mình; các endpoint dùng `restaurantId` vẫn kiểm tra ownership.

### 4.4 Market Agent

```mermaid
flowchart TD
    A[Admin/Ops gán Agent vào Market] --> B[Agent xem assigned markets]
    B --> C[Cập nhật giá / lượng khả dụng]
    C --> D[Hệ thống lưu price history + realtime]

    E[Admin generate manifest và assign batch] --> F[Agent xem procurement tasks của mình]
    F --> G[Xem chi tiết batch]
    G --> H{Có ngoại lệ?}
    H -- Có --> I[Upload proof + report unavailable/damaged/substituted]
    H -- Không --> J[Confirm purchase]
    I --> J
    J --> K[Nhập actual quantity + actual unit price cho đủ dòng]
    K --> L[Handover batch]
    L --> M[System resolve Hub từ batch]
    M --> N[Tạo inbound PENDING; Order → AtHub]
```

Các giới hạn:

- Chỉ cập nhật giá/tồn tại Market được gán.
- Chỉ thấy và thao tác procurement batch được gán cho chính Agent.
- Không truyền `HubId` khi handover; batch đã giữ Hub được resolve từ Market lúc batching.
- Không thể handover trước khi batch ở `Purchasing`.

### 4.5 Hub Staff

```mermaid
flowchart TD
    A[Admin/Ops gán Staff vào Hub] --> B[GET hubs/assigned]
    B --> C[Chọn Hub]
    C --> D{Nghiệp vụ}

    D --> I[Inbound]
    I --> I1[Xem pending inbound / procurement plan]
    I1 --> I2[Scan mã inbound]
    I2 --> I3{Hub còn capacity?}
    I3 -- Không --> I4[Chặn HUB_CAPACITY_EXCEEDED]
    I3 -- Có --> I5[ARRIVED_AT_HUB + tăng inventory]
    I5 --> I6[Record discrepancy nếu lệch]

    D --> S[Sorting]
    S --> S1[Xem orders theo Restaurant]
    S1 --> S2[Mark từng order item SORTED]
    S2 --> S3[Xem sorting progress theo Hub + service date]

    D --> R[Route dispatch]
    R --> R1[List route bắt buộc hub_id]
    R1 --> R2[Xem detail / loading manifest / eligibility]
    R2 --> R3{Route đã reviewed và eligible?}
    R3 -- Không --> R4[Admin/Ops phải hoàn thiện route hoặc sửa tải trọng/xe/driver]
    R3 -- Có --> R5[Assign vehicle + driver]

    D --> O[Outbound và handover]
    O --> O1[Record outbound / giảm inventory]
    O1 --> O2[Chọn eligible driver]
    O2 --> O3[Create handover cho route assigned]
```

Hub Staff:

- Chỉ truy cập Hub có assignment active; Admin/Ops bypass giới hạn này.
- Xem được route của Hub khi truyền `hub_id`, nhưng không được calculate/select/optimize/review.
- Được assign xe + driver cho route của Hub sau khi Admin/Ops đã review.
- Không được acknowledge discrepancy; thao tác này chỉ dành cho Admin/Ops.

Điều kiện assign route:

1. Route phải `reviewed`.
2. Driver bắt buộc, tồn tại, active và có role `driver`.
3. Vehicle tồn tại, active, available và không bị gán route khác cùng service date.
4. Số stop không vượt cấu hình.
5. Nếu có order `AtHub`, mọi order phải có packing lines và mọi line phải có `CapacityKg > 0`.
6. Tổng `quantity × capacityKg` không vượt tải trọng xe.

### 4.6 Driver

```mermaid
flowchart TD
    A[Admin/Ops/Hub Staff assign route] --> B[Driver xem routes/today]
    B --> C[Tùy chọn reorder mọi stop trước khi start]
    C --> H[Tùy chọn checkout handover tại Hub]
    H --> P[Confirm pickup]
    P --> V{OrderIds có đúng toàn bộ tập AtHub của route?}
    V -- Không --> X[Chặn PICKUP_ORDERS_INCOMPLETE]
    V -- Có --> D[Tạo delivery pending cho từng order]
    D --> S[Start route]
    S --> G{Có delivery và không còn discrepancy OPEN?}
    G -- Không --> Y[Chặn start]
    G -- Có --> R[Route in_progress; Order Delivering]
    R --> N[Đến điểm giao: ARRIVED]
    N --> POD[Gắn POD nếu có]
    POD --> Z{Kết quả}
    Z -- Thành công --> OK[DELIVERED]
    Z -- Thất bại --> F[FAILED + failure reason]
    OK --> M{Còn delivery chưa terminal?}
    F --> M
    M -- Có --> N
    M -- Không --> E[Route completed]
```

Các cổng bảo mật:

- Driver chỉ xem/chạy route được gán cho user ID trong JWT.
- Confirm pickup phải gửi **đúng và đủ**, không thừa/thiếu, toàn bộ order `AtHub` thuộc các Restaurant
  stop, đúng Hub và service date của route.
- Chỉ route `assigned` mới pickup/reorder/start; chỉ route `in_progress` mới cập nhật delivery.
- Delivery chỉ đi `pending → arrived → delivered` hoặc từ `pending/arrived → failed`.

## 5. Ma trận API theo Role

| Nhóm thao tác | Admin | Ops | Restaurant | Market Agent | Hub Staff | Driver |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| Auth/profile/notification cá nhân | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Quản lý user/role/Restaurant approval | ✓ | — | — | — | — | — |
| Catalog ghi | ✓ | — | — | — | — | — |
| Catalog/Market đọc | ✓ | ✓ | ✓ | ✓ | ✓ | Một phần |
| Cập nhật giá/tồn Market | — | — | — | Market được gán | — | — |
| Order đặt hàng | — | — | Own | — | — | — |
| Order đọc/điều phối | ✓ | ✓ | Own | — | Theo Hub read model | Qua delivery |
| Procurement batch admin | ✓ | — | — | Task được gán | Chỉ plan/inbound | — |
| Hub CRUD/staff assignment | ✓ | ✓ | — | — | Self-list | — |
| Hub operations | ✓ | ✓ | — | Handover vào Hub | Hub được gán | Checkout riêng |
| Route plan/review | ✓ | ✓ | — | — | — | — |
| Route read/eligibility/assign | ✓ | ✓ | — | — | Hub được gán | Route được gán |
| Delivery execution | — | — | Theo dõi/receipt | — | Dispatch | Route được gán |
| Credit settlement/limit | ✓ | — | Chỉ xem | — | — | — |
| Invoice | Toàn hệ thống | Toàn hệ thống | Own | — | — | — |
| Analytics | Toàn bộ | Toàn bộ | Price trends | — | Hub throughput | — |

### API trọng tâm

| Role | API chính |
|---|---|
| Admin | `/api/v1/admin/*`, catalog write, `/hubs`, `/logistics/*`, Hub operations, `/analytics`, `/invoices` |
| Operations Manager | `/hubs`, Hub staff assignments/operations, `/logistics/*`, order ops, `/analytics`, `/invoices` |
| Restaurant | `/auth/register`, `/restaurants/me/*`, `/orders`, `/assistant/chat`, favorites, credit, invoices |
| Market Agent | `/pricing/assigned-markets`, Market price/quantity, `/procurement/tasks/*` |
| Hub Staff | `/hubs/assigned`, inbound/sorting/outbound/handover, scoped `/logistics/routes`, vehicle read |
| Driver | `/driver/*`, `/hubs/{hubId}/handover/{id}/checkout` |

## 6. Menu UI gợi ý

| Role | Menu tối thiểu |
|---|---|
| Admin | Dashboard · Users · Restaurants · Catalog · Markets · Procurement · Hubs · Routes · Fleet · Credit · Invoices · Analytics · Audit |
| Operations Manager | Dashboard · Orders · Hubs · Hub Staff · Routes · Fleet · Deliveries · Invoices · Analytics |
| Restaurant | Market · Cart · Orders · Scheduled Orders · Favorites · Assistant · Credit · Invoices · Profile |
| Market Agent | Assigned Markets · Price & Stock · Procurement Tasks · Exceptions |
| Hub Staff | My Hubs · Pending Inbound · Procurement Plan · Sorting · Routes · Loading Manifest · Outbound · Handovers |
| Driver | Today Routes · Pickup · Current Delivery · Proof/Issues · History |

## 7. Điểm cần biết trong logic hiện tại

1. Order chuyển `Batched → PickedUp → AtHub` ngay khi Market Agent handover; Hub scan xảy ra sau đó.
2. Driver checkout handover và start route là hai luồng chưa ràng buộc: start chưa kiểm tra handover đã
   `CHECKED_OUT`.
3. POD chưa bắt buộc trước khi chuyển delivery sang `DELIVERED`.
4. Delivery `FAILED` làm Order giữ ở `Delivering`; route vẫn `completed` khi mọi delivery đã
   `delivered/failed`, nên cần Ops follow-up cho đơn thất bại.
5. Eligibility chặn vehicle double-book theo ngày nhưng chưa chặn driver bị gán nhiều route cùng ngày.
6. Khi route chưa có order `AtHub`, kiểm tra weight hiện xem là đầy đủ; pickup tạo 0 delivery và start
   sẽ bị chặn bởi `ROUTE_HAS_NO_DELIVERIES`.
7. `RouteStatus.cancelled` tồn tại nhưng chưa có API chuyển route sang trạng thái này.
8. Procurement admin endpoints hiện là Admin-only, Operations Manager không được manifest/assign Agent/cancel batch.
