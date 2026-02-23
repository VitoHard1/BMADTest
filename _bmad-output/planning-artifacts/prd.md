---
stepsCompleted: [step-01-init, step-02-discovery, step-02b-vision, step-02c-executive-summary, step-03-success, step-04-journeys, step-07-project-type, step-08-scoping, step-09-functional, step-10-nonfunctional, step-11-polish, step-12-complete]
inputDocuments:
  - product-brief-Reenbit-2026-02-20.md
workflowType: 'prd'
documentCounts:
  briefs: 1
  research: 0
  brainstorming: 0
  projectDocs: 0
---

# Product Requirements Document - Reenbit Event Hub

**Author:** Admin
**Date:** 2026-02-20

## Executive Summary

Reenbit Event Hub is a coding-test demonstration project that implements an end-to-end event-driven system using a car rental domain. An Angular SPA captures user interaction events (page views, clicks, purchases) for two hardcoded vehicles, publishes them through a .NET Web API to Azure Service Bus, and persists them via an Azure Function into a SQL database. Events are queryable through a filterable, sortable UI table.

The project demonstrates full-stack proficiency across Angular, .NET, Azure Service Bus, Azure Functions, and SQL — with emphasis on clean architecture, async messaging patterns, idempotent processing, structured logging, and proper error handling.

### What Makes This Special

- **Event-driven architecture** with real async message flow, not synchronous shortcuts
- **Dual-mode execution** — production-pattern Azure or zero-dependency local fallback
- **Observable pipeline** — structured logging at publish, consume, and persist stages
- **Idempotent processing** — safe for duplicate Service Bus delivery
- **Intentionally scoped** — no auth, payments, or deployment noise; focused signal for reviewers

### Project Classification

- **Type:** Full-stack web application (SPA + API + async processor)
- **Domain:** Car rental (demo theme)
- **Complexity:** Medium — multiple runtime components with async messaging
- **Context:** Greenfield coding test project

## Success Criteria

### User Success

- Code reviewer can clone the repo, run locally without an Azure account, and see the full event flow working end-to-end
- Demo end-user can view and reserve cars, then see the resulting events in a filterable table

### Technical Success

- Clean project structure with clear separation of concerns
- Working async pipeline: Angular -> API -> Service Bus -> Function -> DB
- Idempotent event processing (duplicate messages handled gracefully)
- Structured logging at every stage (publish, consume, persist)
- Consistent UTC timestamps across all layers
- ProblemDetails error responses for all failure modes
- All validation rules enforced with clear error messages

### Measurable Outcomes

- ViewCar action produces exactly 1 event (PageView)
- ReserveCar action produces exactly 2 events (Click + Purchase)
- Duplicate event delivery results in a logged no-op, not an error
- GET /api/events returns filtered, sorted results with correct defaults
- Application runs locally with zero cloud dependencies using fallback mode

## Product Scope

### MVP (This Delivery)

- Angular SPA with reactive form and events table
- .NET Web API with POST and GET endpoints
- Azure Service Bus integration (or in-memory fallback)
- Azure Function consumer (or in-process background consumer)
- SQL database persistence (Azure SQL or SQLite fallback)
- Structured logging throughout
- ProblemDetails error handling
- BMAD workflow artifacts

### Out of Scope

- Authentication / authorization
- Real payments, pricing logic, or availability engine
- Calendar or date-based booking
- Actual Azure deployment
- Real-time UI updates (manual refresh is acceptable)
- Multi-tenant or multi-user isolation

## User Journeys

### Journey 1: Code Reviewer Evaluates the Project

A hiring team member receives the repo link. They clone it, read the README, and run the application locally using the fallback mode (no Azure account needed). They open the Angular app, fill in a userId, select a car, and click "View Selected Car." They see a 202 response, wait a moment, hit refresh on the events table, and see a PageView event appear. They then click "Reserve Selected Car" and after refreshing see two new events (Click + Purchase). They inspect the console/logs and see structured entries for publish, consume, and persist. They browse the code, noting clean project structure, proper error handling, and consistent patterns. They check the BMAD artifacts folder and see planning discipline alongside technical execution.

