# Khảo sát & kế hoạch triển khai: Hub Staff Assignment

| | |
|---|---|
| **Ngày khảo sát** | 2026-07-21 |
| **Branch khảo sát** | `SCRUM-364-statement-pdf` |
| **HEAD khảo sát** | `b0c4222` |
| **Epic liên quan** | `SCRUM-256` — follow-up sau epic Hub |
| **Task key** | `SCRUM-365` |
| **Phạm vi** | Many-to-many Hub ↔ Hub Staff, API quản lý assignment, self-list và enforcement theo Hub |
| **Trạng thái** | Đã implement và verify trên branch `SCRUM-365-hub-staff-assignment`; chờ review/commit |

> Survey và quyết định trong tài liệu là baseline của Jira `SCRUM-365` thuộc epic `SCRUM-256`. Implementation đã hoàn tất ngày 2026-07-21; kết quả được ghi ở §9.

## 1. Kết luận khảo sát hiện trạng

### 1.1 API và RBAC

- `HubsController` hiện chỉ cho `admin,operations_manager` quản lý và xem Hub; chưa có endpoint quản lý nhân sự Hub.
- `HubInboundController` đặt `[Authorize(Roles = "hub_staff,admin,operations_manager")]` ở class level. Vì chỉ kiểm role, mọi tài khoản `hub_staff` hiện gọi được mọi endpoint có `hubId`, không có phép kiểm tra staff ↔ Hub.
- Các flow đang mở cho mọi `hub_staff`: record inbound, pending inbound, inbound history, discrepancy create/list, cross-dock create/list, outbound create/history, handover create/list.
- `POST /api/v1/hubs/scan` không có `hubId` trong route. Handler resolve inbound từ mã scan rồi lấy `inbound.HubId`; hiện không nhận caller identity và không kiểm assignment.
- `POST /api/v1/hubs/{hubId}/handover/{id}/checkout` là driver-only và đã kiểm driver trong JWT khớp handover. Flow này không phải thao tác của Hub Staff và phải giữ nguyên.
- Acknowledge discrepancy chỉ dành cho `admin,operations_manager`; hai role này sẽ bypass assignment.
- Repo chưa có current-user service dùng chung. Convention hiện tại là controller lấy `ClaimTypes.NameIdentifier`/`sub`, rồi truyền user ID và quyền bypass tường minh vào command/query.

### 1.2 Schema và module boundary

- Model/migration hiện có các bảng `hubs`, inbound, inventory, discrepancy, cross-dock, outbound và handover; không có bảng Hub Staff assignment.
- `hubs.managed_by` là một `Guid?` độc lập, không có FK cross-module sang Auth và không phải danh sách nhân viên vận hành.
- Auth sở hữu `users`/`roles`; role `hub_staff` đã tồn tại. Hub không được thêm project reference/FK trực tiếp sang Auth.
- Pattern đọc dữ liệu module khác đã có trong `Hub.Infrastructure/CrossModule`: keyless row + `ToSqlQuery` + application abstraction. Assignment phải dùng cùng seam để xác nhận user tồn tại, chưa bị xoá, đang active và có role `hub_staff`.
- `ApiResponse` đang chuẩn hoá success thành `{ "success": true, "data": ... }`; error thành `{ "success": false, "error": { "code", "message" } }`.

### 1.3 Enforcement hiện tại

- Handler chỉ kiểm Hub/inbound/handover theo ID nghiệp vụ; không handler nào kiểm caller có được gán vào Hub hay không.
- Các operational handler cũng chưa có guard `Hub.IsActive` dùng chung; follow-up phải chặn Hub inactive cùng access guard thay vì chỉ ẩn khỏi self-list.
- Vì vậy `hub_staff` có thể đọc và mutate dữ liệu của bất kỳ Hub nào nếu biết ID. Đây là gap cần đóng cùng assignment; chỉ thêm API gán nhân sự mà không enforce sẽ không giải quyết quyền truy cập.
- Scan là path rủi ro nhất vì Hub chỉ được biết sau khi lookup mã. Guard phải chạy sau khi resolve `inbound.HubId` nhưng trước `ConfirmArrival`, update inventory/capacity hoặc `SaveChangesAsync`.

