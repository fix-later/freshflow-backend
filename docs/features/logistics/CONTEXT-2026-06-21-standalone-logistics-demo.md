# Context dự án demo Logistics độc lập

**Loại dự án:** Greenfield demo  
**Phạm vi:** Một hệ thống logistics độc lập, không phụ thuộc bất kỳ dự án hiện hữu nào  
**Mục đích tài liệu:** Làm đầu vào sạch cho phân tích requirement, thiết kế domain, API, database và kế hoạch triển khai

## 1. Bối cảnh

Dự án mô phỏng hoạt động điều phối giao hàng B2B trong một thành phố. Hàng hóa được lấy từ một điểm nguồn, có thể đi qua hub trung chuyển, sau đó được giao đến một hoặc nhiều điểm nhận trên cùng tuyến.

Hệ thống cần hỗ trợ toàn bộ vòng đời cơ bản:

1. Tạo shipment cần giao.
2. Gom các shipment tương thích thành batch.
3. Tính tuyến đường multi-drop.
4. Gán xe, tài xế và giờ xuất phát.
5. Tài xế thực hiện tuyến và cập nhật từng điểm dừng.
6. Điều phối viên theo dõi tiến độ và kết quả giao hàng.

Đây là dự án demo độc lập. Shipment được nhập trực tiếp hoặc tạo từ dữ liệu seed; không lấy dữ liệu từ hệ thống order, catalog, auth hoặc payment bên ngoài.

## 2. Mục tiêu demo

- Minh họa rõ bài toán route planning và delivery execution.
- Có một luồng end-to-end chạy được từ shipment đến delivered.
- Thể hiện multi-drop, vehicle capacity và driver assignment.
- Có trạng thái, lịch sử sự kiện và kiểm soát transition.
- Có REST API và giao diện tối thiểu cho Dispatcher và Driver.
- Có dữ liệu seed để demo mà không cần tích hợp dịch vụ ngoài.
- Domain đủ sạch để sau này thay thuật toán hoặc tích hợp bản đồ thật.

## 3. Actor

### Dispatcher

Người điều phối vận hành hệ thống:

- Quản lý location, hub, vehicle và driver.
- Tạo/import shipment.
- Tạo batch thủ công hoặc chạy auto-batch.
- Tính route plan.
- Gán vehicle, driver và lịch xuất phát.
- Theo dõi delivery run.
- Hủy hoặc điều chỉnh run chưa bắt đầu.

### Driver

Người thực hiện giao hàng:

- Xem các run được phân công.
- Xem danh sách stop theo thứ tự.
- Bắt đầu run.
- Đánh dấu arrived, delivered hoặc failed tại stop.
- Nhập ghi chú và bằng chứng giao hàng tùy chọn.
- Hoàn tất run khi mọi stop đã ở trạng thái kết thúc.

### Recipient Viewer

Vai trò đọc tùy chọn trong demo:

- Xem trạng thái shipment bằng tracking code.
- Xem ETA hiện tại và timeline.
- Không xem thông tin của shipment khác.

## 4. Phạm vi chức năng MVP

### 4.1 Master data

Quản lý các loại location:

- `SOURCE`: điểm lấy hàng hoặc kho nguồn.
- `HUB`: điểm trung chuyển.
- `DESTINATION`: điểm nhận hàng.

Mỗi location có:

- tên
- địa chỉ
- latitude/longitude bắt buộc
- loại location
- trạng thái active/inactive
- service time mặc định tại điểm dừng

Quản lý vehicle:

- biển số duy nhất
- loại xe
- tải trọng tối đa theo kg
- chi phí ước tính trên km
- trạng thái `available`, `in_use`, `maintenance`, `inactive`

Quản lý driver:

- họ tên
- số điện thoại
- số giấy phép lái xe
- trạng thái `available`, `on_duty`, `off_duty`, `inactive`

### 4.2 Shipment

Shipment là đơn vị hàng hóa độc lập cần được giao từ một source đến một destination.

Thông tin bắt buộc:

- tracking code duy nhất
- source location
- destination snapshot: tên người nhận, điện thoại, địa chỉ, latitude/longitude
- service date
- weight kg lớn hơn 0
- delivery zone
- mô tả hàng hóa tùy chọn
- priority: `normal`, `high`, `urgent`
- delivery time window tùy chọn

