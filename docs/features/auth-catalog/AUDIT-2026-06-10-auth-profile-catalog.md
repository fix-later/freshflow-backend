# Báo cáo rà soát: Auth · Account/Profile · Catalog & Market

| | |
|---|---|
| **Ngày rà soát** | 2026-06-10 |
| **Branch** | `SCRUM-126-CAT-Catalog` (HEAD: `686847f`) |
| **Phạm vi** | Authentication, Account & Profile Management, Catalog & Market Management |
| **Trạng thái test** | 463/463 unit tests PASS (Auth: 340, Catalog: 123) — sau khi `dotnet restore --force-evaluate` |

## Tổng quan

Nền tảng tốt: refresh token rotation + family revocation đúng chuẩn, RBAC phủ đầy đủ endpoint, soft delete và unique index có filter (Catalog), ownership check cho delivery address chống IDOR, anti-oracle cho forgot-password, AdminSeeder fail-fast khi thiếu credential.

Tuy nhiên có **1 bug runtime đang ẩn trên happy path** (mọi POST tạo resource trả 500), **1 lỗ hổng hạ tầng build** (floating package versions), và một số khoảng trống bảo mật/spec cần xử lý trước khi merge.

---

## 🔴 CRITICAL

### C1. `CreatedAtAction(nameof(...Async))` gây 500 trên 5 endpoint POST

ASP.NET Core mặc định strip hậu tố `Async` khỏi action name (`SuppressAsyncSuffixInActionNames = true`), nên `CreatedAtAction("GetProductAsync", ...)` ném `InvalidOperationException` khi tạo resource **thành công** → client nhận 500 thay vì 201. Commit `686847f` đã fix đúng bug này cho AdminController nhưng 5 chỗ còn lại vẫn nguyên:

| File | Dòng |
|---|---|
| `src/FreshFlow.API/Controllers/MarketsController.cs` | 46 |
| `src/FreshFlow.API/Controllers/CategoriesController.cs` | 43 |
| `src/FreshFlow.API/Controllers/ProductsController.cs` | 33 |
| `src/FreshFlow.API/Controllers/UnitsController.cs` | 43 |
| `src/FreshFlow.API/Controllers/RestaurantProfileController.cs` | 92 |

Không bị test bắt vì Catalog không có integration test nào.

**Fix:** set `options.SuppressAsyncSuffixInActionNames = false` một lần trong `AddControllers()` tại `Program.cs` (giải pháp gốc, tránh lặp lại bug), hoặc dùng tên đã strip như AdminController.

### C2. Floating package versions `Version="*"` trên toàn bộ csproj

Solution **không build được** tại thời điểm rà soát: EF Core resolve lệch version giữa các project (10.0.4 / 10.0.8 / 10.0.9 → lỗi CS1705) cho đến khi chạy `dotnet restore --force-evaluate`. Build không tái lập được; CI có thể gãy bất kỳ lúc nào khi NuGet phát hành version mới.

Vị trí: tất cả `PackageReference` trong `src/**/*.csproj` và `tests/**/*.csproj`.

**Fix:** pin version cụ thể; lý tưởng là Central Package Management (`Directory.Packages.props` ở repo root).

### C3. Secrets hardcode trong `appsettings.json` (file đang track trong git)

`src/FreshFlow.API/appsettings.json`:
- Password PostgreSQL: `ffx_pgr_251004`
- Password Redis: `redisdev123`

**Fix:** chuyển sang user-secrets / biến môi trường; **rotate** nếu VPS dev dùng chung các password này. Liên quan: file SSH private key `freshflow_cd_ed25519` ở repo root đã được gitignore và không có trong git history, nhưng nên dời ra ngoài working tree (`~/.ssh/`).

---

## 🟠 HIGH

### H1. Không có rate limiting trên bất kỳ endpoint nào

`Program.cs` không đăng ký `AddRateLimiter`. Lockout chỉ bảo vệ login; `forgot-password`, `verify/request`, `register` có thể bị spam để dội email Resend hoặc dò tài khoản.

**Fix:** thêm `AddRateLimiter` với policy riêng cho nhóm `/api/v1/auth/*`.

### H2. Toàn bộ API không dùng response envelope `{success, data, error}`

docs/04-api-design.md §1.3 và CLAUDE.md quy định envelope `ApiResponse.Ok(...)`, nhưng mọi controller trả raw DTO và lỗi dạng `{code, message}`. Deviation hệ thống — càng để lâu càng đắt vì client sẽ bám theo shape hiện tại.

**Quyết định cần chốt:** implement envelope ngay, hoặc cập nhật docs chính thức bỏ envelope.

### H3. Refresh token reuse trả sai mã so với spec

FR-AUTH-007 AC2 yêu cầu **HTTP 409 `REFRESH_TOKEN_REUSE`**, nhưng:
- `RefreshTokenCommandHandler.cs:16` emit `TOKEN_REUSE_DETECTED` → map về 401.
- Token hết hạn trả `TOKEN_INVALID` thay vì `REFRESH_TOKEN_EXPIRED` (`RefreshTokenCommandHandler.cs:34-35`).

