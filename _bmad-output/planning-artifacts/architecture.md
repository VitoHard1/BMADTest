---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
inputDocuments:
  - prd.md
  - product-brief-Reenbit-2026-02-20.md
workflowType: 'architecture'
project_name: 'Reenbit Event Hub'
user_name: 'Admin'
date: '2026-02-20'
lastStep: 8
status: 'complete'
completedAt: '2026-02-20'
---

# Architecture Decision Document

## Project Context Analysis

### Requirements Overview

**Functional Requirements:**
37 functional requirements across 6 capability areas: event submission (FR1-FR10), event publishing (FR11-FR14), event consumption (FR15-FR21), event querying (FR22-FR29), events table UI (FR30-FR34), and error handling (FR35-FR37). The architecture must support an async event pipeline where the API is the producer and a separate function is the consumer.

**Non-Functional Requirements:**
- POST response < 500ms (excluding Service Bus latency)
- GET response < 200ms for typical queries
- Structured logging at all pipeline stages
- ProblemDetails error responses throughout
- Idempotent event processing via unique constraint

**Scale & Complexity:**

- Primary domain: Full-stack web application with async messaging
- Complexity level: Medium — 4 runtime components with event-driven integration
- Architectural components: 5 (Angular SPA, .NET API, Service Bus Queue, Azure Function, SQL Database)

### Technical Constraints & Dependencies

- Angular SPA (latest stable)
- .NET 8+ Web API
- Azure Service Bus (Queue mode, single consumer)
- Azure Functions (.NET isolated worker)
- EF Core with Azure SQL (or SQLite fallback)
- Must run locally without Azure account (fallback mode)
- No authentication/authorization required
- No real deployment — local development only

### Cross-Cutting Concerns Identified

- **Structured logging:** Consistent across API, Function, and DB access layers
- **Error handling:** ProblemDetails pattern in API; retry/dead-letter in Function
- **UTC timestamps:** All datetime values stored and transmitted as UTC
- **Idempotency:** Event.Id uniqueness enforced at DB level
- **Configuration:** Dual-mode (Azure vs local) driven by connection string presence
- **Serialization:** JSON with camelCase property naming throughout

## Technology Stack

### Frontend

| Technology | Version | Purpose |
|-----------|---------|---------|
| Angular | 19.x | SPA framework |
| TypeScript | 5.x | Language |
| Angular Reactive Forms | (bundled) | Form handling with validation |
| Angular HttpClient | (bundled) | API communication |
| Angular Material or plain HTML/CSS | latest | UI components (table, form controls) |

### Backend API

| Technology | Version | Purpose |
|-----------|---------|---------|
| .NET | 8.0 | Runtime |
| ASP.NET Core Web API | 8.0 | HTTP API framework |
| Azure.Messaging.ServiceBus | 7.x | Service Bus client |
| Microsoft.EntityFrameworkCore | 8.x | ORM for GET queries |
| Microsoft.EntityFrameworkCore.SqlServer | 8.x | Azure SQL provider |
| Microsoft.EntityFrameworkCore.Sqlite | 8.x | Local fallback provider |
| Swashbuckle / Scalar | latest | API documentation |

### Event Processor

| Technology | Version | Purpose |
|-----------|---------|---------|
| Azure Functions (.NET isolated) | 4.x | Serverless host |
| Microsoft.Azure.Functions.Worker | latest | Isolated worker model |
| Microsoft.Azure.Functions.Worker.Extensions.ServiceBus | latest | Service Bus trigger binding |
| Microsoft.EntityFrameworkCore | 8.x | ORM for event persistence |

### Infrastructure

| Component | Azure Mode | Local Fallback |
|-----------|-----------|----------------|
| Message Queue | Azure Service Bus Queue | In-memory channel (Channel\<T\>) |
| Database | Azure SQL | SQLite file |
| Event Consumer | Azure Function (separate process) | BackgroundService (in-process with API) |

## Core Architectural Decisions

### Decision 1: Queue over Topic

**Decision:** Use Azure Service Bus **Queue** (not Topic/Subscription).

**Rationale:** Single consumer pattern — only one function processes events. Queue is simpler, cheaper, and sufficient for the demo. No fan-out or multiple subscriber scenarios exist.

### Decision 2: Isolated Worker Azure Function

**Decision:** Use .NET isolated worker model for Azure Functions.

**Rationale:** Isolated worker is the recommended model going forward. It provides better dependency injection, middleware support, and decouples from the Functions host. Aligns with production best practices.

