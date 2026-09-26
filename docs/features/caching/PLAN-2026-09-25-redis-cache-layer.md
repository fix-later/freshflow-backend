# Plan: Redis như lớp cache (không phải như database)

> **Ngày:** 2026-09-25 · **Phạm vi:** cross-cutting (SharedKernel + API host + Catalog + Analytics)
> **Jira:** chưa gán · **Trạng thái:** Phase 1 **xong** (`9629d86`) · Phase 2 **xong** (`fa10199` + tag list) · Phase 3 **xong** (`5dcc81a`) · Phase 4 **đang chờ số đo**, xem mục 10

Mục tiêu: đưa Redis vào dùng rộng hơn trong FreshFlow **với vai trò cache cho đường đọc**.
Postgres vẫn là source of truth duy nhất — không dùng Redis như một database.

---

## 1. Hiện trạng (đọc từ code ngày 2026-09-25, không phải từ docs)

| Thứ | Trạng thái |
|---|---|
| Redis container | Có sẵn — `docker-compose.yml`, `redis:7-alpine`, `--requirepass ${REDIS_PASSWORD}` |
| `IConnectionMultiplexer` | Đã đăng ký singleton **nhưng nằm trong `AddPricingModule`** (`Pricing.Infrastructure/DependencyInjection.cs:69`); module khác muốn dùng Redis phải phụ thuộc Pricing |
| Chỗ duy nhất đang dùng | Price board — `RedisPriceBoardCache` (write-through khi giá đổi, TTL 5'), `RedisPriceBoardReader` (overlay lên kết quả DB) |
| Fail-open | Đã làm đúng ở `GetMarketProductsQueryHandler.cs:45` — Redis lỗi → log warning → trả giá từ DB |
| `SharedKernel/Caching/RedisKeys.cs` | **Dead code** — 0 nơi tham chiếu; Pricing tự có `BuildKey` riêng |
| `"SignalR": { "UseRedis": false }` | Flag tồn tại, backplane **chưa implement** |
| Package | Chỉ `StackExchange.Redis` 2.8.16; target `net10.0` |

Kết luận: pattern cache đã có **một bản mẫu chạy thật**. Việc cần làm là nhân rộng đúng cách,
không phải dựng nền mới.

**Hệ quả đã thấy của việc Redis bị khoá trong Pricing:**
`docs/features/logistics/PLAN-2026-08-11-goong-matrix-cache.md` chọn bảng Postgres thay vì Redis,
lý do ghi thẳng trong doc là *"avoids the Pricing-module Redis coupling — Logistics may not reference
Pricing"*. Phase 1 gỡ đúng ràng buộc đó; plan Goong không bắt buộc phải đổi theo (bảng durable vẫn
hợp lý cho dữ liệu tốn tiền gọi API), nhưng lựa chọn sau này sẽ không còn bị kiến trúc ép.

---

## 2. Nguyên tắc

1. **Postgres là source of truth tuyệt đối.** Redis chỉ chứa dữ liệu *tái tạo được*.
   Xoá sạch Redis → app vẫn đúng, chỉ chậm hơn. Không có dữ liệu nào chỉ tồn tại trong Redis.
2. **Fail-open.** Redis chết = degrade về DB, không 500. Copy đúng khuôn try/catch đang có ở Pricing.
3. **Cache đường đọc, không cache đường ghi.** Giai đoạn này không dùng Redis làm
   lock / queue / session store.
4. **Chỉ cache cái vừa đắt vừa chịu được stale.** Đắt mà không chịu stale
   (credit balance, tồn kho lúc đặt đơn) → không cache.

---

## 3. Kiến trúc chọn: `HybridCache` (built-in .NET 9+), không tự viết abstraction

| Phương án | Kết luận |
|---|---|
| Tự viết `ICacheService` + `RedisCacheService` mỗi module | Loại — ~10 bản copy, tự lo serialize, tự lo cache stampede |
| `IDistributedCache` trần | Loại — không có L1, không chống stampede; dashboard analytics miss đồng thời 20 request = 20 query aggregate |
| **`HybridCache`** | **Chọn** — L1 in-memory + L2 Redis, chống stampede, tag-based invalidation, serializer sẵn. ~10 dòng wiring, 1 dòng mỗi call site |

