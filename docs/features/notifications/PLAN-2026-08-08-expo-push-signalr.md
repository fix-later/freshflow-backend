# PLAN (IMPLEMENTED) — Expo Push Notifications + SignalR Notification Realtime

Date: 2026-08-08 · Status: **IMPLEMENTED (backend); real-device smoke pending** · Module: Notifications

> Backend-first plan. `freshflow-app` and `freshflow-web` are reference clients only; this plan does
> not modify either frontend repo.
>
> This plan continues `AUDIT-2026-07-08-not-backend-plan.md`: the notification inbox, device
> registry, domain-event consumers and retry worker are already complete. It supersedes only the old
> decision to keep `LogPushSender` as the final sender and tightens active device-token ownership.

## Goal

Complete the existing Notifications module so that:

1. A committed business event creates one durable notification row.
2. Connected clients receive `NotificationCreated` through SignalR immediately.
3. iOS/Android clients registered with an Expo push token receive an OS push notification when the
   app is backgrounded or closed.
4. Disconnected/reconnecting clients recover from `GET /api/v1/notifications`; neither SignalR nor
   push is treated as the source of truth.

## What already exists (reuse, do not rebuild)

- `Notification`, `NotificationDevice`, `NotificationType`, `NotificationSendStatus` domain models.
- Authenticated endpoints:
  - `POST /api/v1/notifications/devices`
  - `DELETE /api/v1/notifications/devices`
  - `GET /api/v1/notifications`
  - `PATCH /api/v1/notifications/{id}/read`
- `NotificationWriter`: persist first, then call `IPushSender`, record `pending/sent/failed`.
- `NotificationRetryHostedService` + `NotificationRetryService`: retry failed/pending rows with
  configurable interval, backoff, batch size and maximum attempts.
- Integration-event consumers for order confirmed/cancelled, delivery started/completed, credit
  threshold, credit statement, refund and scheduled-order attention.
- Existing SignalR host wiring and domain hubs:
  - `/hubs/pricing`
  - `/hubs/orders`
  - `/hubs/delivery`
- Existing JWT SignalR query-token support and mobile `@microsoft/signalr` client.
- Existing infrastructure patterns to copy:
  - `OrderHub` / `OrderBroadcastService` for authenticated groups.
  - typed/named `HttpClient` registration in the repo.
  - module EF configuration through `EfAssemblyRegistry`.

## Locked decisions

### 1. Use Expo Push Service for the mobile app

- Mobile is Expo; backend sends `ExpoPushToken` values to
  `https://exp.host/--/api/v2/push/send`.
- Use the .NET `HttpClient`/`System.Text.Json` stack. Do not add a third-party Expo SDK.
- Do not integrate FCM and APNs directly in this phase.
- `platform=web` devices are not sent to Expo; web push/VAPID remains separate work.
- Optional Expo enhanced-security access token comes from configuration/environment, never source.

### 2. Database inbox remains authoritative

- The notification row is persisted before either realtime or push delivery.
- SignalR and push failures never delete or roll back the row.
- SignalR delivery does not change `send_status`; that status continues to describe push dispatch.
- Push `sent` means Expo accepted at least one ticket, not that a person saw the notification.
- With no active mobile devices, mark dispatch complete so the retry worker does not loop forever;
  the notification is still available through the inbox API.

### 3. Dedicated notification hub; keep existing domain hubs

- Add `/hubs/notifications` with `[Authorize]` for every authenticated role.
- On connect, parse JWT `sub` and join `user:{userId}` automatically.
- The hub exposes no client-callable business method.
- Server event name: `NotificationCreated`.
- Event payload is the existing `NotificationDto` shape.
- Do not merge pricing/order/delivery into a mega hub. Domain hubs update live screens;
  `NotificationHub` updates the inbox/badge/toast.

### 4. Reconnect is REST reconciliation, not SignalR acknowledgement

- SignalR is ephemeral and may drop messages during disconnects.
- Client deduplicates by notification ID and reloads `GET /api/v1/notifications` after reconnect.
- No ACK table, connection table, replay buffer or custom message broker in this phase.

### 5. One active installation belongs to one user

The current unique key `(user_id, token)` permits the same phone token to remain active for two
accounts. That can expose account A's notification after the phone signs into account B.

- Active Expo token is globally unique, not unique only inside one user.
- Non-null active `device_id` is globally unique and represents one app installation.
- Registering an existing token/device under a new authenticated user transfers the active
  installation to that user.
- Registering a rotated token for the same `device_id` replaces the previous token.
- Unregister remains scoped to the authenticated user and soft-revokes the active installation.
- `DeviceNotRegistered` from Expo soft-revokes the affected token.

### 6. Keep aggregate push status for MVP

