# PLAN — Market Product Tags as a managed table (supersedes `text[]`)

**Date:** 2026-08-08 · **Module:** Pricing · **Status:** planned
**Supersedes:** `PLAN-2026-08-07-market-product-tags.md` (the `text[]` design, committed `9eec963`).

## Goal
Move product tags out of the `market_products.tags text[]` column into a **managed
catalog table** + a many-to-many join, so tags can be administered (listed, renamed,
soft-deleted) instead of being free-form strings scattered per row.

## Decisions (confirmed with user)
1. **Global catalog** — one shared `tags` table across all markets. No `market_id` on a tag.
2. **Pin via flag** — `tags.pins_to_top boolean`. A product is "featured/pinned" iff it carries
   any tag with `pins_to_top = true`. Replaces the magic string `"nổi bật"`. Seed one such tag.
3. **Catalog-only assignment** — a product is tagged by **`TagId`**; the id must exist in the
   catalog (and not be soft-deleted), else `400`. No free-typed auto-create.

## Schema — 2 new tables (both owned by Pricing)

### `tags` (catalog)
| column | type | notes |
|---|---|---|
| `id` | uuid PK | `gen_random_uuid()` |
| `name` | text NOT NULL | normalized (trim + lower-invariant) |
| `pins_to_top` | boolean NOT NULL DEFAULT false | pin mechanism |
| `created_at` / `updated_at` | timestamptz | |
| `updated_by` | uuid NULL | actor |
| `deleted_at` | timestamptz NULL | soft delete (repo convention) |

- Unique index on `name` **WHERE `deleted_at IS NULL`** (partial) — no dup live names.

### `market_product_tags` (join)
| column | type | notes |
|---|---|---|
| `market_product_id` | uuid NOT NULL | FK → `market_products(Id)`, ON DELETE CASCADE |
| `tag_id` | uuid NOT NULL | FK → `tags(id)`, ON DELETE RESTRICT |
| composite PK | (`market_product_id`, `tag_id`) | |

- Index on `tag_id` (reverse lookup for `?tag=` filter and "which products use this tag").
- **No soft-delete on the join** — untagging hard-deletes the row (mirrors `restaurant_favorites`).
  `RESTRICT` on `tag_id` means a catalog tag in use can't be hard-deleted; deleting a tag =
  soft-delete the `tags` row **and** clear its join rows (handler does both).

## Domain — Pricing.Domain

### New aggregate `Tag`
- `Id, Name, PinsToTop, UpdatedBy` + `SoftDelete()` / `deleted_at`.
- Factory `Create(name, pinsToTop, actor)` — normalizes name (reuse the same trim+lower rule).
- `Rename(name, actor)`, `SetPinsToTop(bool, actor)`.
- Same caps as today: name ≤ 30 chars.

### `MarketProduct`
- Replace `List<string> Tags` with a **skip-navigation** many-to-many: `List<Tag> Tags` mapped
  through `market_product_tags` (`.HasMany(mp => mp.Tags).WithMany().UsingEntity("market_product_tags")`).
- `IsFeatured => Tags.Any(t => t.PinsToTop)` (was `Tags.Contains("nổi bật")`).
- `SetTags(IReadOnlyCollection<Tag> tags, Guid? actor)` — replace the association set; keep the
  no-op/no-concurrency-bump guard when unchanged (compare by tag-id set). Cap ≤ 8 tags.
- Drop `FeaturedTag` const, `NormalizeTags` (moves to `Tag`), the string-array overload.

## Application — Pricing.Application

### Tag catalog CRUD (new, admin-managed)
- `GetTagsQuery` → list all live tags (`{id, name, pinsToTop}`).
- `CreateTagCommand(name, pinsToTop)` → 409/validation on dup live name.
- `UpdateTagCommand(id, name, pinsToTop)` → rename / toggle pin.
- `DeleteTagCommand(id)` → soft-delete tag + clear its `market_product_tags` rows.
- Repository `ITagRepository` (find, findByName, list, track, save). New reader as needed.

### Change `SetMarketProductTags`
- Command carries `IReadOnlyList<Guid> TagIds` (was `IReadOnlyList<string> Tags`).
- Handler: load the `MarketProduct`, **load the requested `Tag`s from catalog**, if any id
  missing/soft-deleted → `Result.Failure(Error.Validation(...))` (→ 400). Then `SetTags(tags, actor)`.
- Validator: `TagIds` not null, ≤ 8, each `NotEmpty`. (Existence check is in the handler — it's a
  DB lookup, not a structural rule.)