Call site sau khi có:

```csharp
var data = await cache.GetOrCreateAsync(
    key,
    ct => ReallyQueryDbAsync(ct),
    new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5) },
    tags: ["analytics:orders"],
    ct);
```

Package thêm vào `Directory.Packages.props` (pin version khớp dòng 10.0.x):
`Microsoft.Extensions.Caching.Hybrid`, `Microsoft.Extensions.Caching.StackExchangeRedis`.

---

## 4. Phases

### Phase 1 — Hạ tầng dùng chung (không đổi behavior)

- Tách đăng ký `IConnectionMultiplexer` khỏi `AddPricingModule` → một extension dùng chung
  `AddRedisCache(config)`, gọi trong `Program.cs` **trước** các `AddXxxModule`.
  Giữ nguyên `AbortOnConnectFail = false` và thông báo lỗi khi thiếu connection string.
- Đăng ký `AddHybridCache()` + `AddStackExchangeRedisCache()` trỏ cùng connection string `Redis`.
- `RedisKeys.cs`: biến thành **nơi duy nhất sinh cache key** (gồm cả `price:` hiện tại của Pricing),
  để không ai đặt key trùng namespace. Bỏ helper chưa dùng và không có kế hoạch dùng.
- Thêm `Cache:Enabled` (bool) + `Cache:DefaultTtlMinutes` vào `appsettings.json` → tắt được cache khi debug.
- **Gate:** `dotnet build FreshFlow.slnx` + toàn bộ test hiện có xanh; price board không đổi hành vi;
  `dotnet format FreshFlow.slnx --verify-no-changes` sạch.

### Phase 2 — Reference data của Catalog (an toàn nhất, làm trước)

Target: `Categories`, `Units`, `PackingCodes`, `Markets`, `Tags` — gần như không đổi, đọc ở mọi màn hình.

- TTL 30 phút + **tag-evict ngay trong command handler khi admin ghi**. Không qua integration
  event — cùng transaction boundary, đơn giản hơn, không cần contract mới.
- Key không chứa yếu tố người dùng → không có rủi ro rò dữ liệu.
- **Gate:** integration test — GET → sửa qua admin API → GET lại thấy giá trị mới **ngay**, không đợi TTL.

**✅ ĐÃ LÀM 2026-09-25.** Cách triển khai (khác plan gốc một điểm, xem mục 9):

| Thành phần | Nơi |
|---|---|
| Chính sách TTL + 4 tag | `Catalog.Application/Caching/CatalogCache.cs` |
| Evict + try/catch (một chỗ duy nhất) | `Catalog.Application/Caching/CatalogCacheEvictor.cs` |
| Cache key | `SharedKernel/Caching/RedisKeys.cs` — đúng quyết định Phase 1: một nơi sinh key |
| Đọc qua cache | 4 list handler: `GetCategories`, `GetMarkets`, `GetUnits`, `ListPackingCodes` |
| Evict khi ghi | 14 command handler (Categories ×4, Markets ×4, PackingCodes ×3, Units ×3) |

**Chỉ cache list query, không cache `GetXxxById`.** Query ById có thể trả `NOT_FOUND`, mà cache một
failure thì hoặc phải bọc thêm một lớp wrapper serialize được `Error`, hoặc chịu rủi ro 404 dính lại
hết TTL. List query trong Catalog luôn trả `Success`, nên bỏ hẳn nhóm ById gỡ được toàn bộ lớp phức
tạp đó mà gần như không mất lợi ích — ById là tra một dòng, rẻ; list mới là thứ mọi màn hình gọi.

### Phase 3 — Analytics (nơi ăn tiền nhất)

9 nhóm query aggregate read-only: `GetDashboardOverview`, `GetOrderMetrics`, `GetProcurementMetrics`,
`GetDeliveryPerformance`, `GetHubThroughput`, `GetDemandHeatmap`, `GetDemandTimeDistribution`,
`GetPriceTrends`, `ExportAnalytics`.

