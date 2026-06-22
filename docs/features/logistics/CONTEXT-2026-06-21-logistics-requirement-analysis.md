# Context phan tich requirement: Logistics / Van chuyen

**Ngay ra soat:** 2026-06-21  
**Pham vi:** backend FreshFlow hien tai, cac module phu thuoc, migration, test, tai lieu kien truc/requirement/spec va Git history.  
**Muc dich:** dung lam context dau vao cho buoc phan tich requirement; khong duoc coi tat ca noi dung trong tai lieu cu la implementation da ton tai.

## 1. Cach doc context nay

Thu tu uu tien khi cac nguon mau thuan:

1. Source code + EF migration/model snapshot tren branch hien tai: su that `as-is`.
2. `docs/06-context-decisions.md`: quyet dinh nghiep vu moi da ghi nhan.
3. Spec/task gan nhat co acceptance criteria cu the.
4. `docs/01..05` va `specs/001-*`: thiet ke muc tieu/legacy, can xac nhan lai truoc khi code.
5. File `bin/` va `obj/`: build artifact, khong phai source of truth.

## 2. Ket luan nhanh ve hien trang

- `Logistics` hien **chua duoc implement**. Ba project Domain/Application/Infrastructure chi co `.csproj`.
- Chua co entity, repository, service, command/query, validator, controller, DI extension, migration, background job, SignalR hub hay test cho Logistics.
- API co `ProjectReference` den `FreshFlow.Logistics.Infrastructure`, nhung `Program.cs` khong goi `AddLogisticsModule()` va khong map `DeliveryHub`.
- PostgreSQL hien khong co `order_groups`, `vehicles`, `delivery_routes`, `route_stops`, `delivery_schedules` hay `deliveries` trong migration/model snapshot.
- `Hub`, `Notifications` va `Analytics` cung moi la scaffold. Vi vay flow Market -> Hub -> Restaurant, hub QC va delivery analytics chua co backend thuc thi.
- Implementation gan Logistics nhat nam trong `Orders`, `Auth`, `Catalog` va `Pricing`: order lifecycle, dia chi giao, driver role/profile, toa do market, market-product.
- Khong co `FreshFlow.Logistics.UnitTests`; integration tests cung khong co thu muc/test Logistics.

## 3. Kien truc hien tai

- .NET 10 modular monolith, mot ASP.NET Core host, mot PostgreSQL database va mot `AppDbContext` dung chung.
- Moi bounded context duoc tach Domain/Application/Infrastructure. Cross-module application coupling du kien qua `FreshFlow.Contracts` + MediatR integration events hoac interface/read projection.
- EF configuration duoc scan tu cac assembly da load. Module phai dang ky assembly va dependencies trong `Program.cs`.
- Domain event duoc dispatch **sau khi SaveChanges thanh cong**. Loi handler sau commit bi log va swallow; v1 khong co outbox.
- Host hien chi dang ky Auth, Catalog, Pricing, Orders; chi map `/hubs/pricing` va `/hubs/orders`.
- Tai lieu mo ta Redis SignalR backplane, nhung host hien chi `AddSignalR()`; chua thay `AddStackExchangeRedis()` trong `Program.cs`.

He qua cho Logistics:

- Event-driven integration hien la in-process, khong dam bao delivery/retry neu handler fail sau commit.
- Logistics khong nen tham chieu truc tiep Application/Domain cua module khac; can contract/read model ro rang.
- Kien truc noi Logistics khong duoc tu y sua `orders.status`; Orders phai la owner cua order lifecycle.

## 4. Ngu canh nghiep vu da duoc mo ta

FreshFlow la nen tang B2B thu mua thuc pham tu cho dau moi cho nha hang tai TP.HCM. Logistics bat dau sau khi order duoc nha hang confirm va du kien gom don luc 22:00 theo gio `Asia/Ho_Chi_Minh`.

Actor du kien:

- **Admin:** quan ly fleet, batch, route, schedule va van hanh logistics. Quyet dinh moi noi khong co role `Logistics Operator` rieng.
- **Driver:** xem route duoc giao, cap nhat trang thai stop/delivery.
- **Hub Staff:** scan nhap hub, QC, ghi missing/damaged/partial va gate truoc khi driver xuat phat.
- **Restaurant:** nhan hang, xem ETA/status, xac nhan receipt va report issue.
- **Market Agent:** cap nhat gia/so luong va thuc hien thu mua; chua co logistics API ro rang trong code.

Out of scope v1 da ghi nhan:

- Continuous GPS/telematics va lich su GPS dai han.
- Fleet maintenance/advanced fleet optimization.
- Cold-chain IoT.
- Multi-city ngoai TP.HCM.
- Tu dong xu ly su co xe hong; Admin xu ly thu cong.

## 5. Dau vao da co tu cac module khac

### 5.1 Orders

Order lifecycle dang chay:

`draft -> confirmed -> batched -> picked_up -> at_hub -> delivering -> delivered`

Nhanh huy chi cho:

`draft -> cancelled` hoac `confirmed -> cancelled`.

Du lieu hien co tren `Order`:

- `RestaurantId`
- `OrderGroupId?` (chi co cot/property; chua co bang va nghiep vu group)
- `ScheduledOrderId?`
- `ScheduledFor?`
- `Status`, `PaymentStatus`, `TotalAmount`, notes
- items gom `MarketProductId`, product name snapshot, integer quantity, unit price, locked price/total, actual quantity.

Khoang 22:00 hien chi duoc dung de normalize `ScheduledFor` khi confirm:

- Confirm truoc 22:00: earliest delivery D+1.
- Confirm tu 22:00: earliest delivery D+2.
- Gioi han toi da D+7.

Day **khong phai** auto-batching job. Chua co job gom don luc 22:00.

`OrderGroupId` chua the gan qua mot domain method sau khi order duoc tao. `AdvanceStatus(Batched)` co ton tai nhung chua co command/API nao goi cho logistics.

### 5.2 Event contract hien co

Orders publish sau commit:

- `OrderConfirmedIntegrationEvent(OrderId, RestaurantId, TotalAmount, OccurredAt)`
- `OrderCancelledIntegrationEvent(OrderId, RestaurantId, CancellationReason, OccurredAt)`

Comment trong code noi Logistics se tao/huy delivery tu hai event nay, nhung hien khong co consumer.

Payload confirm **khong co**:

- delivery address/snapshot/toa do
- `ScheduledFor`/delivery date/window
- order items, unit, weight
- source market ID
- delivery zone
- order group/batch ID

Do do event hien tai khong du de tao route/delivery dung nghiep vu. Logistics se phai co read interface/query model hoac contract moi.

### 5.3 Dia chi nha hang

Auth da co CRUD `delivery_addresses`:

- recipient name, phone, address line
- latitude/longitude nullable
- `IsDefault`
- soft delete

Nhung create/confirm order khong nhan `deliveryAddressId`, khong snapshot dia chi vao order. Neu dia chi bi sua/xoa sau confirm, khong co ban ghi bat bien de giao dung dia diem da chot.

`restaurants` hien co address va pickup start/end, nhung khong co `delivery_zone`; toa do giao hang nam o `delivery_addresses`, khong nam tren order.

### 5.4 Market va source market

- `Market` co name, address, latitude, longitude va active flag.
- `MarketProduct` co `MarketId` + `ProductId`.
- Order item chi luu `MarketProductId`.
- Read projection cua Orders cho market product hien **bo qua `MarketId`**.
- Mot order co the them nhieu `MarketProductId` tu nhieu market; code khong enforce single-market order.

Vi vay rule "group by source market" chua co semantics khi mot order gom item tu nhieu market.

### 5.5 Trong luong va capacity