## 2. Quyết định đã chốt

| Quyết định | Chốt |
|---|---|
| Cardinality | Many-to-many: một Hub có nhiều staff, một staff có thể phụ trách nhiều Hub |
| API quản trị | Hub-centric, `GET/PUT` replace toàn bộ danh sách |
| Rollout | Assignment đi cùng enforcement; không ship bảng/API đơn lẻ |
| Staff chưa assign | Strict: từ chối ngay bằng `HUB_ACCESS_DENIED`/HTTP 403, không có grace period |
| Self-service | Có `GET /api/v1/hubs/assigned` để Hub Staff xem các Hub active của chính mình |
| Bypass | `admin` và `operations_manager` không cần assignment |
| Driver | Checkout và acknowledge discrepancy giữ nguyên contract/RBAC hiện tại |
| `managedBy` | Giữ độc lập; không tự tạo/xoá assignment và không backfill từ cột này |
| Concurrent replace | Last-write-wins; không ETag/version API |

## 3. Public API contract

Tất cả response tiếp tục dùng `ApiResponse`; JSON field theo global camelCase.

### 3.1 Xem assignment của một Hub

`GET /api/v1/hubs/{hubId}/staff-assignments`

- RBAC: `admin,operations_manager`.
- Hub active hoặc inactive đều xem được để assignment vẫn quản trị được sau deactivate.
- 200:

```json
{
  "success": true,
  "data": {
    "hubId": "00000000-0000-0000-0000-000000000001",
    "staffUserIds": [
      "00000000-0000-0000-0000-000000000101",
      "00000000-0000-0000-0000-000000000102"
    ]
  }
}
```

- `HUB_NOT_FOUND` → 404.

### 3.2 Replace assignment của một Hub

`PUT /api/v1/hubs/{hubId}/staff-assignments`

- RBAC: `admin,operations_manager`.
- Request:

```json
{
  "staffUserIds": [
    "00000000-0000-0000-0000-000000000101",
    "00000000-0000-0000-0000-000000000102"
  ]
}
```

- `staffUserIds: []` hợp lệ và xoá toàn bộ assignment của Hub.
- Danh sách phải non-null, không có `Guid.Empty`, không trùng ID. Validate toàn bộ Hub/user trước khi mutate để request lỗi không tạo partial replacement.
- Chỉ user tồn tại, chưa soft-delete, `IsActive = true` và role đúng `hub_staff` được assign.
- Replace chạy atomic trong một transaction. Các replace cùng Hub được serialize trên Hub owner row; request commit sau cùng là tập assignment cuối cùng.
- 200 trả cùng shape `HubStaffAssignmentsResponse` như GET, phản ánh tập đã replace.
- Errors:
  - `VALIDATION_ERROR` → 400: body null/ID rỗng/ID trùng.
  - `HUB_NOT_FOUND` → 404.
  - `USER_NOT_FOUND` → 404: ít nhất một user không tồn tại hoặc đã soft-delete.
  - `INVALID_ASSIGNMENT_TARGET` → 422: user inactive hoặc role khác `hub_staff`.

### 3.3 Staff tự xem Hub được gán

`GET /api/v1/hubs/assigned`

- RBAC: chỉ `hub_staff`.
- User ID chỉ lấy từ JWT, không nhận từ route/query/body.
- 200 dùng `ApiResponse.Ok(...)`, `data` là mảng `HubDto` hiện có; chỉ trả Hub `IsActive = true`, sort ổn định theo `CreatedAt DESC, Id DESC`.
- Chưa được assign trả `data: []`, không phải 403. User đổi role/disable bị auth/RBAC hiện tại chặn trước handler.
- Không pagination ở follow-up này; số Hub trên một staff dự kiến nhỏ. Chỉ thêm pagination khi dữ liệu thực tế chứng minh cần.

### 3.4 Error mapping chung

Thêm `HUB_ACCESS_DENIED` vào `ErrorExtensions` với HTTP 403:

```json
{
  "success": false,
  "error": {
    "code": "HUB_ACCESS_DENIED",
    "message": "You do not have access to this hub."
  }
}
```

401/403 do thiếu token hoặc sai role vẫn do ASP.NET authorization xử lý. `HUB_ACCESS_DENIED` dùng khi caller đã đúng role nhưng không có assignment, hoặc khi một operational flow nhắm tới Hub inactive.

## 4. Persistence plan

### 4.1 Bảng mới

Tạo bảng Hub-owned `hub_staff_assignments`:

| Cột | Kiểu | Constraint |
|---|---|---|
| `hub_id` | `uuid` | NOT NULL; FK nội bộ → `hubs(id)` |
| `user_id` | `uuid` | NOT NULL; ID Auth qua read seam, không FK |

- Composite primary key: `(hub_id, user_id)`; không thêm surrogate `id`.
- Index: `idx_hub_staff_assignments_user_id` trên `user_id` cho self-list và access check.
- Chỉ có FK nội bộ `hub_id`; tuyệt đối không tạo FK sang `users` và không thêm reference từ Hub sang Auth.
- Không thêm timestamp/`assigned_by`: follow-up này không có audit requirement và replace-list không cần metadata từng row.
- Entity/config dùng convention Hub hiện có: sealed class, `IEntityTypeConfiguration<T>`, table/column/index snake_case, đăng ký qua EF assembly discovery.

### 4.2 Repository và cross-module seam

- Thêm `IHubStaffAssignmentRepository`/implementation tối thiểu cho: list theo Hub, replace theo Hub, `IsAssignedAsync(hubId,userId)`, và list Hub active theo user.
- Thêm `IHubStaffReader` ở Application và keyless `HubStaffUserRow`/reader/config ở Infrastructure để đọc `users JOIN roles`. Seam trả đủ `UserId`, `RoleName`, `IsActive`, `DeletedAt` để handler phân biệt `USER_NOT_FOUND` với `INVALID_ASSIGNMENT_TARGET`.
- Thêm một `IHubAccessChecker` dùng repository trên. Mọi handler gọi cùng checker; không copy query assignment vào từng handler.
- Migration chỉ create table/index/FK mới và update snapshot. Không alter dữ liệu Hub hiện có, không backfill từ `hubs.managed_by`, không apply migration vào DB thật trong bước implement.

### 4.3 Strict rollout

- Sau migration, bảng assignment rỗng là trạng thái hợp lệ và có chủ đích.
- Ngay khi code mới chạy, mọi `hub_staff` chưa được assign nhận 403 trên flow Hub; `GET /hubs/assigned` trả mảng rỗng.
- `admin`/`operations_manager` vẫn vận hành được nhờ bypass và dùng PUT để cấp assignment.
- Deactivate Hub hoặc đổi role user không xoá row assignment. Hub inactive không xuất hiện trong self-list và operational access guard trả 403; RBAC chặn role khác `hub_staff`. Assignment còn lại nếu Hub/user được kích hoạt đúng role trở lại.

## 5. Enforcement plan

### 5.1 Cách truyền caller

- Theo convention repo, controller resolve `ActorUserId` từ `NameIdentifier`/`sub` và truyền vào command/query.
- Controller truyền thêm `BypassHubAssignment = User.IsInRole("admin") || User.IsInRole("operations_manager")` cho các endpoint dùng chung ba role.
- Handler kiểm Hub tồn tại, sau đó gọi shared access checker trước mọi query dữ liệu con hoặc mutation. Checker từ chối operational access nếu `Hub.IsActive = false`; admin/operations manager chỉ bypass lookup assignment, không bypass active-state. GET/PUT assignment vẫn quản trị được Hub inactive.
- Với staff không assign, trả `Error.Unauthorized("HUB_ACCESS_DENIED", ...)`; `ErrorExtensions` map code này thành 403.

### 5.2 Ma trận endpoint phải phủ