Shipment lifecycle:

```text
ready_for_planning
    -> batched
    -> assigned
    -> in_transit
    -> delivered | failed

ready_for_planning -> cancelled
batched            -> cancelled
```

Shipment đã `assigned` chỉ được hủy bằng thao tác override của Dispatcher và phải ghi lý do.

### 4.3 Auto-batch

Auto-batch gom các shipment đang `ready_for_planning` theo khóa:

```text
serviceDate + sourceLocationId + deliveryZone
```

Quy tắc:

- Một shipment chỉ thuộc tối đa một active batch.
- Shipment khác ngày giao, source hoặc zone không được cùng batch.
- Batch có thể được tạo thủ công hoặc tự động.
- Auto-batch phải idempotent.
- Hỗ trợ `dryRun` để xem trước mà không ghi dữ liệu.
- Shipment đã batched phải được bỏ qua khi chạy lại.

Batch lifecycle:

```text
open -> planned -> dispatched -> completed
open -> cancelled
planned -> cancelled
```

### 4.4 Route planning

Route plan là kết quả tính toán bất biến cho một batch tại một thời điểm. Nếu đầu vào thay đổi, hệ thống tạo route plan version mới thay vì sửa kết quả cũ.

Hai loại route:

- `DIRECT`: Source -> Destination(s).
- `HUB_RELAY`: Source -> Hub -> Destination(s).

Đầu vào:

- batch ID
- route type
- hub ID nếu là `HUB_RELAY`
- optimization criterion
- vehicle type hoặc vehicle ID tùy chọn
- planned departure time

Optimization criterion:

- `distance`: tối thiểu tổng khoảng cách.
- `time`: tối thiểu thời gian di chuyển cộng service time.
- `cost`: tối thiểu chi phí ước tính.

Mặc định là `distance` để demo có kết quả dễ kiểm chứng.

Kết quả:

- danh sách stop có thứ tự
- tổng khoảng cách
- tổng thời gian ước tính
- tổng chi phí ước tính
- ETA tại từng stop
- tổng tải trọng
- capacity utilization nếu đã chọn vehicle
- các warning như thiếu time-window feasibility hoặc vehicle không đủ tải

Giới hạn demo:

- Tối đa 20 destination stops trong một route.
- Thuật toán nearest-neighbor kết hợp 2-opt.
- Khoảng cách dùng Haversine giữa các tọa độ.
- Thời gian = khoảng cách / vận tốc trung bình cấu hình + service time.
- Chi phí = khoảng cách x vehicle cost/km.
- Không sử dụng traffic thời gian thực.

### 4.5 Delivery run

`RoutePlan` trả lời câu hỏi "đi theo đường nào".  
`DeliveryRun` trả lời câu hỏi "ai chạy, xe nào, lúc nào và kết quả thực tế ra sao".

Một delivery run gồm:

- route plan version
- vehicle
- driver
- planned departure
- actual departure/finish
- ordered run stops được copy từ route plan
- status và timeline

Delivery run lifecycle:

```text
scheduled -> in_progress -> completed
scheduled -> cancelled
in_progress -> cancelled_by_override
```

Quy tắc assignment:

- Vehicle và driver phải active/available.
- Tổng weight không vượt vehicle capacity.
- Vehicle không được có hai run active bị chồng thời gian.
- Driver không được có hai run active bị chồng thời gian.
- Khi run được schedule: shipment chuyển `batched -> assigned`.
- Khi driver bắt đầu run: shipment chuyển `assigned -> in_transit`.

### 4.6 Stop execution

Run stop lifecycle:

```text
pending -> arrived -> completed
pending -> failed
arrived -> failed
pending -> skipped
```

Khi stop destination hoàn tất:

- Các shipment tại stop chuyển sang `delivered`.
- Ghi actual arrival và delivered time.
- Có thể lưu recipient name, note và proof URL.

Khi stop thất bại:

- Driver phải chọn failure reason.
- Shipment chuyển `failed`.
- Dispatcher có thể tạo shipment retry mới; không tái sử dụng shipment đã failed.

Run chỉ được `completed` khi mọi destination stop là `completed`, `failed` hoặc `skipped`.