- TTL 2 tầng: khoảng ngày **đã đóng** (hoàn toàn trong quá khứ) → 6h;
  khoảng có chứa hôm nay → 5 phút.
- Không cần invalidation chủ động — analytics stale vài phút là chấp nhận được, TTL là đủ.

**✅ ĐÃ LÀM 2026-09-25.** 8/9 query cache qua `AnalyticsCache`
(`Analytics.Application/Caching/AnalyticsCache.cs`), key trong `RedisKeys`.

**Bẫy #1 (scope quyền) KHÔNG áp dụng — đã kiểm chứng bằng code.** Grep toàn module Analytics:
không handler nào đọc `ICurrentUser` / `HttpContext` / claim. Mọi filter (`RestaurantId`,
`MarketId`, `HubId`) đến từ **query string**, không suy ra từ JWT, nên kết quả chỉ phụ thuộc tham
số và cache key chỉ cần chứa tham số. Nếu sau này có handler nào đọc danh tính người gọi thì kết
luận này hỏng — phải thêm scope vào key ngay lúc đó.

**Bẫy #2 (ngày VN) áp dụng đúng một chỗ:** `GetDashboardOverviewQuery.Date` nullable. Handler nay
resolve `request.Date ?? cache.Today` **trước** khi dựng key. Có test dịch đồng hồ từ 23:30 sang
00:30 giờ VN chứng minh entry không dính lại sang ngày mới.

**Cache ở mức DTO cuối, không phải ở mức reader** — các handler này còn nhiều post-processing
(gộp bucket, tính tỉ lệ, clamp) sau khi đọc DB; cache DTO cuối bỏ qua được cả hai.

**Không cache `ExportAnalytics`** (lệch plan có chủ đích): export hiếm khi lặp lại đúng tham số,
payload là `byte[]` CSV, nên cache tốn RAM mà gần như không bao giờ hit — trái nguyên tắc #4.
Nó vẫn hưởng lợi gián tiếp vì handler của nó gọi các query khác qua `ISender`, mà những query đó
nay đã có cache.

- **Gate:** đo thời gian dashboard trước/sau trên DB thật — **chưa làm**, xem mục 10.

### Phase 4 — Chọn lọc thêm (chỉ khi đo thấy chậm)

Route suggestions (SCRUM-379), loading manifest, product list theo market.
Làm sau, có số đo rồi mới làm.

### Không nằm trong plan này (cố ý)

- **SignalR Redis backplane** — đó là bài toán *scale-out*, không phải cache; chỉ làm khi thật sự
  chạy >1 instance. Giữ `SignalR:UseRedis = false`.
- Redis làm session store / refresh-token store / distributed lock / queue — YAGNI.
- Cache orders, credit, invoice, auth — đọc rẻ, sai là mất tiền.

---

## 5. Bốn cái bẫy phải xử lý

1. **Key phải mang scope quyền.** Query có lọc theo `restaurantId` hoặc theo role → `restaurantId`
   và role **bắt buộc** nằm trong cache key. Thiếu = IDOR qua cache: nhà hàng A đọc được số liệu
   nhà hàng B. Đây là lỗi CRITICAL, không phải lỗi performance.
2. **Ngày nghiệp vụ là `Asia/Ho_Chi_Minh`.** Key analytics phải chứa **ngày VN đã resolve**
   (`Analytics.Application/Common/VietnamTime.cs`), không phải chuỗi `"today"` — nếu không, qua
   00:00 giờ VN vẫn trả số liệu hôm qua cho đến hết TTL.
3. **Soft-delete + cache.** Bản ghi bị soft-delete phải rơi khỏi cache (tag-evict), nếu không nó
   "sống lại" đến hết TTL.
