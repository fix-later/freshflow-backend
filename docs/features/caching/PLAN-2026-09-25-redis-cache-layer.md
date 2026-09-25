# Plan: Redis như lớp cache (không phải như database)

> **Ngày:** 2026-09-25 · **Phạm vi:** cross-cutting (SharedKernel + API host + Catalog + Analytics)
> **Jira:** chưa gán · **Trạng thái:** Phase 1 **xong, chưa commit** (HIGH-1 + MEDIUM-3 đã fix; MEDIUM-2 chuyển thành gate của Phase 2); Phase 2–4 chưa bắt đầu

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

- TTL 30–60 phút + **tag-evict ngay trong command handler khi admin ghi**
  (`RemoveByTagAsync("catalog:categories")`). Không qua integration event — cùng transaction
  boundary, đơn giản hơn, không cần contract mới.
- Key không chứa yếu tố người dùng → không có rủi ro rò dữ liệu.
- **Gate:** integration test — GET → sửa qua admin API → GET lại thấy giá trị mới **ngay**, không đợi TTL.

### Phase 3 — Analytics (nơi ăn tiền nhất)

9 nhóm query aggregate read-only: `GetDashboardOverview`, `GetOrderMetrics`, `GetProcurementMetrics`,
`GetDeliveryPerformance`, `GetHubThroughput`, `GetDemandHeatmap`, `GetDemandTimeDistribution`,
`GetPriceTrends`, `ExportAnalytics`.

- TTL 2 tầng: khoảng ngày **đã đóng** (hoàn toàn trong quá khứ) → 6–24h;
  khoảng có chứa hôm nay → 3–5 phút.
- Không cần invalidation chủ động — analytics stale vài phút là chấp nhận được, TTL là đủ.
- **Gate:** đo thời gian dashboard trước/sau trên DB thật.

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

### ⏳ MEDIUM-2 (MỞ — là gate của Phase 2) — Fail-open của `HybridCache` chưa được chứng minh
Nguyên tắc #2 của plan (Redis chết → degrade về DB, không 500) hiện **chưa có gì bảo đảm** cho
đường HybridCache: không rõ `HybridCache` nuốt hay ném lỗi L2, và chưa có test. Không chặn Phase 1
(chưa call site nào dùng), nhưng **phải chốt bằng test trước khi Phase 2 ship** — nếu nó ném, mọi
call site Phase 2/3 phải bọc try/catch như `GetMarketProductsQueryHandler` đang làm.

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