- Product chi tham chieu `UnitOfMeasurement` (kg, bunch, crate... theo catalog).
- Khong co `weightKgPerUnit`, conversion factor, volume hay dimension.
- Order quantity la integer va khong snapshot unit/conversion.
- Vi vay chua the tinh `totalWeightKg`, vehicle utilization hay enforce `VEHICLE_CAPACITY_EXCEEDED` mot cach dung.

### 5.6 Driver

- Role `driver` da duoc seed va Admin co the tao driver user.
- Khi tao driver, Auth tao `driver_profiles` gom `UserId`, `LicensePlate?`, `PhoneNumber?`.
- Chua co driver status, license number, current vehicle, FK den user, API cap nhat profile, route assignment hay delivery authorization.
- Model hien tai khac ca hai schema tai lieu.

## 6. Scope Logistics du kien trong tai lieu

### 6.1 Auto-batch tien de

- 22:00 moi ngay, lay order `confirmed` chua batch.
- Group theo `deliveryZone + sourceMarketId`.
- Tao `OrderGroup`/Procurement Batch va chuyen order sang `batched`.
- Admin co the trigger cung service qua `POST /api/v1/admin/order-groups/auto-batch`.
- Ho tro `targetDate`, `dryRun`, `force`; phai idempotent va an toan khi cron/manual chay dong thoi.
- Response du kien co created/batched/skipped counts va chi tiet tung group.

Luu y: day la scope cua **Orders**, nhung la dependency bat buoc truoc route/schedule Logistics.

### 6.2 Route planning

Tai lieu yeu cau hai loai route:

- `DIRECT`: Market -> Restaurant(s).
- `HUB_RELAY`: Market(s) -> Hub(s) -> Restaurant(s).

Yeu cau du kien:

- Multi-drop, toi da 20 stops.
- Optimization criterion: `DISTANCE`, `TIME`, `COST`; default `COST`.
- Luon tra total distance, duration va estimated cost.
- SLA route calculation <= 3 giay.
- Thuat toan du kien: nearest-neighbor + 2-opt; OR-Tools la upgrade path.
- Cache Redis theo hash cua stop IDs + criterion, TTL 1 gio.

Chua co dinh nghia:

- distance/travel-time matrix lay tu Haversine, OSRM, Google/Mapbox hay du lieu tinh.
- cost function cho `COST`.
- service time tai market/hub/restaurant.
- time window va traffic theo thoi diem.
- route co the chua nhieu source market hay phai tach theo market.

### 6.3 Vehicle, assignment va schedule

Tai lieu du kien Admin co the:

- CRUD/register vehicle: plate, capacity kg, type, active/available status.
- Assign vehicle va optional driver cho route.
- Chan double booking theo ngay/khoang thoi gian.
- Tao delivery schedule: route + vehicle + order group + planned departure.
- Enforce capacity va tra utilization percent.
- Schedule lifecycle: `scheduled -> in_progress -> completed | cancelled`.

Chua co quyet dinh ro route da chua vehicle hay vehicle chi nam o schedule. API legacy bat `vehicleId` ngay khi calculate route, trong khi FR khac noi calculate truoc roi assign sau.

### 6.4 Driver execution va delivery

Tai lieu moi yeu cau:

- Driver xem route hom nay va cac stop theo thu tu.
- Driver cap nhat `ARRIVED`, `DELIVERED`, `FAILED` cho delivery duoc assign.
- Driver khong duoc sua delivery cua driver khac.
- Restaurant nhan SignalR update rieng cho order cua minh trong multi-drop route.
- Restaurant co flow xac nhan receipt/report issue da ton tai o Orders, nhung chi cho order da o `delivered`.

DBML con de xuat signature URL va current location, nhung cac field nay khong duoc xac nhan la v1; continuous GPS da duoc ghi out of scope.

### 6.5 Hub/QC dependency

Quyet dinh nghiep vu moi:

1. Hang vao hub khoang 05:00, Hub Staff scan.
2. Missing/damaged/partial duoc ghi truoc dispatch.
3. Discrepancy chua resolve/acknowledge se chan driver depart.
4. Sau gate moi chuyen order sang `delivering`.