### Journey 2: Demo End-User Interacts with Cars

A user opens the app and enters their userId. They select "car-1 Toyota Corolla" from the dropdown. They click "View Selected Car" — the form submits, the API returns 202 Accepted. They click the refresh button on the events table and see a PageView event with their userId, a description like "Viewed car-1 Toyota Corolla", and a UTC timestamp. They then select "car-2 VW Golf", click "Reserve Selected Car", and after refreshing see two new events: a Click event and a Purchase event, both tied to car-2. They use the Type filter to show only Purchase events. They sort by CreatedAt ascending to see chronological order.

### Journey Requirements Summary

- Reactive form with validation (userId required, car required, action required)
- Two distinct action buttons mapping to different event generation logic
- Events table with filtering (userId text, Type select) and sorting (CreatedAt)
- Manual refresh mechanism
- 202 Accepted responses with published message metadata
- Observable async pipeline through structured logs

## Web API + SPA Specific Requirements

### Technical Architecture

```
┌─────────────┐     ┌──────────────┐     ┌─────────────────┐     ┌────────────────┐     ┌──────────┐
│  Angular SPA │────▶│ .NET Web API │────▶│ Azure Service   │────▶│ Azure Function │────▶│ Database │
│  (Frontend)  │     │  (Backend)   │     │ Bus (Queue)     │     │ (Consumer)     │     │ (SQL)    │
└─────────────┘     └──────────────┘     └─────────────────┘     └────────────────┘     └──────────┘
                           │                                                                  │
                           └──────────────── GET /api/events ─────────────────────────────────┘
```

**Frontend:** Angular SPA (standalone components, reactive forms, HttpClient)

**Backend:** .NET Web API (minimal API or controllers, ProblemDetails, structured logging)

**Messaging:** Azure Service Bus Queue (or in-memory queue fallback)

**Consumer:** Azure Function with Service Bus trigger (or background service fallback)

**Database:** Azure SQL (or SQLite fallback) with EF Core

### Dual Runtime Modes

| Component | Option A: Azure | Option B: Local Fallback |
|-----------|----------------|--------------------------|
| Messaging | Azure Service Bus | In-memory queue |
| Database | Azure SQL | SQLite |
| Consumer | Azure Function | In-process background service |
| Config | Connection strings via user secrets / env vars | No external config needed |

### Implementation Considerations

- API and Angular run locally in both modes
- Mode selection driven by configuration (presence/absence of connection strings)
- README documents both options clearly
- Local fallback is the default path for reviewers

## Project Scoping & Phased Development

### MVP Strategy

**Approach:** Single-phase delivery — all features ship together as a cohesive coding test submission. No phased rollout.

**Resource Requirements:** Solo developer, full-stack.

### MVP Feature Set

**Core Capabilities:**
- Angular reactive form with car selection and action triggers
- POST /api/events endpoint with event generation logic
- GET /api/events endpoint with filtering, sorting, optional pagination
- Service Bus publish (with fallback)
- Function consumer with idempotent DB persistence
- Events table with filters and sorting
- Structured logging at all pipeline stages
- ProblemDetails error handling throughout

### Risk Mitigation Strategy

| Risk | Impact | Mitigation |
|------|--------|------------|
| Eventual consistency (UI reads lag behind writes) | User sees stale data | Manual refresh button; document the async nature |
| Duplicate Service Bus delivery | Duplicate events in DB | Unique constraint on Event.Id; duplicate insert treated as no-op with log |
| Service Bus unavailable | Events cannot be published | Return 503 ProblemDetails; log exception; fallback mode available |
| DB unavailable (GET) | Events cannot be queried | Return 500/503 ProblemDetails; log exception |
| DB unavailable (Function) | Events cannot be persisted | Let runtime retry; dead-letter after max delivery attempts |

## Functional Requirements

### Event Submission