4. **Cache phải tắt được.** `Cache:Enabled = false` phải làm mọi đường đọc **đi qua lớp HybridCache**
   quay về DB nguyên trạng — cần thiết để phân định lỗi cache với lỗi query. Cờ này **không** gate
   price board Redis overlay của Pricing: đó là cơ chế write-through riêng, đã có fail-open riêng
   (xem mục 8, LOW-4).

---

## 6. Test tối thiểu

- **Unit:** key builder (đặc biệt ca có scope + ngày VN), TTL selection cho analytics.
- **Integration:** 1 test Phase 2 (write → evict → read thấy giá trị mới),
  1 test fail-open (Redis ngừng → endpoint vẫn 200, trả dữ liệu DB).
  Testcontainers đã dùng cho Postgres; thêm Redis module là vài dòng.

---

## 7. Nhật ký

| Ngày | Việc |
|---|---|
| 2026-09-25 | Viết plan; khảo sát hiện trạng Redis trong repo |
| 2026-09-25 | Phase 1 implement xong (working tree, chưa commit). Gate verify độc lập: build 0 error, `dotnet format` exit 0, `dotnet test` exit 0 — Integration 242/242 trên Postgres thật. Không test nào phải sửa. Review ra 4 finding, xem mục 8. |
| 2026-09-25 | Fix HIGH-1 (L2 dùng lại `IConnectionMultiplexer` qua `ConnectionMultiplexerFactory`) + xoá `ICacheService.cs` (MEDIUM-3). Chốt LOW-4 theo hướng sửa plan. Gate chạy lại: build 0 error, **3198/3198 test pass**, format exit 0. Còn mở: MEDIUM-2. |
| 2026-09-25 | Phase 1 commit `9629d86`. |
| 2026-09-25 | MEDIUM-2 đo xong bằng probe thật → phát hiện đọc fail-open nhưng evict thì ném. Phase 2 implement: 4 list query đọc qua cache, 14 command evict theo tag, 6 unit test + 4 integration test mới. Gate: build 0 error, **246/246 integration** (chạy riêng và solution-wide), format exit 0. |

---

## 8. Review Phase 1 (2026-09-25)

### ✅ HIGH-1 (ĐÃ FIX) — L2 Redis mở connection thứ hai, không dùng lại multiplexer
`AddRedisCache` đăng ký `IConnectionMultiplexer` (ép `AbortOnConnectFail=false`) rồi lại gọi
`AddStackExchangeRedisCache(o => o.Configuration = connString)` — tạo **connection pool thứ hai**
tới cùng Redis, bỏ qua hardening ép trong code. Ba hệ quả:

1. Prod đặt `ConnectionStrings__Redis` thiếu `abortConnect=false` → L2 ném lỗi lúc kết nối, trong
   khi multiplexer cũ vẫn sống. Chính tình huống comment gốc ở Pricing viết ra để chống
   (*"regardless of what the connection string says"*).
2. Hai pool tới một Redis, không cần thiết.
3. **Integration test sẽ hỏng ở Phase 2.** `AuthWebAppFactory`/`AssistantWebAppFactory` thay
   `IConnectionMultiplexer` bằng mock NSubstitute *theo ServiceType*, nhưng **không** thay L2.
   Khi Phase 2 bắt đầu gọi `HybridCache`, L2 sẽ nối Redis thật ở `localhost:6379` — đúng cái
   triệu chứng comment trong test file mô tả: *"every Redis command blocks for syncTimeout (~5 s)
   before throwing"*. Test sẽ treo chứ không đỏ rõ ràng.

**Fix** (một chỗ, gỡ cả ba): trỏ L2 về đúng singleton đã đăng ký.

```csharp
services.AddStackExchangeRedisCache(_ => { });
services.AddOptions<RedisCacheOptions>()
    .Configure<IConnectionMultiplexer>((options, multiplexer) =>
        options.ConnectionMultiplexerFactory = () => Task.FromResult(multiplexer));
```

**Đã áp dụng** 2026-09-25 vào `RedisCacheServiceCollectionExtensions.cs`. Gate chạy lại sau fix:
build 0 error, 3198/3198 test pass (Integration 242/242 trên Postgres thật), `dotnet format` exit 0.