Hub module hien chua implement. Neu MVP chon `HUB_RELAY`, Logistics khong the hoan tat end-to-end neu khong chot contract Hub va thu tu trang thai.

## 7. Schema du kien dang mau thuan

Khong nen generate migration truoc khi chot mot canonical model.

### `docs/03-database-schema.md`

- `vehicles`
- `delivery_routes` co ordered stops trong `route_metadata JSONB`, co `vehicle_id`, `order_group_id`
- `deliveries` la mot row/order/route, co `sequence_number`
- Khong co `route_stops`, khong co `delivery_schedules`

### `docs/03-database-schema.dbml`

- `vehicles` dung status enum
- `delivery_routes` co route date, driver, vehicle, procurement batch/order group
- `route_stops` normalized, unique `(route_id, sequence_number)` va unique `order_id`
- `deliveries` tro den `route_stop`, co driver, departed/arrived/delivered, signature va current location
- Khong co bang schedule rieng

### `docs/02-system-architecture.md`

- Noi `RouteRepository` persist `delivery_routes + route_stops`
- Noi co `delivery_schedules` rieng de link route + vehicle + order group + departure
- Noi route calculation persistence xu ly async, trong khi API legacy tra `routeId` va mo ta route da persisted/cached

### `specs/001-freshflow-platform/data-model.md`

- Quay lai JSONB `route_metadata`
- `deliveries` tro thang den route/order
- Khong co schedule du chinh spec coi Delivery Schedule la key entity

## 8. API contract dang mau thuan

- Requirement dung `/api/logistics/routes/calculate`; API design dung `/api/v1/routes/calculate`.
- Vehicle create co luc la `/api/admin/vehicles`, `/api/v1/vehicles`, hoac `/api/v1/admin/vehicles`.
- FR noi calculate bang sourceMarketIds/hubIds/destinationRestaurantIds; API design noi calculate bang orderIds + vehicleId.
- FR noi calculate route roi assign vehicle; API design bat vehicle khi calculate.
- FR co schedule va driver endpoints; `docs/04-api-design.md` endpoint summary chi co calculate/list/detail routes va create/list vehicles.
- FR driver status dung `ARRIVED/DELIVERED/FAILED`; contract khac dung `picked_up/in_transit/delivered/failed`.
- `docs/04-api-design.md` van dung Restaurant Manager/Staff membership, trong khi DEC-003 va code hien tai chi co mot role `restaurant` va quan he one user -> one restaurant.
- RBAC tai lieu cho Operations Manager quyen logistics, nhung DEC-005 noi merge vao Admin; code van seed ca hai role.

## 9. State ownership can chot

De tranh hai source of truth:

- **Orders owns:** order, order items, order group/batch, order status.
- **Logistics owns:** vehicle, route, stop/delivery execution, driver/vehicle assignment, schedule neu co.
- **Auth owns:** user, role, driver identity/profile, restaurant/delivery addresses.
- **Catalog/Pricing owns:** market coordinates, product/unit, market-product/source market.
- **Hub owns:** hub, inbound/outbound, QC/discrepancy, inventory.
- **Notifications/realtime:** chi broadcast; khong quyet dinh business transition.
- **Analytics:** cross-module read only.

Logistics khong nen update truc tiep `orders.status`. Can mot integration contract/Orders command idempotent cho cac transition nhu:

- Batch created -> `confirmed -> batched`
- Procurement departed market -> `batched -> picked_up`
- Hub received/QC -> `picked_up -> at_hub`
- Driver departed -> `at_hub -> delivering`
- Stop delivered -> `delivering -> delivered`

Nguon phat tung transition va dieu kien gate phai duoc requirement chi ro.

## 10. Cac gap/blocker bat buoc chot truoc implementation