- FR1: User can enter a userId (required, validated)
- FR2: User can select a car from a dropdown: car-1 Toyota Corolla, car-2 VW Golf (required)
- FR3: User can select an action: ViewCar or ReserveCar (required)
- FR4: User can optionally edit a pre-filled description field
- FR5: User can click "View Selected Car" to trigger a ViewCar action
- FR6: User can click "Reserve Selected Car" to trigger a ReserveCar action
- FR7: System generates 1 event (Type=PageView) when ViewCar action is submitted
- FR8: System generates 2 events (Type=Click + Type=Purchase) when ReserveCar action is submitted
- FR9: Each generated event receives a unique GUID as its Id
- FR10: Each generated event receives a CreatedAt timestamp in UTC

### Event Publishing

- FR11: API publishes each generated event as a separate message to Azure Service Bus Queue
- FR12: API returns 202 Accepted with count of published messages and their Ids
- FR13: If Service Bus publish fails, API returns 503 Service Unavailable with ProblemDetails
- FR14: API logs each successful publish with eventId, userId, and eventType

### Event Consumption

- FR15: Azure Function triggers on each Service Bus Queue message
- FR16: Function deserializes the JSON message into an Event DTO
- FR17: Function persists the event to the database via EF Core
- FR18: If the Event.Id already exists in the database, the function treats it as a successful no-op and logs "duplicate ignored"
- FR19: Function logs messageId, eventId, userId, and eventType on receive
- FR20: Function logs the DB insert result (success or duplicate detected)
- FR21: On DB failure, the function lets the Service Bus runtime retry (message returns to queue / dead-letters after max attempts)

### Event Querying

- FR22: User can query events via GET /api/events
- FR23: User can filter by userId (optional string, exact match)
- FR24: User can filter by type (optional enum: PageView, Click, Purchase)
- FR25: User can filter by date range using from and to parameters (optional, ISO 8601 UTC)
- FR26: User can sort results by createdAt_desc (default) or createdAt_asc
- FR27: Results are paginated with page (default 1) and pageSize (default 50, max 200) — optional
- FR28: Invalid or empty query parameters return 400 ProblemDetails with field-level errors
- FR29: API returns events as a JSON array with Id, UserId, Type, Description, CreatedAt fields

### Events Table (UI)

- FR30: User can view events in a table displaying Id, UserId, Type, Description, CreatedAt
- FR31: User can filter the table by UserId (text input)
- FR32: User can filter the table by Type (select dropdown: All, PageView, Click, Purchase)
- FR33: Table displays events sorted by CreatedAt descending by default
- FR34: User can manually refresh the table via a refresh button

### Error Handling

- FR35: All validation errors return 400 ProblemDetails with detailed field errors
- FR36: Unhandled exceptions return 500 ProblemDetails with a generic message and traceId (via global exception handler middleware)
- FR37: External dependency failures (Service Bus, DB) return 503 ProblemDetails with appropriate context

## Non-Functional Requirements

### Data Model

Single table: `Events`

| Column | Type | Constraints |
|--------|------|-------------|
| Id | uniqueidentifier (GUID) | PK |
| UserId | nvarchar(100) | NOT NULL |
| Type | nvarchar(20) | NOT NULL |
| Description | nvarchar(500) | NOT NULL |
| CreatedAt | datetime2 | NOT NULL, UTC |

**Indexes:**
- PK on Id (uniqueness + idempotency)
- IX_Events_CreatedAt (sort performance)
- IX_Events_UserId_CreatedAt (filter + sort)
- IX_Events_Type_CreatedAt (filter + sort)

### Performance

- API response time < 500ms for POST (excluding Service Bus latency)
- GET /api/events response time < 200ms for typical queries
- Event pipeline end-to-end (submit to DB persist): acceptable within seconds for demo purposes

### Logging & Observability

- Structured logging (e.g., Serilog or built-in ILogger with structured parameters)
- Log points: event publish, message receive, DB insert, duplicate detection, errors
- Each log entry includes: eventId, userId, eventType, timestamp
- TraceId propagated in ProblemDetails error responses

### Code Quality

- Clear project structure with logical separation (API, Function, Shared/Domain)
- Consistent naming conventions
- Minimal but meaningful comments where logic isn't self-evident
- No dead code or unused dependencies
- README with setup instructions for both runtime modes
