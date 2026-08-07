# PLAN — Market Product Tags (replace `is_featured`)

**Date:** 2026-08-07 · **Module:** Pricing · **Status:** planned

## Goal
Replace the single `bool IsFeatured` on `market_products` with a **set of free-form
tags** (`text[]`). A product can carry many tags. One designated tag (`"nổi bật"`)
keeps the existing "pin to top of the board" behavior. The board can be filtered by tag.

## Decisions (confirmed with user)
1. **Free-form tags** — arbitrary strings, no tag catalog table. Stored as `text[]` on `market_products`.
2. **Keep pinning** — a special tag value (`FeaturedTag = "nổi bật"`) still pins to top of page 1.
   The bool column and its command go away; pinning is now driven by "tags contains the featured tag".
3. **Filter by tag** — `GET /markets/{id}/products?tag=...` filters the board. GIN index for it.

## Normalization decision (my call — override if you disagree)
On write, each tag is **trimmed + lowercased**, empties dropped, deduped. Rationale: makes
the pin-tag match and `?tag=` filter deterministic in Postgres (array containment `@>` is
case-sensitive). FE title-cases for display if it wants. Caps: **≤ 8 tags/product, ≤ 30 chars each.**

## Scope — what changes (all in Pricing + 1 controller + 1 migration)

### Domain — `MarketProduct.cs`
- Remove `bool IsFeatured` + `SetFeatured(...)`.
- Add `private readonly List<string> _tags = []` backing `IReadOnlyList<string> Tags => _tags`.
- Add `void SetTags(IEnumerable<string> tags, Guid? actor)`: normalize (trim/lowercase/dedupe),
  no-op + no concurrency bump if unchanged (mirror the old `SetFeatured` guard).
- Add `public const string FeaturedTag = "nổi bật";` + `public bool IsFeatured => _tags.Contains(FeaturedTag);`
  (kept as a convenience; pinning in the reader queries the array directly, see below).

### Persistence — `MarketProductConfiguration.cs`
- Drop the `is_featured` property mapping.
- `builder.Property(mp => mp.Tags).HasColumnName("tags").HasDefaultValueSql("'{}'");`
  (Npgsql maps `List<string>` → `text[]` natively; may need `.Metadata.SetValueComparer` /
  a field-access backing config since `_tags` is a private list — verify at build.)
- Add GIN index on `tags` for the `@>` filter.

### Migration (new)
`AddMarketProductTags`:
1. `ADD COLUMN tags text[] NOT NULL DEFAULT '{}'`.
2. Data migrate: `UPDATE market_products SET tags = ARRAY['nổi bật'] WHERE is_featured = true;`
3. `DROP COLUMN is_featured`.
4. `CREATE INDEX idx_market_products_tags ON market_products USING GIN (tags);`
Regenerate `AppDbContextModelSnapshot`. (Pricing already in `ForceLoadModuleAssemblies` — no new keyless row, nothing to add there.)

### Command / endpoint
- Delete `SetMarketProductFeatured` (command/handler/validator) → add `SetMarketProductTags`
  (command + handler + validator enforcing the caps above).
- Controller: replace `PATCH .../products/{productId}/featured` with
  `PUT .../products/{productId}/tags` body `{ "tags": ["nổi bật","khuyến mãi"] }`.
  Same RBAC: `admin,market_agent`. Route through `ISender` (keeps ValidationBehavior).

### Read side — `MarketProductReader.GetPageAsync`
- Pin block: `.Where(x => x.mp.Tags.Contains(MarketProduct.FeaturedTag))` (translates to `@>`),
  keyset stream excludes them — same structure as today, just swap the predicate.
- New optional `string? tag` param → `.Where(x => x.mp.Tags.Contains(tag))` before paging.
- Thread `tag` through `GetMarketProductsQuery` + handler + controller action.

### DTO — `MarketProductItemDto`
- Replace `bool IsFeatured` with `IReadOnlyList<string> Tags`.
  (FE derives "featured" from `Tags.contains("nổi bật")`; drop the standalone bool.)

## Out of scope (YAGNI — say if you want them)
- Tag filtering on the **search** endpoint (only the board gets `?tag=`).
- A tag-management/catalog table, tag colors, per-market tag suggestions.
- Multi-tag AND/OR filtering — single `?tag=` only.

## Tests to update
- `MarketProductTests` (unit): drop `SetFeatured` cases → add `SetTags` normalization/dedupe/cap/no-op.
- `MarketProductsEndpointTests` (integration): featured→tags endpoint; pin-by-tag + `?tag=` filter.
  **Must be integration (real Postgres)** — `text[]` `.Contains` does not translate on EF InMemory.
- `GetMarketProductsQueryHandlerTests`: adjust for `tag` param + DTO shape.

## Risk / notes
- Private-list `text[]` mapping is the one fiddly EF bit — confirm the value comparer so change
  tracking + concurrency work; copy an Npgsql `List<string>` mapping example if the default misbehaves.
- No cross-module reader selects `is_featured` (verified) — dropping the column is safe.
