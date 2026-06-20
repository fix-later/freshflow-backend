# Survey: AI Shopping Assistant — Feasibility & Landscape

| | |
|---|---|
| **Date** | 2026-06-18 (§3 module-boundary rec; §1a thin-query specs; Q2→2-A; Q3 shared-evaluator sketch — same day) |
| **Branch** | `SCRUM-179-ORD-Order-Management` |
| **Type** | Survey / feasibility / design analysis only — **no implementation, no code changes, no module scaffolding** |
| **Author** | Leader agent (team `backend-dev`) |
| **Purpose** | Map what already exists, what is missing, where an "AI Shopping Assistant" would architecturally fit, and what to worry about — to inform a future design discussion. Does **not** propose an LLM provider or write code. |

> **Premise (from supervisor):** A chat layer where users type natural language and an AI orchestrates calls to **existing** backend functions. The AI does intent recognition + conversation only. It never touches the DB directly, never auto-confirms an order, and every state-changing action goes through existing Application-layer commands. All business rules stay in the backend exactly as today. User must explicitly confirm before an order is placed.

---

## 1. Existing capabilities the assistant could map onto (tool surface)

The assistant's "tools" would be 1:1 wrappers over existing MediatR commands/queries. Below is what exists today and what it maps to.

### Fully reusable as-is

| Assistant intent | Existing command/query | Module | Notes |
|---|---|---|---|
| Search products ("tìm cà chua bi") | `GetProductsQuery(Search, Category, IncludeInactive, Page, PageSize)` | Catalog | Has a `Search` text field already. Catalog = global product catalog. |
| Search market products (price + stock in a market) | `GetMarketProductsQuery(MarketId, Category?, Cursor?, PageSize)` | Pricing | Market-scoped; returns price + availability. Cursor-paginated. |
| Product detail | `GetProductByIdQuery(id)` | Catalog | — |
| Stock / price / name check for a specific market product | `IMarketProductReader.FindAsync(marketProductId)` | Orders (cross-module read) | Returns `MarketProductSnapshotDto(ProductName, CurrentPrice, AvailableQuantity)`; **available = current − reserved**. This is the same read Orders uses to validate items. |
| Create draft order | `CreateDraftOrderCommand(UserId, Items, ScheduledFor?, Notes?)` | Orders | Enriches each item with live price/name server-side. |
| Add item to draft | `AddOrderItemCommand(UserId, OrderId, MarketProductId, Quantity)` | Orders | Domain enforces `ORDER_NOT_DRAFT`. |
| Update item qty | `UpdateOrderItemCommand(UserId, OrderId, ItemId, Quantity)` | Orders | — |
| Remove item | `RemoveOrderItemCommand(UserId, OrderId, ItemId)` | Orders | — |
| Calculate total | (implicit) `Order.TotalAmount`, recalculated on every item mutation | Orders | No separate "calculate total" call needed — the draft DTO already carries the running total. |
| Order detail / "prepare confirmation screen" | `GetOrderQuery(UserId, canReadAll, OrderId)` | Orders | Returns order + line items + total. This IS the confirmation-screen payload. |
| List / history ("đặt lại đơn hôm qua" step 1: find it) | `ListOrdersQuery(...)` + `GetOrderQuery(...)` | Orders | Filter by date/status, then fetch detail. |
| Reorder from history ("đặt lại đơn hôm qua") | `ReorderFromHistoryCommand(UserId, OrderId, ScheduledFor?, Notes?)` | Orders | Already builds a fresh draft from a past order using current price/stock. |
| Confirm order (only on explicit user yes) | `ConfirmOrderCommand(UserId, OrderId)` | Orders | Checks credit, locks price, applies cutoff, charges debt. **This is the human-gated step.** |

### Missing / would need a thin new query (not a redesign)

- **Natural product search across markets is not a single call.** `GetProductsQuery` (Catalog) searches the global catalog by text; `GetMarketProductsQuery` (Pricing) lists a *single market's* products by category with a cursor, **not by free-text name**. To answer "tìm cà chua bi" *with price + stock for the user's market(s)*, you'd likely want a thin new read combining text search + market-scoped price/stock (or the assistant chains: text-search in Catalog → resolve productId → look up its market product). Reusable building blocks exist; the *combined* query does not.
- **"Which market is this restaurant ordering from?"** — a draft order is per restaurant, and items are market products, but there's no obvious single query that says "for this user's restaurant, here's the active market + its searchable products." The assistant would need to resolve market context (probably via the restaurant profile / assigned market). Worth confirming during design.
- **No "validate cart without confirming" call distinct from confirm.** Validation (credit check, empty-order, cutoff/delivery-window) currently runs *inside* `ConfirmOrderCommand`. A "show me what will happen if I confirm" preview (e.g. surface `CREDIT_LIMIT_EXCEEDED` *before* the user says yes) would need either a new read-only "preview/dry-run confirm" query or careful reuse of `CanChargeAsync` + the cutoff scheduler. This matters for good assistant UX (warn before the user commits).

**Bottom line:** ~90% of the tool surface already exists as reusable Application-layer commands/queries. The gaps are *thin read-side conveniences* (combined product+price+stock search, market-context resolution, a confirm preview), not new business logic.

---

## 1a. Thin query specs (module-side, per the Option B decision)

These spec out the three gaps from §1. Per §3, they are **normal MediatR queries inside their owning module** — the host orchestrator calls them via the shared `ISender`, so request/response types live in the owning module's `Application` layer with **no contract relocation** needed. Each is a standard `IQuery<T>` (or `IRequest<Result<T>>`) + `internal` handler + co-located validator, discovered by the module's existing `AddMediatR` assembly scan. None requires touching the Option-B host except to call it.

### Query 1 — Combined free-text product search with market price/stock

