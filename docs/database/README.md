# Database Docs

DBML, ERD, and schema companion docs live here. The main schema narrative stays at [`../03-database-schema.md`](../03-database-schema.md).

The three DBML views were reconciled with the EF model on 2026-08-16. They share the same
59 implemented tables; roadmap-only tables are not mixed into the current model.

| File | Purpose |
|---|---|
| [03-database-schema.dbml](./03-database-schema.dbml) | Physical PostgreSQL view: columns, types, indexes, defaults, checks, and 42 enforced FKs |
| [03-database-schema.conceptual.md](./03-database-schema.conceptual.md) | Conceptual narrative (legacy companion) |
| [03-database-schema.conceptual.dbml](./03-database-schema.conceptual.dbml) | Conceptual view: identities and relationship keys |
| [03A-conceptual-erd-chen-core.drawio](./03A-conceptual-erd-chen-core.drawio) | Single-page monochrome conceptual ERD in Chen notation |
| [03-database-schema.logical.dbml](./03-database-schema.logical.dbml) | Logical view: business attributes plus enforced and application relationships |
| [03B-database-erd.md](./03B-database-erd.md) | Compact Mermaid ERD for implemented tables |
| [03C-table-descriptions.md](./03C-table-descriptions.md) | Business purpose of each table |
| [03D-entities-description.md](./03D-entities-description.md) | Entity + attribute descriptions (physical/logical) |
| [03E-conceptual-entities-description.md](./03E-conceptual-entities-description.md) | Entity + attribute descriptions (conceptual) |