| Flow | Endpoint | Enforcement |
|---|---|---|
| Inbound create | `POST /hubs/{hubId}/inbound` | Check trước duplicate/read/add |
| Scan inbound | `POST /hubs/scan` | Resolve inbound + Hub, check assignment, rồi mới confirm/mutate inventory |
| Pending inbound | `GET /hubs/{hubId}/pending-inbound` | Check trước query page |
| Inbound history | `GET /hubs/{hubId}/inbound` | Check trước query history |
| Discrepancy create | `POST /hubs/{hubId}/inbound/{inboundId}/discrepancy` | Check trước lookup inbound/order và create |
| Discrepancy history | `GET /hubs/{hubId}/discrepancies` | Check trước query page |
| Acknowledge discrepancy | `POST /hubs/{hubId}/discrepancies/{id}/acknowledge` | Admin/operations manager bypass; hành vi còn lại giữ nguyên |
| Cross-dock create | `POST /hubs/{hubId}/cross-dock` | Check trước lookup inbound/route và create |
| Cross-dock history | `GET /hubs/{hubId}/cross-dock` | Check trước query page |
| Outbound create | `POST /hubs/{hubId}/outbound` | Check trước stock/route reads và mutation |
| Outbound history | `GET /hubs/{hubId}/outbound` | Check trước query history |
| Handover create | `POST /hubs/{hubId}/handover` | Check trước route/outbound reads và create |
| Handover history | `GET /hubs/{hubId}/handovers` | Check trước query page |

Không đổi:

- Hub CRUD/list/detail vẫn `admin,operations_manager`.
- Driver checkout vẫn `driver` và tiếp tục check JWT driver == `handover.DriverUserId`; không chạy Hub Staff assignment check.
- Existing validation, capacity, inventory, discrepancy, concurrency và route checks giữ nguyên sau access guard.

## 6. Implementation breakdown cho SCRUM-365

1. **Persistence:** entity/config/repository `HubStaffAssignment`; DI; migration create-only + snapshot.
2. **User validation:** Auth read seam và replace command/query/validator/DTO.
3. **Public API:** controller cho GET/PUT assignment và GET assigned self-list; JWT extraction, RBAC, envelopes và error mapping.
4. **Shared enforcement:** access checker + actor/bypass fields; nối lần lượt inbound, scan, discrepancy, cross-dock, outbound và handover/history.
5. **Tests và gates:** unit, persistence metadata, controller/RBAC, real-DB integration, build/format/EF no-drift.

Giữ một Jira task/branch vì schema, API và strict enforcement phải deploy cùng nhau; tách task có thể tạo khoảng thời gian assignment tồn tại nhưng không bảo vệ dữ liệu, hoặc enforcement bật trước khi có API cấp quyền.

## 7. Test plan

### 7.1 Unit/validator/handler

- Validator: `hubId` rỗng, list null, `Guid.Empty`, duplicate; empty list hợp lệ để clear.
- GET assignment: Hub không tồn tại → `HUB_NOT_FOUND`; Hub chưa có assignment → list rỗng.
- PUT replace: happy path nhiều staff; replace loại row cũ/thêm row mới; clear; user thiếu → `USER_NOT_FOUND`; wrong role/inactive → `INVALID_ASSIGNMENT_TARGET`; validate tất cả trước mutate; response đúng tập cuối.
- Self-list: nhiều Hub, chỉ Hub active được trả; staff chưa assign → rỗng; cùng staff nhiều Hub; nhiều staff cùng Hub.
- Shared checker: assigned staff pass; unassigned staff → `HUB_ACCESS_DENIED`; admin/operations manager bypass không query assignment.
- Shared checker: Hub inactive → `HUB_ACCESS_DENIED` cho operational flow, kể cả khi có assignment hoặc caller có role bypass.
- Mỗi handler/flow trong ma trận có regression test denied; guard chạy trước repository con/mutation.
- Scan khác Hub: mã resolve được inbound của Hub B nhưng staff chỉ thuộc Hub A → 403; assert `ConfirmArrival`, inventory, occupied capacity và save đều chưa chạy.

### 7.2 Persistence/controller/RBAC