### Decision 3: EF Core for Data Access

**Decision:** Use Entity Framework Core for both the API (read) and Function (write).

**Rationale:** Shared DbContext configuration across projects. EF Core handles provider switching (SQL Server vs SQLite) cleanly via configuration. Code-first migrations with a single `Events` table keep things simple.

### Decision 4: Dual-Mode Runtime via Configuration

**Decision:** Runtime mode (Azure vs Local) determined by presence of connection strings in configuration.

**Rationale:** No code changes needed to switch modes. If `ServiceBus:ConnectionString` is present, use real Service Bus; otherwise, fall back to in-memory Channel\<T\>. If `ConnectionStrings:DefaultConnection` points to SQL Server, use it; otherwise, use SQLite. This is implemented via conditional DI registration at startup.

### Decision 5: In-Memory Queue via Channel\<T\>

**Decision:** Use `System.Threading.Channels.Channel<T>` as the local fallback for Service Bus.

**Rationale:** Channel\<T\> provides a thread-safe, async-native producer-consumer pattern built into .NET. No external dependencies. The API writes to the channel, a BackgroundService reads from it — mimicking the Service Bus -> Function flow locally.

### Decision 6: BackgroundService as Local Function Replacement

**Decision:** When running locally without Azure, replace the Azure Function with an ASP.NET Core `BackgroundService` hosted inside the API process.

**Rationale:** The BackgroundService reads from Channel\<T\> and persists to the DB using the same EF Core logic as the real Function. This keeps the local experience single-process while preserving the async pattern.

### Decision 7: Shared Domain Project

**Decision:** Extract shared models (Event entity, DTOs, enums) into a shared class library referenced by both API and Function.

**Rationale:** Prevents model drift between producer and consumer. The Event entity, EventType enum, and serialization contracts are defined once and shared.

### Decision 8: API Returns 202 Accepted

**Decision:** POST /api/events returns 202 Accepted (not 201 Created).

**Rationale:** The API publishes messages but does not directly persist events. The response acknowledges that messages were accepted for processing. The response body includes the count and IDs of published messages.

### Decision Impact Analysis

**Implementation Sequence:**
1. Shared domain project (models, DTOs, enums)
2. Database context and migrations
3. API project with POST/GET endpoints
4. Service Bus publisher (with Channel\<T\> fallback)
5. Azure Function project (with BackgroundService fallback)
6. Angular SPA
7. Integration wiring and logging

**Cross-Component Dependencies:**
- Shared project is referenced by both API and Function
- DbContext configuration is shared (provider switching)
- Message contract (Event DTO JSON) must match between publisher and consumer
- Configuration keys must be consistent across projects

## Implementation Patterns & Consistency Rules

### Naming Patterns

**Solution & Project Naming:**
```
ReenbitEventHub.sln
├── src/
│   ├── ReenbitEventHub.Api/              — Web API project
│   ├── ReenbitEventHub.Processor/        — Azure Function project
│   └── ReenbitEventHub.Domain/           — Shared models & contracts
├── src/car-rental-portal/                — Angular SPA
```

**C# Naming Conventions:**
- Classes: `PascalCase` (e.g., `EventController`, `EventService`)
- Interfaces: `IPascalCase` (e.g., `IMessagePublisher`)
- Methods: `PascalCase` (e.g., `PublishEventsAsync`)
- Properties: `PascalCase` (e.g., `UserId`, `CreatedAt`)
- Private fields: `_camelCase` (e.g., `_logger`, `_publisher`)
- Constants: `PascalCase` (e.g., `MaxPageSize`)
- Async methods: suffix with `Async`

**Angular/TypeScript Naming Conventions:**
- Components: `kebab-case` files, `PascalCase` class (e.g., `event-form.component.ts` / `EventFormComponent`)
- Services: `kebab-case` files (e.g., `event.service.ts` / `EventService`)
- Interfaces/Models: `PascalCase` (e.g., `EventDto`, `EventType`)
- Variables/functions: `camelCase`

**Database Naming:**
- Table: `Events` (PascalCase, plural)
- Columns: `PascalCase` (e.g., `UserId`, `CreatedAt`)
- Indexes: `IX_Events_ColumnName` pattern

**API Naming:**
- Endpoints: lowercase plural nouns (`/api/events`)
- Query parameters: `camelCase` (`userId`, `pageSize`, `createdAt_desc`)
- JSON properties: `camelCase` (e.g., `{ "userId": "...", "createdAt": "..." }`)