- Do not add `notification_push_deliveries` yet.
- Send at most 100 messages per Expo request.
- Whole-request network/429/5xx failure returns failure and uses the existing retry worker.
- Per-token permanent errors are logged and invalid tokens are revoked.
- If at least one ticket is accepted, the notification is marked sent; do not retry the whole batch
  and duplicate pushes to devices that already succeeded.
- Push receipt polling and per-device delivery history are a production-hardening follow-up; add
  them before delivery analytics or strict per-device retry is required.

### 7. Single API instance is the initial deployment ceiling

- `SignalR:UseRedis` currently has no code behind it. Keep it false/remove the misleading production
  value for the single-instance deployment.
- Do not add a Redis backplane in this phase.
- Before running two or more API replicas, add
  `Microsoft.AspNetCore.SignalR.StackExchangeRedis`, configure `AddStackExchangeRedis`, sticky
  sessions, and make the retry worker claim rows safely across replicas.

### 8. Keep current notification triggers

- Do not manufacture new events just to demonstrate push.
- Existing restaurant-owner triggers are the MVP notification matrix.
- Driver, market-agent and hub-staff push events require a separate recipient/trigger matrix; add
  them only when the product behavior is defined.

## End-to-end flow

```text
Committed domain event
        |
        v
Notification integration-event handler
        |
        v
NotificationWriter
        |
        +-- 1. INSERT notifications
        |
        +-- 2. SignalR user:{userId} -> NotificationCreated
        |
        +-- 3. ExpoPushSender -> active ios/android Expo tokens
                                      |
                                      +-- accepted ticket -> sent
                                      +-- DeviceNotRegistered -> revoke token
                                      +-- network/429/5xx -> failed -> existing retry job

Reconnect/open notification screen -> GET /api/v1/notifications -> reconcile by ID
```

## Contracts

### SignalR

Endpoint: `GET /hubs/notifications` (SignalR negotiate/WebSocket)

Server event:

```json
{
  "method": "NotificationCreated",
  "payload": {
    "id": "uuid",
    "type": "delivery_update",
    "title": "Đơn hàng đang được giao",
    "body": "Đơn hàng ... đang trên đường giao.",
    "payload": "{\"order_id\":\"...\",\"status\":\"started\"}",
    "isRead": false,
    "readAt": null,
    "createdAt": "2026-08-08T00:00:00Z"
  }
}
```

The actual SignalR client subscribes to `NotificationCreated`; the wrapper above documents the
method/payload pair, not an extra JSON envelope sent by ASP.NET Core SignalR.

### Expo push request

```json
{
  "to": "ExponentPushToken[...]",
  "title": "Đơn hàng đang được giao",
  "body": "Đơn hàng ... đang trên đường giao.",
  "sound": "default",
  "data": {
    "notificationId": "uuid",
    "type": "delivery_update",
    "payload": "{\"order_id\":\"...\",\"status\":\"started\"}"
  }
}
```

- Keep total push payload below 4 KiB.
- Do not include access tokens, personal contact data or full domain entities.
- Credit notification previews should avoid unnecessary sensitive balance details on the lock
  screen; detailed values remain in the authenticated inbox payload.

## Data model change (one migration)

Migration name: `EnforceActiveNotificationDeviceOwnership`

1. Drop `ux_notification_devices_user_token_active`.
2. Before adding global indexes, resolve existing active duplicates:
   - for each token, keep the row with latest `updated_at`; revoke the others;
   - for each non-null device ID, keep the row with latest `updated_at`; revoke the others.
3. Add unique partial index on `token WHERE revoked_at IS NULL`.
4. Add unique partial index on `device_id WHERE device_id IS NOT NULL AND revoked_at IS NULL`.

No notification content/status schema change is required for the MVP.

## Files to ADD

- `FreshFlow.Notifications.Application/Abstractions/INotificationBroadcastService.cs`
  - `BroadcastCreatedAsync(Guid userId, NotificationDto notification, CancellationToken ct)`;
  - `userId` is an internal routing argument and is not added to the client payload.
- `FreshFlow.Notifications.Infrastructure/Realtime/NotificationHub.cs`
  - authenticated, auto-join `user:{sub}`, no public methods.
- `FreshFlow.Notifications.Infrastructure/Realtime/NotificationBroadcastService.cs`
  - `IHubContext<NotificationHub>` -> group -> `NotificationCreated`.
- `FreshFlow.Notifications.Infrastructure/Push/ExpoPushOptions.cs`
  - `Enabled`, `BaseUrl`, optional `AccessToken`, `TimeoutSeconds`.
- `FreshFlow.Notifications.Infrastructure/Push/ExpoPushSender.cs`
  - load active mobile devices, send batches, parse tickets, revoke invalid tokens.