`ErrorExtensions.cs:14,20` đã có sẵn mapping cho cả 2 mã đúng nhưng handler không dùng (dead mapping).

### H4. Restaurant chưa duyệt vẫn login được

`LoginCommandHandler.cs:43-47` là khối `if` rỗng với comment "handled downstream" — không có kiểm tra approval status nào thực sự chạy.

**Quyết định cần chốt:** nếu thiết kế là "pending vẫn login được, chặn ở orders" → xóa khối chết và ghi rõ; nếu phải chặn login → đang thiếu hẳn logic.

---

## 🟡 MEDIUM

| # | Vấn đề | Vị trí | Fix đề xuất |
|---|---|---|---|
| M1 | Search sản phẩm phân biệt hoa thường (`Name.Contains` → LIKE case-sensitive trên Postgres); spec yêu cầu case-insensitive | `ProductRepository.cs:30` | `EF.Functions.ILike` |
| M2 | Filter category chỉ so `LegacyCategory` — sản phẩm mới (dùng `CategoryId`) không bao giờ match | `ProductRepository.cs:33` | Hỗ trợ filter theo `categoryId` |
| M3 | `Product` không có domain invariant (constructor nhận name rỗng), trong khi `Market` có `ValidateName/ValidateCoordinates` | `Product.cs:9-25` | Thêm guard tương tự Market |
| M4 | Section `"Jwt"` chết trong appsettings.json (code bind section `"JWT"` với schema khác `Key/Issuer/Audience`); chạy ngoài Development sẽ crash ở `ValidateOnStart` | `appsettings.json` vs `DependencyInjection.cs:41` | Xóa section cũ, thêm placeholder đúng schema |
| M5 | Lockout hardcode 5 lần / 15 phút; FR-AUTH-009 ghi "configurable" | `User.cs:9-10` | Đưa vào options pattern |
| M6 | Unique index email không filter soft-delete — user bị soft-delete chặn vĩnh viễn email đăng ký lại (Catalog đã làm đúng với `HasFilter`) | `UserConfiguration.cs:14` | `HasFilter("\"DeletedAt\" IS NULL")` + migration |
| M7 | VerifyEmail lộ trạng thái tài khoản: email đã verify + code bừa → 200; email không tồn tại → 400 (oracle) | `VerifyEmailCommandHandler.cs:30-31` | Kiểm tra code trước khi trả idempotent-success |
| M8 | Exception handler nuốt lỗi không log; ForgotPassword nuốt lỗi gửi email không log | `Program.cs:64-84`, `ForgotPasswordCommandHandler.cs:41-44` | Thêm `ILogger` |
| M9 | Login user-enumeration qua timing: user không tồn tại return ngay, không chạy BCrypt | `LoginCommandHandler.cs:27-28` | Verify dummy hash để cân bằng thời gian |
| M10 | Không có CORS, HTTPS redirection/HSTS trong pipeline | `Program.cs` | Thêm trước khi frontend Angular tích hợp |

---

## 🔵 LOW

| # | Vấn đề | Ghi chú |
|---|---|---|
| L1 | Thiếu validator cho command chỉ-có-Guid (`ApproveRestaurant`, `DeactivateCategory/Market/Unit`, `DeleteMarket`, `DeleteDeliveryAddress`) trong khi `DeactivateProduct` lại có | Thống nhất một hướng |
| L2 | `GetMarkets/GetCategories/GetUnits` không phân trang | Chấp nhận được cho lookup table; nên ghi chú |
| L3 | `RevokeByUserAsync` không set `RevokedReason` (`RefreshTokenRepository.cs:26-29`) trong khi `RevokeByFamilyAsync` có | Mất dấu vết audit |
| L4 | Docs lệch code: docs/04 thiếu endpoint `/units`, `/categories`, markets CRUD; POST /products trong docs nhận `unit` string; FR-AUTH-011 nói "không self-registration" nhưng `/auth/register` tồn tại | Cập nhật docs khớp Jira |
| L5 | Rác local: speckit files (`spec.md`, `plan.md`…) nằm nhầm trong `tests/`; các thư mục `TestResults/` | Chưa track trong git; nên dọn + bổ sung .gitignore |

---

## Khoảng trống test lớn nhất

**Catalog có 0 integration test** (Auth có 10 file trong `tests/Integration/FreshFlow.IntegrationTests/Auth/`, Catalog không có) — đây chính là lý do C1 sống sót. Ưu tiên cao nhất sau khi fix: thêm integration test cho các POST endpoint của Catalog.

## Thứ tự xử lý đề xuất

1. **C1** — một dòng config trong `Program.cs` (5 phút)
2. **C2** — pin package versions / Central Package Management
3. **C3** — dọn secrets, rotate nếu cần
4. **H3 + H4** — sửa theo spec (H4 cần chốt thiết kế)
5. **H1 + H2** — quyết định kiến trúc (rate limiter, envelope)
6. **M1–M10**, sau đó **L1–L5**
7. Bổ sung integration tests cho Catalog