### ✅ MEDIUM-2 (ĐÃ ĐO — kết quả BẤT ĐỐI XỨNG) — Fail-open của `HybridCache`
Đo trực tiếp trên package 10.0.0 (2026-09-25) thay vì suy đoán. Kết quả **không đồng nhất giữa
đọc và evict** — đây là điều quan trọng nhất của Phase 2:

| Thao tác | Khi backing store chết |
|---|---|
| `GetOrCreateAsync` (đọc) | **Nuốt lỗi**, chạy factory, trả dữ liệu DB → fail-open sẵn, call site **không** cần try/catch |
| `RemoveAsync` / `RemoveByTagAsync` (evict) | **Ném exception** |

Hệ quả nếu bỏ qua: admin sửa reference data → DB đã commit → evict ném → API trả **500**. Admin
tưởng thao tác hỏng và sửa lại, trong khi dữ liệu đã ghi. Vì vậy mọi lần evict **bắt buộc** đi qua
`CatalogCacheEvictor`, nơi try/catch nằm đúng một chỗ. Có unit test khoá hành vi này lại.

Hai giả định khác cũng đã đo, vì Phase 2 phụ thuộc hoàn toàn vào chúng:
- Factory **ném** lỗi → entry **không** bị cache (nên một `Result.Failure` không thể dính lại hết TTL).
- `RemoveByTagAsync` evict thật, và evict tag này **không** đụng tag khác.

### ✅ MEDIUM-3 (ĐÃ FIX) — `SharedKernel/Caching/ICacheService.cs` vẫn còn (dead code)
Interface cache tự viết, 0 implementation, 0 consumer — đúng loại pattern mục 3 đã loại khi chọn
HybridCache. Để lại là bẫy: dev sau thấy interface có sẵn sẽ implement nó thay vì dùng HybridCache.
**Đã xoá** 2026-09-25 cùng đợt dọn `RedisKeys`.

### ✅ LOW-4 (ĐÃ CHỐT) — Plan bẫy #4 lệch với implementation
Bẫy #4 viết *"`Cache:Enabled=false` phải làm mọi đường đọc quay về DB nguyên trạng"*, nhưng
implementation chỉ gate đường HybridCache — price board Redis overlay vẫn chạy. Quyết định:
**giữ implementation, sửa plan** — price board là cơ chế write-through riêng, đã có fail-open riêng,
và `Cache:Enabled` được định nghĩa lại là *"cờ cho lớp HybridCache mới"*. Bẫy #4 đọc theo nghĩa đó.

### NOTE — version package
`Microsoft.Extensions.Caching.Hybrid = 10.0.0` đã verify đúng: package này release **minor-only**
(10.0.0 → 10.1.0 → … → 10.10.0), không tồn tại patch 10.0.x nào khác. Tuy nhiên các package
minor-versioned khác trong repo (`Microsoft.Extensions.AI`, `Http.Resilience`) đang ở **10.7.0**.
Cân nhắc nâng cho đồng bộ — không phải lỗi.

---

## 9. Ghi chú triển khai Phase 2

**Đã cân nhắc và loại: MediatR pipeline behavior.** Đặt `CachingBehavior<,>` vào pipeline (giống
`ValidationBehavior`) trông gọn hơn — khỏi sửa 18 handler. Loại vì hai lý do cứng:

1. Handler trả `Result<T>`, mà `Result<T>` có constructor private và không có ctor rỗng →
   `HybridCache` **không serialize được**. Muốn cache qua behavior thì phải cache nguyên
   `Result<T>`, tức phải thêm một lớp wrapper serialize được chỉ để phục vụ behavior.
2. Để behavior lấy được `TValue` bên trong `Result<TValue>` thì phải khai báo
   `IPipelineBehavior<TRequest, Result<TValue>>`, đòi MS.DI suy ngược type argument từ generic lồng
   nhau — thứ container không làm được.

Inject thẳng `HybridCache` vào 4 handler đọc thì cache đúng DTO (JSON-friendly), không cần wrapper,
và try/catch của evict vẫn gom đúng một chỗ trong `CatalogCacheEvictor`.