### 4.7 Tracking và realtime

- Mỗi thay đổi trạng thái tạo một timeline event bất biến.
- Dispatcher dashboard nhận realtime update của run và stop.
- Recipient Viewer chỉ nhận/xem update của tracking code tương ứng.
- REST API là source of truth; realtime event không cần replay khi client offline.
- Demo có thể dùng WebSocket/SignalR/SSE, không bắt buộc công nghệ cụ thể ở bước requirement.

## 5. Domain model đề xuất

### Location

- `id`
- `code`
- `name`
- `type`
- `address`
- `latitude`, `longitude`
- `defaultServiceMinutes`
- `isActive`

### Vehicle

- `id`
- `plateNumber`
- `vehicleType`
- `capacityKg`
- `averageSpeedKmh`
- `costPerKm`
- `status`

### Driver

- `id`
- `fullName`
- `phone`
- `licenseNumber`
- `status`

### Shipment

- `id`, `trackingCode`
- `sourceLocationId`
- destination snapshot
- `serviceDate`, optional time window
- `deliveryZone`
- `weightKg`, `priority`
- `status`
- `batchId?`

### DeliveryBatch

- `id`, `batchNumber`
- `serviceDate`
- `sourceLocationId`
- `deliveryZone`
- `status`
- `totalShipments`, `totalWeightKg`

### RoutePlan

- `id`, `version`
- `batchId`
- `routeType`, `optimizationCriterion`
- `hubLocationId?`
- metrics và ordered stops
- `inputHash`
- `createdAt`

### RoutePlanStop

- `id`, `routePlanId`
- `sequenceNumber`
- `locationType`, `locationId?`
- destination snapshot nếu là điểm giao
- `estimatedArrivalAt`, `estimatedDepartureAt`
- `distanceFromPreviousKm`

### DeliveryRun

- `id`, `runNumber`
- `routePlanId`
- `vehicleId`, `driverId`
- planned/actual timestamps
- `status`
- `createdBy`

### RunStop

- `id`, `deliveryRunId`
- `routePlanStopId`
- `sequenceNumber`
- `status`
- actual timestamps
- failure reason, note, proof URL

### ShipmentEvent

- `id`, `shipmentId`
- `eventType`
- previous/new status
- actor và timestamp
- metadata tùy chọn

## 6. API capability tối thiểu

Không khóa framework hoặc URL chi tiết ở context này. Requirement/API design nên bao phủ các capability sau:

### Dispatcher

- CRUD/list locations.
- CRUD/list vehicles và drivers.
- Create/list/get/cancel shipments.
- Auto-batch preview và execute.
- List/get/cancel batches.
- Calculate và compare route plans.
- Create/list/get/cancel delivery runs.
- Xem dashboard active runs.

### Driver

- List assigned runs.
- Get run detail và ordered stops.
- Start run.
- Mark stop arrived/completed/failed/skipped.
- Complete run.

### Tracking

- Get shipment by tracking code.
- Get shipment timeline và ETA.

## 7. Business invariants

- Tracking code, plate number, batch number và run number là duy nhất.
- Tọa độ phải hợp lệ; location thiếu tọa độ không được route planning.
- Shipment weight phải lớn hơn 0.
- Một shipment chỉ thuộc một active batch.
- Một batch chỉ có shipment cùng service date, source và zone.
- Route plan không được sửa sau khi tạo; thay đổi tạo version mới.
- Sequence number trong route/run là duy nhất và liên tục.
- Vehicle capacity không được vượt quá khi schedule run.
- Driver chỉ cập nhật run được assign cho mình.
- State transition phải được validate ở domain/application layer.
- Mọi status update phải idempotent để driver có thể retry khi mạng yếu.
- Mọi operational timestamp lưu UTC; service date hiển thị theo timezone cấu hình.
- Timeline event không được sửa hoặc xóa.

## 8. Error scenarios cần có

- Shipment/location/vehicle/driver/run không tồn tại.
- Location inactive hoặc thiếu tọa độ.
- Batch rỗng hoặc shipment không đủ điều kiện.
- Shipment đã thuộc batch khác.
- Route vượt giới hạn stop.
- Hub không active.
- Vehicle không đủ tải hoặc không available.
- Driver không available.
- Vehicle/driver bị double booking.
- Invalid state transition.
- Driver cập nhật run của người khác.
- Stop update trùng do client retry.
- Run chưa hoàn tất mọi stop nhưng bị yêu cầu complete.