### DTO
- `MarketProductItemDto.Tags` : `IReadOnlyList<string>` → `IReadOnlyList<MarketProductTagDto>`
  where `MarketProductTagDto(Guid Id, string Name, bool PinsToTop)`.
  (FE needs ids to pre-select in the edit UI; `pinsToTop` lets it badge "nổi bật".)

## Read side — `MarketProductReader.GetPageAsync`
Tags are now same-module navigations (no keyless seam needed):
- **Pin block:** `.Where(x => x.mp.Tags.Any(t => t.PinsToTop))` (EXISTS subquery).
- **Filter `?tag=`:** keep the param as a **name** (human-readable URL). Normalize input, then
  `.Where(x => x.mp.Tags.Any(t => t.Name == normalizedTag && t.DeletedAt == null))`.
- **Projection:** `x.mp.Tags.Where(t => t.DeletedAt == null).Select(t => new MarketProductTagDto(t.Id, t.Name, t.PinsToTop))`.
- Keyset stream unchanged (still excludes pinned rows: `!x.mp.Tags.Any(t => t.PinsToTop)`).

> ⚠️ Navigation `.Any()` / collection projection does **not** run on EF InMemory — pin/filter/
> projection are only provable via **integration tests on real Postgres** (unchanged constraint).

## Controller — `MarketsController`
- Board `GET .../products?tag=` — unchanged signature (still a name string).
- `PUT .../products/{productId}/tags` — request body `{ "tagIds": ["<guid>", ...] }`
  (`SetMarketProductTagsRequest(IReadOnlyList<Guid> TagIds)`). Same RBAC `admin,market_agent`.
- **New `TagsController`** (`/api/v1/tags`): `GET` (any authed), `POST`/`PUT`/`DELETE`
  `admin` only. Route through `ISender`.

## Migration (new) — `ReplaceMarketProductTagsWithTable`
1. `CREATE TABLE tags (...)` + partial unique index on `name`.
2. `CREATE TABLE market_product_tags (...)` + FKs + `tag_id` index.
3. **Data migrate** from the existing `text[]`:
   - `INSERT INTO tags(id, name, pins_to_top, created_at) SELECT gen_random_uuid(), t, (t = 'nổi bật'), now() FROM (SELECT DISTINCT unnest(tags) t FROM market_products) s WHERE t <> '';`
   - `INSERT INTO market_product_tags SELECT mp."Id", tg.id FROM market_products mp CROSS JOIN LATERAL unnest(mp.tags) AS v(name) JOIN tags tg ON tg.name = v.name;`
   - Ensure a seeded `'nổi bật'` (`pins_to_top=true`) exists even if no product currently has it
     (prod today has **0** tag rows — insert it explicitly if absent).
4. `DROP INDEX idx_market_products_tags;` then `ALTER TABLE market_products DROP COLUMN tags;`
5. Regenerate `AppDbContextModelSnapshot`. Both new configs live in Pricing.Infrastructure
   (already in `ForceLoadModuleAssemblies`) — no new keyless Row, nothing to add to
   `DesignTimeDbContextFactory`.
- `Down()` reverses: re-add `tags text[]`, backfill from join, drop the two tables.

## Out of scope (YAGNI — say if you want)
- Tag colors / display-name separate from `name` / per-market tag suggestions.
- Multi-tag AND/OR filtering (still single `?tag=`).
- Tag usage counts, merge-tags, bulk retag tooling.

## Tests
- **Unit** `Tag` (create/normalize/rename/pin/soft-delete), `MarketProduct.SetTags` (id-set diff,
  no-op guard, ≤8 cap), `SetMarketProductTags` handler (unknown TagId → 400).
- **Integration (Postgres)** — pin-by-flag ordering, `?tag=` filter by name, tag CRUD, cascade on
  product delete, RESTRICT on tag delete-while-in-use, DTO shape.
- Update existing `MarketProductReaderPageSizeTests`, `GetMarketProductsQueryHandlerTests`,
  `MarketProductsEndpointTests` for the new DTO/param.

## Risk / notes
- Skip-nav many-to-many + soft-delete filter on the *navigated* entity is the fiddly EF bit —
  add a global query filter on `Tag` (`deleted_at IS NULL`) so projection & filter exclude
  soft-deleted tags automatically.
- `?tag=` stays a **name** for URL friendliness; names are normalized so lookups are deterministic.
- Confirm nothing outside Pricing reads `market_products.tags` — verified: only Pricing's own
  reader/config touch it (`grep` 2026-08-08).
- This is a **breaking API change** for the tags endpoint (`tags: string[]` → `tagIds: guid[]`,
  and DTO `Tags` shape). Coordinate with FE.