### Structure Patterns

**Backend Project Organization:**
```
ReenbitEventHub.Api/
├── Controllers/
│   └── EventsController.cs
├── Services/
│   ├── IMessagePublisher.cs
│   ├── ServiceBusPublisher.cs
│   └── InMemoryPublisher.cs
├── BackgroundServices/
│   └── EventProcessorService.cs        — local fallback consumer
├── Middleware/
│   └── GlobalExceptionMiddleware.cs
├── Models/
│   ├── Requests/
│   │   └── CreateEventRequest.cs
│   └── Responses/
│       └── CreateEventResponse.cs
├── Validators/
│   └── CreateEventRequestValidator.cs
├── Data/
│   ├── EventDbContext.cs
│   └── Migrations/
├── Program.cs
└── appsettings.json
```

**Function Project Organization:**
```
ReenbitEventHub.Processor/
├── Functions/
│   └── EventProcessorFunction.cs
├── Services/
│   └── EventPersistenceService.cs
├── Program.cs
├── host.json
└── local.settings.json
```

**Shared Domain Project:**
```
ReenbitEventHub.Domain/
├── Entities/
│   └── Event.cs
├── Enums/
│   └── EventType.cs
├── DTOs/
│   └── EventMessage.cs
└── Constants/
    └── CarCatalog.cs
```

**Angular Project Organization:**
```
car-rental-portal/
├── src/
│   ├── app/
│   │   ├── components/
│   │   │   ├── event-form/
│   │   │   │   ├── event-form.component.ts
│   │   │   │   ├── event-form.component.html
│   │   │   │   └── event-form.component.css
│   │   │   └── events-table/
│   │   │       ├── events-table.component.ts
│   │   │       ├── events-table.component.html
│   │   │       └── events-table.component.css
│   │   ├── models/
│   │   │   ├── event.model.ts
│   │   │   └── create-event-request.model.ts
│   │   ├── services/
│   │   │   └── event.service.ts
│   │   ├── app.component.ts
│   │   ├── app.component.html
│   │   └── app.config.ts
│   ├── environments/
│   │   ├── environment.ts
│   │   └── environment.development.ts
│   └── index.html
├── angular.json
├── package.json
└── tsconfig.json
```

### Format Patterns

**API Request Format (POST /api/events):**
```json
{
  "userId": "user-123",
  "action": "ViewCar",
  "carId": "car-1",
  "description": "Viewed car-1 Toyota Corolla"
}
```

**API Response Format (202 Accepted):**
```json
{
  "publishedCount": 1,
  "eventIds": ["3fa85f64-5717-4562-b3fc-2c963f66afa6"]
}
```

**API Response Format (GET /api/events):**
```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "userId": "user-123",
      "type": "PageView",
      "description": "Viewed car-1 Toyota Corolla",
      "createdAt": "2026-02-20T14:30:00.000Z"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 50
}
```

**ProblemDetails Error Format:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation Error",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "traceId": "00-abc123-def456-00",
  "errors": {
    "userId": ["UserId is required."],
    "carId": ["CarId must be 'car-1' or 'car-2'."]
  }
}
```

**Service Bus Message Format (JSON body):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "user-123",
  "type": "PageView",
  "description": "Viewed car-1 Toyota Corolla",
  "createdAt": "2026-02-20T14:30:00.000Z"
}
```

**Date/Time Format:** ISO 8601 UTC with `Z` suffix everywhere. All `CreatedAt` values generated server-side via `DateTime.UtcNow`.

### Communication Patterns

**Event Generation Logic (API-side):**
```
ViewCar action → 1 event:
  - { type: "PageView", description: "Viewed {carId} {carName}" }

ReserveCar action → 2 events:
  - { type: "Click", description: "Clicked reserve for {carId} {carName}" }
  - { type: "Purchase", description: "Reserved {carId} {carName}" }
```

Each event gets a new `Guid.NewGuid()` for Id and `DateTime.UtcNow` for CreatedAt.

**Publisher Interface:**
```csharp
public interface IMessagePublisher
{
    Task<IReadOnlyList<Guid>> PublishEventsAsync(
        IReadOnlyList<EventMessage> events,
        CancellationToken cancellationToken = default);
}
```

Two implementations registered via DI:
- `ServiceBusPublisher` — sends to real Azure Service Bus Queue
- `InMemoryPublisher` — writes to `Channel<EventMessage>`

### Process Patterns

