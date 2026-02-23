# Reenbit Event Hub — Agent Instructions

## Source of Truth

All planning documents live at `d:/Reenbit/_bmad-output/planning-artifacts/`. Read them before implementing anything:

| Document | Path | Purpose |
|----------|------|---------|
| PRD | `planning-artifacts/prd.md` | Functional requirements FR1–FR37, success criteria, scope |
| Architecture | `planning-artifacts/architecture.md` | Tech stack, decisions, patterns, naming conventions, data flow |
| Epics & Stories | `planning-artifacts/epics.md` | Story-level acceptance criteria, implementation notes, FR coverage map |
| Readiness Report | `planning-artifacts/implementation-readiness-report-2026-02-20.md` | Recommended implementation order |

**When in doubt, the PRD and Architecture are the final arbiter. Do not invent requirements.**

---

## Solution Layout

```
d:/Reenbit/src/api/CarRentalApi/
├── CarRentalApi.slnx                        — Solution file
├── CarRentalApi/                            — ASP.NET Core Web API (entry point)
│   ├── Controllers/EventsController.cs      — Only HTTP entry point
│   ├── Program.cs                           — DI registration, middleware pipeline
│   ├── appsettings.json                     — ConnectionStrings:EventsDb, ServiceBus:ConnectionString
│   └── appsettings.Development.json
├── ReenbitEventHub.Application/             — Use-case / service layer
│   ├── Events/CreateEventRequest.cs         — POST request: userId, action, carId, description?
│   ├── Events/CreateEventResponse.cs        — POST response: publishedCount, eventIds[]
│   ├── Events/EventAction.cs                — Enum: ViewCar, ReserveCar
│   ├── Events/EventResponse.cs              — Single event DTO returned in GET list
│   ├── Events/GetEventsQueryRequest.cs      — GET query params: userId?, type?, from?, to?, sort, page, pageSize
│   ├── Events/GetEventsResponse.cs          — GET response: items[], totalCount, page, pageSize
│   ├── Events/IEventApplicationService.cs   — Service contract
│   ├── Events/EventApplicationService.cs    — Business logic: validation, event generation, publish
│   └── DependencyInjection.cs               — AddApplication()
├── ReenbitEventHub.Domain/                  — Pure domain, no framework dependencies
│   ├── Entities/Event.cs                    — Aggregate: Id, UserId, Type, Description, CreatedAt; use Event.Create()
│   ├── Enums/EventType.cs                   — PageView | Click | Purchase
│   ├── DTOs/EventMessage.cs                 — Message contract sent over the queue
│   ├── Constants/CarCatalog.cs              — car-1→Toyota Corolla, car-2→VW Golf
│   └── Repositories/IEventRepository.cs    — AddAsync + QueryAsync
└── ReenbitEventHub.Infrastructure/          — EF Core, DB, messaging
    ├── Data/EventDbContext.cs               — DbSet<Event>, Fluent API config, indexes
    ├── Data/Repositories/EventRepository.cs — EF Core implementation of IEventRepository
    └── DependencyInjection.cs               — AddInfrastructure(), InitializeInfrastructureAsync()
```

**Note:** `CarRentalApi/Application/`, `CarRentalApi/Contracts/`, `CarRentalApi/Data/` are stale folders excluded from compilation via `<Compile Remove>` in the csproj. Ignore them — do not edit or reference them.

---

## Story Backlog (implementation order)

| Story | Status | Scope |
|-------|--------|-------|
| BE-01 | Done | POST /api/events — event generation + publisher abstraction + 202 response |
| FN-01 | Done | BackgroundService consumer + DB persistence + idempotency |
| BE-02 | Done | GET /api/events — filtering, sorting, pagination |
| FE-01 | Done | Angular reactive form |
| FE-02 | Done | Angular events table |
| Q-01  | Done | Structured logging audit, global error middleware, README |

---

## Architectural Rules (non-negotiable)