- Focused tests:
  - `tests/Unit/FreshFlow.Notifications.UnitTests/Push/ExpoPushSenderTests.cs`
  - `tests/Unit/FreshFlow.Notifications.UnitTests/Realtime/NotificationHubTests.cs`
  - `tests/Unit/FreshFlow.Notifications.UnitTests/Realtime/NotificationBroadcastServiceTests.cs`

## Files to MODIFY

- `FreshFlow.Notifications.Application/Abstractions/INotificationDeviceRepository.cs`
  - query active devices for a user;
  - revoke an invalid token;
  - support transfer/rotation semantics through existing register seam.
- `FreshFlow.Notifications.Application/Services/NotificationWriter.cs`
  - persist -> realtime best-effort -> push;
  - realtime failure must not alter push status.
- `FreshFlow.Notifications.Domain/Entities/NotificationDevice.cs`
  - controlled user/token reassignment for an authenticated installation.
- `FreshFlow.Notifications.Infrastructure/Repositories/NotificationDeviceRepository.cs`
  - lookup globally by active token/device ID and transfer atomically.
- `FreshFlow.Notifications.Infrastructure/Persistence/Configurations/NotificationDeviceConfiguration.cs`
  - replace the current per-user active-token index with global active indexes.
- `FreshFlow.Notifications.Infrastructure/DependencyInjection.cs`
  - register notification broadcaster;
  - `Enabled=true`: typed `HttpClient` + `ExpoPushSender`;
  - `Enabled=false`: retain `LogPushSender` for local development.
- `FreshFlow.Notifications.Infrastructure.csproj`
  - add `FrameworkReference Include="Microsoft.AspNetCore.App"` for SignalR;
  - add the centrally-versioned `Microsoft.Extensions.Http` reference if required by `AddHttpClient`.
- `FreshFlow.API/Program.cs`
  - map `NotificationHub` at `/hubs/notifications`;
  - do not add a second `AddSignalR()` registration.
- `FreshFlow.Auth.Infrastructure/DependencyInjection.cs`
  - accept query-string `access_token` only when request path starts with `/hubs`.
- `FreshFlow.API/appsettings.json` and `appsettings.Development.json`
  - add `Notifications:Push` configuration;
  - make the single-instance SignalR configuration truthful.
- Existing notification writer/device/DI/persistence tests affected by constructor and registration
  changes.
- EF migration + `AppDbContextModelSnapshot`.

## Configuration

```json
{
  "Notifications": {
    "Push": {
      "Enabled": false,
      "BaseUrl": "https://exp.host/--/api/v2/push/send",
      "AccessToken": "",
      "TimeoutSeconds": 10
    }
  },
  "SignalR": {
    "UseRedis": false
  }
}
```

Production environment variables:

```text
Notifications__Push__Enabled=true
Notifications__Push__AccessToken=<secret, only when Expo push security is enabled>
```

Do not commit the Expo access token, EAS credentials, FCM service account or APNs key.

## Implementation order

### Phase 1 — Secure device ownership

1. Add failing tests for account switch, token rotation, idempotent re-register and unregister
   ownership.
2. Update `NotificationDevice` + repository behavior.
3. Update EF indexes and generate the migration with duplicate cleanup.
4. Verify register remains safe under a unique-violation race.

Acceptance:

- The same active token/device cannot belong to two users.
- Signing account B into the same installation stops account A pushes to that installation.
- A rotated token leaves only the newest token active.

### Phase 2 — Real Expo push sender

1. Add `ExpoPushOptions` and typed `HttpClient`.
2. Implement active-device query and `ExpoPushSender` in batches of 100.
3. Handle HTTP failure, accepted/error tickets and `DeviceNotRegistered`.
4. Switch DI by `Notifications:Push:Enabled`; keep log sender in development.

Acceptance:

- Disabled config performs no external HTTP call.
- Enabled config sends correct title/body/data to active iOS/Android Expo tokens only.
- Network/429/5xx failure leaves notification retryable.
- Invalid device ticket revokes only that device.
- At least one accepted ticket marks the aggregate notification sent.

### Phase 3 — Notification SignalR

1. Add broadcast abstraction, `NotificationHub` and implementation.
2. Map `/hubs/notifications`.
3. Broadcast only after the notification row exists and before the slower external push call.
4. Restrict JWT query-token extraction to `/hubs`.

Acceptance:

- Unauthenticated connection is rejected.
- A user joins only their own `user:{sub}` group.
- All concurrent connections for the same user receive `NotificationCreated` once per persisted
  notification.
- Another user receives nothing.
- SignalR failure does not fail push or delete the inbox row.

