# Dev Plan: Settlement, Credit Statement PDF, and Invoice Stub Hardening

**Date:** 2026-08-10
**Status:** Implemented
**Scope:** Development environment only

## 1. Objective

Make the existing external-payment recording flow safe enough for development and make credit statements useful for reconciliation, while keeping e-invoicing explicitly sandbox-only.

This plan does not add a payment gateway or a production e-invoice provider.

## 2. Current baseline

- Admin can record an external payment through `POST /api/v1/admin/restaurants/{restaurantId}/credit/settle`.
- A successful settlement immediately reduces `restaurant_credit.outstanding_balance` and adds a `credit_transactions` ledger row.
- Settlement reference is optional and has no uniqueness constraint, so the same payment can be recorded more than once.
- The ledger does not identify the admin who recorded the settlement.
- Monthly credit statements and downloadable QuestPDF files already exist.
- Statement PDF lines do not preserve/display enough order and payment information for reliable reconciliation.
- Invoicing always uses `StubEInvoiceProvider`; its tax-authority code, lookup URL, XML reference, and PDF reference are fake.
- The invoice export endpoint returns locally constructed XML and still contains pending seller placeholders.

## 3. Fixed decisions

1. `reference` is required for every new settlement, including `manual` settlements.
2. References are trimmed and normalized to uppercase before persistence.
3. A reference is unique per restaurant for settlement transactions.
4. A duplicate reference returns HTTP `409` with `CREDIT_SETTLEMENT_DUPLICATE_REFERENCE` and never moves the balance twice.
5. `recordedByUserId` comes from the authenticated admin token, never from the request body.
6. Cross-module FK from Orders to Auth is not added; the actor is stored as a raw `Guid`.
7. Statement lines snapshot `OrderId` and `PaymentMethod` at generation time.
8. Existing QuestPDF is reused; no PDF dependency is added.
9. Stub invoices remain `Issued` in the existing development lifecycle, but API/XML responses explicitly expose that they are sandbox artifacts.
10. No legal invoice PDF endpoint is implemented until a real e-invoice provider exists.

## 4. Phase 1 — Harden settlement recording

### 4.1 API contract

Keep the existing body shape:

```json
{
  "amount": 1000000,
  "paymentMethod": "bank_transfer",
  "reference": "VCB-20260810-001",
  "note": "Đã đối soát"
}
```

Changes:

- Require `reference`, maximum 200 characters.
- Continue accepting only `bank_transfer` and `manual`.
- Resolve the current admin ID from `ClaimTypes.NameIdentifier` or `sub`.
- Add `RecordedByUserId` to `SettleRestaurantCreditCommand` and pass it through the handler/service.
- Return the existing `RestaurantCreditDto` on success.

Expected errors:

| Case | HTTP | Code |
|---|---:|---|
| Missing/blank reference | 400 | `VALIDATION_ERROR` |
| Amount is non-positive | 422 | `INVALID_AMOUNT` |
| Amount exceeds outstanding balance | 422 | `CREDIT_SETTLEMENT_EXCEEDS_BALANCE` |
| Duplicate settlement reference | 409 | `CREDIT_SETTLEMENT_DUPLICATE_REFERENCE` |
| Concurrent account update | 409 | `OPTIMISTIC_CONCURRENCY_CONFLICT` |

### 4.2 Domain and persistence

Add to `CreditTransaction`:

```text
RecordedByUserId: Guid?
```

Rules for newly-created rows:

- Settlement: `PaymentMethod`, normalized `Reference`, and `RecordedByUserId` are required.
- Charge/refund: these settlement-only fields remain null.
- Settlement mutation and transaction insertion stay in one `SaveChangesAsync` call.

Database changes:

- Add nullable `recorded_by_user_id uuid` for legacy compatibility.
- Backfill settlement rows with no reference to `LEGACY-{transaction_id}`.
- Normalize existing non-null settlement references with trim/uppercase before creating the index.
- Add a CHECK constraint requiring a non-blank reference when `type = 'settlement'`.
- Add a partial unique index:

```sql
UNIQUE (restaurant_id, reference)
WHERE type = 'settlement' AND reference IS NOT NULL
```

Do not silently rewrite duplicate non-null references. Run a duplicate preflight query before applying the migration to a shared database; reset a disposable dev database or resolve those rows explicitly.

### 4.3 Duplicate error handling

- Detect the unique violation by its constraint name in `CreditRepository`.
- Throw a settlement-specific persistence exception before the generic concurrency mapping.
- Convert it to `Error.Conflict("CREDIT_SETTLEMENT_DUPLICATE_REFERENCE", ...)`.
- Add the new error code to the conflict branch in `ErrorExtensions`.

### 4.4 Ledger response

Add `RecordedByUserId` to `CreditTransactionDto` and `CreditDtoMapper` so transaction history shows who recorded the payment.

No separate audit endpoint or audit table is added in this scope.

### 4.5 Primary files

- `src/FreshFlow.API/Controllers/AdminController.cs`
- `src/FreshFlow.API/Extensions/ErrorExtensions.cs`
- `src/Modules/Orders/FreshFlow.Orders.Application/Commands/SettleRestaurantCredit/*`
- `src/Modules/Orders/FreshFlow.Orders.Application/Abstractions/ICreditService.cs`
- `src/Modules/Orders/FreshFlow.Orders.Application/Services/CreditService.cs`
- `src/Modules/Orders/FreshFlow.Orders.Application/Dtos/CreditTransactionDto.cs`
- `src/Modules/Orders/FreshFlow.Orders.Application/Dtos/CreditDtoMapper.cs`
- `src/Modules/Orders/FreshFlow.Orders.Domain/Entities/CreditTransaction.cs`
- `src/Modules/Orders/FreshFlow.Orders.Infrastructure/Persistence/Configurations/CreditTransactionConfiguration.cs`
- `src/Modules/Orders/FreshFlow.Orders.Infrastructure/Repositories/CreditRepository.cs`

## 5. Phase 2 — Snapshot reconciliation fields

Add nullable fields to `CreditStatementLine`:

```text
OrderId: Guid?
PaymentMethod: PaymentMethod?
```

Generation behavior:

- Charge/refund copies `CreditTransaction.OrderId`.
- Settlement copies `CreditTransaction.PaymentMethod` and `Reference`.
- Note and reference remain separate snapshot fields.
- Existing statements remain readable with null values.
- Generated statements remain immutable.

Update:

- `CreditStatementLine` entity and EF configuration.
- `CreditStatementLineDto` and mapper.
- `CreditStatementGenerationService` line construction.
- Statement generation tests to verify the new snapshot fields.

## 6. Phase 3 — Improve credit statement PDF

### 6.1 Content

- Display an inclusive local date range. Example: `01/07/2026 - 31/07/2026`, not the exclusive `01/08/2026` boundary.
- Display statement ID and restaurant ID.
- Translate transaction types:

| Stored type | PDF label |
|---|---|
| `charge` | `Phát sinh nợ` |
| `settlement` | `Thanh toán` |
| `refund` | `Hoàn tiền` |
| fallback | Original value |

- Translate payment methods:

| Stored method | PDF label |
|---|---|
| `bank_transfer` | `Chuyển khoản` |
| `manual` | `Thủ công` |

- Charge/refund detail includes `OrderId` when present.
- Settlement detail includes payment method and reconciliation reference.
- Note is shown independently so it cannot hide the reference.
- Amount and balance columns are right-aligned.
- Standardize the download filename as `statement-{yyyy-MM}.pdf`.

### 6.2 Layout

Use the existing QuestPDF renderer only:

- Light header background and clear title hierarchy.
- Compact two-column balance summary.
- Padded table cells and row separators.
- Repeated table header on additional pages.
- Existing page-number footer retained.
- Long notes wrap without overlapping adjacent cells.

Deferred from the PDF:

- Logo and full seller/restaurant legal profile, because the current statement DTO has no stable source for them.
- PDF/A, digital signatures, accessibility tagging, and legal-invoice styling.

### 6.3 Primary files

- `src/Modules/Orders/FreshFlow.Orders.Domain/Entities/CreditStatementLine.cs`
- `src/Modules/Orders/FreshFlow.Orders.Application/Dtos/CreditStatementLineDto.cs`
- `src/Modules/Orders/FreshFlow.Orders.Application/Dtos/CreditStatementDtoMapper.cs`
- `src/Modules/Orders/FreshFlow.Orders.Application/Services/CreditStatementGenerationService.cs`
- `src/Modules/Orders/FreshFlow.Orders.Infrastructure/Documents/StatementPdfRenderer.cs`
- `src/Modules/Orders/FreshFlow.Orders.Infrastructure/Persistence/Configurations/CreditStatementLineConfiguration.cs`
- `src/FreshFlow.API/Controllers/RestaurantCreditController.cs`

## 7. Phase 4 — Make invoice stub unmistakable

### 7.1 Invoice API DTOs

Expose:

```text
ProviderName
IsSandbox
```

`IsSandbox` is derived as `ProviderName == "stub"`. Include it in detail and summary DTOs so clients can show a warning without guessing from fake tax codes.

### 7.2 Stub values

- Change the fake tax-authority code prefix to `DEV-MCQT-`.
- Keep stub URI schemes and the sandbox lookup URL.
- Do not add a network call or provider configuration.

### 7.3 XML export

When `ProviderName == "stub"`:

- Add `environment="development"` to the root.
- Add `legalValue="false"` to the root.
- Add notice `BẢN NHÁP - KHÔNG CÓ GIÁ TRỊ THUẾ`.
- Use filename `invoice-dev-draft-{serial}-{number}.xml`.
- Keep pending seller values only as explicitly marked development placeholders.

For a future non-stub provider, do not add the development attributes or warning.

### 7.4 Primary files

- `src/Modules/Invoicing/FreshFlow.Invoicing.Application/Dtos/InvoiceDtos.cs`
- `src/Modules/Invoicing/FreshFlow.Invoicing.Application/Queries/ExportInvoice/ExportInvoiceQuery.cs`
- `src/Modules/Invoicing/FreshFlow.Invoicing.Infrastructure/Provider/StubEInvoiceProvider.cs`

## 8. Migration strategy

Create one Orders migration after all entity/configuration changes:

```text
HardenCreditSettlementAndStatementDetails
```

It contains:

- `credit_transactions.recorded_by_user_id`.
- Settlement reference backfill/normalization.
- Settlement reference CHECK constraint.
- Partial unique settlement-reference index.
- `credit_statement_lines.order_id`.
- `credit_statement_lines.payment_method`.

After generation:

1. Review `Up`, `Down`, and model snapshot manually.
2. Run `dotnet ef migrations has-pending-model-changes`.
3. Do not apply the migration to a shared/real database unless explicitly requested.

## 9. Test plan

### 9.1 Orders unit tests

- Missing/blank settlement reference fails validation.
- Reference is trimmed and normalized.
- Handler passes `RecordedByUserId` to the service.
- Settlement writes actor, method, and normalized reference.
- Settlement exceeding balance does not save.
- Duplicate persistence exception maps to `CREDIT_SETTLEMENT_DUPLICATE_REFERENCE`.
- Charge/refund behavior is unchanged.
- Statement generation snapshots `OrderId`, `PaymentMethod`, note, and reference.
- PDF renders an empty statement.
- PDF renders long details and approximately 100 rows without throwing.

### 9.2 PostgreSQL integration test

Add `CreditSettlementEndpointTests`:

1. Seed a restaurant credit account with an outstanding balance.
2. Log in as the seeded admin.
3. POST a settlement with a unique reference and assert `200`.
4. POST the same reference again and assert `409` plus `CREDIT_SETTLEMENT_DUPLICATE_REFERENCE`.
5. Query PostgreSQL and assert exactly one settlement row.
6. Assert the balance decreased exactly once.
7. Assert `recorded_by_user_id` equals the authenticated admin ID.

The database-level duplicate test must use PostgreSQL because EF InMemory does not enforce partial unique indexes.

### 9.3 Statement endpoint/integration tests

- Owner and admin continue receiving `application/pdf`.
- Cross-restaurant access remains `404`.
- Filename uses the statement period.
- PDF starts with `%PDF`.

### 9.4 Invoicing tests

- Stub detail/summary DTO returns `ProviderName = "stub"` and `IsSandbox = true`.
- Stub tax code uses `DEV-MCQT-`.
- Stub XML contains development attributes and warning.
- Stub export filename contains `dev-draft`.
- A non-stub issued invoice is not marked sandbox and receives no development warning.

## 10. Verification commands

Run the smallest relevant checks first, then the integration slice:

```bash
dotnet test tests/Unit/FreshFlow.Orders.UnitTests/FreshFlow.Orders.UnitTests.csproj --no-restore
dotnet test tests/Unit/FreshFlow.Invoicing.UnitTests/FreshFlow.Invoicing.UnitTests.csproj --no-restore
dotnet test tests/Integration/FreshFlow.IntegrationTests/FreshFlow.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~CreditSettlementEndpointTests|FullyQualifiedName~CreditStatementPdfEndpointTests|FullyQualifiedName~InvoicingPostgresTests"
dotnet build --no-restore
dotnet ef migrations has-pending-model-changes
git diff --check
```

Also generate one PDF sample with long notes and many rows, render it to an image, and inspect spacing, wrapping, Vietnamese text, date range, transaction labels, reference, and page headers.

Existing EF Core `10.0.4`/`10.0.9` version-conflict warnings are not part of this work unless they become build/test failures.

## 11. Acceptance criteria

- The same settlement reference cannot reduce a restaurant balance twice.
- Every new settlement records the authenticated admin ID.
- Settlement account update and ledger insertion remain atomic.
- Credit transaction history exposes the recording admin.
- New statements preserve order and payment reconciliation data.
- Statement PDF uses Vietnamese labels, inclusive period dates, and displays order/payment/reference/note without hiding data.
- Long statements render without overlap or exceptions.
- Stub invoice APIs/XML clearly state they are sandbox artifacts with no tax value.
- No payment gateway, production provider, or new PDF library is introduced.
- All targeted unit/integration tests pass and EF reports no pending model changes.

## 12. Explicitly out of scope

- Payment gateway or webhook/callback processing.
- Real MISA/VNPT/Viettel integration.
- Real digital signature or tax-authority submission.
- Legal invoice PDF/XML download from a provider.
- Settlement proof upload.
- Settlement reversal/void workflow.
- Payment verification states such as `pending`, `verified`, or `rejected`.
- Statement `paid`/`overdue` lifecycle.
- Guaranteed notification delivery/outbox.
- PDF/A and accessibility tagging.

Add these only when staging/production requirements make them necessary.

## 13. Suggested implementation order

1. Settlement API/domain changes and duplicate error mapping.
2. Statement snapshot fields.
3. Generate and review the single migration.
4. PDF renderer changes.
5. Invoice stub labeling.
6. Unit tests, PostgreSQL integration tests, migration drift check, and visual PDF review.

Suggested commit boundaries:

1. `fix(orders): prevent duplicate credit settlements`
2. `feat(orders): add reconciliation details to credit statements`
3. `chore(invoicing): mark stub invoices as development-only`
4. `test: cover settlement duplicate protection and statement pdf`