- EF metadata: table/columns/index snake_case; composite PK đúng thứ tự; chỉ một FK tới Hub; không FK tới Auth; index `user_id` tồn tại.
- Repository real provider: replace/clear atomic, access lookup theo composite key, self-list join đúng và không trả Hub inactive.
- Controller: route template, request mapping, success envelope và error envelope.
- RBAC:
  - GET/PUT assignment: admin + operations manager được phép; hub_staff/driver/restaurant bị 403.
  - GET assigned: chỉ hub_staff; unauthenticated 401; role khác 403.
  - Operational endpoints: assigned staff pass, unassigned staff 403, admin/operations manager pass.
  - Driver checkout vẫn pass cho đúng driver và không bị assignment ảnh hưởng.

### 7.3 Integration scenarios

- Multi-Hub/multi-staff: staff A chỉ truy cập Hub A; staff B chỉ Hub B; staff gán cả hai truy cập cả hai.
- PUT replace từ `[A,B]` sang `[B,C]`, rồi `[]`; GET admin và GET self phản ánh đúng mỗi bước.
- Strict rollout: bảng rỗng → hub_staff bị 403 trên inbound/scan/history; admin/operations manager vẫn pass.
- Assignment giữ nguyên sau deactivate; self-list loại Hub inactive. Đổi user khỏi `hub_staff` làm RBAC chặn nhưng không xoá row.
- Scan cross-Hub bị chặn trước mutation và dữ liệu DB không đổi.

### 7.4 Verification gates

Sau implement:

```bash
dotnet build FreshFlow.slnx --no-restore
dotnet test tests/Unit/FreshFlow.Hub.UnitTests/FreshFlow.Hub.UnitTests.csproj --no-restore
dotnet test tests/Integration/FreshFlow.IntegrationTests/FreshFlow.IntegrationTests.csproj --no-restore
dotnet format FreshFlow.slnx --verify-no-changes --no-restore
dotnet ef migrations has-pending-model-changes --project src/FreshFlow.Infrastructure.Persistence --startup-project src/FreshFlow.API
dotnet build-server shutdown
```

Kỳ vọng: build/test/format pass; EF báo không còn pending model changes sau migration; snapshot diff chỉ thêm assignment table/index/FK và cross-module keyless row cần thiết.

## 8. Assumptions và phần cố ý không làm

- Replace dùng last-write-wins; không optimistic version, ETag hoặc merge semantics.
- Không cache assignment. Query DB trực tiếp cho correctness; chỉ thêm cache khi profiling chứng minh cần.
- Không audit event/domain event/notification cho thay đổi assignment.
- Không pagination self-list và không pagination danh sách staff của một Hub.
- Không có API add/remove từng staff; PUT replace-list là contract duy nhất.
- Không đồng bộ `managedBy` với assignment theo bất kỳ chiều nào.
- Không xoá assignment khi Hub deactivated hoặc user đổi role/disable; RBAC và active-state quyết định quyền sử dụng tại thời điểm request.
- Không backfill. Strict rollout yêu cầu admin/operations manager cấp assignment rõ ràng sau deploy.
- Implementation dùng đúng branch/key `SCRUM-365`; migration chỉ được tạo trong source, chưa apply lên database thật và chưa commit.

## 9. Kết quả implementation — 2026-07-21

- Branch: `SCRUM-365-hub-staff-assignment`.
- Đã thêm ba public API theo §3, validation user qua Auth read seam, replace atomic/last-write-wins và self-list chỉ gồm Hub active.
- Đã thêm shared Hub access behavior cho toàn bộ request có `hubId`; scan dùng cùng checker sau khi resolve Hub và trước mutation. Driver checkout và acknowledge discrepancy giữ nguyên.
- Migration create-only: `20260721054533_AddHubStaffAssignments`; không backfill `managed_by`, không có FK sang Auth và chưa apply database.
- `Up`/`Down` của migration không có operation cho Notifications; model snapshot đồng thời đồng bộ metadata `NotificationRecipientRow` đã drift sẵn ở HEAD.
- Verification: build toàn solution 0 lỗi; Hub unit `237/237`; Integration `158/158`; format verify sạch; EF báo không có pending model changes; `git diff --check` sạch.