### Phase 4 — Verification and backend handoff

1. Run focused notification tests, full build, EF drift check and format gate.
2. Smoke SignalR with two authenticated users.
3. Smoke Expo push with one real Expo token once the mobile/EAS setup is available.
4. Document the frontend contract; do not edit frontend repos in this implementation.

## Tests

Minimum focused coverage:

- `NotificationDeviceRepository`
  - register new token;
  - idempotent same user/token;
  - transfer same token/device from A to B;
  - rotate token for same device;
  - unique-violation race;
  - caller cannot unregister another user's token.
- `ExpoPushSender`
  - no active mobile token -> no HTTP;
  - ignore revoked and web tokens;
  - request serialization + batches of 100;
  - optional Authorization header;
  - 429/5xx/network exception -> retryable failure;
  - accepted ticket;
  - mixed accepted + `DeviceNotRegistered`;
  - all rejected.
- `NotificationWriter`
  - persist occurs before broadcast/push;
  - broadcast receives the persisted DTO;
  - broadcast failure does not block push;
  - push failure persists failed status.
- `NotificationHub`
  - valid `sub` joins correct group;
  - missing/malformed `sub` rejects connection.
- `NotificationBroadcastService`
  - exact method name and user group.
- Auth bearer handler
  - query token accepted on `/hubs/...`;
  - ignored on ordinary API paths.

## Verification commands (must actually run during implementation)

```bash
dotnet build FreshFlow.slnx
dotnet test tests/Unit/FreshFlow.Notifications.UnitTests/
dotnet test tests/Unit/FreshFlow.Orders.UnitTests/ --filter "FullyQualifiedName~Notifications"
dotnet test tests/Integration/FreshFlow.IntegrationTests/ --filter "FullyQualifiedName~Notification"
dotnet ef migrations has-pending-model-changes \
  --project src/FreshFlow.Infrastructure.Persistence \
  --startup-project src/FreshFlow.API
dotnet format FreshFlow.slnx --verify-no-changes
dotnet build-server shutdown
```

Manual smoke after the mobile team supplies a real Expo token:

1. Register device through `POST /api/v1/notifications/devices`.
2. Connect the same JWT to `/hubs/notifications`.
3. Trigger an existing order-confirmed notification.
4. Assert one DB inbox row, one `NotificationCreated` event and one Expo push ticket.
5. Disconnect SignalR, trigger another event, reconnect and verify REST reconciliation finds it.

## Deployment notes

- Run migration before enabling push.
- Deploy with `Notifications__Push__Enabled=false`; validate health/API/SignalR first.
- Enable push only after EAS/FCM/APNs credentials and a real mobile build are ready.
- Monitor Expo HTTP status, accepted/error ticket counts, invalid-token revocations, retry queue size
  and SignalR connection failures. Do not log raw Expo tokens or access tokens.
- Rollback switch: set `Notifications__Push__Enabled=false`; inbox and SignalR continue working.

## Deferred / add only when needed

- Expo receipt polling (~15 minutes), per-device delivery table and delivery analytics.
- Direct FCM/APNs integration.
- Web Push/VAPID.
- Notification preferences, quiet hours, topics and mark-all-read.
- Transactional outbox. Add when notifications become no-loss business records; current post-commit
  dispatch can lose an event if the process dies after the business commit but before handler work.
- Redis SignalR backplane and distributed retry locking. Add before multiple API replicas.
- Driver/market-agent/hub-staff trigger matrix.

## Frontend handoff (reference only)

After backend completion, `freshflow-app` still needs a separate task to:

- install/configure `expo-notifications` and `expo-constants`;
- set `extra.eas.projectId` and platform push credentials;
- request permission and obtain `ExpoPushToken`;
- register on login/token rotation and unregister on logout;
- keep one `/hubs/notifications` connection at app scope;
- subscribe to `NotificationCreated`, dedupe by ID and reconcile after reconnect;
- handle notification taps using semantic payload fields rather than backend-owned screen names.

## Backend implementation handoff

Implemented in four phase commits:

1. `d15f663` — secure active device ownership and migration.
2. `f458c45` — Expo push sender, batching, ticket handling and invalid-token revocation.
3. `02fb4d1` — authenticated notification SignalR hub and best-effort broadcast.
4. Phase 4 commit — final verification, truthful single-instance defaults and this handoff.

Runtime defaults remain safe: Expo push is disabled and Redis SignalR is disabled. No frontend repo
was modified.

Automated verification completed: solution build, 159 notification unit tests, the focused Orders
notification test, two notification integration tests, 405 Auth unit tests, EF model drift check and
format gate. The remaining manual checks require external assets not present in this backend repo:
two real authenticated SignalR clients and a real Expo token/EAS build.