1. **MVP route scope:** DIRECT, HUB_RELAY hay ca hai? Co compare mode khong?
2. **Canonical API namespace va request shape:** calculate tu `orderGroupId`, `orderIds`, hay raw stop IDs?
3. **Canonical persistence:** JSONB stops, normalized `route_stops`, hay hybrid?
4. **Route vs schedule:** co can entity `delivery_schedule` rieng hay route chinh la lich chay?
5. **Address selection:** order phai chon `deliveryAddressId` luc tao/confirm va snapshot field nao?
6. **Geocoding:** co bat buoc lat/lng cho market/hub/delivery address? Xu ly dia chi thieu toa do the nao?
7. **Delivery zone:** dinh nghia/quan ly bang enum, district, polygon hay derived cluster?
8. **Multi-market order:** cam, split thanh procurement lines/batches, hay mot order thuoc nhieu source market?
9. **Weight model:** kg/unit conversion, product net weight, crate/bunch handling va rounding.
10. **Capacity semantics:** tinh theo ordered hay actual quantity; weight hay unit/volume; cho over-capacity override khong?
11. **Optimization data:** distance/time provider, cost formula, traffic/time window/service duration.
12. **Vehicle availability:** status vs booking calendar; conflict overlap; maintenance/inactive semantics.
13. **Driver assignment:** route hay schedule; mot/multiple driver; driver status/current vehicle; reassign mid-route.
14. **Delivery granularity:** delivery = order, restaurant stop, route leg hay order-at-stop?
15. **Status mapping/source:** route, schedule, stop, delivery va order states; ai duoc transition state nao?
16. **Hub gate:** unresolved discrepancy block route/schedule/stop nao; Admin override va audit ra sao?
17. **Cancellation/replanning:** confirmed order huy truoc batch da co event; sau batch khong cho huy. Xu ly route da calculate khi batch/order thay doi?
18. **Failure flow:** FAILED co terminal khong; retry/redelivery, reason catalog, partial delivery, proof of delivery.
19. **ETA updates:** tinh mot lan hay recalculate; event nao cap nhat `estimatedDeliveryAt` dang null trong OrderHub?
20. **Idempotency/concurrency:** cron vs manual batch, double calculate, double assign, double status update, driver offline retry.
21. **Reliable integration:** chap nhan best-effort MediatR hien tai hay can outbox/inbox cho flow van hanh quan trong?
22. **Audit:** ai tao/sua route, assign/reassign, status history, manual override, proof va timestamp.
23. **Security:** RBAC canonical; resource-level authorization cho driver va restaurant; SignalR group isolation.
24. **Timezone:** storage UTC; business date/cutoff/schedule/report theo `Asia/Ho_Chi_Minh`.
25. **NFR:** data volume, peak 02:00-09:00, <=20 stops, <=3s calculate, realtime latency, retention.

## 11. Invariant toi thieu nen dua vao requirement

- Mot order chi thuoc toi da mot active order group.
- Mot order chi co toi da mot active delivery execution tai mot thoi diem.
- Stop sequence trong mot route la duy nhat va lien tuc.
- Driver chi update delivery duoc assign.
- Vehicle khong co hai active schedule overlap.
- Route/schedule khong start neu vehicle/driver inactive hoac hub QC gate chua dat.
- `DELIVERED` phai idempotent; retry request khong tao duplicate event/credit action.
- Restaurant chi thay order/delivery cua minh, ke ca route multi-drop.
- Dia chi giao va item/unit/weight can cho van chuyen phai la snapshot tai moc nghiep vu da chot.
- Order status transition chi qua Orders boundary.
- Tat ca operational timestamps luu UTC, business-date logic dung Vietnam timezone.

## 12. De xuat baseline de phan tich requirement

Day la baseline hop ly nhat tu code hien tai, nhung van can Product Owner xac nhan:

1. Hoan tat Orders auto-batching truoc Logistics.
2. Order chon va snapshot delivery address khi confirm.
3. Chot single-market order cho MVP, hoac mo hinh procurement batch item-level neu can multi-market.
4. Them weight conversion de capacity co y nghia.
5. Logistics nhan `orderGroupId`, resolve immutable planning read model, calculate route.
6. Dung normalized stops neu can driver cap nhat tung stop; JSONB chi luu optimization metadata/raw solver output.
7. Dung schedule rieng neu route template co the tai su dung/doi vehicle/departure; neu khong, gop schedule vao route de giam scope.
8. Driver cap nhat stop/delivery; Logistics phat integration event; Orders thuc hien transition idempotent.
9. DeliveryHub phat per-restaurant sau DB commit; REST van la source of truth khi reconnect.
10. MVP khong continuous GPS; chi manual `arrived/delivered/failed` + timestamps/reason/proof optional.

## 13. Khong duoc tu suy dien khi viet requirement

- Khong coi Logistics da co code chi vi `.csproj` va DLL trong `bin/obj` ton tai.
- Khong coi `docs/03-database-schema.md` hay DBML la canonical khi chung dang mau thuan.
- Khong gia dinh order chi co mot source market; code hien khong enforce.
- Khong gia dinh quantity = kg.
- Khong gia dinh default delivery address la dia chi cua order.
- Khong gia dinh 22:00 cutoff job da ton tai; hien chi co schedule normalization khi confirm.
- Khong gia dinh Redis backplane/outbox/retry da duoc cau hinh.
- Khong gia dinh Hub/Notifications/Analytics da san sang de tich hop.
- Khong dua payment gateway/refund gateway cu vao flow; quyet dinh moi la B2B credit/cong no.

## 14. File tham chieu chinh

### Source of truth hien tai

- `src/Modules/Logistics/**`: scaffold only.
- `src/FreshFlow.API/Program.cs`: module/hub wiring hien tai.
- `src/Modules/Orders/FreshFlow.Orders.Domain/Entities/Order.cs`: order state machine.
- `src/Modules/Orders/FreshFlow.Orders.Application/Services/OrderCutoffScheduler.cs`: cutoff semantics hien tai.
- `src/Shared/FreshFlow.Contracts/OrderConfirmedIntegrationEvent.cs`
- `src/Shared/FreshFlow.Contracts/OrderCancelledIntegrationEvent.cs`
- `src/Modules/Auth/FreshFlow.Auth.Infrastructure/CrossModule/DeliveryAddressRow.cs`
- `src/Modules/Auth/FreshFlow.Auth.Domain/Entities/DriverProfile.cs`
- `src/Modules/Pricing/FreshFlow.Pricing.Domain/Entities/MarketProduct.cs`
- `src/Modules/Catalog/FreshFlow.Catalog.Domain/Entities/Market.cs`
- `src/FreshFlow.Infrastructure.Persistence/Migrations/AppDbContextModelSnapshot.cs`

### Intended/legacy design can doi chieu

- `docs/06-context-decisions.md`
- `docs/01-requirements-spec.md` section FR-LOG/FR-HUB/FR-NOT
- `docs/02-system-architecture.md` section Logistics/Realtime
- `docs/03-database-schema.md`
- `docs/03-database-schema.dbml`
- `docs/04-api-design.md` section Logistics/DeliveryHub/RBAC
- `specs/001-freshflow-platform/tasks.md` T035 va T039-T043
- `specs/002-pricing-hub-admin-batch/spec.md`

## 15. Prompt ngan de feed cho agent phan tich requirement

> Hay phan tich requirement module Logistics cua FreshFlow dua tren context nay. Phan biet ro `as-is`, `target`, `conflict` va `open decision`. Khong coi tai lieu schema/API legacy la source of truth khi no mau thuan code hoac context decisions. Truoc khi viet user story/AC, hay chot domain ownership, canonical state machines, order address/source-market/weight snapshots, route-vs-schedule model, Hub dependency, API namespace, idempotency va authorization. Uu tien MVP end-to-end co the test duoc; danh dau ro scope deferred.