**Error Handling — API:**
1. Validation errors → 400 ProblemDetails with field-level `errors` dictionary
2. Service Bus publish failure → 503 ProblemDetails, log exception with structured data
3. DB query failure (GET) → 500/503 ProblemDetails, log exception
4. Unhandled exceptions → Global exception middleware → 500 ProblemDetails with traceId, generic message

**Error Handling — Function:**
1. Deserialization failure → log error, let message dead-letter
2. Duplicate Event.Id → catch `DbUpdateException` with unique constraint violation → log "duplicate ignored", treat as success
3. DB failure → let exception propagate → Service Bus runtime retries (up to max delivery count) → dead-letter

**Logging Pattern:**
```
[API]      Publishing {EventCount} events for user {UserId} | eventIds: [{EventIds}]
[API]      Published event {EventId} type={EventType} to Service Bus
[Function] Received message {MessageId} | eventId={EventId} userId={UserId} type={EventType}
[Function] Persisted event {EventId} to database
[Function] Duplicate event {EventId} ignored (already exists)
[Function] Error persisting event {EventId}: {Error}
```

### Enforcement Guidelines

**All AI Agents MUST:**
- Use the `IMessagePublisher` interface, never call Service Bus directly from controllers
- Use `DateTime.UtcNow` for all timestamps, never `DateTime.Now`
- Return ProblemDetails for all error responses, never custom error objects
- Use structured logging with named parameters `{EventId}`, never string interpolation
- Use `camelCase` for all JSON serialization
- Place all shared models in `ReenbitEventHub.Domain`, never duplicate across projects

## Project Structure & Boundaries

### Complete Project Directory Structure

```
ReenbitEventHub/
├── _bmad-output/                         — BMAD workflow artifacts
│   ├── planning-artifacts/
│   │   ├── product-brief-Reenbit-2026-02-20.md
│   │   ├── prd.md
│   │   └── architecture.md
│   └── implementation-artifacts/
├── src/
│   ├── ReenbitEventHub.Api/
│   │   ├── Controllers/
│   │   │   └── EventsController.cs
│   │   ├── Services/
│   │   │   ├── IMessagePublisher.cs
│   │   │   ├── ServiceBusPublisher.cs
│   │   │   └── InMemoryPublisher.cs
│   │   ├── BackgroundServices/
│   │   │   └── EventProcessorService.cs
│   │   ├── Middleware/
│   │   │   └── GlobalExceptionMiddleware.cs
│   │   ├── Models/
│   │   │   ├── Requests/
│   │   │   │   └── CreateEventRequest.cs
│   │   │   └── Responses/
│   │   │       └── CreateEventResponse.cs
│   │   ├── Validators/
│   │   │   └── CreateEventRequestValidator.cs
│   │   ├── Data/
│   │   │   ├── EventDbContext.cs
│   │   │   └── Migrations/
│   │   ├── Extensions/
│   │   │   └── ServiceCollectionExtensions.cs
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── appsettings.Development.json
│   ├── ReenbitEventHub.Processor/
│   │   ├── Functions/
│   │   │   └── EventProcessorFunction.cs
│   │   ├── Services/
│   │   │   └── EventPersistenceService.cs
│   │   ├── Program.cs
│   │   ├── host.json
│   │   └── local.settings.json
│   ├── ReenbitEventHub.Domain/
│   │   ├── Entities/
│   │   │   └── Event.cs
│   │   ├── Enums/
│   │   │   └── EventType.cs
│   │   ├── DTOs/
│   │   │   └── EventMessage.cs
│   │   └── Constants/
│   │       └── CarCatalog.cs
│   └── car-rental-portal/
│       ├── src/
│       │   ├── app/
│       │   │   ├── components/
│       │   │   │   ├── event-form/
│       │   │   │   └── events-table/
│       │   │   ├── models/
│       │   │   ├── services/
│       │   │   ├── app.component.ts
│       │   │   ├── app.component.html
│       │   │   └── app.config.ts
│       │   ├── environments/
│       │   └── index.html
│       ├── angular.json
│       ├── package.json
│       └── tsconfig.json
├── ReenbitEventHub.sln
├── .gitignore
└── README.md
```

### Architectural Boundaries

**API Boundaries:**
- `EventsController` is the only HTTP entry point
- Controller delegates to `IMessagePublisher` for publishing and `EventDbContext` for queries
- No direct Service Bus SDK calls outside of `ServiceBusPublisher`
- No direct DB calls outside of controller (GET queries) and `EventPersistenceService` (writes)

