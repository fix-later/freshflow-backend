# 02E — Class Diagrams (Detailed Design)

> Companion to the SDD (Report 4) **§3 Detailed Design**. One `classDiagram` per implemented feature,
> derived from the actual source under `src/`. Signatures are trimmed for readability (async suffixes and
> `CancellationToken` parameters omitted); see the referenced source files for the full API.
>
> Shared request pipeline (not repeated per feature): `Controller -> MediatR ISender -> ValidationBehavior -> Command/Query Handler -> Repository -> EF Core -> post-commit domain events`. See [`02A-system-overview-diagrams.md §4`](./02A-system-overview-diagrams.md#4-http-request-pipeline).

---

## 1. Authentication & Authorization (§3.1)

Source: `src/Modules/Auth/**`, `src/FreshFlow.API/Controllers/AuthController.cs`

```mermaid
classDiagram
    class AuthController {
        +Login(LoginRequest) IActionResult
        +Refresh(RefreshRequest) IActionResult
        +Logout(LogoutRequest) IActionResult
        +ForgotPassword(ForgotPasswordRequest) IActionResult
        +ResetPassword(ResetPasswordRequest) IActionResult
        +Verify(VerifyRequest) IActionResult
    }

    class ISender {
        <<MediatR>>
        +Send(command) Task~Result~
    }

    class LoginCommandHandler {
        +Handle(LoginCommand) Task~Result~LoginResponse~~
    }
    class RefreshTokenCommandHandler {
        +Handle(RefreshTokenCommand) Task~Result~RefreshTokenResponse~~
    }

    class ITokenService {
        <<interface>>
        +GenerateAccessToken(userId, email, role) string
        +GenerateRefreshToken() string
        +HashRefreshToken(rawToken) string
        +AccessTokenTtlSeconds int
        +RefreshTokenTtlDays int
    }
    class JwtTokenService

    class IPasswordHasher {
        <<interface>>
        +Hash(password) string
        +Verify(password, hash) bool
    }
    class BCryptPasswordHasher

    class IUserRepository {
        <<interface>>
    }
    class IRefreshTokenRepository {
        <<interface>>
    }
    class IPasswordResetSender {
        <<interface>>
    }
    class ResendPasswordResetSender

    class User {
        <<AggregateRoot>>
        +Guid Id
        +string Email
        +string PasswordHash
        +Guid RoleId
        +bool IsActive
        +int FailedLoginCount
        +DateTime? LockedUntil
        +Create(email, hash, role, phone) User
        +RecordFailedLogin() void
        +RecordSuccessfulLogin() void
        +Unlock() void
        +ChangePassword(hash) void
        +MarkEmailVerified() void
        +CanLogin() bool
    }
    class Role {
        +Guid Id
        +string Name
        +string Description
    }
    class RefreshToken {
        +Guid Id
        +Guid UserId
        +string TokenHash
        +Guid FamilyId
        +DateTime ExpiresAt
        +DateTime? RevokedAt
        +Revoke(replacedById, reason) void
    }

    AuthController --> ISender
    ISender ..> LoginCommandHandler
    ISender ..> RefreshTokenCommandHandler
    LoginCommandHandler --> IUserRepository
    LoginCommandHandler --> IPasswordHasher
    LoginCommandHandler --> ITokenService
    LoginCommandHandler --> IRefreshTokenRepository
    RefreshTokenCommandHandler --> IRefreshTokenRepository
    RefreshTokenCommandHandler --> ITokenService
    LoginCommandHandler ..> IPasswordResetSender
    JwtTokenService ..|> ITokenService
    BCryptPasswordHasher ..|> IPasswordHasher
    ResendPasswordResetSender ..|> IPasswordResetSender
    IUserRepository ..> User
    User "1" --> "1" Role
    User "1" --> "*" RefreshToken
```

---

## 2. Real-time Pricing Update (§3.2)

Source: `src/Modules/Pricing/**`, `src/FreshFlow.API/Controllers/MarketsController.cs`

```mermaid
classDiagram
    class MarketsController {
        +UpdateProductPriceAsync(marketId, productId, body) IActionResult
        +GetPriceHistoryAsync(marketId, productId) IActionResult
    }
    class ISender {
        <<MediatR>>
    }
    class UpdateProductPriceCommandHandler {
        +Handle(UpdateProductPriceCommand) Task~Result~
    }

    class IMarketProductRepository {
        <<interface>>
    }
    class IPriceSnapshotRepository {
        <<interface>>
    }
    class IPricingBroadcastService {
        <<interface>>
        +BroadcastPriceUpdate(PriceUpdateBroadcastDto) Task
    }
    class PricingBroadcastService

    class MarketProduct {
        <<AggregateRoot>>
        +Guid Id
        +Guid MarketId
        +Guid ProductId
        +decimal CurrentPrice
        +int CurrentQuantity
        +Guid? UpdatedBy
        +UpdatePrice(newPrice, actor) void
        +ApplyUpdate(newPrice, newQuantity, actor) void
    }
    class PriceSnapshot {
        +Guid Id
        +Guid MarketProductId
        +decimal Price
        +int Quantity
        +DateTime RecordedAt
    }
    class PriceUpdatedDomainEvent {
        <<DomainEvent>>
    }
    class PriceUpdatedDomainEventHandler {
        +Handle(PriceUpdatedDomainEvent) Task
    }
    class PricingHub {
        <<SignalR Hub>>
        +JoinMarketAsync(marketId) Task
        +LeaveMarketAsync(marketId) Task
    }

    MarketsController --> ISender
    ISender ..> UpdateProductPriceCommandHandler
    UpdateProductPriceCommandHandler --> IMarketProductRepository
    UpdateProductPriceCommandHandler --> IPriceSnapshotRepository
    IMarketProductRepository ..> MarketProduct
    IPriceSnapshotRepository ..> PriceSnapshot
    MarketProduct ..> PriceUpdatedDomainEvent : raises
    PriceUpdatedDomainEvent ..> PriceUpdatedDomainEventHandler : dispatched post-commit
    PriceUpdatedDomainEventHandler --> IPricingBroadcastService
    PricingBroadcastService ..|> IPricingBroadcastService
    PricingBroadcastService --> PricingHub : IHubContext
```

---

## 3. Order Creation & B2B Credit (§3.3)

Source: `src/Modules/Orders/**`, `src/FreshFlow.API/Controllers/OrdersController.cs`

```mermaid
classDiagram
    class OrdersController {
        +CreateDraftOrderAsync(body) IActionResult
        +ConfirmOrderAsync(orderId) IActionResult
        +AddOrderItemAsync(orderId, body) IActionResult
    }
    class ISender {
        <<MediatR>>
    }
    class ConfirmOrderCommandHandler {
        +Handle(ConfirmOrderCommand) Task~Result~
    }

    class IOrderRepository {
        <<interface>>
    }
    class ICreditService {
        <<interface>>
        +CanChargeAsync(restaurantId, amount) Task~Result~CreditCheckDto~~
        +ChargeAsync(restaurantId, amount) Task~Result~
        +SettleAsync(restaurantId, amount) Task~Result~
        +RefundAsync(restaurantId, amount) Task~Result~
    }
    class CreditService
    class ICreditRepository {
        <<interface>>
    }
    class IOrderBroadcastService {
        <<interface>>
    }

    class Order {
        <<AggregateRoot>>
        +Guid Id
        +Guid RestaurantId
        +OrderStatus Status
        +OrderPaymentStatus PaymentStatus
        +decimal TotalAmount
        +AddItem(marketProductId, name, qty, unitPrice) Result
        +CanConfirm() Result
        +Confirm() Result
        +Cancel(reason) Result
        +ConfirmReceipt(confirmedAtUtc) Result
    }
    class OrderItem {
        +Guid Id
        +Guid OrderId
        +Guid MarketProductId
        +int Quantity
        +decimal UnitPrice
    }
    class RestaurantCredit {
        +Guid RestaurantId
        +decimal CreditLimit
        +decimal OutstandingBalance
        +Charge(amount) void
        +Settle(amount) void
        +Refund(amount) void
        +SetCreditLimit(newLimit) void
    }
    class CreditTransaction {
        +Guid Id
        +Guid RestaurantId
        +CreditTransactionType Type
        +decimal Amount
    }
    class IMarketProductReader {
        <<interface>>
    }
    class IRestaurantReader {
        <<interface>>
    }
    class OrderHub {
        <<SignalR Hub>>
        +OnConnectedAsync() Task
    }

    OrdersController --> ISender
    ISender ..> ConfirmOrderCommandHandler
    ConfirmOrderCommandHandler --> IOrderRepository
    ConfirmOrderCommandHandler --> ICreditService
    ConfirmOrderCommandHandler --> IRestaurantReader
    ConfirmOrderCommandHandler ..> IMarketProductReader
    CreditService ..|> ICreditService
    CreditService --> ICreditRepository
    ICreditRepository ..> RestaurantCredit
    ICreditRepository ..> CreditTransaction
    IOrderRepository ..> Order
    Order "1" --> "*" OrderItem
    RestaurantCredit "1" --> "*" CreditTransaction
    ConfirmOrderCommandHandler ..> IOrderBroadcastService
    IOrderBroadcastService ..> OrderHub
```

---

## 4. Scheduled Order Generation (§3.4)

Source: `src/Modules/Orders/FreshFlow.Orders.Application/Services/ScheduledOrderGenerationService.cs`, `ScheduledOrderGenerationHostedService`

```mermaid
classDiagram
    class ScheduledOrderGenerationHostedService {
        <<IHostedService>>
        +ExecuteAsync(stoppingToken) Task
    }
    class ScheduledOrderGenerationService {
        +GenerateDueAsync(nowUtc) Task~ScheduledOrderGenerationResultDto~
    }
    class IScheduledOrderRepository {
        <<interface>>
    }
    class IOrderRepository {
        <<interface>>
    }
    class ScheduledOrder {
        <<BaseEntity>>
        +Guid Id
        +Guid RestaurantId
        +RecurrenceType RecurrenceType
        +DateTime FirstRunAt
        +DateTime? LastExecutedAt
        +DateTime? CancelledAt
        +RecordExecution(executedAt) void
        +UpdateSchedule(type, firstRunAt, notes) Result
        +Cancel(cancelledAtUtc) Result
    }
    class Order {
        <<AggregateRoot>>
        +Guid? ScheduledOrderId
        +OrderStatus Status
    }

    ScheduledOrderGenerationHostedService --> ScheduledOrderGenerationService : scoped resolve
    ScheduledOrderGenerationService --> IScheduledOrderRepository
    ScheduledOrderGenerationService --> IOrderRepository
    IScheduledOrderRepository ..> ScheduledOrder
    ScheduledOrderGenerationService ..> Order : creates Draft
    ScheduledOrder "1" --> "*" Order : generates
```

---

## 5. AI Shopping Assistant (§3.5)

Source: `src/FreshFlow.API/Assistant/**`, `src/FreshFlow.API/Controllers/AssistantController.cs`

```mermaid
classDiagram
    class AssistantController {
        +ChatAsync(AssistantChatRequest) IActionResult
    }
    class AssistantOrchestrator {
        +RunAsync(...) Task~AssistantTurnOutcome~
    }
    class IConversationStore {
        <<interface>>
        +LoadAsync(sessionId) Task~ConversationState~
        +SaveAsync(ConversationState) Task
    }
    class DbConversationStore
    class IAssistantChatClient {
        <<interface>>
        +CompleteAsync(...) Task~AssistantTurnResult~
    }
    class ZenMuxChatClient
    class IAssistantToolRegistry {
        <<interface>>
        +Tools IReadOnlyList~AssistantTool~
        +InvokeAsync(toolName, argsJson, ctx) Task~string~
    }
    class AssistantToolRegistry
    class ConfirmationGate {
        +Evaluate(toolName, argsJson, confirmOrderIdFlag) ConfirmationGateResult
    }
    class AssistantTool {
        <<record>>
        +string Name
        +string Description
    }
    class ISender {
        <<MediatR>>
    }
    class ConversationState {
        <<record>>
        +string SessionId
        +Guid UserId
    }

    AssistantController --> AssistantOrchestrator
    AssistantOrchestrator --> IConversationStore
    AssistantOrchestrator --> IAssistantChatClient
    AssistantOrchestrator --> IAssistantToolRegistry
    AssistantOrchestrator --> ConfirmationGate
    DbConversationStore ..|> IConversationStore
    ZenMuxChatClient ..|> IAssistantChatClient
    AssistantToolRegistry ..|> IAssistantToolRegistry
    AssistantToolRegistry "1" --> "*" AssistantTool
    AssistantToolRegistry --> ISender : invokes command/query
    IConversationStore ..> ConversationState
```

---

### Rendering

These are GitHub-flavoured mermaid `classDiagram` blocks. To export images for the SDD:
`mmdc -i docs/diagrams/02E-class-diagrams.md -o out.png` (mermaid-cli), or paste a single block into <https://mermaid.live>.