1. **Controller never calls infrastructure directly.** Controller → `IEventApplicationService` only.
2. **Never call Service Bus SDK from the controller.** Use `IMessagePublisher` abstraction (to be implemented in BE-01).
3. **All timestamps use `DateTime.UtcNow`.** Never `DateTime.Now`.
4. **All error responses use ProblemDetails.** Never custom error objects.
5. **Structured logging only.** Use named parameters: `_logger.LogInformation("Published {EventId}", id)`. Never string interpolation in log calls.
6. **camelCase for all JSON.** Controllers must be configured with `AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase)` or use the default (ASP.NET Core default is already camelCase).
7. **Shared models live in `ReenbitEventHub.Domain`.** Never duplicate entity/enum definitions across projects.
8. **`Event.Create()` factory is the only way to instantiate events.** Never use `new Event { ... }` directly.

---

## Key Patterns

### Event Generation Logic (BE-01)
```
ViewCar   → 1 event: { Type=PageView,  Description="Viewed {carId} {carName}" }
ReserveCar → 2 events:
              { Type=Click,    Description="Clicked reserve for {carId} {carName}" }
              { Type=Purchase, Description="Reserved {carId} {carName}" }
```
Description is auto-filled if not provided. carId must be `car-1` or `car-2` (validated via `CarCatalog`).

### POST Response Contract (202 Accepted)
```json
{ "publishedCount": 2, "eventIds": ["<guid>", "<guid>"] }
```

### GET Response Contract (200 OK)
```json
{ "items": [...], "totalCount": 42, "page": 1, "pageSize": 50 }
```

### Publisher Interface (IMessagePublisher — to be created)
```csharp
public interface IMessagePublisher
{
    Task<IReadOnlyList<Guid>> PublishEventsAsync(
        IReadOnlyList<EventMessage> events,
        CancellationToken cancellationToken = default);
}
```
Two implementations: `ServiceBusPublisher` (real Azure) and `InMemoryPublisher` (`Channel<EventMessage>`).
Registered conditionally in `Program.cs`: if `ServiceBus:ConnectionString` is present → use real bus; otherwise → in-memory channel + `EventProcessorService` (BackgroundService).

### Error Handling
- Validation failure → `ArgumentException` (caught by `UseExceptionHandler` in `Program.cs`) → 400 ProblemDetails
- Publisher unavailable → 503 ProblemDetails
- Unhandled exception → 500 ProblemDetails with `traceId`

### Logging Pattern
```
[API] Publishing {EventCount} events for user {UserId}
[API] Published event {EventId} type={EventType}
[Consumer] Received message {MessageId} | eventId={EventId} userId={UserId} type={EventType}
[Consumer] Persisted event {EventId} to database
[Consumer] Duplicate event {EventId} ignored (already exists)
```

---

## Configuration Keys

```json
{
  "ConnectionStrings": {
    "EventsDb": "Data Source=events.db"          // SQLite local fallback; replace with SQL Server connection string for Azure
  },
  "ServiceBus": {
    "ConnectionString": "",                        // Leave empty for local fallback mode
    "QueueName": "events"
  }
}
```

Dual-mode logic: if `ServiceBus:ConnectionString` is null/empty → register `InMemoryPublisher` + `EventProcessorService`; otherwise → register `ServiceBusPublisher`.

---

## Build & Run

```bash
# From solution root
dotnet build

# Run API (http://localhost:5113)
dotnet run --project CarRentalApi/CarRentalApi.csproj
```

Local mode requires zero Azure configuration. SQLite database (`events.db`) is created automatically on first run.

---

## What NOT to Do

- Do not add authentication or authorization (out of scope per PRD).
- Do not add real-time UI updates (manual refresh is acceptable).
- Do not duplicate models — always reference `ReenbitEventHub.Domain`.
- Do not edit files in `CarRentalApi/Application/`, `CarRentalApi/Contracts/`, or `CarRentalApi/Data/` — they are stale and excluded from compilation.
- Do not use `DateTime.Now` — always `DateTime.UtcNow`.
- Do not return non-ProblemDetails error shapes from any endpoint.