**Data Boundaries:**
- `EventDbContext` is the sole data access mechanism
- Entity configuration (indexes, constraints) defined via Fluent API in `OnModelCreating`
- Provider switching (SQL Server vs SQLite) happens at DI registration, not in business logic
- EF Core migrations generated against SQL Server, SQLite uses `EnsureCreated()` for simplicity

**Message Boundaries:**
- `IMessagePublisher` abstracts all messaging — controller never knows if it's Service Bus or Channel\<T\>
- Message payload is always `EventMessage` DTO serialized as JSON
- Consumer (Function or BackgroundService) receives the same JSON contract

### Data Flow

```
[User Action in Angular]
       │
       ▼
POST /api/events { userId, action, carId, description? }
       │
       ▼
[EventsController] → validate request → generate Event(s) based on action
       │
       ▼
[IMessagePublisher.PublishEventsAsync(events)]
       │
       ├── Azure Mode: ServiceBusPublisher → Service Bus Queue
       │                                          │
       │                                          ▼
       │                              [EventProcessorFunction]
       │                                          │
       └── Local Mode: InMemoryPublisher → Channel<T>
                                               │
                                               ▼
                                   [EventProcessorService (BackgroundService)]
                                               │
                                               ▼
                              [EventPersistenceService.PersistAsync(event)]
                                               │
                                               ▼
                                    [EventDbContext → Events table]
                                               │
                                               ▼
                              GET /api/events → [EventsController] → query DB → return JSON
                                               │
                                               ▼
                                    [Angular events-table component]
```

## Architecture Validation Results

### Coherence Validation

**Decision Compatibility:** All decisions are mutually supportive. Queue + isolated Function + EF Core + shared Domain project form a clean, consistent stack. The dual-mode fallback (Channel\<T\> + BackgroundService + SQLite) mirrors the Azure mode without introducing conflicting patterns.

**Pattern Consistency:** Naming conventions, error handling patterns, and logging format are consistent across API and Function. The shared Domain project prevents model drift.

**Structure Alignment:** Project structure directly supports the architectural boundaries — each concern has a clear home.

### Requirements Coverage Validation

**Functional Requirements Coverage:**
- FR1-FR10 (Event Submission): Covered by Angular reactive form + EventsController + event generation logic
- FR11-FR14 (Event Publishing): Covered by IMessagePublisher abstraction + ServiceBusPublisher/InMemoryPublisher
- FR15-FR21 (Event Consumption): Covered by EventProcessorFunction + EventPersistenceService + idempotency via unique constraint
- FR22-FR29 (Event Querying): Covered by EventsController GET endpoint + EF Core queries with filtering/sorting/pagination
- FR30-FR34 (Events Table UI): Covered by Angular events-table component
- FR35-FR37 (Error Handling): Covered by GlobalExceptionMiddleware + ProblemDetails patterns

**Non-Functional Requirements Coverage:**
- Data model with indexes: defined in EF Core Fluent API
- Performance targets: achievable with direct DB queries and async messaging
- Structured logging: pattern defined, consistent across all components
- Code quality: enforced by project structure and naming conventions

### Architecture Completeness Checklist

- [x] Project context thoroughly analyzed
- [x] Scale and complexity assessed
- [x] Technical constraints identified
- [x] Cross-cutting concerns mapped
- [x] Technology stack fully specified with versions
- [x] Critical decisions documented with rationale
- [x] Integration patterns defined (IMessagePublisher abstraction)
- [x] Performance considerations addressed
- [x] Naming conventions established (C#, Angular, DB, API, JSON)
- [x] Structure patterns defined (all 4 projects)
- [x] Communication patterns specified (event generation, message format)
- [x] Process patterns documented (error handling, logging)
- [x] Complete directory structure defined
- [x] Component boundaries established
- [x] Data flow mapped end-to-end
- [x] Requirements to structure mapping complete

### Architecture Readiness Assessment

**Overall Status:** READY FOR IMPLEMENTATION

**Confidence Level:** High

**Key Strengths:**
- Clean separation via shared Domain project
- IMessagePublisher abstraction enables seamless Azure/local switching
- Idempotency strategy is simple and proven (DB unique constraint)
- Project structure is clear and predictable for reviewers

**First Implementation Priority:**
1. Create solution structure and all projects
2. Implement Domain project (entities, enums, DTOs)
3. Implement API with POST/GET endpoints and dual-mode DI
4. Implement Function/BackgroundService consumer
5. Implement Angular SPA
