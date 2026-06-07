# Feature Specification: FreshFlow Auth Module v1

**Feature Branch**: `003-auth-module`
**Created**: 2026-05-31
**Status**: Draft
**Input**: User description: "Implement FreshFlow Auth module v1. Admin creates users through POST /api/v1/admin/users. Public Restaurant self-registration is deferred. Include login, refresh token rotation, logout, Admin user creation for Market Agent, Hub Staff, Driver, Restaurant, Restaurant approval, JWT RBAC, seeded Admin, and tests."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Authenticated User Logs In And Maintains Session (Priority: P1)

As any registered FreshFlow user, I want to log in with email and password and maintain my session through refresh tokens, so that I can use my role-specific workflows without repeatedly entering credentials.

**Why this priority**: Every v1 feature depends on authenticated users and role claims. No downstream module should start until login, refresh, and logout are reliable.

**Independent Test**: Seed an Admin account, log in, call a protected endpoint with the returned access token, refresh the token pair, and log out.

**Acceptance Scenarios**:

1. **Given** a registered active user, **When** they submit valid credentials to `POST /api/v1/auth/login`, **Then** the response includes an access token, refresh token, `expiresIn: 900`, and user role.
2. **Given** a wrong password or unknown email, **When** login is attempted, **Then** the response is HTTP 401 with `INVALID_CREDENTIALS` and no token fields.
3. **Given** a valid refresh token, **When** it is submitted to `POST /api/v1/auth/refresh`, **Then** the server returns a new access token and refresh token and invalidates the old refresh token.
4. **Given** a refresh token has already been used, **When** it is submitted again, **Then** the request is rejected and the token family is invalidated.
5. **Given** an authenticated user logs out with a refresh token, **When** logout succeeds, **Then** that refresh token can no longer be used and other active sessions for the same user are not affected.

---

### User Story 2 - Admin Creates And Approves Users (Priority: P1)

As an Admin, I want to create accounts for Market Agents, Hub Staff, Drivers, and Restaurants, so that FreshFlow can onboard internal operators and restaurants without public self-registration.

**Why this priority**: FreshFlow v1 uses controlled onboarding. Public Restaurant self-registration is explicitly deferred, so Admin user creation is the only account creation path after bootstrap.

**Independent Test**: Log in as seeded Admin, create users for each v1 role through `POST /api/v1/admin/users`, approve a Restaurant, and verify non-Admin users cannot call Admin endpoints.

**Acceptance Scenarios**:

1. **Given** an Admin access token, **When** the Admin creates a user through `POST /api/v1/admin/users`, **Then** the system creates the account and returns HTTP 201 with the new user ID.
2. **Given** a non-Admin access token, **When** the user calls `POST /api/v1/admin/users`, **Then** the system returns HTTP 403.
3. **Given** an Admin creates a Restaurant user, **When** the account is created, **Then** the Restaurant starts with `is_approved = false`.
4. **Given** an unapproved Restaurant, **When** Admin calls `PATCH /api/v1/admin/restaurants/{restaurantId}/approve`, **Then** the Restaurant becomes approved and can use Restaurant-only workflows.
5. **Given** an email address already exists, **When** Admin tries to create another user with that email, **Then** the system returns HTTP 409 with `EMAIL_ALREADY_EXISTS`.

---

### User Story 3 - Role-Based Access Is Enforced (Priority: P1)

As the platform owner, I want every protected endpoint to enforce role access consistently, so that users can only perform actions allowed by their role.

**Why this priority**: Auth without RBAC is unsafe for all domain modules.

**Independent Test**: Use tokens from multiple roles against Admin-only and role-specific protected endpoints and verify 401/403 behavior.

**Acceptance Scenarios**:

1. **Given** no access token, **When** a protected endpoint is called, **Then** the response is HTTP 401.
2. **Given** a valid token with insufficient role, **When** a restricted endpoint is called, **Then** the response is HTTP 403.
3. **Given** a valid token with required role, **When** a restricted endpoint is called, **Then** authorization succeeds and control reaches the endpoint handler.
4. **Given** a seeded Admin account, **When** the system is started repeatedly, **Then** only one bootstrap Admin account exists.

### Edge Cases

- Login for inactive, deleted, or pending-approval accounts is rejected.
- Refresh token expiry returns `REFRESH_TOKEN_EXPIRED`.
- Logout returns success without exposing whether the submitted refresh token exists.
- Self-registered restaurants start unapproved and cannot place orders until Admin approves.
- Legacy role value `kiosk_staff` may be accepted as an alias for `market_agent`, but new contracts use `market_agent`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-AUTH-001**: The system MUST authenticate users through `POST /api/v1/auth/login` with email and password.
- **FR-AUTH-002**: Login success MUST return an access token, refresh token, `expiresIn: 900`, and user identity including role.
- **FR-AUTH-003**: Login failure for wrong password and unknown email MUST return the same `INVALID_CREDENTIALS` response.
- **FR-AUTH-004**: The system MUST support refresh token rotation through `POST /api/v1/auth/refresh`; a used refresh token MUST be invalidated immediately.
- **FR-AUTH-005**: Refresh token reuse MUST invalidate the token family and prevent further refresh from that family.
- **FR-AUTH-006**: `POST /api/v1/auth/logout` MUST revoke only the submitted refresh token for the authenticated user session.
- **FR-AUTH-007**: Admin user creation MUST use `POST /api/v1/admin/users`; `POST /api/v1/auth/register` is not a v1 endpoint.
- **FR-AUTH-008**: Admin MUST be able to create `market_agent`, `hub_staff`, `driver`, and `restaurant` users.
- **FR-AUTH-009**: Restaurant users created by Admin MUST start unapproved and require `PATCH /api/v1/admin/restaurants/{restaurantId}/approve`.
- **FR-AUTH-010**: Restaurant owners MAY self-register via `POST /api/v1/auth/register` (UC-AUTH-11). The account starts with `is_approved = false` and cannot place orders until approved by an Admin.
- **FR-AUTH-011**: RBAC MUST return HTTP 401 for missing/invalid tokens and HTTP 403 for valid tokens with insufficient role.
- **FR-AUTH-012**: The first Admin account MUST be seeded idempotently from deployment configuration.

### Key Entities

- **User**: Authenticated account with email, password hash, role, active status, and audit timestamps.
- **Refresh Token**: Opaque session credential stored only as a hash; belongs to a token family for rotation and reuse detection.
- **Restaurant Profile**: Restaurant account details and approval status; created when Admin creates a Restaurant user.
- **Market Assignment**: Link between a Market Agent and a market they are allowed to update.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Valid login returns a token pair with a 15-minute access-token TTL.
- **SC-002**: Unknown email and wrong password are indistinguishable to clients.
- **SC-003**: Refresh token reuse is detected and blocks the whole token family.
- **SC-004**: Admin can create all v1 roles through `POST /api/v1/admin/users`.
- **SC-005**: Non-Admin users receive HTTP 403 for Admin user creation.
- **SC-006**: Pending Restaurant users cannot access Restaurant-only workflows until approved.
- **SC-007**: Build and Auth unit/integration tests pass before any downstream module depends on Auth.

## Assumptions

- `docs/` remains the product source of truth; this feature spec narrows the Auth implementation slice.
- Restaurant self-registration is available via `POST /api/v1/auth/register` (UC-AUTH-11); self-registered accounts start pending approval.
- The Auth module is implemented before Pricing, Orders, Logistics, Hub, Analytics, and Notifications.
- Existing Clean Architecture boundaries remain in effect: domain models do not depend on other modules.
