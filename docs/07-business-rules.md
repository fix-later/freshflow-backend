# 07 — Business Rules Catalogue

> **Nguồn sự thật: mã nguồn.** Tài liệu này được rà soát trực tiếp từ code (`src/`), không phải từ
> các doc thiết kế cũ. Ở đâu doc `01`–`05` mâu thuẫn với bảng dưới, **code thắng**.
>
> Rà soát ngày **2026-08-18**, nhánh `dev`, commit `f347f53`. **119 business rule.**
>
> Quy ước ID: `BR-<MODULE>-<nnn>`. Mỗi dòng là **một luật gộp** — các ràng buộc cùng chủ đề được
> gom lại, nên một dòng có thể liệt kê nhiều điểm enforce và nhiều mã lỗi.
> Cột **Enforce tại** là nơi luật được *thực sự* chặn (domain entity / handler / validator / DB),
> không phải nơi nó được mô tả. Cột **Mã lỗi** là `Error.Code` trả về cho client —
> `ErrorExtensions.ToActionResult()` map theo allow-list: `_NOT_FOUND` → 404,
> `VALIDATION_ERROR` → 400, lỗi xác thực → 401, `FORBIDDEN` → 403, xung đột → 409,
> phần lớn lỗi nghiệp vụ/validation → 422, `ACCOUNT_LOCKED` → 423; mã không đăng ký → 500.

---

## Mục lục