**Decision: this belongs in the Pricing module.** Justification: the *output* the assistant needs is price + availability (`available = current − reserved`), which is Pricing-owned data (`market_products`). Catalog only knows the global product (name/category/active), not price or stock. The free-text match is on the product *name*, but Pricing already joins to Catalog's `products` for the name snapshot (see the existing `MarketProductRow` projection, which carries `ProductName` sourced from Catalog). So Pricing can satisfy the whole request in one round-trip; routing it through Catalog would force a second hop back to Pricing for price/stock. Putting it in Catalog would also make Catalog depend on Pricing data it doesn't own — wrong direction.

- **Name:** `SearchMarketProductsQuery`
- **Module:** Pricing (`FreshFlow.Pricing.Application/Queries/SearchMarketProducts/`)
- **Parameters:**
  - `Guid MarketId` — the market to search within (see Query 2 for how the assistant obtains this; today every product lookup is implicitly market-scoped via the `marketProductId`, but free-text search needs an explicit market scope).
  - `string SearchText` — free-text matched against product name (and optionally category).
  - `string? Category` — optional category filter (mirrors existing `GetMarketProductsQuery`).
  - `bool InStockOnly = false` — convenience filter so the assistant can ask "what tomatoes are actually available".
  - `string? Cursor = null`, `int PageSize = 20` — reuse the cursor-pagination shape already established by `GetMarketProductsQuery`.
- **Returns:** `SearchMarketProductsResultDto` — a paged list of `MarketProductSearchItemDto(Guid MarketProductId, Guid ProductId, string ProductName, string? Category, decimal CurrentPrice, int AvailableQuantity)` + `string? NextCursor`. Note `AvailableQuantity` is the **computed** `CurrentQuantity − ReservedQuantity`, identical to what `IMarketProductReader.FindAsync` already returns for a single product — so the search returns the same availability semantics the order-validation path uses (no divergence).
- **How it resolves the three requirements in one round-trip:** it's a single EF read over `market_products` **joined to** Catalog `products` (the join already exists in the `MarketProductRow`/`MarketProductReader` projection): filter `market_id = @MarketId` AND `products.name ILIKE %SearchText%` (+ active product, + soft-delete excluded) AND (`InStockOnly` ⇒ `current_quantity − reserved_quantity > 0`); project `available = current_quantity − reserved_quantity`; order + cursor-paginate. One query, no N+1.
- **New query or new projection?** **New query, reusing an existing projection shape.** It does **not** need to touch `IMarketProductReader` (that's a single-product point lookup owned by Orders' cross-module reader — wrong module and wrong cardinality). It needs **a new repository method on Pricing's own `IMarketProductRepository`** (e.g. `SearchAsync(criteria, ct)`) implemented over the Pricing-owned `MarketProduct` entity + the existing Catalog join. This keeps the read inside Pricing's own data ownership.
- **Effort: M.** New query/handler/validator + one repository method with a name-search join and cursor pagination + DTOs + unit tests. No schema change (an index on product name may be worth adding for ILIKE performance, but functionally optional for v1).

### Query 2 — Market context (DECIDED: 2-A — market selected at app-shell, assistant just reads it)

**Product decision (2026-06-18):** the user picks the market/chợ at the **app-shell level** (a market switcher shown at login / app start), **before** entering the main flow. By the time the user chats with the assistant, a `MarketId` is already selected and live as **client session/app context**. The assistant (host orchestrator) simply **reads the already-selected `MarketId`** from whatever the client sends and passes it straight into `SearchMarketProductsQuery` etc. The assistant does **not** ask "bạn muốn mua ở chợ nào?" in normal flow — only as a fallback if no market is somehow selected. No restaurant→market binding is introduced.

**Verified in code (per instruction — checked the existing `market:{marketId}` mechanic before claiming reuse):**