**Test integration phải tự chứng minh là nó có ý nghĩa.** Ba test "ghi xong đọc thấy ngay" vẫn pass
kể cả khi cache bị tắt hoàn toàn — lúc đó chúng chỉ đọc DB hai lần. Nên có thêm
`A_read_actually_populates_the_cache`: sau một GET, dò đúng key bằng `GetOrCreateAsync` với factory
đánh dấu; factory không được chạy. Test này **fail ngay lần đầu viết** vì dò sai kiểu — entry
HybridCache có kiểu, dò cùng key với shape khác đọc ra là miss. Ghi lại ở đây vì đó là cái bẫy rất
dễ vấp khi viết test cache.

**Tags — ĐÃ LÀM 2026-09-26** (đợt riêng, vì `ITagRepository` nằm ở module **Pricing** chứ không
phải Catalog như plan xếp). `PricingCache` gói gọn **một** class thay vì cặp policy + evictor như
Catalog: Pricing chỉ có đúng một query được cache và một tag, nên key và entry options nằm luôn
trong class, không cần generic wrapper. `SetMarketProductTags` **không** evict — nó gán tag cho
market product, không đổi danh sách tag.

Evictor bị **trùng lặp** giữa Catalog và Pricing một cách có chủ ý: nhà chung duy nhất sẽ là
SharedKernel, mà SharedKernel không được phụ thuộc Redis (dependency rule trong CLAUDE.md) — đúng
lý do Phase 1 đặt `AddRedisCache` ở host. Cùng tinh thần với `ValidationBehavior` (11 bản copy,
CLAUDE.md ghi rõ đừng gom lại).

| 2026-09-25 | Phase 3 (Analytics): 8/9 query cache theo TTL 2 tầng, key gồm ngày VN đã resolve. 6 unit test mới. Gate: build 0 error, **3214 test pass** (246 integration), format exit 0. |

---

## 10. Việc còn mở

| # | Việc | Ghi chú |
|---|---|---|
| 1 | **Đo hiệu năng dashboard trước/sau** | Gate của Phase 3, **chưa chạy** — và đây là thứ chặn Phase 4. Test hiện có chứng minh cache *đúng*, không chứng minh cache *đáng*. ⚠️ `appsettings.json` trỏ `localhost:5433` = **DB PRODUCTION**; không benchmark ở đó. Hai lựa chọn an toàn: (a) seed dữ liệu tổng hợp vào Postgres Testcontainers rồi đo cold/warm — an toàn nhưng số liệu phụ thuộc lượng seed; (b) đo trên bản sao dữ liệu thật ở DB riêng. |
| 2 | ~~`Tags` (module Pricing)~~ | ✅ xong 2026-09-26. |
| 3 | Phase 4 | Route suggestions, loading manifest, product list theo market. **Chặn bởi mục 1** — plan quy định "có số đo rồi mới làm". Cache thêm mà không có số đo là đoán, và mỗi chỗ cache thêm là một chỗ có thể stale. |
| 4 | Version `Microsoft.Extensions.Caching.Hybrid` | Đang ghim 10.0.0; các package minor-versioned khác trong repo ở 10.7.0. Không phải lỗi. |

**Cảnh báo cho người làm tiếp:** `AddAnalyticsModule`, `AddCatalogModule` và `AddPricingModule` giờ **không còn tự đủ** —
chúng cần `HybridCache` do host đăng ký. Unit test của Analytics resolve handler qua DI container
thật nên phải gọi thêm `services.AddHybridCache()`; nếu một module mới copy pattern này mà quên,
lỗi sẽ là `Unable to resolve service for type 'HybridCache'` lúc chạy, không phải lúc build.

| 2026-09-26 | Tag list (Pricing) cache 30' + evict khi create/update/delete tag; 3 unit test mới. Gate: build 0 error, **3217 test pass** (246 integration), format exit 0. Phase 4 **không** làm tiếp vì thiếu số đo — xem mục 10. |