## 9. Non-functional requirements cho demo

- Route tối đa 20 destination stops hoàn thành trong 3 giây.
- API thông thường phản hồi trong 500 ms với dữ liệu seed/demo.
- Realtime update đến dashboard trong 2 giây.
- Có optimistic concurrency hoặc version check cho run/stop update.
- Có structured logging và correlation ID.
- Có health check.
- Có seed data tái tạo được.
- Có unit test cho state machine, batching và route solver.
- Có integration test cho shipment -> batch -> plan -> run -> delivered.
- Không lưu secret trong source code.

## 10. Dữ liệu demo đề xuất

- 2 source locations.
- 1 hub.
- 8-12 destination locations thuộc 2 delivery zones.
- 3 vehicles có capacity khác nhau.
- 3 drivers.
- 12-20 shipments cho cùng một service date.

Kịch bản trình diễn chính:

1. Chạy dry-run auto-batch.
2. Tạo hai batch theo source/zone.
3. Calculate một DIRECT route và một HUB_RELAY route.
4. So sánh distance/time/cost.
5. Chọn route, gán vehicle và driver.
6. Driver bắt đầu run.
7. Hoàn tất hai stop, đánh dấu một stop failed.
8. Dispatcher thấy dashboard và timeline cập nhật.
9. Tra cứu một shipment bằng tracking code.

## 11. Ngoài phạm vi

- Tích hợp hệ thống order/e-commerce thật.
- Payment, invoice, credit hoặc refund.
- Quản lý kho và tồn kho tại hub.
- Procurement hoặc mua hàng tại source.
- Traffic và bản đồ/navigation thời gian thực.
- Continuous GPS tracking.
- Barcode/QR hardware integration.
- Proof upload storage thật; demo chỉ lưu URL hoặc metadata giả lập.
- Push notification, SMS hoặc email thật.
- Multi-tenant billing.
- Fleet maintenance nâng cao.
- Machine-learning route optimization.

## 12. Các giả định đã chốt cho demo

- Mỗi shipment có đúng một source và một destination.
- Weight luôn được nhập trực tiếp bằng kg.
- Delivery zone là chuỗi do Dispatcher chọn khi tạo shipment.
- Batch được gom theo ngày + source + zone.
- Một run dùng đúng một vehicle và một driver.
- HUB_RELAY trong demo là một tuyến liên tục Source -> Hub -> Destinations.
- Route plan và delivery run là hai entity riêng.
- Stop được lưu normalized để cập nhật độc lập.
- Haversine là đủ cho demo; kết quả không đại diện navigation đường phố thực tế.
- Retry delivery tạo shipment mới liên kết shipment cũ, không hồi sinh shipment failed.
- Authentication chỉ cần RBAC cơ bản cho Dispatcher và Driver.

## 13. Tiêu chí hoàn thành MVP

MVP được xem là hoàn thành khi:

- Có thể tạo và batch shipment hợp lệ.
- Route solver trả ordered stops và metrics ổn định.
- Capacity và assignment conflict được kiểm tra.
- Driver thực hiện được run từ scheduled đến completed.
- Shipment phản ánh đúng delivered/failed theo stop.
- Dashboard hoặc API thể hiện đúng trạng thái và timeline.
- Luồng end-to-end có test tự động.
- Toàn bộ demo chạy bằng seed data mà không cần dịch vụ bên ngoài.

## 14. Prompt dùng để phân tích requirement

> Hãy phân tích requirement cho một dự án demo Logistics greenfield dựa duy nhất trên context này. Không tham chiếu hoặc giả định tồn tại bất kỳ module, database, API hay source code nào từ dự án khác. Hãy tạo actor/use case, user stories, acceptance criteria, domain state machines, API contract, data model, validation, authorization, error codes và test scenarios. Giữ đúng phạm vi MVP; mọi tính năng ngoài mục "Ngoài phạm vi" phải được đánh dấu deferred. Ưu tiên luồng end-to-end Shipment -> Batch -> RoutePlan -> DeliveryRun -> Stop execution -> Tracking.