- **`MarketId` is always client-supplied per call, never server-side session/JWT state.** `GET /api/v1/markets/{marketId}/products` takes it as a **route param**; `PricingHub.JoinMarketAsync(marketId)` takes it as a **client-invoked method argument**. There is **no backend "current market" session concept anywhere** — the client decides the market on every request. So "market lives as session/app context" is a **client-side** convention today, not something the backend stores or resolves. **Good news for the assistant:** this is exactly the pattern the refinement wants — the orchestrator reads the client-sent `MarketId` (route/query/header, however the chat transport carries it) and forwards it, identical to how `MarketsController` already forwards a route `marketId` into `GetMarketProductsQuery`. **No new "current market" concept is invented; the assistant reuses the existing "client supplies MarketId per call" pattern.**
- **BUT the existing real-time market-scoping mechanic (PricingHub) is the WRONG audience and is NOT reusable as-is for restaurants.** `PricingHub.JoinMarketAsync` gates access via `AuthorizeMarketAccessAsync`, which requires the caller to be **admin** or hold an active `user_market_assignments` row — i.e. the **`market_agent`** flow (agents managing markets they're assigned to). A **restaurant** user has **no** `user_market_assignments` rows, so a restaurant would be **denied** `JoinMarketAsync` for any market. **Honest gap:** there is no existing restaurant-facing market-selection/scoping mechanic — the only one that exists is built for price-setting agents, a different audience. The *pattern* (client supplies `MarketId` per call) is reusable; the *PricingHub authorization mechanic* is not. The assistant's REST tool calls don't need PricingHub at all — they just need the client-sent `MarketId`, which carries no agent-assignment gate on the read endpoints (`GET /markets/{marketId}/products` is open to any authenticated user).

**What backs the app-shell market picker:** `GetMarketsQuery(bool ActiveOnly = true) : IQuery<IReadOnlyList<MarketDto>>` (Catalog) — **verified it already exists** and its endpoint `GET /api/v1/markets` is open to **any authenticated user** (no role gate, no agent-assignment gate), so it can power a restaurant-facing market switcher **as-is, zero new code**. This query backs the **picker UI at app-shell level**, NOT the chat — the assistant never calls it in the normal path; it only consumes the resulting selected `MarketId`.

- **Assistant-side requirement (not a new backend query):** the chat transport must carry the selected `MarketId` into the orchestrator (e.g. a header/query param on the chat request, or part of the chat session payload), exactly as `MarketsController` receives a route `marketId`. The orchestrator passes it to `SearchMarketProductsQuery`. Fallback only: if `MarketId` is absent, the assistant can surface `GetMarketsQuery` results to prompt a pick — an edge case, not the main flow.
- **Option 2-B (a real restaurant→market "home market" binding) remains explicitly DEFERRED / not chosen.** Would be a schema + Auth-module change + product decision; not needed under 2-A. Recorded only as the alternative product could pick later.
- **Effort: S → effectively XS for the backend.** `GetMarketsQuery` is reusable as-is (zero new backend code for the picker). The only real work is **client/transport plumbing** to carry the selected `MarketId` into the chat request — an assistant-feature concern, not a module query. No schema change. (2-B remains **L**, deferred.)

### Query 3 — Confirm-preview / dry-run (surface confirm-time failures without mutating state)

- **Name:** `PreviewOrderConfirmationQuery`
- **Module:** Orders (`FreshFlow.Orders.Application/Queries/PreviewOrderConfirmation/`)
- **Parameters:** `Guid UserId`, `Guid OrderId` — identical inputs to `ConfirmOrderCommand`, so the assistant calls preview with exactly what it would later confirm with.
- **Returns:** `OrderConfirmationPreviewDto(bool WouldSucceed, IReadOnlyList<PreviewIssueDto> Issues, decimal TotalAmount, DateTime? ResolvedScheduledFor, decimal? RemainingCreditAfter)` where `PreviewIssueDto(string Code, string Message)` carries the would-be errors (`ORDER_NOT_DRAFT`, `ORDER_EMPTY`, `CREDIT_LIMIT_EXCEEDED`, `DELIVERY_DATE_OUT_OF_WINDOW`, `FORBIDDEN`). `WouldSucceed = Issues.Count == 0`. Surfacing `ResolvedScheduledFor` lets the assistant say "this will be delivered on D+2 because it's after the 22:00 cutoff" before the user commits; `RemainingCreditAfter` powers a "you'll have X credit left" confirmation message.
- **How it reuses existing validation WITHOUT duplicating rules — the key design point.** Today `ConfirmOrderCommandHandler` runs, in order: load order (404) → ownership (`FORBIDDEN`) → `order.CanConfirm()` (non-mutating: `ORDER_NOT_DRAFT`/`ORDER_EMPTY`) → `creditService.CanChargeAsync(...)` (`CREDIT_LIMIT_EXCEEDED`) → `OrderCutoffScheduler.ResolveScheduledFor(...)` + `IsWithinDeliveryWindow(...)` (`DELIVERY_DATE_OUT_OF_WINDOW`). **Every one of these checks is already non-mutating** — the mutation only starts at `order.RescheduleFor(...)` / `order.Confirm()` / `ChargeAsync(...)`. So the preview is literally "run the handler's validation prologue and stop before the first mutation."
  - **Recommended refactor: extract the shared validation prologue into one method** — e.g. `internal static OrderConfirmationCheck OrderConfirmationValidator.Evaluate(Order order, RestaurantSnapshotDto restaurant, CreditCheckDto creditCheck, DateTime nowUtc)` returning the resolved schedule + the list of issues. Both `ConfirmOrderCommandHandler` (which then proceeds to mutate when issues are empty) and `PreviewOrderConfirmationQueryHandler` (which just maps the result to the DTO) call it. This guarantees the preview and the real confirm can never diverge — they execute the *same* code. `CanConfirm()` already exists on the aggregate (non-mutating) and stays as-is; `CanChargeAsync` already exists on `ICreditService` and is reused directly (it's already the read-only half of the charge).
- **Is the refactor invasive? No — Low.** `ConfirmOrderCommandHandler` is ~50 lines; extracting its pre-mutation checks into a static evaluator is a mechanical move that leaves the confirm behavior byte-for-byte identical (same checks, same order, same error codes) and is fully covered by the existing 7 ConfirmOrder handler tests as a regression guard. The new query is then thin. The one genuinely new read is computing `RemainingCreditAfter` (current outstanding + total vs. limit), which `CanChargeAsync`/the credit account already has the inputs for.
- **Effort: M** — small shared-evaluator extraction (Low on its own) + new query/handler/DTOs + tests asserting preview matches confirm outcomes across each issue code.

#### Query 3 — shared evaluator sketch

Verified against the current code before sketching: `ICreditService.CanChargeAsync(Guid restaurantId, decimal amount, CancellationToken)` returns `Result<CreditCheckDto>` where `CreditCheckDto(RestaurantId, CreditLimit, OutstandingBalance, AvailableCredit, RequestedAmount, CanCharge)`; `OrderCutoffScheduler.ResolveScheduledFor(DateTime confirmedAtUtc, DateTime? requested)` and `IsWithinDeliveryWindow(DateTime nowUtc, DateTime scheduledFor)` are pure static methods; `order.TotalAmount` is computed from line items only.

**1. Method signature (one evaluator, always collect-all).**

```
// New file: FreshFlow.Orders.Application/Services/OrderConfirmationEvaluator.cs
internal static class OrderConfirmationEvaluator
{
    // Pure, non-mutating. Does NOT load the order, check ownership, or call the DB —
    // those stay in the handler (they need repositories). It takes the already-loaded
    // aggregate + the already-resolved restaurant + the credit-check result + "now",
    // and decides the schedule + collects every business issue.
    public static OrderConfirmationEvaluation Evaluate(
        Order order,
        CreditCheckDto creditCheck,
        DateTime nowUtc);
}
```

Deliberate scoping decision: the evaluator covers the **pure, in-memory** part of the prologue (`CanConfirm` + credit verdict interpretation + schedule resolution/window). The two checks that require I/O — **load order (404)** and **ownership (`FORBIDDEN`)** — stay in each handler, because they need `IOrderRepository`/`IRestaurantReader` and both call sites must do them first anyway (you can't evaluate an order you couldn't load or that isn't the caller's). This keeps the evaluator a pure function (trivially unit-testable, no mocks) and still guarantees the *business-rule* checks can't diverge. The credit check itself (`CanChargeAsync`, async/DB) is run by the **caller** and its `CreditCheckDto` passed in — so the evaluator stays synchronous and pure, and both call sites share the exact interpretation of that DTO.

**2. Return shape — one shape, always collects all issues; confirm path picks the first.**

```
internal sealed record OrderConfirmationEvaluation(
    IReadOnlyList<Error> Issues,        // empty => would succeed; uses the SAME Error factory
                                        // (Error.Conflict/Validation) the handler returns today,
                                        // so codes are identical by construction
    DateTime? ResolvedScheduledFor,     // output of ResolveScheduledFor (null only if order had none)
    decimal TotalAmount,                // echo of order.TotalAmount for the DTO
    CreditCheckDto CreditCheck);        // echoed so the query can compute RemainingCreditAfter

internal bool WouldSucceed => Issues.Count == 0;
```

**One shape serves both modes.** The evaluator always runs every check and appends to `Issues` (collect-all). The two call sites consume it differently with **zero behavior change**:
- **Confirm handler (fail-fast):** `if (eval.Issues.Count > 0) return Result.Failure(eval.Issues[0]);` — returns the **first** issue. Because the evaluator appends issues **in the exact same order the handler checks them today** (CanConfirm → credit → delivery window), `Issues[0]` is byte-for-byte the same `Error` the current fail-fast code would have returned. So the real confirm path is behaviorally identical.
- **Preview query (collect-all):** maps the **whole** `Issues` list into `PreviewIssueDto[]`, so the assistant shows every problem at once.

This is the cleanest resolution of the fail-fast-vs-collect-all tension: **collect-all is a superset of fail-fast**; fail-fast is just "take `Issues[0]`". No second code path, no mode flag.

**3. Where it lives + before/after of the confirm handler (mechanical).**

Lives in `FreshFlow.Orders.Application/Services/OrderConfirmationEvaluator.cs` (a new `internal static` class, peer to the existing `OrderCutoffScheduler` static service). `ConfirmOrderCommandHandler`'s first ~20 lines change from inline checks to: load + ownership (unchanged) → call `CanChargeAsync` → call `Evaluate(...)` → fail-fast on `Issues[0]`. Conceptual before/after:

```
// BEFORE (today, inline):
order = await repo.FindByIdAsync(...);            if (order is null) return NotFound;     // stays
restaurant = await restaurantReader...;           if (!owns) return Forbidden;            // stays
var canConfirm = order.CanConfirm();              if (canConfirm.IsFailure) return it;     // → evaluator
var canCharge  = await credit.CanChargeAsync(...);if (canCharge.IsFailure) return it;      // → evaluator
var resolved   = Scheduler.ResolveScheduledFor(...);
if (resolved is not null && !Scheduler.IsWithinDeliveryWindow(...)) return DeliveryError;  // → evaluator
// ... then MUTATE: RescheduleFor / Confirm / ChargeAsync

// AFTER (extracted; mutation tail unchanged):
order = await repo.FindByIdAsync(...);            if (order is null) return NotFound;      // unchanged
restaurant = await restaurantReader...;           if (!owns) return Forbidden;             // unchanged
var canCharge = await credit.CanChargeAsync(order.RestaurantId, order.TotalAmount, ct);
//   NOTE: if CanChargeAsync returns Result.Failure (e.g. missing account), handler still
//   short-circuits exactly as today — see edge case (5). On success, pass its CreditCheckDto in.
var eval = OrderConfirmationEvaluator.Evaluate(order, canCharge.Value, nowUtc);
if (!eval.WouldSucceed) return Result<OrderDto>.Failure(eval.Issues[0]);   // same first error as before
// ... then MUTATE exactly as today: order.RescheduleFor(eval.ResolvedScheduledFor) / Confirm / ChargeAsync
```

The mutation tail (`RescheduleFor` → `Confirm` → `Track` → `ChargeAsync`) is untouched. The 7 existing `ConfirmOrderCommandHandler` tests assert the same error codes/precedence and the same success path, so they stay green as the regression guard — that's the proof the extraction is pure.

**4. `PreviewOrderConfirmationQueryHandler` — same evaluator, maps full list.**

```
// load order (404) + ownership (FORBIDDEN) — SAME repo/reader calls as confirm
var canCharge = await credit.CanChargeAsync(order.RestaurantId, order.TotalAmount, ct);
var eval = OrderConfirmationEvaluator.Evaluate(order, canChargeValueOrSynthesized, nowUtc);

return new OrderConfirmationPreviewDto(
    WouldSucceed:        eval.WouldSucceed,
    Issues:              eval.Issues.Select(e => new PreviewIssueDto(e.Code, e.Message)).ToList(),
    TotalAmount:         eval.TotalAmount,
    ResolvedScheduledFor:eval.ResolvedScheduledFor,
    RemainingCreditAfter:eval.CreditCheck.AvailableCredit - eval.TotalAmount);
//   RemainingCreditAfter is derived purely from CreditCheckDto (CreditLimit/OutstandingBalance/
//   AvailableCredit) — no new repo read; the same DTO the confirm path already fetches.
```

Note the preview handler still returns 404/FORBIDDEN as `Result` failures for a missing/not-owned order (same as confirm) — those are not "preview issues", they're access errors; the assistant shouldn't preview an order the user can't see.

**5. Pure refactor? Yes — with one sequencing nuance to handle explicitly.**

- **Confirm path is behaviorally identical** because collect-all + take-`Issues[0]` reproduces today's fail-fast first-error, given the evaluator appends in today's check order.
- **The edge case the supervisor flagged — does `CanChargeAsync` depend on schedule resolution? NO, verified.** `CanChargeAsync` charges `order.TotalAmount`, which is computed from line items only (`RecalculateTotal`) and is **independent of `ScheduledFor`**. Schedule resolution (`ResolveScheduledFor`/window) and the credit check are **orthogonal** — neither feeds the other. So "collect all" can run them in either relative order without changing any individual verdict; we simply keep today's order (credit before delivery-window) so `Issues[0]` matches the current fail-fast result.
- **The one real nuance — `CanChargeAsync` returning a `Result.Failure` vs. a `CreditCheckDto` with `CanCharge=false`.** Today the handler treats a failed `CanChargeAsync` `Result` as a short-circuit. Two sub-cases:
  - If `CanChargeAsync` returns `Result.Failure` for *infrastructure* reasons (e.g. missing credit account) → both handlers should short-circuit on it **before** calling the evaluator (it's not a "business issue" the preview can enumerate alongside others; it's an I/O error). Confirm behavior unchanged.
  - If over-limit is represented as `Result.Success(CreditCheckDto{CanCharge=false})` (not a failure Result), then the evaluator inspects `creditCheck.CanCharge` and appends the `CREDIT_LIMIT_EXCEEDED` issue — enabling collect-all. **Action item for implementation:** confirm which representation `CreditService.CanChargeAsync` actually uses for "over limit" (Result.Failure vs. CanCharge=false flag); the evaluator's credit branch must match it so confirm precedence is preserved. This is the single thing to pin down during implementation; it does not change the design, only the credit branch's exact condition. *(For the assistant's "show all problems" UX, the `CanCharge=false`-as-success representation is preferable since it lets over-limit coexist with, say, a delivery-window issue in one preview; if it's currently a hard `Result.Failure`, a tiny adapter in the preview handler can still surface it as an issue without changing the confirm path.)*

**Net:** pure extraction, Low invasiveness, confirm path provably unchanged (guarded by the 7 existing tests), and the only implementation-time decision is matching the credit branch to `CanChargeAsync`'s over-limit representation.

### Where the request/response types live (all three) & summary

| Query | Module | New repo method? | Schema change? | Effort |
|---|---|---|---|---|
| `SearchMarketProductsQuery` | Pricing | Yes (`IMarketProductRepository.SearchAsync`) | No (optional name index) | **M** |
| Market picker (2-A, DECIDED) | Catalog | No — **reuses existing `GetMarketsQuery`** (open to any auth'd user) | No | **XS** (backs app-shell picker; assistant just reads client-sent `MarketId`) |
| (`restaurant→market` binding, 2-B) | Auth + product decision | n/a | **Yes** | **L — defer, needs product sign-off** |
| `PreviewOrderConfirmationQuery` | Orders | No (reuses repo + `ICreditService` + scheduler) | No | **M** (incl. Low shared-evaluator extraction) |

All request/response types are ordinary MediatR query + DTO records inside their owning module's `Application` project — consistent with Option B (host calls `ISender`; **no contract relocation, no shared-assembly work**). The only cross-module-shaped item is Query 2's underlying *product question* (no restaurant→market concept exists today), which is flagged for product, not silently invented.

---

## 2. Draft-order mechanics (confirmed — the assistant is just a new client)

The Orders module already implements full draft/cart semantics on the `Order` aggregate (`src/Modules/Orders/FreshFlow.Orders.Domain/Entities/Order.cs`):

- **Draft state:** `Order` is created in `OrderStatus.Draft`, `PaymentStatus.NotApplicable`, `TotalAmount = 0`.
- **Item mutations** (`AddItem` / `UpdateItem` / `RemoveItem`): all guard `Status == Draft` (return `ORDER_NOT_DRAFT` otherwise) and call `RecalculateTotal()` after every change. So the running total is always current on the aggregate.
- **Confirm** (`CanConfirm()` then `Confirm()`): `CanConfirm()` is a non-mutating guard (rejects non-draft → `ORDER_NOT_DRAFT`, empty → `ORDER_EMPTY`). `Confirm()` locks each item's price, transitions `Draft → Confirmed`, sets `PaymentStatus.Outstanding`, raises `OrderConfirmedDomainEvent`. The handler additionally does credit check + cutoff reschedule + debt charge in one DbContext transaction.
- **State machine** is explicit (`AllowedTransitions`): `Draft → Confirmed|Cancelled`, then the logistics pipeline. The assistant cannot skip states; the domain rejects invalid transitions.

**Implication:** The assistant adds **zero** new order logic. It calls the same commands the `OrdersController` already calls. The "never auto-confirm" rule is naturally enforceable: the assistant simply must not call `ConfirmOrderCommand` until the user explicitly confirms — and even if it did, all the business guards (credit, empty, cutoff, ownership) still fire server-side.

---

## 3. Where this fits architecturally (modular-monolith rules) — deep analysis + recommendation

Per `CLAUDE.md`: `Domain → SharedKernel`; `Application → Domain + SharedKernel + Contracts`; `Infrastructure → Application + EF/Redis`; **no module's Domain/Application may reference another module's projects** — cross-module only via `FreshFlow.Contracts` (events) or read-projections.

**Tension:** The assistant inherently needs to *call* Orders and Pricing/Catalog **commands/queries** (not just consume events). That is a synchronous Application-layer dependency, which the current rules deliberately avoid between modules (Orders reads Pricing data via a read-only projection over the shared `AppDbContext`, not by referencing `Pricing.Application`).

### Verified technical facts that drive the decision

These were checked directly against the code, and they change the calculus materially:

1. **MediatR is registered per-module but into ONE shared DI container.** Each module's `DependencyInjection` calls `services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly))`, all into the same `IServiceCollection`. `Program.cs` chains `AddAuthModule → AddCatalogModule → AddPricingModule → AddOrdersModule`. **Net effect: a single `ISender` in the host already resolves handlers from every module.** So host code can dispatch *any* module's command today with zero new wiring.
2. **`ICommand`/`IQuery` live in `FreshFlow.SharedKernel/Application`** and reference only `Result` + MediatR — **no Domain coupling**. Request *interfaces* are shared already.
3. **BUT each command record references its own module's `Application.Dtos`** — e.g. `CreateDraftOrderCommand` takes `DraftOrderItemRequest` and returns `OrderDto`, both in `Orders.Application.Dtos` (not Domain, not Shared). So a command record is not a free-standing contract; it drags its request/response DTOs with it.
4. **Handlers are `internal sealed`.** They're discovered by assembly scanning *within* their module and never referenced by type from outside. Only the *request/response types* need to be visible to a caller — the handler itself stays internal regardless of option chosen.

**Consequence:** The hard part is **not dispatch** (the shared MediatR container already makes cross-module `ISender.Send(...)` work). The hard part is **type visibility/ownership** — to send `ConfirmOrderCommand` from a new Assistant *module*, that module must reference the *type*, and the type currently lives in `Orders.Application` (off-limits under the dependency rule), and it pulls `OrderDto`/`DraftOrderItemRequest` with it.

### Option A — New `FreshFlow.Assistant.*` module via shared contracts/facades

Two concrete sub-variants:

**A1 — Relocate the assistant-callable contracts to a shared assembly.** Move each command/query record **and its request/response DTOs** out of `{Module}.Application` into `FreshFlow.Contracts` (or a new `FreshFlow.Contracts.Application` "public API" assembly), leaving the `internal` handlers in place inside the module (the module's Application references Contracts, which it already does). What would have to move for the §1 tool surface:

| Type to relocate | Drags along (DTOs) |
|---|---|
| `CreateDraftOrderCommand` | `DraftOrderItemRequest`, `OrderDto`, `OrderItemDto` |
| `AddOrderItemCommand` / `UpdateOrderItemCommand` / `RemoveOrderItemCommand` | `OrderDto` (+ item DTO) |
| `ConfirmOrderCommand` | `OrderDto` |
| `ReorderFromHistoryCommand` | `OrderDto` |
| `GetOrderQuery` / `ListOrdersQuery` | `OrderDto`, paged-list DTO |
| `GetProductsQuery` / `GetProductByIdQuery` | `GetProductsResponse` + product DTOs (Catalog) |
| `GetMarketProductsQuery` | `MarketProductPageDto` (Pricing) |

- **Real refactor, not plumbing.** It touches three modules (Orders, Pricing, Catalog), moves ~8–10 request types plus a cluster of DTOs, and updates every existing controller + handler `using`. It also **erodes the encapsulation it's trying to protect**: today these DTOs are a module's *internal* application contract; promoting them to a shared assembly makes them a public contract every module can couple to, which is a bigger architectural commitment than the assistant alone justifies. Risk: medium-high, broad blast radius, churns code the Orders pass just stabilized.

**A2 — Publish a thin facade interface per module in a shared assembly.** Define `IOrdersFacade` / `ICatalogQueryFacade` / `IPricingQueryFacade` in a shared assembly (interface + its own neutral DTOs), implement each **inside the owning module's Infrastructure** by delegating to `ISender`. The Assistant module references only the **interfaces**.

- This **honors the dependency rule cleanly** (Assistant → shared interfaces only; each module owns its implementation) and is the *correct long-term* shape if the assistant grows into a real bounded context.
- But it means **hand-writing and maintaining a facade + a parallel DTO set per module** — duplicating the shape of commands/queries that already exist, plus mapping code. For ~10 operations that's a meaningful, ongoing surface to keep in sync. Risk: medium; the cost is *new boilerplate and a second DTO vocabulary*, not breakage.

**Either A-variant requires an explicit, up-front architecture decision and a non-trivial amount of work before a single assistant feature runs.**

### Option B — Host-layer orchestration in `FreshFlow.API`

Concretely: a new `FreshFlow.API/Assistant/` folder — `AssistantController` (the chat endpoint) + an `AssistantOrchestrationService` (intent → tool routing) that injects `ISender` and calls the **existing** commands/queries directly, exactly as the existing controllers do. Tool wrappers would mirror the controller bodies: resolve `UserId`/roles from the JWT, apply the same role-narrowing, `sender.Send(...)`, map the `Result`.

- **No new cross-module project reference is introduced.** The host already references all modules' Infrastructure (transitively their Application), and the shared MediatR container already resolves every handler. **This works today with zero relocation/refactor** — it's the only option where dispatch *and* type visibility are already satisfied.
- **The "logic creep" line, made concrete.** Acceptable in the host: parse intent, pick which existing tool/command to call, sequence a few calls, shape the conversational reply, enforce the human-confirm gate. **Over the line:** any *business decision* that should be a domain/application rule. Concrete example for this feature — **"product X is out of stock; suggest an alternative."** Deciding *which* alternative (same category? closest price? same market? substitution rules?) is **domain/catalog logic** and must **not** live in the host orchestrator; the host should call a (possibly new, thin) Catalog/Pricing query like "find substitutes for productId" and just relay the result. Rule of thumb: the host may decide *which tool to call and in what order*; it may **not** compute an answer the backend should own. Other creep smells to watch: recomputing totals/credit/eligibility in the orchestrator instead of reading them from the command results; embedding cutoff/delivery-window math in the host instead of reusing the scheduler.
- **Role-narrowing (§4): Option B does NOT make it worse — but it does NOT inherit it for free either.** Because the orchestrator bypasses the existing controllers, the per-endpoint narrowing those controllers do (e.g. `ProductsController` forcing `IncludeInactive=false` for non-privileged roles; `canReadAll` flags) is **not automatically applied** — the orchestrator's tool wrappers must replicate it. The mitigating factor: this is the *same* class of code the controllers already contain, written the *same* way (read JWT, narrow, send), so it's a **copy-the-pattern** task, not a new hard problem. It is strictly no worse than any option that bypasses controllers (A also bypasses them). The honest framing: **every option that isn't "go through the real controllers" must re-apply controller-layer narrowing**; B just makes that obligation local and obvious.

### Option C — Extend the `Hub` module

`Hub` is an **empty scaffold** (no `.cs` files). The name fits a "central interaction hub," but with no existing assistant-relevant code it's effectively **Option A wearing the Hub name** and inherits the exact same type-visibility tension and up-front cost. No new finding changes this — **C = A.** Not recommended as distinct.

### Recommendation (a clear pick, not a survey)

**Build Option B first (host-layer orchestration in `FreshFlow.API/Assistant/`).** Treat A2 (per-module facades) as the *planned* refactor target *if and when* the assistant graduates from a feature into its own bounded context. Explicitly **defer/accept A1 (relocating contracts) as something we likely never do** — it trades the assistant's convenience for a permanent loss of module encapsulation, which is the wrong trade.

**Why B first:**
- It's the **only option whose dispatch and type-visibility already work today** — the shared MediatR container + host's existing references mean zero relocation, zero new shared-contract assembly, zero churn in the just-stabilized Orders/Pricing/Catalog code. Fastest path to a working prototype that exercises the *real* commands with *real* business rules.
- The **safety-critical properties don't depend on the module boundary at all.** The human-confirm gate, prompt-injection containment, grounding, and (most importantly) the server-side guards (credit, empty-order, cutoff, **handler-level ownership checks**) all fire regardless of where the orchestrator lives. So choosing B costs us nothing on correctness or security — those live in the domain/handlers, which every option reuses identically.
- The **role-narrowing obligation is identical** under A or B (both bypass controllers), so B is not at a disadvantage there; it just makes the obligation explicit and local.
- B keeps the **blast radius to new files in the host** — it adds no coupling to existing modules, so backing out or later migrating to A2 is low-cost (the orchestration logic and tool wrappers move largely intact behind facade interfaces).

**What I'd explicitly defer / accept as future cleanup under Option B:**
1. **Migration to A2 facades** when/if the assistant grows (more modules, more state, a team owning it). Until then, host orchestration is proportionate.
2. **A discipline guardrail to contain creep:** the orchestrator's tool wrappers must be thin (resolve identity → narrow by role → `sender.Send` → map result) with **no business computation**; any "smart" decision (e.g. out-of-stock substitution) must be pushed down as a new thin Catalog/Pricing query, not coded in the host. This should be a written rule in the eventual design + enforced in review.
3. **The thin read-side gaps from §1** (combined product+price+stock search, market-context resolution, confirm-preview) are **better added as proper queries in the owning modules** (Catalog/Pricing/Orders), *not* synthesized in the host — building them module-side now also keeps Option-B orchestration honest and makes a later A2 migration cleaner.
4. **Conversation-state + audit-trail homes** (see §6) remain open regardless of A/B and should be settled in the design phase; they're orthogonal to the module-boundary pick.

---

## 4. Authorization / security model (how identity flows today — and whether it's reusable)

**Critical finding for the design: identity is passed explicitly, not ambient.**

- There is **no** `ICurrentUser` / `IHttpContextAccessor` / ambient user-context service in the codebase (grep found none). Authorization is **two-layered and explicit**:
  1. **Controller layer:** `[Authorize(Roles = "...")]` gates the endpoint, and the controller extracts identity from JWT claims (`User.IsInRole(...)`, `ResolveUserId()` → `NameIdentifier`/`sub` claim) and **passes it into the command/query as an explicit parameter** (e.g. `ConfirmOrderCommand(ResolveUserId(), orderId)`, `ListOrdersQuery(userId, canReadAll, ...)`). The controller also **narrows queries by role** before sending (e.g. `ProductsController` forces `IncludeInactive = false` for non-privileged roles).
  2. **Handler layer:** ownership/business checks (e.g. `ConfirmOrderCommandHandler` verifies the resolved restaurant owns the order → `FORBIDDEN`).

- **Reusability for AI-initiated calls:** This model is **reusable as-is and is actually a strength** for the assistant. Because every command already takes the user identity as a parameter and re-checks ownership in the handler, an assistant that runs **under the authenticated user's own JWT** can call the exact same commands with the user's real `UserId`/roles — there is **no elevated "assistant identity"** needed or wanted. The assistant must extract the same `UserId`/roles from the *user's* JWT (whatever transport the chat uses) and pass them into the commands, identical to how controllers do it. The handler-level ownership checks then protect against the assistant being tricked into acting on someone else's order.
- **What must be preserved:** The **controller-layer role narrowing** (e.g. forcing `IncludeInactive = false`, `canReadAll` flags, role-gated endpoints). If the assistant bypasses controllers and calls `ISender` directly, **it must replicate that narrowing**, or those guards are lost. This is the most important security carry-over: the role gates and query-narrowing currently live in controllers, not in the handlers, for several endpoints. (See §3 recommendation — this obligation is identical under Option A or B, since both bypass the real controllers.)

---

## 5. Relevant docs & product-owner decisions that constrain the assistant

- **No existing assistant/AI/chat docs.** Grep of `docs/` found nothing on assistant, chatbot, NLP, or tool-calling. This is greenfield from a documentation standpoint.
- **B2B credit model (binding constraint).** Per `docs/AUDIT-2026-06-17-orders-module-research.md` §2 Q1 and memory `project_payment_model`: orders are **credit/công nợ, not per-order payment gateway**. Confirm goes `Draft → Confirmed` directly if within credit limit, else `422 CREDIT_LIMIT_EXCEEDED`. **The assistant must not imply a payment step** ("pay now", VNPay/MoMo) — there is none. A good assistant UX would surface remaining credit / `CREDIT_LIMIT_EXCEEDED` *before* the user confirms (ties to the "confirm preview" gap in §1).
- **Cutoff / delivery window (UC-ORD-07).** Audit §7: delivery date must be within D..D+7; confirm before 22:00 Asia/Ho_Chi_Minh → earliest D+1, after → D+2; out of window → `422 DELIVERY_DATE_OUT_OF_WINDOW`. The assistant should understand "giao ngày mai" can be rejected/reshaped by the cutoff scheduler.
- **DB-only / over-allocation trade-off (audit §2).** Soft-reservation via Redis is **deferred** (Task #10). Stock availability the assistant reports (`available = current − reserved`) can be over-allocated; two carts can claim the same stock. This is a known accepted trade-off, not a bug — but it means the assistant's "in stock" answer is best-effort, not a hold.
- **Access matrix (`docs/04-api-design.md`).** Roles are real and enforced (e.g. only `restaurant` creates/confirms orders; `admin,operations_manager` can read all). The assistant inherits these — a `restaurant` user's assistant cannot do admin things.

---

## 6. Open questions & risks (to worry about — no solutions proposed here)

1. **Module-boundary decision (highest priority).** How does the Assistant synchronously invoke Orders/Pricing/Catalog commands without violating the "no cross-module Application reference" rule? **Resolved in §3: build Option B (host-layer orchestration) first; treat A2 facades as the deferred migration target; avoid A1 contract relocation.** Remaining sub-decisions for the design phase: the anti-creep discipline rule and whether the thin §1 read gaps are added module-side (recommended) or host-side.
2. **Prompt injection → out-of-scope tool calls.** A user (or injected content in product names/notes) could try to make the AI call tools it shouldn't (e.g. confirm without consent, act on another order). Mitigation surface already exists server-side (handler ownership checks, role gates) — but the assistant must not be the *only* line of defense, and the "never auto-confirm" rule needs a hard, non-LLM gate (an explicit user-confirmation step the AI cannot satisfy on its own).
3. **Loss of controller-layer guards.** If the assistant calls `ISender` directly, the role-narrowing currently done in controllers (e.g. `IncludeInactive=false`, `canReadAll`) must be re-applied, or it's a privilege/visibility leak (§4). Applies identically to Options A and B.
4. **Hallucinated product data.** The AI might invent products/prices. Every product reference the assistant surfaces must be grounded in a real query result (Catalog/Pricing), and every order item must go through `IMarketProductReader`-validated commands (which already reject unknown/deleted products) — the AI should never fabricate a `marketProductId`.
5. **Conversation & draft-cart state across turns.** A draft order is already persisted (it's a real `Order` row in Draft status), so the cart survives across turns by `orderId`. But *conversational* state (what the user is mid-flow on, pending confirmations) is new and has no home today — needs a decision (stateless re-derivation from the draft vs. a new conversation store). Note: abandoned drafts accumulate as Draft orders; cleanup policy is an open question. Orthogonal to the §3 module-boundary pick.
6. **Rate limiting on the chat endpoint.** A per-restaurant `orders` rate-limit policy already exists (`Program.cs`, keyed on the JWT `NameIdentifier`, default 30/min). A chat endpoint that fans out to many tool calls per message needs its own policy — one chat message could otherwise trigger many order mutations and blow the existing budget, or conversely need a separate, more generous read budget.
7. **Confirm-preview gap (UX + safety).** Without a dry-run, the user only learns about `CREDIT_LIMIT_EXCEEDED` / `DELIVERY_DATE_OUT_OF_WINDOW` *at* confirm. For a "confirm?" UX the assistant should preview these — needs a thin read-only preview path (§1; best built module-side per §3).
8. **Auditability.** AI-initiated state changes should be attributable (who/what confirmed an order — user via assistant vs. user direct). No assistant-action audit trail exists today. Orthogonal to the §3 module-boundary pick.

---

## 7. Summary verdict

**Feasible and low-risk on the backend side** *as a new client of existing functionality* — the order/cart domain, validation, credit, cutoff, and role/ownership checks are all already implemented and are invoked via explicit-identity commands that an assistant running under the user's own JWT can reuse directly. ~90% of the tool surface exists.

**The real work is not in Orders/Pricing** (they're ready); it's in: (1) the **module-boundary decision** — now resolved in §3: **build host-layer orchestration (Option B) first**, defer per-module facades (A2) as the migration target, avoid contract relocation (A1); (2) a few **thin read-side additions** (combined product+price+stock search, market-context resolution, confirm-preview), best built **module-side**; (3) **preserving controller-layer role narrowing** in the orchestrator (identical obligation under any option that bypasses controllers); and (4) the **AI-specific safety layer** (hard non-LLM confirmation gate, prompt-injection containment, grounding, conversation-state home, chat rate limiting).

No LLM provider is recommended here and no code was written — this is a landscape + design-analysis survey to inform the design discussion.
