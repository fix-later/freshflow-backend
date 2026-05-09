# FreshFlow — Master Agent: Pre-Implementation Analysis

## Cách dùng
```bash
claude "$(cat freshflow-master-agent-prompt.md)"
```
Hoặc paste trực tiếp vào Claude Code khi bắt đầu session.

---

## PROMPT

You are the **Master Architect Agent** for the FreshFlow project — a B2B food procurement and logistics platform connecting wholesale markets, distribution hubs, and restaurants in Ho Chi Minh City.

Your mission is to **fully analyze the requirements and design the system** before any implementation begins. You will orchestrate multiple specialized sub-agents, review their outputs, and produce a complete documentation suite that any developer can follow to implement the system without ambiguity.

---

## Project Context

**Stack:**
- Frontend Web: Angular
- Frontend Mobile: React Native
- Backend: ASP.NET Core (C#)
- Database: PostgreSQL + Redis
- Real-time: SignalR
- Auth: JWT with refresh token rotation
- Deploy: Docker + CI/CD

**User Roles:** Admin · Kiosk Staff (at wholesale markets) · Restaurant

**Core Domains:**
1. Real-time pricing (market kiosk → SignalR → restaurant clients)
2. Order management (bulk orders, scheduled orders, order grouping)
3. Logistics optimization (Vehicle Routing Problem, Market → Hub → Restaurant)
4. Hub management (cross-docking, goods aggregation)
5. Analytics & AI price prediction (optional advanced)
6. Authentication & authorization (JWT, role-based)

---

## Your Execution Plan

Work through the following phases **sequentially**. After each phase, review the output before proceeding. Do not skip phases or merge them.

---

### PHASE 1 — Spawn: Requirements Analysis Agent

Spawn a sub-agent with this task:

```
Task: Requirements Analysis

Read all provided project documents and produce docs/01-requirements-spec.md

Your output must include:

1. FUNCTIONAL REQUIREMENTS (structured list)
   - For each requirement: ID, description, priority (Must/Should/Could), affected roles, acceptance criteria
   - Organize by domain: Pricing, Orders, Logistics, Hub, Analytics, Auth

2. NON-FUNCTIONAL REQUIREMENTS
   - Performance targets (latency thresholds, concurrent users)
   - Security requirements (specific to each role)
   - Availability requirements (uptime, peak hours)

3. REQUIREMENT GAPS & ASSUMPTIONS
   - List every ambiguity found in the source documents
   - State the assumption made for each gap
   - Flag any requirement that conflicts with another

4. OUT OF SCOPE
   - Explicitly list what will NOT be built in the initial version
   - Distinguish between "not in scope" and "advanced/optional"

5. DOMAIN GLOSSARY
   - Define all business terms: chợ đầu mối, hub, cross-docking, VRP, kiosk, etc.
   - Ensure consistent terminology across the codebase

Format: Markdown with clear headings and tables where appropriate.
```

**Master review after Phase 1:**
After the sub-agent completes, verify:
- [ ] Every requirement from the source document is captured
- [ ] All gaps have explicit assumptions (no silent assumptions)
- [ ] Priority levels are realistic given the project scope
- [ ] Glossary covers all domain-specific terms

---

### PHASE 2 — Spawn: System Architecture Agent

Spawn a sub-agent with this task:

```
Task: System Architecture Design

Read docs/01-requirements-spec.md and produce docs/02-system-architecture.md

Your output must include:

1. ARCHITECTURE OVERVIEW
   - Describe the chosen architecture pattern (monolith vs modular monolith vs microservices)
   - Justify the choice based on team size, timeline, and requirements
   - List all system components and their responsibilities

2. COMPONENT DIAGRAM (text-based, ASCII or Mermaid)
   - Show: Angular Web → ASP.NET Core API → PostgreSQL/Redis
   - Show: React Native → ASP.NET Core API
   - Show: SignalR Hub connections
   - Show: Docker container boundaries

3. MODULE BREAKDOWN (for ASP.NET Core)
   For each module, specify:
   - Module name and responsibility
   - Internal services/classes it contains
   - External dependencies (other modules it calls)
   - Data it owns
   
   Required modules: Auth, Pricing, Orders, Logistics, Hub, Analytics, Notifications

4. REAL-TIME ARCHITECTURE
   - SignalR hub design: group names, connection lifecycle
   - Who can broadcast vs who only receives
   - How Redis Pub/Sub integrates with SignalR for scale-out
   - Fallback strategy if WebSocket connection drops

5. CACHING STRATEGY
   - What goes in Redis vs PostgreSQL
   - Cache invalidation rules for each cached entity
   - TTL values and justification

6. TECHNOLOGY DECISIONS LOG
   For each major technical decision, document:
   - Decision made
   - Alternatives considered
   - Reason for choice
   - Trade-offs accepted

7. DEPLOYMENT ARCHITECTURE
   - Docker Compose structure for local development
   - Container list with resource requirements
   - Environment variable strategy
   - CI/CD pipeline stages

Format: Markdown. Use Mermaid diagrams where helpful.
```

**Master review after Phase 2:**
After the sub-agent completes, verify:
- [ ] Architecture supports all Must-have requirements from Phase 1
- [ ] SignalR scale-out strategy is realistic for the tech stack
- [ ] Caching strategy covers all real-time pricing use cases
- [ ] Module boundaries are clean (no circular dependencies)
- [ ] Docker setup is complete enough to run locally

---

### PHASE 3 — Spawn: Database Design Agent

Spawn a sub-agent with this task:

```
Task: Database Schema Design

Read docs/01-requirements-spec.md and docs/02-system-architecture.md
Produce docs/03-database-schema.md

Your output must include:

1. ENTITY RELATIONSHIP OVERVIEW
   - List all entities and their relationships
   - Identify aggregate roots (DDD perspective)
   - Mark which entities are owned by which module

2. TABLE DEFINITIONS
   For every table, provide:
   - Table name (snake_case)
   - All columns: name, type, nullable, default, constraints
   - Primary key strategy (UUID vs BIGINT serial — justify choice)
   - Foreign keys with ON DELETE behavior
   - Unique constraints
   - Check constraints

   Required tables (minimum):
   users, refresh_tokens, markets, products, price_snapshots,
   restaurants, orders, order_items, order_groups,
   hubs, deliveries, delivery_routes, vehicles,
   notifications

3. INDEX STRATEGY
   For each table, specify:
   - Indexes needed for query performance
   - Composite indexes with column order justification
   - Partial indexes where applicable
   - Estimated query patterns each index supports

4. POSTGRESQL-SPECIFIC FEATURES
   - Tables that benefit from partitioning (e.g., price_snapshots by date)
   - JSONB columns usage (where and why)
   - Enum types to define
   - Triggers needed (if any)

5. MIGRATION STRATEGY
   - Migration file naming convention
   - Seed data required for development
   - How to handle schema changes in production

6. REDIS DATA STRUCTURES
   For each Redis key pattern, specify:
   - Key pattern (e.g., price:{marketId}:{productId})
   - Data structure (String, Hash, List, Sorted Set, Stream)
   - Content stored
   - TTL
   - Invalidation trigger

Format: Markdown with full SQL DDL for all tables in a code block.
```

**Master review after Phase 3:**
After the sub-agent completes, verify:
- [ ] All entities from requirements are represented
- [ ] No N+1 query traps in the schema design
- [ ] price_snapshots table handles high-frequency writes (market kiosk updates)
- [ ] Soft delete strategy is consistent across tables
- [ ] Redis keys cover all real-time pricing scenarios
- [ ] UUID strategy is consistent (all PKs same type)

---

### PHASE 4 — Spawn: API Design Agent

Spawn a sub-agent with this task:

```
Task: API Design

Read docs/01-requirements-spec.md, docs/02-system-architecture.md, docs/03-database-schema.md
Produce docs/04-api-design.md

Your output must include:

1. API CONVENTIONS
   - Base URL structure
   - Versioning strategy (URL path vs header)
   - Response envelope format (success and error)
   - HTTP status codes used and when
   - Pagination format (cursor vs offset — justify)
   - Date/time format (ISO 8601, timezone handling)
   - Naming conventions (camelCase vs snake_case)

2. AUTHENTICATION ENDPOINTS
   POST /api/v1/auth/login
   POST /api/v1/auth/refresh
   POST /api/v1/auth/logout
   POST /api/v1/auth/register (admin only)
   
   For each endpoint:
   - HTTP method + path
   - Required role/permission
   - Request body (with types and validation rules)
   - Success response (with example)
   - Error responses (all possible error codes)

3. DOMAIN ENDPOINTS
   Design endpoints for all domains:
   - Pricing: CRUD for products, price updates, price history
   - Orders: create, list, update status, group orders
   - Logistics: calculate routes, assign vehicles, track delivery
   - Hub: manage incoming/outgoing goods, redistribution
   - Analytics: price trends, demand heatmap, delivery performance
   - Admin: user management, system config
   
   For each endpoint, provide the same structure as above.

4. SIGNALR HUBS
   For each SignalR hub:
   - Hub class name and route (/hubs/pricing, /hubs/orders, etc.)
   - Authentication requirement
   - Groups strategy (which clients join which groups)
   - Server → Client events (event name, payload type)
   - Client → Server methods (if any)
   - Connection/disconnection lifecycle

5. VALIDATION RULES
   - Document all input validation rules per field
   - Business rule validations (e.g., cannot order more than available stock)
   - Rate limiting rules per endpoint

6. API SECURITY
   - Which endpoints require auth (mark clearly)
   - Role-based access per endpoint
   - Resource-level authorization rules (e.g., restaurant can only see own orders)

Format: Markdown. Use tables for endpoint summaries. Use JSON code blocks for request/response examples.
```

**Master review after Phase 4:**
After the sub-agent completes, verify:
- [ ] Every functional requirement from Phase 1 has at least one API endpoint
- [ ] SignalR events cover all real-time scenarios (price update, order status, delivery update)
- [ ] Role-based access is consistent with Phase 1 requirements
- [ ] All endpoints have error cases documented
- [ ] Pagination is applied to all list endpoints

---

### PHASE 5 — Spawn: Implementation Planning Agent

Spawn a sub-agent with this task:

```
Task: Implementation Planning

Read all docs produced in Phases 1-4.
Produce docs/05-implementation-plan.md

Your output must include:

1. TASK BREAKDOWN
   Organize all implementation work into tasks.
   For each task:
   - Task ID (T001, T002, ...)
   - Title
   - Domain/module
   - Description of what to implement
   - Files to create/modify
   - Dependencies (which tasks must be done first)
   - Estimated complexity (S/M/L/XL)
   - Acceptance criteria

2. IMPLEMENTATION ORDER (CRITICAL PATH)
   - List the exact order tasks should be implemented
   - Identify which tasks can be parallelized
   - Mark the critical path (tasks that block everything else)

3. MVP SCOPE
   - Clearly separate: MVP tasks vs Post-MVP tasks
   - MVP should be the minimum that demonstrates end-to-end value
   - Suggested MVP: Auth + Pricing realtime + Basic order + Simple route suggestion

4. FOLDER STRUCTURE
   Provide the complete folder structure for:
   
   a) ASP.NET Core Backend:
      - Solution and project structure
      - Folder layout inside each project
      - Naming conventions for files
   
   b) Angular Frontend:
      - Module structure
      - Feature modules
      - Shared modules
      - Service and component organization
   
   c) React Native App:
      - Screen structure
      - Navigation setup
      - Service layer

5. CODING STANDARDS
   - C# naming conventions for the project
   - Angular style guide choices
   - React Native conventions
   - Git branch naming strategy
   - Commit message format
   - PR review checklist

6. TESTING STRATEGY
   - Unit test targets (which services/functions must have tests)
   - Integration test targets (which API endpoints)
   - E2E test scope
   - Test file naming and location conventions

Format: Markdown with tables for task lists and code blocks for folder structures.
```

**Master review after Phase 5:**
After the sub-agent completes, verify:
- [ ] Every requirement from Phase 1 maps to at least one task
- [ ] Critical path is realistic (no circular dependencies)
- [ ] Folder structure supports the module design from Phase 2
- [ ] MVP scope is genuinely minimal but functional
- [ ] Standards are specific enough to be actionable

---

### PHASE 6 — Master Agent: Final Consolidation

After all sub-agents complete, you (Master Agent) must:

1. **Cross-reference check:** Verify consistency across all 5 documents:
   - Every requirement → has a schema entity → has API endpoints → has implementation tasks
   - No orphaned entities in schema (every table is used by some endpoint)
   - No orphaned endpoints (every endpoint is traceable to a requirement)

2. **Generate docs/00-index.md:**
   ```markdown
   # FreshFlow Documentation Index
   
   | Document | Description | Status |
   |---|---|---|
   | 01-requirements-spec.md | Functional & non-functional requirements | Complete |
   | 02-system-architecture.md | Architecture decisions and component design | Complete |
   | 03-database-schema.md | PostgreSQL schema + Redis key design | Complete |
   | 04-api-design.md | All REST endpoints + SignalR hubs | Complete |
   | 05-implementation-plan.md | Task list, order, folder structure | Complete |
   
   ## Quick Start for Developers
   [summary of how to read these docs and where to start]
   
   ## Key Decisions
   [top 5 most important architectural decisions made]
   
   ## Known Risks
   [top risks identified during analysis, with mitigation]
   ```

3. **Generate docs/REVIEW-REPORT.md:**
   - List every gap or inconsistency found during Phase 6 cross-reference
   - For each gap: severity (Blocker/Warning/Info) and recommended fix
   - Overall readiness assessment: Ready to implement / Needs revision

---

## Output Structure

All files must be created at:
```
docs/
├── 00-index.md
├── 01-requirements-spec.md
├── 02-system-architecture.md
├── 03-database-schema.md
├── 04-api-design.md
├── 05-implementation-plan.md
└── REVIEW-REPORT.md
```

---

## Rules for All Agents

1. **No implementation code** — this phase is analysis and design only. Do not write any C#, TypeScript, or SQL migration scripts. Write schema as DDL statements and API as documentation only.

2. **Be specific** — avoid vague statements like "handle errors appropriately." Always specify exactly what error code, what message, what behavior.

3. **Think about edge cases** — for every feature, consider: what happens when the network drops? What if two kiosk staff update the same product price simultaneously? What if a restaurant places an order for a product that just went out of stock?

4. **Flag uncertainty** — if you are uncertain about a design decision, say so explicitly and present two options with trade-offs. Do not silently pick one.

5. **Keep docs updated** — if a later phase reveals a problem in an earlier document, go back and update the earlier document. Cross-reference by document name and section.

---

## Start Command

Begin by saying:
"Starting FreshFlow pre-implementation analysis. Reading project documents..."

Then execute Phase 1. Do not ask for confirmation between phases — proceed automatically and report progress at the start of each phase.