| # | Nhóm | Số luật |
|---|---|---|
| 0 | [Luật xuyên suốt](#0-luật-xuyên-suốt-cross-cutting) | 8 |
| 1 | [Auth — tài khoản, nhà hàng, phân quyền](#1-auth--tài-khoản-nhà-hàng-phân-quyền) | 12 |
| 2 | [Catalog — danh mục, sản phẩm, quy cách](#2-catalog--danh-mục-sản-phẩm-quy-cách) | 5 |
| 3 | [Pricing — bảng giá chợ](#3-pricing--bảng-giá-chợ) | 7 |
| 4 | [Orders — đơn hàng, đơn định kỳ, khiếu nại](#4-orders--đơn-hàng-đơn-định-kỳ-khiếu-nại) | 23 |
| 5 | [Credit — công nợ B2B](#5-credit--công-nợ-b2b) | 9 |
| 6 | [Procurement — phiên chợ & lô mua](#6-procurement--phiên-chợ--lô-mua) | 13 |
| 7 | [Hub — nhập kho, phân loại, giao ca](#7-hub--nhập-kho-phân-loại-giao-ca) | 11 |
| 8 | [Logistics — tuyến, xe, giao hàng](#8-logistics--tuyến-xe-giao-hàng) | 14 |
| 9 | [Invoicing — hóa đơn VAT](#9-invoicing--hóa-đơn-vat) | 5 |
| 10 | [Notifications](#10-notifications) | 3 |
| 11 | [Analytics](#11-analytics) | 4 |
| 12 | [AI Assistant](#12-ai-assistant) | 5 |
| — | [Phụ lục A — tham số cấu hình được](#phụ-lục-a--tham-số-cấu-hình-được) | |
| — | [Phụ lục B — máy trạng thái](#phụ-lục-b--máy-trạng-thái) | |
| — | [Phụ lục C — integration events](#phụ-lục-c--integration-events) | |
| — | [Phụ lục D — khoảng trống & cảnh báo](#phụ-lục-d--khoảng-trống--cảnh-báo) | |
| — | [Phụ lục E — bảng business rule cho SRS](#phụ-lục-e--bảng-business-rule-cho-srs) | 119 |
| — | [Appendix F — business rules (English, SRS)](#appendix-f--business-rules-english-srs) | 119 |

---

## 0. Luật xuyên suốt (cross-cutting)

| ID | Luật | Enforce tại |
|---|---|---|
| BR-GEN-001 | Mọi PK là `UUID` (`gen_random_uuid()`). **Soft-delete không phải quy ước toàn cục**: chỉ lọc `deleted_at IS NULL` khi EF configuration thực sự map cột này. Các ngoại lệ đã xác nhận gồm `order_items`, `scheduled_order_items`, `restaurant_favorites`, `operational_settings`, `restaurant_credit`, `price_snapshots` và `refresh_tokens`; với bảng khác phải kiểm tra configuration tương ứng. | các EF configuration; `OrderItemConfiguration`, `ScheduledOrderItemConfiguration`, `RestaurantFavoriteConfiguration`, `OperationalSettingsConfiguration`, `RestaurantCreditConfiguration` |
| BR-GEN-002 | Timestamp lưu **UTC** (`timestamptz`); ngày nghiệp vụ là **`Asia/Ho_Chi_Minh`**, chuyển đổi tại biên handler. Cột kiểu `date` (`batch_date`, `service_date`) đã là ngày nghiệp vụ — **không** convert lại. | `VietnamTime.cs`, `OrderCutoffScheduler` |
| BR-GEN-003 | Business rule **không** ném exception — trả `Result<T>` với `Error`; exception chỉ cho lỗi hệ thống. Mã lỗi chưa đăng ký trong `ErrorExtensions.ToActionResult()` rơi xuống **500**, nên luôn tái dùng mã đã có. | `SharedKernel/Result.cs`, `ErrorExtensions` |
| BR-GEN-004 | Mọi request đi qua `ISender` (MediatR) để `ValidationBehavior` chạy. Bỏ qua `ISender` ⇒ validator **không chạy** (xem BR-AI-004). | `Program.cs`, `ValidationBehavior` mỗi module |
| BR-GEN-005 | Ghi đồng thời trên aggregate được bảo vệ bằng optimistic concurrency (`xmin`); xác nhận đơn còn chạy trong transaction **SERIALIZABLE**. Xung đột trả mã yêu cầu client retry: `OPTIMISTIC_CONCURRENCY_CONFLICT`, `SERIALIZATION_CONFLICT`. | `*Repository.cs`, `OrderRepository.ExecuteInSerializableTransactionAsync` |
| BR-GEN-006 | Chỉ **5 vai trò** tồn tại: `admin`, `market_agent`, `restaurant`, `hub_staff`, `driver`. Tên khác trong `[Authorize]` → **403 vĩnh viễn, build vẫn xanh**. `[Authorize]` xếp chồng theo **AND**. | Migration `20260606152229_AddRolesTable` |
| BR-GEN-007 | Rate limit: Auth **10**, Orders **30**, Assistant **15** request/phút. Phân trang mặc định `pageSize = 20`, trần **100** (Auth, Orders, Invoicing, lịch sử giá) hoặc **200** (Catalog, Hub, Logistics, Notifications) tùy query. | `appsettings.json → RateLimiting`, các `*QueryValidator.cs` |
| BR-GEN-008 | **Không có `FromSqlRaw` trong repo.** Đọc chéo module đi qua keyless Row + `ToSqlQuery` tĩnh, filter tham số hóa áp ở LINQ. Cấm nối chuỗi vào SQL; sort/group do client chọn phải qua allow-list `switch`. | `{Module}.Infrastructure/CrossModule/`, `OrderQueryParsing`, `ListRoutesQueryHandler` |

### 0.1 Ma trận vai trò (tổng hợp từ `[Authorize(Roles=…)]`)

| Vai trò | Phạm vi chính |
|---|---|
| `admin` | Toàn quyền — quản trị user/role **và** toàn bộ vận hành: phiên chợ, lô mua, tuyến, hub, cấu hình |
| `restaurant` | Đơn của chính mình, địa chỉ giao, công nợ, khiếu nại, yêu thích, hóa đơn |
| `market_agent` | Bảng giá của chợ **được gán**; nhận & xác nhận mua của phiên chợ được gán |
| `hub_staff` | Nhập/xuất kho, phân loại, giao ca; **đọc** xe + tuyến, gán xe toàn đội |
| `driver` | Tuyến được gán cho chính mình: nhận hàng, cập nhật giao hàng, POD |

---

## 1. Auth — tài khoản, nhà hàng, phân quyền

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-AUTH-001 | Đăng nhập bằng email **hoặc** SĐT + mật khẩu. Sai → thông báo chung, không tiết lộ tài khoản có tồn tại hay không; khi identifier không tồn tại vẫn hash một mật khẩu giả để **chống timing attack**. | `LoginCommandHandler`, `DummyPasswordHash` | `INVALID_CREDENTIALS` |
| BR-AUTH-002 | Sai mật khẩu **5 lần** → khóa **15 phút**. Tài khoản bị vô hiệu hóa không đăng nhập được. | `User.MaxFailedAttempts=5`, `User.LockDuration=15m` | `ACCOUNT_LOCKED`, `ACCOUNT_INACTIVE` |
| BR-AUTH-003 | Access token TTL **15 phút**, refresh token TTL **7 ngày**. Refresh **dùng một lần** (rotation); dùng lại token đã rotate → **thu hồi toàn bộ phiên** của user. Refresh không hợp lệ / hết hạn / user đã bị vô hiệu hóa đều bị từ chối. `refresh_tokens` append-only, thu hồi bằng `revoked_at`. | `RefreshTokenCommandHandler`, `appsettings → JWT` | `REFRESH_TOKEN_REUSE`, `TOKEN_INVALID`, `REFRESH_TOKEN_EXPIRED`, `UNAUTHORIZED` |
| BR-AUTH-004 | v1 chỉ hỗ trợ kênh **EMAIL**. OTP xác thực email sống **10 phút**, token reset mật khẩu sống **15 phút**, cả hai **dùng một lần**. | `VerificationCode.CodeTtl`, `PasswordResetToken.TokenTtl` | `CHANNEL_NOT_SUPPORTED`, `OTP_INVALID`, `RESET_OTP_INVALID` |
| BR-AUTH-005 | Đổi mật khẩu phải nhập đúng mật khẩu hiện tại và mật khẩu mới **phải khác** mật khẩu cũ. Mật khẩu lưu bằng **BCrypt work factor 12**. | `ChangePasswordCommandHandler`, `BCryptPasswordHasher` | `INVALID_CURRENT_PASSWORD`, `VALIDATION_ERROR` |
| BR-AUTH-006 | **Email và số điện thoại đều là duy nhất toàn hệ thống** — kiểm cả khi đăng ký, khi admin tạo user, lẫn khi cập nhật hồ sơ. | `RegisterRestaurantCommandHandler`, `CreateUserCommandHandler`, `UpdateMyProfileCommandHandler` + unique index | `EMAIL_ALREADY_EXISTS`, `PHONE_ALREADY_EXISTS` |
| BR-AUTH-007 | Định dạng: SĐT `^\+?[0-9]{7,15}$`, mã số thuế `^\d{10}(-\d{3})?$`. Độ dài: `FullName` ≤255, `Phone` ≤20, `Email` ≤256, `Address` ≤500, `ContactPerson` ≤200, URL ≤512. | các `*CommandValidator.cs` | `VALIDATION_ERROR` |
| BR-AUTH-008 | Vòng đời nhà hàng `Pending → Active ⇄ Suspended`: đăng ký ở `Pending` phải được admin duyệt; chỉ `Active` mới đình chỉ được; chỉ `Suspended` mới kích hoạt lại được; không duyệt/đình chỉ hai lần. | `ApproveRestaurantCommandHandler`, `SuspendRestaurantCommandHandler`, `ReactivateRestaurantCommandHandler` | `ALREADY_APPROVED`, `NOT_ACTIVE`, `ALREADY_SUSPENDED`, `NOT_SUSPENDED` |
| BR-AUTH-009 | Nhà hàng **chưa duyệt hoặc đang đình chỉ không được đặt đơn, xác nhận đơn, hay tạo lịch định kỳ**. | `CreateDraftOrderCommandHandler`, `ConfirmOrderCommandHandler`, `CreateScheduledOrderCommandHandler` | `RESTAURANT_NOT_APPROVED`, `RESTAURANT_NOT_ACTIVE` |
| BR-AUTH-010 | Admin **không thể tự vô hiệu hóa tài khoản của chính mình**; chỉ gán được role có trong bảng `roles`. | `ActivateUserCommandHandler`, `AssignRoleCommandHandler` | `CANNOT_DEACTIVATE_SELF`, `INVALID_ROLE` |
| BR-AUTH-011 | Chỉ user role `market_agent` mới gán được vào chợ, và chỉ gán vào chợ **tồn tại + active**. | `ReplaceMarketAssignmentsCommandHandler`, `CreateUserCommandHandler` | `INVALID_ASSIGNMENT_TARGET`, `INVALID_MARKET` |
| BR-AUTH-012 | Địa chỉ giao: `Latitude ∈ [-90,90]`, `Longitude ∈ [-180,180]`; khung nhận hàng `PickupEnd > PickupStart`; mỗi nhà hàng tối đa **một** địa chỉ `IsDefault`. Cột `restaurants.status` lưu **lowercase snake_case** (`active`), khác các cột PascalCase cùng bảng. | `AddDeliveryAddressCommandValidator`, `UpdateRestaurantProfileCommandValidator`, `RestaurantConfiguration` | `VALIDATION_ERROR` |

---

## 2. Catalog — danh mục, sản phẩm, quy cách

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-CAT-001 | Tên **danh mục**, tên **đơn vị tính** và mã **packing code** đều là duy nhất. | `CreateCategoryCommandHandler`, `CreateUnitCommandHandler`, `CreatePackingCodeCommandHandler` | `CATEGORY_NAME_CONFLICT`, `UNIT_NAME_CONFLICT`, `PACKING_CODE_CONFLICT` |
| BR-CAT-002 | Cây danh mục **tối đa 2 cấp**: cha phải là danh mục **gốc + active**; danh mục không thể là cha của chính nó; danh mục **đã có con** không thể trở thành danh mục con. | `CreateCategoryCommandHandler`, `UpdateCategoryCommandHandler` | `INVALID_CATEGORY_PARENT` |
| BR-CAT-003 | Không vô hiệu hóa danh mục còn **danh mục con đang active**. | `DeactivateCategoryCommandHandler` | `CATEGORY_HAS_ACTIVE_CHILDREN` |
| BR-CAT-004 | Sản phẩm phải trỏ tới đơn vị / danh mục / packing code **tồn tại và active**. | `CreateProductCommandHandler` | `INVALID_UNIT`, `INVALID_CATEGORY`, `INVALID_PACKING_CODE` |
| BR-CAT-005 | Packing code: mã `^[A-Z]+$` ≤ **8 ký tự**, `CapacityKg` **> 0, số nguyên, ≤ `Logistics:Box:MaxLoadKg` (25 kg)**. Độ dài: mô tả sản phẩm ≤2000, danh mục ≤1000, packing ≤500, `ImageUrl` ≤512, `Abbreviation` ≤20. `products.VatRate` là nguồn tạo **snapshot thuế** khi Orders chốt giá; Invoicing đọc snapshot này từ `order_items`. | `CreatePackingCodeCommandValidator`, các validator, `OrderPricingCalculator`, `OrderInvoiceRowConfiguration` | `VALIDATION_ERROR` |
| BR-CAT-006 | Không vô hiệu hóa sản phẩm còn **niêm yết đang active** ở bất kỳ chợ nào (tránh mồ côi reservation ở Pricing). | `DeactivateProductCommandHandler`, `IMarketListingReader` | `PRODUCT_HAS_ACTIVE_LISTINGS` |

---

## 3. Pricing — bảng giá chợ

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-PRC-001 | Một sản phẩm chỉ được niêm yết **một lần** tại mỗi chợ. | `CreateMarketProductCommandHandler` + unique index | `MARKET_PRODUCT_ALREADY_EXISTS` |
| BR-PRC-002 | Giá **> 0**, **≤ `Pricing:MaxPriceVnd` (50.000.000 VND)**, tối đa 2 chữ số thập phân; số lượng khả dụng **≥ 0**; `SearchText` khi tìm kiếm bắt buộc và ≤200 ký tự. | `UpdateProductPriceCommandHandler`, `CreateMarketProductCommandValidator`, `SearchMarketProductsQueryValidator` | `INVALID_PRICE`, `INVALID_QUANTITY`, `VALIDATION_ERROR` |
| BR-PRC-003 | **Market agent chỉ sửa được giá / tồn của chợ mình được gán**; admin không bị ràng buộc này. | `UpdateAvailableQuantityCommandHandler` | `MARKET_ACCESS_DENIED` |
| BR-PRC-004 | Mỗi `market_product` gắn tối đa **8 tag** và tag phải tồn tại; tên tag duy nhất, ≤ **30 ký tự**, chuẩn hóa trước khi so trùng. | `MarketProduct.MaxTags`, `Tag.MaxNameLength`, `SetMarketProductTagsCommandHandler`, `CreateTagCommandHandler` | `TAG_NAME_CONFLICT`, `VALIDATION_ERROR` |
| BR-PRC-005 | Mỗi lần đổi giá ghi một dòng **`price_snapshots`** — append-only, không soft-delete, **không partition**. Lịch sử theo `marketProductId` trả tối đa **100** bản ghi. | `PriceSnapshotRepository` | — |
| BR-PRC-006 | Bảng giá cache Redis **TTL 5 phút**; đổi giá invalidate cache và broadcast SignalR tới group `market:{marketId}`. | `RedisPriceBoardCache.Ttl`, `PricingHub` | — |
| BR-PRC-007 | Cập nhật giá/tồn dùng optimistic concurrency. Giá tham chiếu trên bảng giá được **đồng bộ từ lô mua đã xác nhận**. | `UpdateAvailableQuantityCommandHandler`, `ProcurementPurchaseConfirmedIntegrationEventHandler` | `OPTIMISTIC_CONCURRENCY_CONFLICT` |
| BR-PRC-008 | Không thể hạ `CurrentQuantity` xuống dưới `ReservedQuantity` đã giữ cho đơn hàng đã xác nhận; không thể xóa niêm yết còn `ReservedQuantity` **> 0**. | `MarketProduct.ApplyUpdate`, `UpdateAvailableQuantityCommandHandler`, `UpdateProductPriceCommandHandler`, `DeleteMarketProductCommandHandler` | `QUANTITY_BELOW_RESERVED`, `MARKET_PRODUCT_HAS_RESERVED_STOCK` |

---

## 4. Orders — đơn hàng, đơn định kỳ, khiếu nại

### 4.1 Vòng đời

```
Draft ──► Confirmed ──► Batched ──► PickedUp ──► AtHub ──► Delivering ──► Delivered
  │            │
  └────────────┴──► Cancelled
```

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-ORD-001 | Chuyển trạng thái chỉ theo bảng `AllowedTransitions`; mọi bước nhảy khác bị chặn. | `Order.AllowedTransitions` | `ORDER_INVALID_TRANSITION` |
| BR-ORD-002 | Hủy đơn chỉ từ `Draft` hoặc `Confirmed`. **Ngoại lệ:** khi cả phiên chợ bị hủy, đơn ở `Batched` cũng hủy theo (`CancelWithSession`) — hợp lệ vì phiên chỉ hủy được trước khi agent mua hàng. | `Order.Cancel()`, `Order.CancelWithSession()` | `ORDER_NOT_CANCELLABLE` |
| BR-ORD-003 | Chỉ đơn `Draft` mới được thêm/sửa/xóa item và áp giá. | `Order.AddItem/UpdateItem/RemoveItem/ApplyPricing` | `ORDER_NOT_DRAFT` |
| BR-ORD-004 | Địa chỉ giao chốt **một lần duy nhất** khi đơn còn `Draft`. | `Order.CaptureDeliveryAddress()` | `DELIVERY_ADDRESS_ALREADY_CAPTURED` |
| BR-ORD-005 | Xác nhận đơn yêu cầu: đơn thuộc nhà hàng đang đăng nhập, nhà hàng đã duyệt, trạng thái `Draft`, và **có ít nhất 1 item**. | `ConfirmOrderCommandHandler` | `FORBIDDEN`, `RESTAURANT_NOT_ACTIVE`, `ORDER_NOT_DRAFT`, `ORDER_EMPTY` |
| BR-ORD-006 | **Một đơn (và một lịch định kỳ) chỉ chứa sản phẩm của một chợ duy nhất.** | `OrderConfirmationService`, `CreateScheduledOrderCommandHandler` | `ORDER_MARKET_MISMATCH` |
| BR-ORD-007 | Admin chỉ đẩy tay được sang `batched`, `picked_up`, `at_hub`. | `AdvanceOrderStatusCommandHandler` | `ORDER_STATUS_NOT_ADVANCEABLE` |
| BR-ORD-008 | Xác nhận đã nhận hàng chỉ sau khi đơn `Delivered`, và chỉ **một lần**. | `Order.ConfirmReceipt()` | `ORDER_NOT_DELIVERED`, `ORDER_RECEIPT_ALREADY_CONFIRMED` |

### 4.2 Cutoff & cửa sổ giao hàng

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-ORD-009 | **Cutoff hằng ngày 22:00 giờ VN** (`operational_settings.daily_cutoff_time`). Ngày giao sớm nhất = **D+1** nếu xác nhận **trước** cutoff, **D+2** nếu **từ** cutoff trở đi. Ngày yêu cầu bị **nâng lên** ngày hợp lệ sớm nhất nếu để trống hoặc quá sớm — không báo lỗi. | `OrderCutoffScheduler` | — |
| BR-ORD-010 | Cận trên: ngày giao ≤ **D + `delivery_window_days`** (mặc định **7**, admin cấu hình 1–30) và không ở quá khứ. | `OrderCutoffScheduler.IsWithinDeliveryWindow` | `DELIVERY_DATE_OUT_OF_WINDOW` |

### 4.3 Định giá

```
subtotal    = Σ(item.Subtotal)
vatAmount   = Σ round(item.Subtotal × vatPercent / 100, 2, AwayFromZero)   // theo từng item
distanceKm  = round(roadDistanceKm, 2, AwayFromZero)
rawFee      = max(BaseFee + distanceKm × RatePerKm, MinimumFee)
deliveryFee = RoundingUnit == 0 ? round(rawFee, 2) : round(rawFee / RoundingUnit) × RoundingUnit
totalAmount = subtotal + vatAmount + deliveryFee
```

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-ORD-011 | Thuế suất map từ `products.VatRate`: `"5"`→5%, `"8"`→8%, `"10"`→10%; rỗng/null → **`KCT` 0%**; mã hợp lệ khác (`KCT`, `KKKNT`, `0`) → 0%. Kết quả được chốt vào từng `order_item`; `VAT_RATE_MISSING` chỉ xảy ra nếu `Order.ApplyPricing` không nhận được snapshot thuế cho item. | `OrderPricingCalculator.ResolveTax`, `Order.ApplyPricing` | `VAT_RATE_MISSING` |
| BR-ORD-012 | Phí giao tính theo **quãng đường đường bộ thật** (Goong); API lỗi → fallback Haversine × `FallbackRoadFactor 1.4`. Thiếu tọa độ địa chỉ giao hoặc chợ nguồn, hoặc thiếu snapshot quãng đường đầy đủ → chặn xác nhận. Mọi tham số phí và quãng đường phải **≥ 0**. | `GoongRoadDistanceProvider`, `RoadDistanceCalculator`, `OrderPricingCalculator`, `Order` | `DELIVERY_COORDINATES_REQUIRED`, `INVALID_DELIVERY_SNAPSHOT`, `INVALID_DELIVERY_FEE`, `INVALID_DELIVERY_DISTANCE`, `HAVERSINE_FALLBACK` |
| BR-ORD-013 | Ràng buộc số lượng theo sản phẩm: tổng số lượng của một market product phải **≥ `MinimumOrderQuantity`**, và nếu sản phẩm có `PackingWeightKg > 0` thì phải là **bội số** của nó. | `OrderPricingCalculator`, `ValidateQuantity` | `MINIMUM_ORDER_QUANTITY_NOT_MET`, `PACKING_QUANTITY_MISMATCH` |
| BR-ORD-014 | Mặc định `DeliveryFeePerKm = 5.000 VND/km`, `BaseFee = MinimumFee = RoundingUnit = 0`. Admin sửa trong khoảng: fee/km 0–1.000.000, base/min 0–10.000.000, rounding 0–1.000.000, precision (14,2). | `OperationalSettings`, `UpdateOperationalSettingsCommandValidator` | `VALIDATION_ERROR` |

### 4.4 Cổng xác nhận đơn — thứ tự kiểm tra

`ConfirmOrderCommandHandler` chạy đúng thứ tự sau; lỗi đầu tiên chặn cả giao dịch:

1. Đơn tồn tại → `ORDER_NOT_FOUND`
2. Đơn thuộc nhà hàng đang đăng nhập → `FORBIDDEN`
3. Nhà hàng đã được duyệt → `RESTAURANT_NOT_ACTIVE`
4. Trạng thái `Draft` → `ORDER_NOT_DRAFT`
5. Có ít nhất 1 item → `ORDER_EMPTY`
6. Tính quãng đường đường bộ → `DELIVERY_COORDINATES_REQUIRED`
7. **Mở transaction SERIALIZABLE:** cùng một chợ → hạn mức công nợ → cửa sổ giao hàng → phiên chợ → giữ chỗ tồn kho → ghi nợ
8. Input định tuyến đổi giữa chừng → `ROUTING_INPUTS_CHANGED`

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-ORD-015 | Phải có **phiên chợ khả dụng, đang mở, còn sức chứa** cho ngày giao (khi `Orders:MarketSessions:Enforce = true`). | `OrderConfirmationService` | `MARKET_SESSION_NOT_AVAILABLE`, `MARKET_SESSION_NOT_OPEN`, `MARKET_SESSION_CAPACITY_EXCEEDED` |
| BR-ORD-016 | Tồn kho được **giữ chỗ (reserve) ngay khi xác nhận**, không phải khi mua; và được **giải phóng** khi lô mua bị hủy. | `OrderRepository.TryReserveStockAsync`, `ProcurementBatchCancelledIntegrationEventHandler` | `INSUFFICIENT_STOCK`, `STOCK_RESERVATION_CONFLICT` |
| BR-ORD-017 | Nếu item / xuất xứ sản phẩm / địa chỉ giao đổi trong lúc xác nhận → hủy giao dịch, yêu cầu thử lại. | `OrderConfirmationService` | `ROUTING_INPUTS_CHANGED` |

### 4.5 Item, tồn kho, số liệu thực mua

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-ORD-018 | `Quantity` mỗi item **> 0**; sản phẩm phải còn khả dụng; tổng số lượng không vượt tồn khả dụng (kiểm cả lúc thêm item lẫn lúc xác nhận). **`order_items` không có soft-delete** — lọc `deleted_at` trên bảng này lỗi runtime. | `AddOrderItemCommandHandler`, `UpdateOrderItemCommandHandler`, `CreateDraftOrderCommandHandler`, `OrderItemConfiguration` | `VALIDATION_ERROR`, `INVALID_PRODUCT`, `INSUFFICIENT_STOCK` |
| BR-ORD-019 | Ghi số lượng thực mua: `ActualQuantity ≥ 0` và ≤ số lượng đặt; `ActualQuantity > 0` thì `ActualUnitPrice` phải > 0; chỉ áp được khi đơn ở `Batched`. Ghi chú đơn / lý do hủy ≤ **500 ký tự**. | `Order.ApplyProcurementActuals()`, validators | `INVALID_ACTUAL_QUANTITY`, `INVALID_ACTUAL_UNIT_PRICE`, `ORDER_NOT_BATCHED` |

### 4.6 Đơn định kỳ

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-ORD-020 | Tạo lịch định kỳ yêu cầu nhà hàng **đã duyệt**, `RecurrenceType ∈ {daily, weekly}`, `FirstRunAt` ở **tương lai**, sản phẩm **một chợ**. Không thao tác được trên lịch đã hủy / không active. | `CreateScheduledOrderCommandHandler`, `ScheduledOrder` | `RESTAURANT_NOT_APPROVED`, `VALIDATION_ERROR`, `SCHEDULED_ORDER_FIRST_RUN_IN_PAST`, `ORDER_MARKET_MISMATCH`, `SCHEDULED_ORDER_NOT_ACTIVE`, `SCHEDULED_ORDER_ALREADY_CANCELLED` |
| BR-ORD-021 | Lịch mang theo **template sản phẩm + địa chỉ giao**; job (chạy mỗi **60s**) sinh đơn thật và **tự xác nhận**. Lỗi hoặc lịch cũ rỗng → hạ cấp thành `Draft` + phát `ScheduledOrderNeedsAttentionIntegrationEvent`. Mỗi lần chạy xử lý tối đa **366 lần đáo hạn** cho mỗi lịch. | `ScheduledOrderGenerationService`, `ScheduledOrderGenerationHostedService` | — |

### 4.7 Sự cố & khiếu nại

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-ORD-022 | **Sự cố** chỉ báo cho đơn **`Delivered`**; loại ∈ {`missing`, `wrong`, `damaged`}; `AffectedQuantity` > 0 và ≤ số lượng đã đặt; mô tả 1–**1000** ký tự; sự cố đã `Resolved` không xử lý lại. | `ReportOrderIssueCommandHandler`, `OrderIssue` | `ORDER_ISSUE_NOT_ALLOWED`, `VALIDATION_ERROR`, `INVALID_ISSUE_QUANTITY`, `ORDER_ISSUE_ALREADY_RESOLVED` |
| BR-ORD-023 | **Khiếu nại** chỉ mở khi đơn ở `AtHub`/`Delivered`; số tiền > 0 và **không vượt số đã ghi nợ cho đơn**; claim đã ở trạng thái cuối không đổi được; **từ chối bắt buộc có ghi chú**, **duyệt bắt buộc có người duyệt + giao dịch hoàn tiền**. Lý do ≤500, ghi chú quyết định ≤1000, `ProofImageUrl` ≤2000. **Hoàn tiền = điều chỉnh công nợ, không qua cổng thanh toán.** | `FileClaimCommandHandler`, `OrderClaim`, `CreditService.RefundAsync` | `CLAIM_ORDER_NOT_CLAIMABLE`, `INVALID_CLAIM_AMOUNT`, `CLAIM_INVALID_TRANSITION`, `INVALID_CLAIM_DECISION_NOTE`, `VALIDATION_ERROR` |

---

## 5. Credit — công nợ B2B

> Mô hình thanh toán là **công nợ**, **không** phải thanh toán từng đơn qua cổng.
> Điều này ghi đè mô tả trong `docs/01` (FR-ORD-008).

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-CRE-001 | `AvailableCredit = CreditLimit − OutstandingBalance`. Ghi nợ được phép khi `amount > 0` **và** `OutstandingBalance + amount ≤ CreditLimit`. | `RestaurantCredit.CanCharge` | `CREDIT_LIMIT_EXCEEDED` |
| BR-CRE-002 | **Ghi nợ tại thời điểm xác nhận đơn** (charge-at-confirm), không phải khi giao xong. | `OrderConfirmationService` | — |
| BR-CRE-003 | Mọi khoản (charge / settle / refund / adjust) phải **> 0**. | `CreditService` | `INVALID_AMOUNT` |
| BR-CRE-004 | Hạn mức phải **≥ 0** và **không được đặt thấp hơn dư nợ hiện tại**. | `CreditService.SetCreditLimitAsync` | `INVALID_CREDIT_LIMIT`, `CREDIT_LIMIT_BELOW_OUTSTANDING_BALANCE` |
| BR-CRE-005 | Thanh toán / hoàn tiền không vượt dư nợ; hoàn tiền cho một đơn không vượt **số còn lại đã ghi nợ cho chính đơn đó**. | `RestaurantCredit.Settle/Refund`, `CreditService` | `CREDIT_REFUND_EXCEEDS_ORDER_CHARGE` |
| BR-CRE-006 | Bút toán thanh toán bắt buộc có **người ghi nhận + số tham chiếu**, tham chiếu **duy nhất theo nhà hàng** (chống ghi trùng). | `CreditService.SettleAsync` | `INVALID_SETTLEMENT_DETAILS`, `CREDIT_SETTLEMENT_DUPLICATE_REFERENCE` |
| BR-CRE-007 | Cảnh báo hạn mức: **≥ 80%** → `Warning`, **≥ 100%** → `Exceeded`; mỗi mức chỉ phát sự kiện **một lần** cho tới khi tụt xuống mức thấp hơn. | `RestaurantCredit.LastAlertedLevel` | — |
| BR-CRE-008 | Loại giao dịch `Charge(1)` / `Settlement(2)` / `Refund(3)` / `Adjustment(4)`; phương thức `BankTransfer(1)` / `Manual(2)`. | enums | — |
| BR-CRE-009 | Sao kê theo **tháng dương lịch giờ VN** (`PeriodStart` inclusive, `PeriodEnd` exclusive), **hạn thanh toán = kết thúc kỳ + 15 ngày**, chỉ phát hành khi **kỳ đã kết thúc hoàn toàn**, và là **snapshot bất biến**. Job quét mỗi **60 phút**; truy vấn giới hạn năm 2020–2100, tháng 1–12; nhà hàng chỉ xem dữ liệu của chính mình. | `CreditStatementPeriodCalculator`, `CreditStatementGenerationService`, `MonthlyCreditStatementHostedService` | `STATEMENT_PERIOD_NOT_CLOSED`, `STATEMENT_GENERATION_CONFLICT`, `FORBIDDEN`, `VALIDATION_ERROR` |

---

## 6. Procurement — phiên chợ & lô mua

> **Phiên chợ = `ProcurementBatch`.** Không có entity `Session` riêng. Tiếng Việt dùng
> **"phiên chợ"** (không dùng "lô chợ"); code dùng `ProcurementBatch`.

### 6.1 Phiên chợ (`MarketSession`) — `Draft → Open → Closed`

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-PROC-001 | Phiên cần **chợ + ngày phục vụ + `ClosesAt` (UTC, phải ở tương lai)**. | `MarketSession` | `INVALID_MARKET_SESSION`, `INVALID_MARKET_SESSION_CLOSE_TIME` |
| BR-PROC-002 | Phiên **sẵn sàng** yêu cầu: **hub active**, `PlannedCapacityKg > 0` (≤1.000.000), **≥1 xe**, **≥1 market agent**. | `MarketSession`, `ConfigureMarketSessionResourcesCommandValidator` | `HUB_NOT_CONFIGURED_FOR_MARKET`, `INVALID_MARKET_SESSION_RESOURCES` |
| BR-PROC-003 | Không mở phiên **sau cutoff** hoặc khi còn cảnh báo chưa xử lý. | `MarketSession.Open()`, `OpenMarketSessionCommandHandler` | `MARKET_SESSION_CUTOFF_PASSED`, `MARKET_SESSION_NOT_READY` |
| BR-PROC-004 | Phiên **đã đóng** không sửa được nữa; lý do đóng ≤500 ký tự; sửa phiên dùng optimistic concurrency; truy vấn yêu cầu `From ≤ To` và `Status ∈ {draft, open, closed}`. | `MarketSession`, `CloseMarketSessionCommandHandler`, `UpdateMarketSessionCommandHandler`, `GetMarketSessionsQueryHandler` | `MARKET_SESSION_CLOSED`, `MARKET_SESSION_CONFLICT`, `VALIDATION_ERROR` |

### 6.2 Gom lô

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-PROC-005 | Chỉ **phiên đã đóng** mới gom lô được, và phiên phải có **hub** đã cấu hình. Một phiên / một đơn chỉ thuộc **một lô đang hoạt động** tại một thời điểm. | `BatchConfirmedOrdersService` | `MARKET_SESSION_NOT_CLOSED`, `HUB_NOT_CONFIGURED_FOR_MARKET`, `ORDER_ALREADY_IN_ACTIVE_GROUP` |
| BR-PROC-006 | Job gom lô chạy mỗi **60s** (bật/tắt bằng `Procurement:Batching:Enabled`). Reset lô chỉ khi lô **và** các đơn của nó **chưa vượt quá giai đoạn batching**. | `ProcurementBatchingHostedService`, `ProcurementBatchRepository` | `BATCH_RESET_NOT_ALLOWED` |

### 6.3 Vòng đời lô — `Built → Manifested → Purchasing → HandedOff → Completed` / `Cancelled`

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-PROC-007 | Lô cần **mã lô, ngày, chợ và ít nhất một dòng sản phẩm dương**; số lượng gộp một sản phẩm không vượt ngưỡng hỗ trợ; chỉ nhận thêm đơn khi lô ở trạng thái cho phép merge. | `ProcurementBatch` | `INVALID_PROCUREMENT_BATCH`, `BATCH_NOT_MERGEABLE` |
| BR-PROC-008 | **Lập manifest yêu cầu mọi market product trong lô đều có giá tham chiếu**, và phải manifest **trước** khi gán agent và trước khi xác nhận mua. | `ProcurementBatch.Manifest()` | `REFERENCE_PRICE_MISSING`, `BATCH_NOT_MANIFESTABLE`, `BATCH_NOT_MANIFESTED` |
| BR-PROC-009 | Agent được gán phải là `market_agent` **đang active, được gán vào chợ đó và vào chính phiên đó**; danh sách gán phải có `marketProductId` duy nhất; agent chỉ thao tác trên **item của chính mình**. | `AssignBatchItemsCommandHandler`, `ProcurementBatch` | `AGENT_NOT_ELIGIBLE`, `AGENT_NOT_ASSIGNED_TO_SESSION`, `ITEM_NOT_ASSIGNED_TO_AGENT`, `VALIDATION_ERROR` |
| BR-PROC-010 | Xác nhận mua: sản phẩm phải thuộc lô và **chưa mua**, phải có **đúng một dòng cho mỗi item không miễn trừ được gán cho agent**, `ActualQuantity > 0` và `ActualUnitPrice > 0`. | `ProcurementBatch.ConfirmPurchase()`, `ConfirmPurchaseCommandValidator` | `PRODUCT_NOT_IN_BATCH`, `ITEM_ALREADY_PURCHASED`, `PURCHASE_LINES_MISMATCH`, `INVALID_PURCHASE_LINE` |
| BR-PROC-011 | Ngoại lệ (`Unavailable`/`Shortfall`/`PriceDiscrepancy`/`Damaged`) chỉ báo ở trạng thái cho phép; số lượng **không âm**; ghi chú ≤500 ký tự. | `ProcurementBatch.ReportException()`, validator | `BATCH_NOT_REPORTABLE`, `INVALID_EXCEPTION_QUANTITY`, `INVALID_EXCEPTION_TYPE` |
| BR-PROC-012 | **Bàn giao** yêu cầu: lô đã mua xong, **mọi item không miễn trừ đã được xử lý**, hub đã xác định và **đang active**; không bàn giao hai lần; dùng transaction + optimistic concurrency. | `ProcurementBatch.HandOff()`, `HandoverBatchCommandHandler`, `ProcurementBatchRepository` | `BATCH_NOT_PURCHASED`, `BATCH_INCOMPLETE`, `HUB_NOT_CONFIGURED_FOR_MARKET`, `HUB_INACTIVE`, `BATCH_ALREADY_HANDED_OFF`, `PROCUREMENT_HANDOVER_CONFLICT`, `SERIALIZATION_CONFLICT` |
| BR-PROC-013 | Bàn giao đẩy **số lượng/giá thực mua** sang Orders — các đơn được phủ phải còn tồn tại và **vào hub cùng nhau**. Lô chuyển `HandedOff → Completed` khi **chuyến giao cuối cùng hoàn tất**. Hủy lô chỉ trước khi mua và khi đơn chưa đi tiếp; lô đã hủy thì mọi thao tác đều bị chặn. | `ProcurementBatchHandedOffIntegrationEventHandler`, `ProcurementBatch.Complete()/Cancel()` | `PROCUREMENT_ORDER_MISSING`, `PROCUREMENT_ORDER_STATE_CONFLICT`, `PURCHASE_ACTUALS_MISMATCH`, `BATCH_NOT_COMPLETABLE`, `BATCH_NOT_CANCELLABLE`, `BATCH_CANCELLED` |

---

## 7. Hub — nhập kho, phân loại, giao ca

> Trạng thái hub dùng **SCREAMING_CASE** (`PENDING`, `ARRIVED_AT_HUB`, `PENDING_CHECKOUT`,
> `CHECKED_OUT`, `OPEN`, `ACKNOWLEDGED`, `SORTED`) — khác Orders (PascalCase) và
> Logistics/deliveries (lowercase).

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-HUB-001 | **Mỗi chợ chỉ có tối đa một hub active**, và không gán hub vào chợ **inactive**. | `CreateHubCommandHandler`, `UpdateHubCommandHandler` | `HUB_ALREADY_CONFIGURED_FOR_MARKET`, `MARKET_INACTIVE` |
| BR-HUB-002 | Không hạ `CapacityKg` xuống **dưới sức chứa đang bị chiếm**; `CapacityKg > 0`, tọa độ trong khoảng hợp lệ, địa chỉ ≤500 ký tự. | `UpdateHubCommandHandler`, `CreateHubCommandValidator` | `HUB_CAPACITY_BELOW_OCCUPIED`, `VALIDATION_ERROR` |
| BR-HUB-003 | Không vô hiệu hóa hub khi còn **chuyến nhập chờ xử lý**. | `DeactivateHubCommandHandler` | `HUB_HAS_PENDING_DELIVERIES` |
| BR-HUB-004 | `hub_staff` / `driver` chỉ truy cập được **hub mình được gán**; đối tượng gán phải **active** và đúng role. | `HubAccessChecker`, `ReplaceHubStaffAssignmentsCommandHandler`, `ReplaceHubDriverAssignmentsCommandHandler` | `HUB_ACCESS_DENIED`, `INVALID_ASSIGNMENT_TARGET` |
| BR-HUB-005 | Bàn giao lô mua **tự động tạo `HubInboundEvent` ở `PENDING`**; không ghi nhận lịch nhập hai lần; quét nhập kho phải khớp một inbound đang `PENDING`. | `ProcurementBatchHandedOffIntegrationEventHandler` (Hub), `RecordInboundCommandHandler`, `ScanInboundCommandHandler` | `ALREADY_RECEIVED`, `SCAN_NO_MATCH` |
| BR-HUB-006 | Không nhập vượt **sức chứa còn lại của hub**; cập nhật sức chứa dùng optimistic concurrency. | `ScanInboundCommandHandler` | `HUB_CAPACITY_EXCEEDED`, `OPTIMISTIC_CONCURRENCY_CONFLICT` |
| BR-HUB-007 | Ghi chênh lệch chỉ **sau khi hàng đã `ARRIVED_AT_HUB`**; order item phải khớp một sản phẩm trong chính chuyến nhập đó; `AffectedQuantity` **không vượt số lượng của order item**; ảnh bằng chứng phải là **URL Cloudinary HTTPS** ≤512 ký tự. | `RecordDiscrepancyCommandHandler` + validator | `INBOUND_NOT_ARRIVED`, `ORDER_ITEM_NOT_IN_INBOUND`, `INVALID_ISSUE_QUANTITY`, `VALIDATION_ERROR` |
| BR-HUB-008 | Không acknowledge một chênh lệch **đã được acknowledge**. Chênh lệch chưa xử lý **chặn khởi hành tuyến**. | `AcknowledgeDiscrepancyCommandHandler`, `StartRouteCommandHandler` | `DISCREPANCY_ALREADY_ACKNOWLEDGED`, `PENDING_HUB_DISCREPANCY` |
| BR-HUB-009 | Phân loại chỉ được cho order item **thuộc hub này và đúng ngày phục vụ**; `SortedQuantityKg` **> 0 và ≤ số lượng yêu cầu**, khi cập nhật lại không được nhỏ hơn số đã phân loại trước đó. | `MarkLineSortedCommandHandler` | `ORDER_NOT_AT_HUB`, `INVALID_SORTED_QUANTITY` |
| BR-HUB-010 | **Khi đã bắt đầu phân loại, thứ tự điểm dừng của tuyến bị khóa.** | `ReorderDriverRouteCommandHandler` (Logistics) | `ROUTE_LOCKED_FOR_SORTING` |
| BR-HUB-011 | Xuất kho phải trỏ tới **tuyến đích tồn tại** và không vượt **tồn thực tế tại hub**; cross-dock chỉ sau `ARRIVED_AT_HUB` (`pending → in_progress → completed`). Giao ca yêu cầu tuyến **đúng hub, đã gán, có tài xế, tài xế khớp**; tài xế chỉ checkout **ca của mình** và chỉ **một lần**. | `RecordOutboundCommandHandler`, `CreateCrossDockCommandHandler`, `CreateHandoverCommandHandler`, `DriverCheckoutCommandHandler` | `OUTBOUND_ROUTE_INVALID`, `INSUFFICIENT_HUB_STOCK`, `INBOUND_NOT_ARRIVED`, `ROUTE_HUB_MISMATCH`, `ROUTE_NOT_ASSIGNED`, `ROUTE_HAS_NO_DRIVER`, `DRIVER_ROUTE_MISMATCH`, `FORBIDDEN`, `HUB_HANDOVER_ALREADY_CHECKED_OUT` |

---

## 8. Logistics — tuyến, xe, giao hàng

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-LOG-001 | `VehicleType ∈ {van, truck, motorbike}`, **biển số duy nhất**, `CapacityKg > 0`. Không có `Vehicle.HubId` cứng — **điều xe là toàn đội** (một hub / một chợ), đây là quyết định thiết kế có chủ đích. | `RegisterVehicleCommandHandler` + validator | `VALIDATION_ERROR`, `PLATE_NUMBER_DUPLICATE` |
| BR-LOG-002 | **Mọi dòng đơn cần định tuyến phải có packing code hợp lệ** (để tính khối lượng) trước khi hoạch định. | `RoutePlanningInputBuilder` | `ROUTE_WEIGHT_INCOMPLETE` |
| BR-LOG-003 | Hub và **mọi nhà hàng đích phải có tọa độ** cho ngày phục vụ. | `CalculateRouteCommandHandler` | `MISSING_COORDINATES` |
| BR-LOG-004 | **Tối đa 20 điểm dừng mỗi tuyến** (`Logistics:MaxStopsPerVehicle`). | `CalculateRouteCommandHandler` | `STOP_LIMIT_EXCEEDED` |
| BR-LOG-005 | Solver OR-Tools giới hạn **3 giây**; không có lời giải → bất khả thi. Tham số hoạch định: bắt đầu **6:00**, phục vụ **10 phút/điểm**, chi phí **5.000 VND/km**, hệ số sử dụng tải **90%**. | `OrToolsRoutePlanningSolver`, `appsettings → Logistics` | `ROUTE_PLAN_INFEASIBLE` |
| BR-LOG-006 | Ma trận khoảng cách gọi Goong theo lô **10**, cache **30 ngày**; lỗi API → fallback Haversine với tốc độ giả định **30 km/h**. | `GoongRouteMatrixProvider` | `HAVERSINE_FALLBACK` |
| BR-LOG-007 | Chỉ **một đề xuất tuyến** cho mỗi (hub, ngày phục vụ) tại một thời điểm. | `PlanRoutesCommandHandler` | `ROUTE_PLAN_CONFLICT` |
| BR-LOG-008 | Duyệt kế hoạch: chỉ từ `proposed`; bị chặn nếu đầu vào (đơn, tọa độ, đội xe, cấu hình routing) **đã đổi sau khi hoạch định**; yêu cầu **không còn đơn chưa gán** và **mọi tuyến đều có xe đề xuất**; đơn/xe bị lần duyệt khác giữ trước → xung đột. | `ApproveRoutePlanCommandHandler` | `ROUTE_PLAN_NOT_PROPOSED`, `PLAN_STALE`, `PLAN_HAS_UNASSIGNED_ORDERS`, `ROUTE_PLAN_APPROVAL_CONFLICT` |
| BR-LOG-009 | Phải **tối ưu** tuyến trước khi review; `OptimizationCriteria ∈ {DISTANCE, TIME, COST}`. Lọc trạng thái dùng `Enum.TryParse<RouteStatus>` nên nhận `{planned, selected, reviewed, assigned, in_progress, completed, cancelled}`; message validation trong handler hiện chưa liệt kê `in_progress` và `completed`. | `ReviewRouteCommandHandler`, `OptimizeRouteCommandHandler`, `ListRoutesQueryHandler` | `ROUTE_INVALID_TRANSITION`, `VALIDATION_ERROR` |
| BR-LOG-010 | Gán xe yêu cầu **`DriverUserId`**, xe **đã gán hub và cùng hub với tuyến**, **tải tuyến ≤ sức chở xe**, xe/tài xế đủ điều kiện, và **một xe không gán cho 2 tuyến cùng ngày phục vụ**. | `AssignVehicleCommandHandler` | `DRIVER_REQUIRED`, `VEHICLE_HUB_UNASSIGNED`, `VEHICLE_HUB_MISMATCH`, `VEHICLE_WEIGHT_CAPACITY_EXCEEDED`, `VEHICLE_NOT_ELIGIBLE`, `VEHICLE_NOT_AVAILABLE` |
| BR-LOG-011 | Sắp xếp lại điểm dừng chỉ khi tuyến ở `assigned`, **chưa khởi hành**, và hub **chưa bắt đầu phân loại**. | `ReorderDriverRouteCommandHandler` | `ROUTE_NOT_REORDERABLE`, `ROUTE_LOCKED_FOR_SORTING` |
| BR-LOG-012 | Khởi hành yêu cầu tuyến **đã gán**, **đã điều phối (có delivery)**, và **không còn chênh lệch hub chờ xử lý**. | `StartRouteCommandHandler` | `ROUTE_NOT_STARTABLE`, `ROUTE_HAS_NO_DELIVERIES`, `PENDING_HUB_DISCREPANCY` |
| BR-LOG-013 | Tài xế chỉ thao tác trên **tuyến/chuyến của chính mình**. Nhận hàng yêu cầu tuyến đã gán, danh sách **khớp chính xác snapshot tuyến đã duyệt** và gồm **mọi đơn `AtHub` của tuyến+hub đó**, nhà hàng phải là **điểm dừng trên tuyến**, và **mỗi đơn chỉ một bản ghi delivery**. | `ConfirmPickupCommandHandler`, `AttachProofOfDeliveryCommandHandler`, `RoutesController` | `FORBIDDEN`, `ROUTE_NOT_ASSIGNED`, `PICKUP_ORDERS_INCOMPLETE`, `ORDER_NOT_ON_ROUTE`, `DELIVERY_ALREADY_EXISTS` |
| BR-LOG-014 | Cập nhật trạng thái giao chỉ khi **tuyến đang chạy** và theo chuyển tiếp hợp lệ; sự cố giao ∈ {`undeliverable`, `damaged`, `customer_rejected`, `other`}, mô tả ≤1000. Manifest xếp hàng liệt kê hàng **theo từng điểm dừng, thứ tự xếp ngược với thứ tự giao**, nguồn là **đơn ở `AtHub`** (không phải bảng `deliveries` — chưa tồn tại trước pickup). Ước lượng thùng theo kg: tare **2 kg**, tải tối đa **25 kg**. `deliveries.status` lưu **lowercase**. | `UpdateDeliveryStatusCommandHandler`, `DeliveryIssue`, `GetLoadingManifestQueryHandler`, `appsettings → Logistics:Box` | `DELIVERY_ROUTE_NOT_IN_PROGRESS`, `DELIVERY_STATUS_INVALID` |

---

## 9. Invoicing — hóa đơn VAT

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-INV-001 | Mỗi **đơn được giao hoàn tất** phát `DeliveryCompletedIntegrationEvent` để tạo tối đa một hóa đơn; hóa đơn là **snapshot bất biến của đơn đã giao**. Thuế suất đọc từ snapshot `order_items.vat_rate_code`, không đọc live từ `products`. | `DeliveryCompletedIntegrationEventHandler`, `OrderInvoiceRowConfiguration`, `InvoiceRepository` | — |
| BR-INV-002 | Chỉ **hóa đơn đã phát hành** và **đủ dữ liệu xuất** mới export được. | `ExportInvoiceQuery` | `INVOICE_NOT_ISSUED`, `INVOICE_EXPORT_INCOMPLETE` |
| BR-INV-003 | Job retry phát hành: mỗi **120s**, lô **50**, tối đa **5 lần thử**, backoff **120s**; lý do lỗi lưu ≤**500 ký tự**. | `InvoiceIssuanceRetryHostedService`, `Invoice.MaxErrorReasonLength` | — |
| BR-INV-004 | PDF render bằng culture **`vi-VN`**, giờ **`Asia/Ho_Chi_Minh`**. Truy vấn yêu cầu `from < to`, `status` hợp lệ, pageSize ≤**100**; tài khoản không gắn nhà hàng nào thì không xem được. | `InvoicePdfRenderer`, `GetInvoiceSummaryQuery`, `GetInvoicesQuery` | `VALIDATION_ERROR`, `FORBIDDEN` |
| BR-INV-005 | Nhà cung cấp hóa đơn điện tử hiện là **stub** — chưa tích hợp NCC/HSM thật. | `StubInvoiceProvider` | — |

---

## 10. Notifications

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-NOT-001 | Đăng ký thiết bị yêu cầu `userId` + `token`, `platform ∈ {ios, android, web}`. Trạng thái gửi `pending → sent` / `failed`. | `RegisterDeviceCommandHandler`, `NotificationSendStatus` | `VALIDATION_ERROR` |
| BR-NOT-002 | Job retry: mỗi **60s**, lô **50**, tối đa **5 lần thử**, backoff **60s**. Expo push gửi lô tối đa **100** thiết bị/request, timeout **10s**; push **tắt mặc định**. | `NotificationRetryHostedService`, `ExpoPushSender`, `appsettings` | — |
| BR-NOT-003 | Thông báo được **persist trước, gửi sau** — mọi integration event tiêu thụ đều ghi một bản ghi `notifications`. Email thiếu địa chỉ người nhận → không gửi. | `NotificationWriter`, `SmtpEmailSender` | `EMAIL_RECIPIENT_MISSING` |

---

## 11. Analytics

> Module **chỉ đọc** — 0 bảng, 0 migration. Mọi truy vấn đi qua keyless Row seam.

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-ANA-001 | Xuất dữ liệu giới hạn **50.000 dòng**, và chỉ các dataset trong allow-list mới xuất được. | `ExportAnalyticsQueryHandler` | `VALIDATION_ERROR` |
| BR-ANA-002 | Ngưỡng **trễ giao hàng = 15 phút**. Thời lượng giao tính từ **mốc bàn giao tại hub** — hệ thống **không có timestamp khởi hành riêng**. | `DeliveryPerformanceReader` | — |
| BR-ANA-003 | Tổng hợp theo ngày dùng offset **UTC+7 cứng** (không phải TimeZoneInfo). Xu hướng giá giới hạn **12 tháng** ở handler, **24 tháng** ở validator. | `OrderMetricsReader`, `HubThroughputReader`, `DemandHeatmapReader`, `GetPriceTrendsQuery*` | `VALIDATION_ERROR` |
| BR-ANA-004 | Analytics **không được dùng enum của module khác** — giá trị khai lại thành hằng chuỗi cục bộ. | `GetOrderMetricsQueryHandler` | — |

---

## 12. AI Assistant

> Nằm trong host `FreshFlow.API/Assistant/`, **không phải một module**.

| ID | Luật | Enforce tại | Mã lỗi |
|---|---|---|---|
| BR-AI-001 | Tin nhắn ≤ **4000 ký tự**, `sessionId` ≤ **128 ký tự**, rate limit **15 request/phút**. | `AssistantChatRequestValidator`, `appsettings` | `VALIDATION_ERROR` |
| BR-AI-002 | Tối đa **6 vòng gọi tool** mỗi lượt; hội thoại giữ **20 lượt gần nhất**, TTL **30 phút**; tool danh sách yêu thích trả tối đa **20 mục**. | `Assistant:MaxToolHops`, `DbConversationStore`, `ToolDefinitions` | — |
| BR-AI-003 | **Kiểm IDOR khi resume session**: `existing.UserId` phải khớp JWT, sai → **404** (không phải 403, tránh lộ tồn tại session). | `AssistantController` | — |
| BR-AI-004 | Endpoint assistant **không đi qua `ISender`** ⇒ `ValidationBehavior` không chạy ⇒ **phải validate thủ công**. Tham số tool sai định dạng trả lỗi có cấu trúc cho model, không ném exception. | `AssistantController`, `ToolArgsParser` | `INVALID_TOOL_ARGS` |
| BR-AI-005 | Failover nhà cung cấp theo thứ tự `Assistant:Providers` (`Gemini` → `ZenMux`); **mọi lỗi đều failover trừ `AuthenticationFailed`**. Hành động ghi chỉ thực hiện sau khi qua **confirmation gate**. | `FailoverChatClient`, `ConfirmationGateResult` | — |

---

## Phụ lục A — Tham số cấu hình được

| Tham số | Mặc định | Nguồn | Ai đổi được |
|---|---|---|---|
| `daily_cutoff_time` | `22:00` (VN) | `operational_settings` | admin |
| `delivery_window_days` | `7` (hợp lệ 1–30) | `operational_settings` | admin |
| `delivery_fee_per_km` | `5.000 VND` (0–1.000.000) | `operational_settings` | admin |
| `base_fee` / `minimum_fee` | `0` (0–10.000.000) | `operational_settings` | admin |
| `rounding_unit` | `0` (0–1.000.000) | `operational_settings` | admin |
| `default_route_type` | `hub_relay` | `operational_settings` | admin (**chưa có reader**) |
| `batching_enabled` (DB) | `true` | `operational_settings` | admin (**chưa có reader**) |
| `Pricing:MaxPriceVnd` | `50.000.000` | appsettings | deploy |
| `Procurement:Batching:*` | bật, 60s | appsettings | deploy |
| `Orders:MarketSessions:Enforce` | `true` | appsettings | deploy |
| `Logistics:MaxStopsPerVehicle` | `20` | appsettings | deploy |
| `Logistics:CapacityUtilizationPercent` | `90` | appsettings | deploy |
| `Logistics:Box:TareKg / MaxLoadKg` | `2` / `25` kg | appsettings | deploy |
| `Logistics:Routing:*` | solver 3s, start 6h, service 10', 5.000 VND/km | appsettings | deploy |
| `Delivery:Goong:FallbackRoadFactor` | `1.4` | appsettings | deploy |
| `JWT:AccessTokenTtlSeconds` | `900` | appsettings | deploy |
| `JWT:RefreshTokenTtlDays` | `7` | appsettings | deploy |
| `RateLimiting:*` | Auth 10, Orders 30, Assistant 15 / phút | appsettings | deploy |
| Khóa tài khoản | 5 lần sai / 15 phút | hằng số code | dev |
| OTP email / reset | 10 phút / 15 phút | hằng số code | dev |
| Kỳ hạn thanh toán công nợ | 15 ngày | hằng số code | dev |
| Ngưỡng cảnh báo công nợ | 80% / 100% | hằng số code | dev |
| Cache bảng giá Redis | 5 phút | hằng số code | dev |

---

## Phụ lục B — Máy trạng thái

| Aggregate | Trạng thái | Kiểu chữ | Chuyển tiếp |
|---|---|---|---|
| `Order` | Draft, Confirmed, Batched, PickedUp, AtHub, Delivering, Delivered, Cancelled | PascalCase | tuyến tính; Cancelled chỉ từ Draft/Confirmed (+ Batched khi hủy cả phiên) |
| `OrderPaymentStatus` | NotApplicable, Outstanding, Settled, Waived | PascalCase | — |
| `OrderClaim` | Submitted, Approved, Rejected | PascalCase | Submitted → terminal |
| `OrderIssue` | Open, Resolved | PascalCase | một chiều |
| `RestaurantStatus` | Pending, Active, Suspended | enum int; DB `status` lowercase | Pending→Active⇄Suspended |
| `MarketSession` | Draft, Open, Closed (Auto/Manual) | PascalCase | một chiều |
| `ProcurementBatch` | Built, Manifested, Purchasing, HandedOff, Completed, Cancelled | PascalCase | tuyến tính + Cancelled sớm |
| `HubInboundEvent` | PENDING, ARRIVED_AT_HUB | SCREAMING | một chiều |
| `HubSortingProgress` | PENDING, SORTED | SCREAMING | một chiều |
| `HubDiscrepancy` | OPEN, ACKNOWLEDGED | SCREAMING | một chiều |
| `HubHandoverEvent` | PENDING_CHECKOUT, CHECKED_OUT | SCREAMING | một chiều |
| `CrossDockTransfer` | pending, in_progress, completed | lowercase | tuyến tính |
| `DeliveryRoute` | planned, selected, reviewed, assigned, in_progress, completed, cancelled | lowercase | tuyến tính |
| `RoutePlan` | proposed, approved, stale, superseded | lowercase | proposed → approved/stale/superseded |
| `Delivery` | pending, arrived, delivered, failed | lowercase | pending→arrived→delivered / failed |
| `DeliveryIssue` | open, resolved | lowercase | một chiều |
| `Invoice` | Draft, PendingIssuance, Issued, Failed, Adjusted, Cancelled | enum int | Failed có thể retry |
| `Notification` | pending, sent, failed | lowercase | failed có thể retry |

---

## Phụ lục C — Integration events

Luồng nghiệp vụ chéo module (`FreshFlow.Contracts`). Bảng chỉ liệt kê consumer
nghiệp vụ; các audit-log handler được lược bỏ:

| Event | Phát bởi | Tiêu thụ bởi → hệ quả |
|---|---|---|
| `OrderConfirmedIntegrationEvent` | Orders | Notifications |
| `OrderCancelledIntegrationEvent` | Orders | Notifications |
| `PriceUpdatedIntegrationEvent` | Pricing | — (chỉ audit log; Redis/history/SignalR xử lý từ domain event) |
| `ProcurementBatchBuiltIntegrationEvent` | Procurement | Orders (đẩy đơn sang `Batched`) |
| `ProcurementManifestGeneratedIntegrationEvent` | Procurement | — (chỉ audit log) |
| `ProcurementAgentAssignedIntegrationEvent` | Procurement | — (chỉ audit log) |
| `ProcurementPurchaseConfirmedIntegrationEvent` | Procurement | Pricing (đồng bộ giá tham chiếu) |
| `ProcurementBatchHandedOffIntegrationEvent` | Procurement | Orders (số lượng thực mua, `PickedUp`→`AtHub`), Hub (tạo inbound PENDING) |
| `ProcurementBatchCancelledIntegrationEvent` | Procurement | Orders (giải phóng giữ chỗ tồn kho) |
| `HubDiscrepancyRecordedIntegrationEvent` | Hub | Orders |
| `DeliveryStartedIntegrationEvent` | Logistics | Orders (`Delivering`), Notifications, Logistics SignalR broadcast |
| `DeliveryStopUpdatedIntegrationEvent` | Logistics | Logistics SignalR broadcast |
| `DeliveryCompletedIntegrationEvent` | Logistics | Orders (`Delivered`), Invoicing (phát hành hóa đơn), Procurement (đóng lô), Notifications |
| `CreditLimitThresholdReachedIntegrationEvent` | Orders/Credit | Notifications |
| `CreditStatementGeneratedIntegrationEvent` | Orders/Credit | Notifications |
| `RestaurantRefundIssuedIntegrationEvent` | Orders/Credit | Notifications |
| `ScheduledOrderNeedsAttentionIntegrationEvent` | Orders | Notifications |

> Domain event → chỉ trong nội bộ module. Một handler duy nhất dịch domain event thành
> integration event tương ứng.

---

## Phụ lục D — Khoảng trống & cảnh báo

**Luật đã mô tả trong doc nhưng KHÔNG tồn tại trong code:**

| Mô tả sai | Ở đâu | Thực tế |
|---|---|---|
| `price_snapshots` phân vùng theo tháng + `PartitionMaintenanceJob` | `docs/03`, comment trong `PriceSnapshotConfiguration` | Là **bảng thường**, index thường; job không tồn tại |
| Vai trò `restaurant_manager`, `restaurant_staff` | `docs/04` | **Không tồn tại** — dùng sẽ 403 vĩnh viễn |
| Thanh toán từng đơn qua cổng | `docs/01` FR-ORD-008 | Mô hình là **công nợ B2B** |
| Nhiều user cho một tài khoản nhà hàng | doc cũ | Đã gỡ theo DEC-003 |
| Bảng `billing` | `docs/03` (phần physical) | Đã gỡ theo DEC-002 |

**Luật chưa được implement / còn dở:**

| Hạng mục | Trạng thái |
|---|---|
| Cảnh báo biến động giá (price alert trigger) | Cấu hình ngưỡng đã có, **chưa có gì kích hoạt cảnh báo** (SCRUM-173) |
| `operational_settings.batching_enabled` và `default_route_type` | Lưu được, **chưa có reader nào đọc** |
| Concurrency token cho `DeliveryRoute` | Chưa có — follow-up của epic LOG |
| Nhà cung cấp hóa đơn điện tử thật (NCC/HSM), VAT theo từng nhóm hàng | Đang dùng stub |
| Khuyến mãi / voucher, đánh giá sản phẩm, thanh toán online | Chưa có (gap so với Kamereo) |
| SignalR Redis backplane | `AddSignalR()` in-memory — **chỉ chạy được một instance** |

**Bẫy kỹ thuật ảnh hưởng tới việc enforce luật:**

- Casing cột **không nhất quán và trộn lẫn trong cùng một bảng** (`orders`, `order_items`,
  `market_products`, `restaurants`). Định danh sai chỉ **lỗi lúc chạy**, không lỗi lúc build.
- Giá trị enum dạng chuỗi cũng không nhất quán: `orders."Status"` PascalCase,
  `deliveries.status` lowercase, `hub_*.status` SCREAMING_CASE.
- `ToSqlQuery` **không chạy trên EF InMemory** — seam đọc chéo module chỉ được chứng minh bằng
  **integration test trên Postgres thật**.
- Mã lỗi chưa đăng ký trong `ErrorExtensions.ToActionResult()` rơi xuống **500**. Luôn tái sử
  dụng mã đã có.

---

## Phụ lục E — Bảng business rule cho SRS

> Bảng này để **chép thẳng vào tài liệu SRS**. Cùng bộ ID với các mục 0–12 ở trên (truy vết 1–1),
> nhưng phát biểu thuần nghiệp vụ: **không nhắc tên lớp, hàm, bảng, khóa cấu hình hay mã lỗi**.
> Chi tiết kỹ thuật (nơi enforce, mã lỗi trả về) tra ở phần tương ứng phía trên.
>
> **119 luật.**

| ID | Nhóm | Phát biểu luật |
|---|---|---|
| BR-GEN-001 | Chung | Mọi bản ghi được định danh bằng mã duy nhất toàn hệ thống. Dữ liệu nghiệp vụ chính dùng cơ chế xóa mềm (đánh dấu đã xóa thay vì xóa vật lý); một số dữ liệu chỉ ghi thêm (lịch sử giá, phiên đăng nhập) và dòng chi tiết đơn thì không áp dụng xóa mềm. |
| BR-GEN-002 | Chung | Thời điểm lưu theo giờ chuẩn quốc tế; mọi ngày nghiệp vụ (ngày đặt, ngày giao, ngày phiên chợ) được tính theo múi giờ **Việt Nam (GMT+7)**. |
| BR-GEN-003 | Chung | Vi phạm quy tắc nghiệp vụ phải trả về thông báo lỗi có cấu trúc và mã lỗi xác định cho người dùng, không được làm gián đoạn hệ thống. |
| BR-GEN-004 | Chung | Mọi yêu cầu từ người dùng đều phải qua kiểm tra hợp lệ dữ liệu đầu vào trước khi được xử lý. |
| BR-GEN-005 | Chung | Khi nhiều người cùng sửa một đối tượng, hệ thống phát hiện xung đột và yêu cầu thao tác sau thực hiện lại; việc xác nhận đơn được xử lý cô lập để không phát sinh dữ liệu sai khi truy cập đồng thời. |
| BR-GEN-006 | Chung | Hệ thống có đúng **5 vai trò**: quản trị viên, nhà hàng, nhân viên chợ, nhân viên kho (hub), tài xế. Người dùng chỉ thực hiện được chức năng thuộc vai trò của mình. |
| BR-GEN-007 | Chung | Giới hạn tần suất gọi: đăng nhập/xác thực **10 lần/phút**, đơn hàng **30 lần/phút**, trợ lý AI **15 lần/phút**. Danh sách trả về mặc định **20 bản ghi/trang**, tối đa **100** hoặc **200** tùy loại danh sách. |
| BR-GEN-008 | Chung | Dữ liệu đầu vào của người dùng không bao giờ được ghép trực tiếp vào câu truy vấn; tiêu chí sắp xếp / nhóm do người dùng chọn phải nằm trong danh sách được phép. |
| BR-AUTH-001 | Auth | Đăng nhập bằng email **hoặc** số điện thoại kèm mật khẩu. Đăng nhập sai chỉ hiện thông báo chung, không tiết lộ tài khoản có tồn tại hay không. |
| BR-AUTH-002 | Auth | Nhập sai mật khẩu **5 lần liên tiếp** → khóa tài khoản **15 phút**. Tài khoản đã bị vô hiệu hóa không đăng nhập được. |
| BR-AUTH-003 | Auth | Phiên đăng nhập có hiệu lực **15 phút**, gia hạn được trong **7 ngày**. Mã gia hạn chỉ dùng **một lần**; nếu bị dùng lại, toàn bộ phiên của người dùng đó bị thu hồi. |
| BR-AUTH-004 | Auth | Phiên bản 1 chỉ gửi mã qua **email**. Mã xác thực email có hiệu lực **10 phút**, mã đặt lại mật khẩu **15 phút**, cả hai chỉ dùng một lần. |
| BR-AUTH-005 | Auth | Đổi mật khẩu phải nhập đúng mật khẩu hiện tại và mật khẩu mới phải khác mật khẩu cũ. Mật khẩu luôn được lưu ở dạng băm, không lưu bản rõ. |
| BR-AUTH-006 | Auth | Email và số điện thoại là **duy nhất toàn hệ thống**, áp dụng khi đăng ký, khi quản trị viên tạo tài khoản và khi cập nhật hồ sơ. |
| BR-AUTH-007 | Auth | Số điện thoại 7–15 chữ số (cho phép tiền tố `+`); mã số thuế 10 chữ số, có thể kèm 3 chữ số chi nhánh. Giới hạn độ dài: họ tên ≤255, điện thoại ≤20, email ≤256, địa chỉ ≤500, người liên hệ ≤200, đường dẫn ảnh/tài liệu ≤512 ký tự. |
| BR-AUTH-008 | Auth | Nhà hàng có vòng đời **Chờ duyệt → Hoạt động ⇄ Tạm ngưng**: đăng ký mới ở trạng thái chờ duyệt và phải được quản trị viên phê duyệt; chỉ nhà hàng đang hoạt động mới bị tạm ngưng, chỉ nhà hàng đang tạm ngưng mới được kích hoạt lại; không duyệt hay tạm ngưng hai lần. |
| BR-AUTH-009 | Auth | Nhà hàng **chưa được duyệt hoặc đang tạm ngưng** không được đặt đơn, xác nhận đơn hay tạo lịch đặt hàng định kỳ. |
| BR-AUTH-010 | Auth | Quản trị viên không được tự vô hiệu hóa tài khoản của chính mình; chỉ gán được các vai trò hệ thống đã định nghĩa. |
| BR-AUTH-011 | Auth | Chỉ nhân viên chợ mới được phân công phụ trách chợ, và chỉ phân công vào chợ đang hoạt động. |
| BR-AUTH-012 | Auth | Địa chỉ giao phải có tọa độ hợp lệ (vĩ độ −90…90, kinh độ −180…180); khung giờ nhận hàng phải có giờ kết thúc sau giờ bắt đầu; mỗi nhà hàng chỉ có **một** địa chỉ mặc định. |
| BR-CAT-001 | Catalog | Tên danh mục, tên đơn vị tính và mã quy cách đóng gói đều phải là duy nhất. |
| BR-CAT-002 | Catalog | Cây danh mục **tối đa 2 cấp**: danh mục cha phải là danh mục gốc đang hoạt động; danh mục không thể là cha của chính nó; danh mục đã có danh mục con không thể trở thành danh mục con. |
| BR-CAT-003 | Catalog | Không được vô hiệu hóa danh mục còn danh mục con đang hoạt động. |
| BR-CAT-004 | Catalog | Sản phẩm phải gắn với đơn vị tính, danh mục và quy cách đóng gói đang tồn tại và đang hoạt động. |
| BR-CAT-005 | Catalog | Mã quy cách đóng gói viết hoa, tối đa **8 ký tự**; sức chứa là số nguyên dương và **không vượt 25 kg**. Giới hạn độ dài: mô tả sản phẩm ≤2000, mô tả danh mục ≤1000, mô tả quy cách ≤500, đường dẫn ảnh ≤512, viết tắt ≤20 ký tự. Thuế suất khai báo trên sản phẩm là nguồn để chốt thuế cho đơn hàng. |
| BR-PRC-001 | Pricing | Một sản phẩm chỉ được niêm yết **một lần** tại mỗi chợ. |
| BR-PRC-002 | Pricing | Giá phải **lớn hơn 0**, không vượt **50.000.000 VND**, tối đa 2 chữ số thập phân; số lượng khả dụng không âm; từ khóa tìm kiếm bắt buộc và tối đa 200 ký tự. |
| BR-PRC-003 | Pricing | Nhân viên chợ chỉ được sửa giá và tồn của **chợ mình được phân công**; quản trị viên không bị giới hạn này. |
| BR-PRC-004 | Pricing | Mỗi sản phẩm niêm yết gắn tối đa **8 nhãn**; nhãn phải tồn tại, tên nhãn duy nhất và tối đa **30 ký tự**. |
| BR-PRC-005 | Pricing | Mỗi lần thay đổi giá đều được lưu lại thành một bản ghi lịch sử **chỉ ghi thêm, không sửa/xóa**. Lịch sử giá của một sản phẩm niêm yết trả tối đa **100 bản ghi** mỗi lần xem. |
| BR-PRC-006 | Pricing | Bảng giá được lưu đệm tối đa **5 phút**; khi giá thay đổi, bảng giá được làm mới và đẩy thông báo thời gian thực tới người dùng đang theo dõi chợ đó. |
| BR-PRC-007 | Pricing | Giá tham chiếu trên bảng giá được **cập nhật tự động theo giá mua thực tế đã xác nhận** của lô mua. |
| BR-ORD-001 | Orders | Đơn hàng chỉ chuyển trạng thái theo lộ trình đã định (Nháp → Đã xác nhận → Đã gom lô → Đã lấy hàng → Tại kho → Đang giao → Đã giao); mọi bước nhảy khác đều bị chặn. |
| BR-ORD-002 | Orders | Đơn chỉ được hủy khi đang ở trạng thái **Nháp** hoặc **Đã xác nhận**. Ngoại lệ: khi cả phiên chợ bị hủy, đơn đã gom lô cũng bị hủy theo — chỉ xảy ra trước khi nhân viên chợ đi mua. |
| BR-ORD-003 | Orders | Chỉ đơn ở trạng thái **Nháp** mới được thêm, sửa, xóa sản phẩm và áp giá. |
| BR-ORD-004 | Orders | Địa chỉ giao được chốt **một lần duy nhất** khi đơn còn ở trạng thái Nháp và không đổi được sau đó. |
| BR-ORD-005 | Orders | Xác nhận đơn yêu cầu: đơn thuộc chính nhà hàng đang đăng nhập, nhà hàng đã được duyệt, đơn đang ở trạng thái Nháp và **có ít nhất một sản phẩm**. |
| BR-ORD-006 | Orders | Một đơn hàng (và một lịch đặt định kỳ) chỉ được chứa sản phẩm của **một chợ duy nhất**. |
| BR-ORD-007 | Orders | Quản trị viên chỉ được đẩy đơn thủ công sang các trạng thái **Đã gom lô, Đã lấy hàng, Tại kho**. |
| BR-ORD-008 | Orders | Nhà hàng chỉ xác nhận đã nhận hàng sau khi đơn ở trạng thái **Đã giao**, và chỉ xác nhận **một lần**. |
| BR-ORD-009 | Orders | **Giờ chốt đơn hằng ngày là 22:00 giờ Việt Nam** (cấu hình được). Ngày giao sớm nhất là **hôm sau (D+1)** nếu xác nhận trước giờ chốt, **D+2** nếu từ giờ chốt trở đi. Ngày giao yêu cầu quá sớm hoặc bỏ trống sẽ được **tự động dời** tới ngày hợp lệ gần nhất thay vì báo lỗi. |
| BR-ORD-010 | Orders | Ngày giao không được ở quá khứ và không vượt quá **cửa sổ giao hàng** tính từ hôm nay (mặc định **7 ngày**, quản trị viên cấu hình 1–30 ngày). |
| BR-ORD-011 | Orders | Thuế suất của từng dòng đơn được chốt tại thời điểm áp giá theo thuế suất khai báo của sản phẩm (5%, 8%, 10%, hoặc 0% với hàng không chịu thuế / không kê khai). |
| BR-ORD-012 | Orders | Phí giao hàng tính theo **quãng đường đường bộ thực tế** từ chợ nguồn tới địa chỉ giao; khi không lấy được dữ liệu đường bộ, hệ thống ước lượng theo đường chim bay nhân hệ số **1,4**. Thiếu tọa độ chợ hoặc địa chỉ giao thì không cho xác nhận đơn. Mọi tham số phí và quãng đường không được âm. |
| BR-ORD-013 | Orders | Số lượng đặt của mỗi sản phẩm phải **đạt số lượng đặt tối thiểu** của sản phẩm đó và, nếu sản phẩm bán theo quy cách đóng gói, phải là **bội số của khối lượng quy cách**. |
| BR-ORD-014 | Orders | Phí giao mặc định **5.000 VND/km**, không có phí cơ bản, phí tối thiểu hay làm tròn. Quản trị viên chỉnh trong khoảng: phí theo km 0–1.000.000, phí cơ bản và phí tối thiểu 0–10.000.000, đơn vị làm tròn 0–1.000.000 VND. |
| BR-ORD-015 | Orders | Chỉ xác nhận được đơn khi ngày giao có **phiên chợ đang mở và còn sức chứa**. |
| BR-ORD-016 | Orders | Tồn kho được **giữ chỗ ngay tại thời điểm xác nhận đơn** (không phải khi đi mua) và được **giải phóng** khi lô mua bị hủy. |
| BR-ORD-017 | Orders | Nếu sản phẩm, chợ nguồn hoặc địa chỉ giao thay đổi trong lúc đang xác nhận đơn, giao dịch bị hủy và người dùng phải thực hiện lại. |
| BR-ORD-018 | Orders | Số lượng mỗi dòng đơn phải **lớn hơn 0**; sản phẩm phải còn được bán; tổng số lượng đặt không vượt tồn khả dụng — kiểm tra cả khi thêm sản phẩm lẫn khi xác nhận đơn. |
| BR-ORD-019 | Orders | Ghi nhận mua thực tế: số lượng thực mua không âm và không vượt số lượng đặt; nếu có mua thì đơn giá thực phải lớn hơn 0; chỉ ghi nhận được khi đơn đã gom lô. Ghi chú đơn và lý do hủy tối đa **500 ký tự**. |
| BR-ORD-020 | Orders | Tạo lịch đặt định kỳ yêu cầu nhà hàng đã được duyệt, chu kỳ **theo ngày hoặc theo tuần**, lần chạy đầu tiên ở tương lai, và sản phẩm thuộc **một chợ**. Không thao tác được trên lịch đã hủy hoặc đang ngưng. |
| BR-ORD-021 | Orders | Lịch định kỳ lưu sẵn **danh sách sản phẩm mẫu và địa chỉ giao**; đến hạn, hệ thống tự sinh đơn và **tự xác nhận**. Nếu sinh đơn thất bại hoặc lịch cũ không có sản phẩm mẫu, đơn được hạ xuống trạng thái Nháp và nhà hàng được thông báo để xử lý. |
| BR-ORD-022 | Orders | Sự cố hàng hóa chỉ được báo cho đơn **Đã giao**, thuộc các loại **thiếu hàng / sai hàng / hư hỏng**; số lượng ảnh hưởng lớn hơn 0 và không vượt số lượng đặt; mô tả 1–1000 ký tự; sự cố đã xử lý xong không mở lại. |
| BR-ORD-023 | Orders | Khiếu nại chỉ mở khi đơn ở trạng thái **Tại kho** hoặc **Đã giao**; số tiền khiếu nại lớn hơn 0 và **không vượt số tiền đã ghi nợ cho đơn đó**; khiếu nại đã có kết luận thì không đổi được; **từ chối bắt buộc kèm ghi chú**, **chấp thuận bắt buộc có người duyệt và bút toán hoàn tiền**. Lý do ≤500, ghi chú quyết định ≤1000 ký tự. **Hoàn tiền được thực hiện bằng điều chỉnh công nợ, không qua cổng thanh toán.** |
| BR-CRE-001 | Credit | Hạn mức khả dụng = hạn mức tín dụng − dư nợ hiện tại. Chỉ ghi nợ được khi số tiền lớn hơn 0 và **dư nợ sau khi ghi không vượt hạn mức**. |
| BR-CRE-002 | Credit | Công nợ được ghi **tại thời điểm nhà hàng xác nhận đơn**, không phải khi giao hàng xong. |
| BR-CRE-003 | Credit | Mọi bút toán (ghi nợ, thanh toán, hoàn tiền, điều chỉnh) phải có số tiền **lớn hơn 0**. |
| BR-CRE-004 | Credit | Hạn mức tín dụng không âm và **không được đặt thấp hơn dư nợ hiện tại**. |
| BR-CRE-005 | Credit | Thanh toán và hoàn tiền không được vượt dư nợ; hoàn tiền cho một đơn không vượt số tiền còn lại đã ghi nợ cho chính đơn đó. |
| BR-CRE-006 | Credit | Bút toán thanh toán bắt buộc ghi nhận **người thực hiện và số tham chiếu**; số tham chiếu là duy nhất theo từng nhà hàng để chống ghi trùng. |
| BR-CRE-007 | Credit | Cảnh báo hạn mức: dùng **≥ 80%** → cảnh báo, **≥ 100%** → vượt hạn mức; mỗi mức chỉ cảnh báo **một lần** cho tới khi tỷ lệ sử dụng tụt xuống mức thấp hơn. |
| BR-CRE-008 | Credit | Giao dịch công nợ gồm 4 loại: ghi nợ, thanh toán, hoàn tiền, điều chỉnh; hình thức thanh toán gồm chuyển khoản và ghi nhận thủ công. |
| BR-CRE-009 | Credit | Sao kê công nợ lập theo **tháng dương lịch giờ Việt Nam**, **hạn thanh toán là 15 ngày sau khi kết thúc kỳ**, chỉ phát hành khi kỳ đã kết thúc, và là **bản chốt bất biến**. Nhà hàng chỉ xem được sao kê của chính mình. |
| BR-PROC-001 | Procurement | Mỗi phiên chợ phải xác định **chợ, ngày phục vụ và thời điểm đóng phiên**; thời điểm đóng phải ở tương lai. |
| BR-PROC-002 | Procurement | Phiên chợ chỉ sẵn sàng khi có **kho (hub) đang hoạt động**, **sức chứa dự kiến lớn hơn 0** (tối đa 1.000.000 kg), **ít nhất 1 xe** và **ít nhất 1 nhân viên chợ**. |
| BR-PROC-003 | Procurement | Không mở phiên chợ sau giờ chốt đơn hoặc khi còn cảnh báo chưa xử lý. |
| BR-PROC-004 | Procurement | Phiên chợ **đã đóng thì không sửa được**; lý do đóng tối đa 500 ký tự; khoảng thời gian tra cứu phải hợp lệ và trạng thái lọc thuộc {nháp, đang mở, đã đóng}. |
| BR-PROC-005 | Procurement | Chỉ **phiên chợ đã đóng** mới được gom lô mua, và phiên phải đã gán kho. Mỗi phiên và mỗi đơn chỉ thuộc **một lô mua đang hoạt động** tại một thời điểm. |
| BR-PROC-006 | Procurement | Việc gom lô chạy tự động định kỳ (mỗi phút) và có thể bật/tắt. Chỉ được gom lại (reset) khi lô và các đơn trong lô **chưa vượt quá giai đoạn gom lô**. |
| BR-PROC-007 | Procurement | Lô mua phải có **mã lô, ngày, chợ và ít nhất một dòng sản phẩm với số lượng dương**; chỉ nhận thêm đơn khi lô còn ở trạng thái cho phép gộp. |
| BR-PROC-008 | Procurement | **Chỉ lập được danh sách đi chợ khi mọi sản phẩm trong lô đều có giá tham chiếu**; phải lập danh sách **trước khi** phân công nhân viên chợ và trước khi xác nhận mua. |
| BR-PROC-009 | Procurement | Nhân viên được phân công phải **đang hoạt động, được phân công vào chợ đó và vào chính phiên đó**; mỗi sản phẩm chỉ giao cho một người; nhân viên chỉ thao tác trên phần việc của mình. |
| BR-PROC-010 | Procurement | Xác nhận mua: sản phẩm phải thuộc lô và **chưa được mua**, phải khai báo đủ **mỗi sản phẩm được giao một dòng kết quả**, với số lượng thực mua và đơn giá thực đều lớn hơn 0. |
| BR-PROC-011 | Procurement | Các trường hợp bất thường khi mua (không có hàng, thiếu hàng, lệch giá, hàng hư) chỉ được báo ở đúng giai đoạn cho phép; số lượng không âm; ghi chú tối đa 500 ký tự. |
| BR-PROC-012 | Procurement | **Bàn giao lô cho kho** yêu cầu: đã mua xong, **mọi sản phẩm không được miễn trừ đều đã xử lý**, kho đã xác định và đang hoạt động; không bàn giao hai lần. |
| BR-PROC-013 | Procurement | Khi bàn giao, **số lượng và giá mua thực tế được cập nhật về các đơn hàng** tương ứng; các đơn thuộc lô cùng vào kho một lượt. Lô chuyển sang **Hoàn tất** khi chuyến giao cuối cùng của lô hoàn thành. Chỉ được hủy lô trước khi đi mua và khi các đơn chưa đi tiếp; lô đã hủy thì mọi thao tác đều bị chặn. |
| BR-HUB-001 | Hub | Mỗi chợ chỉ có **tối đa một kho đang hoạt động**, và không gán kho cho chợ đã ngưng hoạt động. |
| BR-HUB-002 | Hub | Không được hạ sức chứa kho xuống **dưới khối lượng đang chiếm dụng**; sức chứa phải lớn hơn 0, tọa độ hợp lệ, địa chỉ tối đa 500 ký tự. |
| BR-HUB-003 | Hub | Không được vô hiệu hóa kho khi vẫn còn **chuyến nhập hàng chờ xử lý**. |
| BR-HUB-004 | Hub | Nhân viên kho và tài xế chỉ truy cập được **kho mình được phân công**; người được phân công phải đang hoạt động và đúng vai trò. |
| BR-HUB-005 | Hub | Khi lô mua được bàn giao, hệ thống **tự tạo phiếu nhập kho ở trạng thái chờ**; không ghi nhận nhập kho hai lần; thao tác quét nhập phải khớp một phiếu đang chờ. |
| BR-HUB-006 | Hub | Không được nhập hàng vượt **sức chứa còn lại của kho**. |
| BR-HUB-007 | Hub | Chênh lệch hàng hóa chỉ được ghi nhận **sau khi hàng đã về kho**; dòng đơn bị chênh lệch phải thuộc chính chuyến nhập đó; số lượng chênh lệch không vượt số lượng của dòng đơn; ảnh bằng chứng phải là đường dẫn an toàn (HTTPS) tối đa 512 ký tự. |
| BR-HUB-008 | Hub | Một chênh lệch đã được xác nhận xử lý thì không xác nhận lại. **Chênh lệch chưa xử lý sẽ chặn tuyến giao khởi hành.** |
| BR-HUB-009 | Hub | Phân loại hàng chỉ áp dụng cho dòng đơn **thuộc kho này và đúng ngày phục vụ**; khối lượng đã phân loại phải lớn hơn 0, không vượt khối lượng yêu cầu và không được nhỏ hơn khối lượng đã phân loại trước đó. |
| BR-HUB-010 | Hub | **Khi đã bắt đầu phân loại hàng, thứ tự điểm dừng của tuyến giao bị khóa.** |
| BR-HUB-011 | Hub | Xuất kho phải gắn với **tuyến giao có thật** và không vượt tồn thực tế tại kho; hàng trung chuyển chỉ xử lý sau khi đã về kho. Giao ca cho tài xế yêu cầu tuyến **đúng kho, đã gán xe và đúng tài xế**; tài xế chỉ nhận ca của mình và chỉ nhận **một lần**. |
| BR-LOG-001 | Logistics | Xe thuộc một trong ba loại **xe van, xe tải, xe máy**; biển số là duy nhất; tải trọng lớn hơn 0. Xe dùng chung cho toàn đội (một kho / một chợ), không cố định theo kho. |
| BR-LOG-002 | Logistics | **Mọi dòng đơn cần định tuyến đều phải có quy cách đóng gói hợp lệ** để tính được khối lượng trước khi hoạch định tuyến. |
| BR-LOG-003 | Logistics | Kho và **mọi nhà hàng đích đều phải có tọa độ** cho ngày phục vụ thì mới hoạch định được tuyến. |
| BR-LOG-004 | Logistics | **Tối đa 20 điểm dừng cho mỗi tuyến giao.** |
| BR-LOG-005 | Logistics | Hệ thống tối ưu tuyến trong tối đa **3 giây**; không tìm được phương án thì báo bất khả thi. Tham số hoạch định mặc định: bắt đầu **6:00**, phục vụ **10 phút mỗi điểm**, chi phí **5.000 VND/km**, hệ số sử dụng tải **90%**. |
| BR-LOG-006 | Logistics | Khoảng cách giữa các điểm được lấy từ dịch vụ bản đồ và lưu đệm **30 ngày**; khi không lấy được, hệ thống ước lượng theo đường chim bay với vận tốc giả định **30 km/h**. |
| BR-LOG-007 | Logistics | Mỗi kho và ngày phục vụ chỉ có **một phương án tuyến đang đề xuất** tại một thời điểm. |
| BR-LOG-008 | Logistics | Phê duyệt phương án tuyến chỉ từ trạng thái **đề xuất**, và bị chặn nếu dữ liệu đầu vào (đơn hàng, tọa độ, đội xe, cấu hình định tuyến) **đã thay đổi sau khi hoạch định**; yêu cầu **không còn đơn chưa gán tuyến** và **mọi tuyến đều có xe đề xuất**. |
| BR-LOG-009 | Logistics | Tuyến phải được **tối ưu trước khi trình duyệt**; tiêu chí tối ưu là **quãng đường, thời gian hoặc chi phí**. |
| BR-LOG-010 | Logistics | Gán xe cho tuyến yêu cầu **có tài xế**, xe **thuộc đúng kho của tuyến**, **khối lượng tuyến không vượt tải trọng xe**, xe và tài xế đủ điều kiện, và **một xe không phục vụ hai tuyến trong cùng ngày**. |
| BR-LOG-011 | Logistics | Chỉ được sắp xếp lại thứ tự điểm dừng khi tuyến **đã gán xe, chưa khởi hành** và kho **chưa bắt đầu phân loại hàng**. |
| BR-LOG-012 | Logistics | Tuyến chỉ khởi hành được khi **đã gán xe/tài xế**, **đã lập danh sách giao hàng**, và **không còn chênh lệch hàng hóa chờ xử lý tại kho**. |
| BR-LOG-013 | Logistics | Tài xế chỉ thao tác trên **tuyến và chuyến giao của chính mình**. Nhận hàng yêu cầu danh sách hàng **khớp đúng phương án tuyến đã duyệt** và gồm **mọi đơn đang ở kho thuộc tuyến đó**; nhà hàng phải là điểm dừng trên tuyến; mỗi đơn chỉ có **một chuyến giao**. |
| BR-LOG-014 | Logistics | Cập nhật trạng thái giao chỉ khi tuyến **đang chạy** và theo lộ trình trạng thái hợp lệ; sự cố giao gồm **không giao được, hàng hư, khách từ chối, khác**, mô tả tối đa 1000 ký tự. Phiếu xếp hàng liệt kê hàng **theo từng điểm dừng, xếp theo thứ tự ngược với thứ tự giao**. Ước lượng thùng theo khối lượng: bì thùng **2 kg**, tải tối đa **25 kg/thùng**. |
| BR-INV-001 | Invoicing | Mỗi đơn **giao hoàn tất** phát sinh **tối đa một hóa đơn**; hóa đơn là **bản chốt bất biến của đơn đã giao**, thuế suất lấy theo giá trị đã chốt trên đơn chứ không lấy lại từ danh mục sản phẩm. |
| BR-INV-002 | Invoicing | Chỉ hóa đơn **đã phát hành** và **đủ dữ liệu** mới được xuất file. |
| BR-INV-003 | Invoicing | Hóa đơn phát hành lỗi được **tự động thử lại tối đa 5 lần**, mỗi lần cách nhau 2 phút; lý do lỗi lưu tối đa 500 ký tự. |
| BR-INV-004 | Invoicing | Hóa đơn trình bày theo **định dạng Việt Nam và giờ Việt Nam**. Tra cứu hóa đơn phải có khoảng thời gian hợp lệ, trạng thái hợp lệ, tối đa 100 bản ghi/trang; tài khoản không gắn nhà hàng nào thì không xem được hóa đơn. |
| BR-INV-005 | Invoicing | Kết nối nhà cung cấp hóa đơn điện tử hiện là **bản giả lập** — chưa tích hợp nhà cung cấp và chữ ký số thật. |
| BR-NOT-001 | Notifications | Đăng ký nhận thông báo đẩy cần định danh người dùng, mã thiết bị và nền tảng thuộc **iOS / Android / Web**. Mỗi thông báo có trạng thái gửi: chờ gửi → đã gửi hoặc thất bại. |
| BR-NOT-002 | Notifications | Thông báo gửi lỗi được **tự động thử lại tối đa 5 lần**, mỗi lần cách nhau 1 phút. Thông báo đẩy **mặc định tắt**. |
| BR-NOT-003 | Notifications | Thông báo được **lưu trước, gửi sau** — mọi sự kiện nghiệp vụ cần thông báo đều được ghi nhận vào hộp thông báo của người dùng kể cả khi kênh gửi thất bại. Không có địa chỉ người nhận thì không gửi email. |
| BR-ANA-001 | Analytics | Xuất dữ liệu báo cáo giới hạn **50.000 dòng** mỗi lần và chỉ với các tập dữ liệu được phép xuất. |
| BR-ANA-002 | Analytics | Đơn được tính là **giao trễ khi chậm quá 15 phút**. Thời lượng giao được tính từ **mốc bàn giao tại kho** (hệ thống chưa ghi nhận mốc khởi hành riêng). |
| BR-ANA-003 | Analytics | Số liệu tổng hợp theo ngày được tính theo giờ Việt Nam. Biểu đồ xu hướng giá giới hạn tối đa **12 tháng**. |
| BR-ANA-004 | Analytics | Báo cáo là dữ liệu **chỉ đọc**, không tạo hay thay đổi dữ liệu nghiệp vụ. |
| BR-AI-001 | AI Assistant | Mỗi tin nhắn tối đa **4000 ký tự**, mã phiên hội thoại tối đa 128 ký tự, giới hạn **15 lượt hỏi/phút**. |
| BR-AI-002 | AI Assistant | Mỗi lượt trả lời, trợ lý gọi tối đa **6 lượt tra cứu dữ liệu**; hội thoại lưu **20 lượt gần nhất** và hết hạn sau **30 phút** không tương tác. |
| BR-AI-003 | AI Assistant | Người dùng chỉ tiếp tục được **hội thoại của chính mình**; truy cập hội thoại của người khác được trả về như không tồn tại. |
| BR-AI-004 | AI Assistant | Mọi dữ liệu người dùng gửi cho trợ lý đều được kiểm tra hợp lệ trước khi xử lý; tham số tra cứu sai định dạng được báo lỗi có cấu trúc thay vì làm gián đoạn hội thoại. |
| BR-AI-005 | AI Assistant | Khi nhà cung cấp mô hình AI gặp sự cố, hệ thống **tự chuyển sang nhà cung cấp dự phòng** theo thứ tự ưu tiên (trừ lỗi xác thực). Trợ lý chỉ **thực hiện hành động thay đổi dữ liệu sau khi người dùng xác nhận**. |

---

## Appendix F — Business Rules (English, SRS)

> Sequential IDs **BR-001…BR-119**, in the same order as Appendix E (row *n* here = row *n* there).
> English wording, no grouping column.
> Business statements only — no class, function, table, configuration key or error code names.

| ID | Rule |
|---|---|
| BR-001 | Every record is identified by a system-wide unique identifier. Core business data uses soft delete (records are marked as deleted rather than physically removed); append-only data (price history, login sessions) and order line items are excluded. |
| BR-002 | Timestamps are stored in universal time; all business dates (order date, delivery date, market session date) are determined in **Vietnam time (GMT+7)**. |
| BR-003 | A business rule violation must return a structured message with a defined error code to the user, and must never interrupt system operation. |
| BR-004 | Every user request must pass input validation before it is processed. |
| BR-005 | When several users edit the same object concurrently, the system detects the conflict and requires the later operation to be retried; order confirmation is processed in isolation so that concurrent access cannot produce inconsistent data. |
| BR-006 | The system has exactly **5 roles**: administrator, restaurant, market agent, hub staff, driver. A user may only perform functions belonging to their role. |
| BR-007 | Rate limits: authentication **10 requests/minute**, orders **30 requests/minute**, AI assistant **15 requests/minute**. Lists return **20 records per page** by default, up to **100** or **200** depending on the list. |
| BR-008 | User input is never concatenated directly into a data query; sort and grouping criteria chosen by the user must come from an allowed list. |
| BR-009 | Login uses email **or** phone number together with a password. A failed login shows only a generic message and never reveals whether the account exists. |
| BR-010 | **5 consecutive wrong passwords** lock the account for **15 minutes**. A deactivated account cannot log in. |
| BR-011 | A login session is valid for **15 minutes** and can be renewed for up to **7 days**. A renewal credential is **single-use**; if it is reused, all sessions of that user are revoked. |
| BR-012 | Version 1 sends codes by **email** only. The email verification code is valid for **10 minutes**, the password reset code for **15 minutes**; both are single-use. |
| BR-013 | Changing a password requires the correct current password, and the new password must differ from the old one. Passwords are always stored hashed, never in plain text. |
| BR-014 | Email and phone number are **unique system-wide**, enforced on self-registration, on administrator-created accounts, and on profile updates. |
| BR-015 | Phone numbers have 7–15 digits (an optional leading `+`); tax codes have 10 digits with an optional 3-digit branch suffix. Length limits: full name ≤255, phone ≤20, email ≤256, address ≤500, contact person ≤200, image/document link ≤512 characters. |
| BR-016 | A restaurant follows the lifecycle **Pending → Active ⇄ Suspended**: new registrations start as pending and must be approved by an administrator; only an active restaurant can be suspended, only a suspended restaurant can be reactivated; approval and suspension cannot be applied twice. |
| BR-017 | A restaurant that is **not yet approved or currently suspended** cannot place orders, confirm orders, or create recurring order schedules. |
| BR-018 | An administrator cannot deactivate their own account, and may only assign roles defined by the system. |
| BR-019 | Only market agents can be assigned to a market, and only to a market that is currently active. |
| BR-020 | A delivery address must have valid coordinates (latitude −90…90, longitude −180…180); the pickup window must end after it starts; each restaurant has exactly **one** default address. |
| BR-021 | Category names, unit-of-measure names and packing codes must each be unique. |
| BR-022 | The category tree has **at most 2 levels**: a parent category must be an active root category; a category cannot be its own parent; a category that already has children cannot become a child itself. |
| BR-023 | A category with active child categories cannot be deactivated. |
| BR-024 | A product must reference a unit of measure, a category and a packing code that exist and are active. |
| BR-025 | Packing codes are uppercase, at most **8 characters**; capacity is a positive whole number and **must not exceed 25 kg**. Length limits: product description ≤2000, category description ≤1000, packing description ≤500, image link ≤512, abbreviation ≤20 characters. The VAT rate declared on the product is the source for the tax rate locked onto an order. |
| BR-026 | A product may be listed **only once** per market. |
| BR-027 | Price must be **greater than 0**, at most **50,000,000 VND**, with at most 2 decimal places; available quantity cannot be negative; a search term is required and limited to 200 characters. |
| BR-028 | A market agent may only edit prices and stock for the **markets they are assigned to**; administrators are not restricted this way. |
| BR-029 | A listed product carries at most **8 tags**; tags must already exist, tag names are unique and at most **30 characters**. |
| BR-030 | Every price change is recorded in an **append-only** price history that is never edited or deleted. A product's price history returns at most **100 records** per request. |
| BR-031 | The price board is cached for at most **5 minutes**; when a price changes the board is refreshed and pushed in real time to users watching that market. |
| BR-032 | Reference prices on the price board are **updated automatically from the confirmed actual purchase prices** of a procurement batch. |
| BR-033 | An order may only move along the defined lifecycle (Draft → Confirmed → Batched → Picked up → At hub → Delivering → Delivered); any other transition is blocked. |
| BR-034 | An order can be cancelled only while it is **Draft** or **Confirmed**. Exception: when an entire market session is cancelled, its batched orders are cancelled with it — this can only happen before the market agent has started purchasing. |
| BR-035 | Only a **Draft** order may have items added, changed or removed, and have pricing applied. |
| BR-036 | The delivery address is locked **exactly once** while the order is still Draft and cannot be changed afterwards. |
| BR-037 | Confirming an order requires: the order belongs to the logged-in restaurant, the restaurant is approved, the order is Draft, and it contains **at least one item**. |
| BR-038 | One order (and one recurring schedule) may contain products from **exactly one market**. |
| BR-039 | An administrator may manually advance an order only to **Batched, Picked up or At hub**. |
| BR-040 | A restaurant may confirm receipt only after the order is **Delivered**, and only **once**. |
| BR-041 | The **daily order cutoff is 22:00 Vietnam time** (configurable). The earliest delivery date is **the next day (D+1)** when confirmed before the cutoff, and **D+2** from the cutoff onwards. A requested date that is missing or too early is **automatically moved** to the nearest valid date instead of being rejected. |
| BR-042 | The delivery date must not be in the past and must fall within the **delivery window** counted from today (default **7 days**, configurable by an administrator between 1 and 30 days). |
| BR-043 | The VAT rate of each order line is locked at pricing time from the rate declared on the product (5%, 8%, 10%, or 0% for non-taxable / non-declarable goods). |
| BR-044 | The delivery fee is based on the **actual road distance** from the source market to the delivery address; if road data is unavailable the system estimates it from straight-line distance multiplied by **1.4**. An order cannot be confirmed when the market or delivery address has no coordinates. All fee and distance parameters must be non-negative. |
| BR-045 | The ordered quantity of each product must meet that product's **minimum order quantity** and, if the product is sold by packing unit, must be a **multiple of the packing weight**. |
| BR-046 | The default delivery fee is **5,000 VND/km**, with no base fee, minimum fee or rounding. An administrator may set: fee per km 0–1,000,000, base and minimum fee 0–10,000,000, rounding unit 0–1,000,000 VND. |
| BR-047 | An order can only be confirmed when the delivery date has an **open market session with remaining capacity**. |
| BR-048 | Stock is **reserved at the moment the order is confirmed** (not at purchase time) and is **released** when the procurement batch is cancelled. |
| BR-049 | If the products, the source market or the delivery address change while an order is being confirmed, the transaction is aborted and the user must retry. |
| BR-050 | Each order line quantity must be **greater than 0**; the product must still be available; the total ordered quantity must not exceed available stock — checked both when adding items and when confirming the order. |
| BR-051 | Recording actual purchases: the actual quantity is non-negative and not greater than the ordered quantity; if anything was purchased the actual unit price must be greater than 0; recording is only possible once the order is batched. Order notes and cancellation reasons are limited to **500 characters**. |
| BR-052 | Creating a recurring schedule requires an approved restaurant, a **daily or weekly** recurrence, a first run in the future, and products from **one market**. Cancelled or inactive schedules cannot be modified. |
| BR-053 | A recurring schedule stores a **product template and a delivery address**; when due, the system generates the order and **confirms it automatically**. If generation fails, or a legacy schedule has no product template, the order is downgraded to Draft and the restaurant is notified to handle it. |
| BR-054 | A goods incident may only be reported for a **Delivered** order, of type **missing, wrong or damaged**; the affected quantity must be greater than 0 and not exceed the ordered quantity; the description is 1–1000 characters; a resolved incident cannot be reopened. |
| BR-055 | A claim may only be opened while the order is **At hub** or **Delivered**; the claim amount must be greater than 0 and **must not exceed the amount charged for that order**; a claim in a final state cannot be changed; **rejection requires a note**, **approval requires an approver and a refund entry**. Reason ≤500, decision note ≤1000 characters. **Refunds are issued as credit adjustments, never through a payment gateway.** |
| BR-056 | Available credit = credit limit − outstanding balance. A charge is allowed only when the amount is greater than 0 and **the resulting balance does not exceed the credit limit**. |
| BR-057 | Credit is charged **when the restaurant confirms the order**, not when delivery completes. |
| BR-058 | Every credit entry (charge, settlement, refund, adjustment) must have an amount **greater than 0**. |
| BR-059 | The credit limit must be non-negative and **must not be set below the current outstanding balance**. |
| BR-060 | Settlements and refunds must not exceed the outstanding balance; a refund for one order must not exceed the amount still charged for that same order. |
| BR-061 | A settlement entry must record **who registered it and a reference number**; the reference is unique per restaurant to prevent duplicate entries. |
| BR-062 | Credit usage alerts: **≥ 80%** raises a warning, **≥ 100%** flags the limit as exceeded; each level is raised **only once** until usage falls back to a lower level. |
| BR-063 | Credit transactions are of four types — charge, settlement, refund and adjustment; settlement methods are bank transfer and manual entry. |
| BR-064 | Credit statements cover a **calendar month in Vietnam time**, are **due 15 days after the period ends**, are issued only after the period has fully closed, and are **immutable snapshots**. A restaurant can only view its own statements. |
| BR-065 | A market session must define a **market, a service date and a closing time**; the closing time must be in the future. |
| BR-066 | A market session is ready only when it has an **active hub**, a **planned capacity greater than 0** (up to 1,000,000 kg), **at least 1 vehicle** and **at least 1 market agent**. |
| BR-067 | A market session cannot be opened after the order cutoff or while unresolved warnings remain. |
| BR-068 | A **closed** market session can no longer be edited; the closing reason is limited to 500 characters; lookup ranges must be valid and the status filter must be one of draft, open or closed. |
| BR-069 | Only a **closed** market session can be batched, and the session must have a hub assigned. A session and an order each belong to **only one active batch** at a time. |
| BR-070 | Batching runs automatically every minute and can be switched on or off. A batch may only be rebuilt while the batch **and** its orders have **not progressed past the batching stage**. |
| BR-071 | A batch must have a **batch code, a date, a market and at least one product line with a positive quantity**; further orders can be merged in only while the batch is in a mergeable state. |
| BR-072 | A shopping manifest can be produced **only when every product in the batch has a reference price**, and must be produced **before** market agents are assigned and before purchases are confirmed. |
| BR-073 | An assigned agent must be a market agent who is **active, assigned to that market and to that specific session**; each product is assigned to only one agent; an agent may only act on their own assignments. |
| BR-074 | Confirming a purchase requires the product to belong to the batch and to be **not yet purchased**, with **exactly one result line per assigned item**, and both the actual quantity and the actual unit price greater than 0. |
| BR-075 | Purchasing exceptions (unavailable, shortfall, price discrepancy, damaged goods) may only be reported at the permitted stage; quantities cannot be negative; notes are limited to 500 characters. |
| BR-076 | **Handing a batch over to the hub** requires purchasing to be complete, **every non-exempt item to be resolved**, and the hub to be identified and active; a batch cannot be handed over twice. |
| BR-077 | On handover, **actual purchased quantities and prices are written back to the corresponding orders**, and the orders in the batch enter the hub together. A batch becomes **Completed** when its last delivery is finished. A batch may only be cancelled before purchasing begins and while its orders have not progressed further; a cancelled batch blocks all further operations. |
| BR-078 | Each market has **at most one active hub**, and a hub cannot be assigned to an inactive market. |
| BR-079 | Hub capacity must not be reduced **below the volume currently occupied**; capacity must be greater than 0, coordinates valid, and the address at most 500 characters. |
| BR-080 | A hub cannot be deactivated while **inbound shipments are still pending**. |
| BR-081 | Hub staff and drivers may only access the **hub they are assigned to**; the assignee must be active and hold the correct role. |
| BR-082 | Handing over a procurement batch **automatically creates a pending inbound record**; an inbound shipment is never recorded twice; a receiving scan must match a pending inbound record. |
| BR-083 | Goods must not be received beyond the hub's **remaining capacity**. |
| BR-084 | A discrepancy may only be recorded **after the goods have arrived at the hub**; the affected order line must belong to that inbound shipment; the affected quantity must not exceed the order line quantity; evidence photos must be secure (HTTPS) links of at most 512 characters. |
| BR-085 | A discrepancy that has already been acknowledged cannot be acknowledged again. **Unresolved discrepancies block a route from departing.** |
| BR-086 | Sorting applies only to order lines **belonging to this hub and to the correct service date**; the sorted weight must be greater than 0, must not exceed the requested quantity, and must never be lowered below a previously sorted weight. |
| BR-087 | **Once sorting has started, the stop order of the route is locked.** |
| BR-088 | An outbound movement must reference an **existing route** and must not exceed the actual stock at the hub; cross-docked goods are handled only after arrival at the hub. Handing a shift over to a driver requires a route belonging to the correct hub, with a vehicle assigned and the matching driver; a driver may check out **only their own shift**, and only **once**. |
| BR-089 | A vehicle is a **van, truck or motorbike**; license plates are unique; capacity must be greater than 0. Vehicles are shared fleet-wide (one hub / one market) rather than fixed to a hub. |
| BR-090 | **Every order line to be routed must have a valid packing code** so that its weight can be computed before route planning. |
| BR-091 | The hub and **every destination restaurant must have coordinates** for the service date before routes can be planned. |
| BR-092 | **At most 20 stops per delivery route.** |
| BR-093 | Route optimization is limited to **3 seconds**; if no solution is found the plan is reported as infeasible. Default planning parameters: start at **6:00**, **10 minutes service time per stop**, cost of **5,000 VND/km**, and a **90%** load utilization factor. |
| BR-094 | Distances between points come from a mapping service and are cached for **30 days**; when unavailable the system estimates straight-line distance at an assumed **30 km/h**. |
| BR-095 | Each hub and service date has **only one proposed route plan** at a time. |
| BR-096 | A route plan can be approved only from the **proposed** state, and is blocked if its inputs (orders, coordinates, fleet, routing configuration) **changed after planning**; approval requires **no unassigned orders** and **a proposed vehicle for every route**. |
| BR-097 | Routes must be **optimized before they are submitted for review**; the optimization criterion is **distance, time or cost**. |
| BR-098 | Assigning a vehicle requires **a driver**, a vehicle **belonging to the route's hub**, a **route load within the vehicle capacity**, an eligible vehicle and driver, and **no vehicle serving two routes on the same service date**. |
| BR-099 | Stops may be reordered only while the route is **assigned, not yet departed** and the hub **has not started sorting**. |
| BR-100 | A route may depart only when it is **assigned a vehicle and driver**, its **delivery list has been created**, and **no hub discrepancies remain unresolved**. |
| BR-101 | A driver may only act on **their own routes and deliveries**. Pickup requires the goods list to **match the approved route plan exactly** and to include **every order at the hub for that route**; the restaurant must be a stop on the route; each order has **exactly one delivery**. |
| BR-102 | Delivery status may be updated only while the route is **in progress** and along valid transitions; delivery incidents are **undeliverable, damaged, customer rejected or other**, with a description of at most 1000 characters. The loading manifest lists goods **per stop, in reverse delivery order**. Box estimation by weight uses a **2 kg** tare and a **25 kg** maximum load per box. |
| BR-103 | Each **completed delivery** produces **at most one invoice**; the invoice is an **immutable snapshot of the delivered order**, and its VAT rate comes from the rate locked on the order rather than from the current product catalogue. |
| BR-104 | Only invoices that are **issued** and **have complete data** can be exported. |
| BR-105 | Failed invoice issuance is **retried automatically up to 5 times**, 2 minutes apart; the failure reason is stored in at most 500 characters. |
| BR-106 | Invoices are rendered in **Vietnamese format and Vietnam time**. Invoice lookups require a valid date range and status, and return at most 100 records per page; an account not linked to a restaurant cannot view invoices. |
| BR-107 | The e-invoice provider integration is currently a **stub** — no real provider or digital signature service is connected yet. |
| BR-108 | Registering for push notifications requires a user identifier, a device token and a platform of **iOS, Android or Web**. Each notification has a send status: pending → sent or failed. |
| BR-109 | Failed notifications are **retried automatically up to 5 times**, 1 minute apart. Push delivery is **disabled by default**. |
| BR-110 | Notifications are **persisted first and sent afterwards** — every business event that warrants a notification is recorded in the user's inbox even if the delivery channel fails. Email is not sent when the recipient address is missing. |
| BR-111 | Report exports are limited to **50,000 rows** per request and only cover datasets that are allowed to be exported. |
| BR-112 | An order counts as **late when it is more than 15 minutes overdue**. Delivery duration is measured from the **hub handover point** (the system does not record a separate departure timestamp). |
| BR-113 | Daily aggregates are computed in Vietnam time. Price trend charts cover at most **12 months**. |
| BR-114 | Analytics is **read-only** and never creates or modifies business data. |
| BR-115 | Each message is limited to **4,000 characters**, the conversation identifier to 128 characters, with a limit of **15 requests per minute**. |
| BR-116 | Per reply the assistant performs at most **6 data lookups**; a conversation keeps its **last 20 turns** and expires after **30 minutes** of inactivity. |
| BR-117 | A user may only resume **their own conversation**; accessing another user's conversation is reported as not found. |
| BR-118 | All user input sent to the assistant is validated before processing; malformed lookup parameters produce a structured error instead of interrupting the conversation. |
| BR-119 | If the AI model provider fails, the system **falls back to a backup provider** in priority order (except on authentication errors). The assistant only **performs data-changing actions after the user confirms them**. |
