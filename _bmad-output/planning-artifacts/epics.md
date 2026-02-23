---
stepsCompleted: [step-01-validate-prerequisites, step-02-design-epics, step-03-create-stories, step-04-complete]
inputDocuments:
  - prd.md
  - architecture.md
---

# Reenbit Event Hub - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Reenbit Event Hub, decomposing the PRD and Architecture into 6 implementable stories across 4 epics.

## Requirements Inventory

### Functional Requirements

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
- FR11: API publishes each generated event as a separate message to Azure Service Bus Queue
- FR12: API returns 202 Accepted with count of published messages and their Ids
- FR13: If Service Bus publish fails, API returns 503 Service Unavailable with ProblemDetails
- FR14: API logs each successful publish with eventId, userId, and eventType
- FR15: Azure Function triggers on each Service Bus Queue message
- FR16: Function deserializes the JSON message into an Event DTO
- FR17: Function persists the event to the database via EF Core
- FR18: If the Event.Id already exists in the database, the function treats it as a successful no-op and logs "duplicate ignored"
- FR19: Function logs messageId, eventId, userId, and eventType on receive
- FR20: Function logs the DB insert result (success or duplicate detected)
- FR21: On DB failure, the function lets the Service Bus runtime retry
- FR22: User can query events via GET /api/events
- FR23: User can filter by userId (optional string, exact match)
- FR24: User can filter by type (optional enum: PageView, Click, Purchase)
- FR25: User can filter by date range using from and to parameters (optional, ISO 8601 UTC)
- FR26: User can sort results by createdAt_desc (default) or createdAt_asc
- FR27: Results are paginated with page (default 1) and pageSize (default 50, max 200)
- FR28: Invalid or empty query parameters return 400 ProblemDetails with field-level errors
- FR29: API returns events as a JSON array with Id, UserId, Type, Description, CreatedAt fields
- FR30: User can view events in a table displaying Id, UserId, Type, Description, CreatedAt
- FR31: User can filter the table by UserId (text input)
- FR32: User can filter the table by Type (select dropdown: All, PageView, Click, Purchase)
- FR33: Table displays events sorted by CreatedAt descending by default
- FR34: User can manually refresh the table via a refresh button
- FR35: All validation errors return 400 ProblemDetails with detailed field errors
- FR36: Unhandled exceptions return 500 ProblemDetails with a generic message and traceId
- FR37: External dependency failures return 503 ProblemDetails

### NonFunctional Requirements

- NFR1: Single Events table with PK on Id (GUID), columns: UserId nvarchar(100), Type nvarchar(20), Description nvarchar(500), CreatedAt datetime2 — all NOT NULL
- NFR2: Indexes: IX_Events_CreatedAt, IX_Events_UserId_CreatedAt, IX_Events_Type_CreatedAt
- NFR3: POST response < 500ms (excluding Service Bus latency)
- NFR4: GET response < 200ms for typical queries
- NFR5: Structured logging at publish, consume, persist, and error stages
- NFR6: Consistent UTC timestamps across all layers
- NFR7: Clear project structure: API, Processor, Domain (shared), Angular SPA

### Additional Requirements

- Architecture specifies IMessagePublisher abstraction with ServiceBusPublisher and InMemoryPublisher implementations
- Dual-mode runtime: Azure (Service Bus + SQL Server) vs Local (Channel<T> + SQLite + BackgroundService)
- Mode selection driven by configuration (presence of connection strings)
- Shared ReenbitEventHub.Domain project for entities, enums, DTOs
- BackgroundService replaces Azure Function in local mode
- JSON serialization with camelCase throughout
- ProblemDetails for all error responses
- Global exception middleware

### FR Coverage Map

| FR | Story |
|----|-------|
| FR1-FR6 | FE-01 |
| FR7-FR10 | BE-01 |
| FR11-FR14 | BE-01 |
| FR15-FR21 | FN-01 |
| FR22-FR29 | BE-02 |
| FR30-FR34 | FE-02 |
| FR35-FR37 | Q-01 |
| NFR1-NFR2 | FN-01 |
| NFR3-NFR4 | BE-01, BE-02 |
| NFR5-NFR6 | Q-01 |
| NFR7 | BE-01 (project setup) |

## Epic List

| Epic | Title | Stories |
|------|-------|---------|
| Epic 1 | Backend API & Messaging | BE-01, BE-02 |
| Epic 2 | Event Processing | FN-01 |
| Epic 3 | Frontend SPA | FE-01, FE-02 |
| Epic 4 | Quality & Polish | Q-01 |

---

## Epic 1: Backend API & Messaging

Build the .NET Web API with event generation, Service Bus publishing, and query endpoints. Includes solution structure, shared Domain project, EF Core setup, and dual-mode DI registration.

### Story BE-01: POST /api/events — Publish Generated Events

**Goal:** Implement the POST endpoint that accepts user actions, generates the correct events, and publishes them to Service Bus (or in-memory fallback).

**Acceptance Criteria:**

**Given** a valid request with userId="user-1", action="ViewCar", carId="car-1"
**When** POST /api/events is called
**Then** the API generates 1 event with Type=PageView
**And** publishes it to the message queue
**And** returns 202 Accepted with `{ "publishedCount": 1, "eventIds": ["<guid>"] }`

**Given** a valid request with userId="user-1", action="ReserveCar", carId="car-2"
**When** POST /api/events is called
**Then** the API generates 2 events: Type=Click and Type=Purchase
**And** publishes both to the message queue
**And** returns 202 Accepted with `{ "publishedCount": 2, "eventIds": ["<guid>", "<guid>"] }`

**Given** a request with missing userId
**When** POST /api/events is called
**Then** the API returns 400 ProblemDetails with field error for userId

**Given** a request with invalid carId (not car-1 or car-2)
**When** POST /api/events is called
**Then** the API returns 400 ProblemDetails with field error for carId

**Given** the message queue is unavailable
**When** POST /api/events is called with a valid request
**Then** the API returns 503 ProblemDetails
**And** logs the exception with structured data

**Implementation Notes:**
- Create solution structure: ReenbitEventHub.sln with Api, Domain, Processor projects
- Domain project: Event entity, EventType enum, EventMessage DTO, CarCatalog constants
- EF Core: EventDbContext with Fluent API config (indexes, constraints), provider switching in Program.cs
- IMessagePublisher interface in Api/Services with two implementations:
  - ServiceBusPublisher — sends JSON to Azure Service Bus Queue
  - InMemoryPublisher — writes to Channel<EventMessage>
- Conditional DI registration in Program.cs: check for ServiceBus connection string presence
- EventsController with POST endpoint, request validation (DataAnnotations or FluentValidation)
- CreateEventRequest model: userId (required), action (required, enum), carId (required, enum), description (optional)
- CreateEventResponse model: publishedCount, eventIds
- Event generation logic: ViewCar → 1 PageView; ReserveCar → Click + Purchase
- Each event gets Guid.NewGuid() for Id, DateTime.UtcNow for CreatedAt
- Auto-fill description if not provided: "Viewed car-1 Toyota Corolla" / "Clicked reserve for car-2 VW Golf" / "Reserved car-2 VW Golf"
- Structured log: "Publishing {EventCount} events for user {UserId}"
- Structured log per event: "Published event {EventId} type={EventType}"

**Test Notes:**
- Verify ViewCar generates exactly 1 PageView event
- Verify ReserveCar generates exactly 2 events (Click + Purchase)
- Verify 400 for missing/invalid fields (userId, carId, action)
- Verify 202 response shape matches contract
- Verify each event has unique GUID and UTC timestamp
- Verify structured logs are emitted on publish

---

### Story BE-02: GET /api/events — Query Events from Database

**Goal:** Implement the GET endpoint that returns filtered, sorted, paginated events from the database.

**Acceptance Criteria:**

**Given** events exist in the database
**When** GET /api/events is called with no filters
**Then** the API returns events sorted by CreatedAt descending
**And** page=1, pageSize=50 by default

**Given** events exist for userId="user-1" and userId="user-2"
**When** GET /api/events?userId=user-1 is called
**Then** the API returns only events where UserId="user-1"

**Given** events exist with types PageView, Click, Purchase
**When** GET /api/events?type=Purchase is called
**Then** the API returns only events where Type="Purchase"

**Given** events exist across a date range
**When** GET /api/events?from=2026-02-20T00:00:00Z&to=2026-02-20T23:59:59Z is called
**Then** the API returns only events within that UTC range

**Given** the sort parameter is set to createdAt_asc
**When** GET /api/events?sort=createdAt_asc is called
**Then** the API returns events sorted by CreatedAt ascending

**Given** an invalid type value is provided
**When** GET /api/events?type=InvalidType is called
**Then** the API returns 400 ProblemDetails with field error

**Implementation Notes:**
- Add GET endpoint to EventsController
- Query parameters: userId (string?), type (EventType?), from (DateTime?), to (DateTime?), sort (string? default "createdAt_desc"), page (int? default 1), pageSize (int? default 50, max 200)
- Build IQueryable with conditional .Where() clauses for each filter
- Apply .OrderBy() or .OrderByDescending() based on sort param
- Apply .Skip() / .Take() for pagination
- Return response with items array, totalCount, page, pageSize
- Validate sort value is "createdAt_desc" or "createdAt_asc", else 400
- Validate type value against EventType enum, else 400
- Validate pageSize <= 200, else 400

**Test Notes:**
- Verify default sort is CreatedAt descending
- Verify each filter works independently (userId, type, from, to)
- Verify filters combine correctly
- Verify pagination (page, pageSize, totalCount)
- Verify 400 for invalid type and invalid sort values
- Verify response shape matches contract

---

## Epic 2: Event Processing

Implement the async consumer that reads messages from the queue and persists events to the database with idempotency.

### Story FN-01: Service Bus Trigger Function — Persist Events Idempotently

**Goal:** Implement the Azure Function (and local BackgroundService fallback) that consumes queue messages, deserializes events, and persists them to the database. Duplicate Event.Id inserts are treated as no-op with a log entry.

**Acceptance Criteria:**

**Given** a valid event message is in the queue
**When** the function triggers
**Then** it deserializes the JSON into an EventMessage DTO
**And** persists a new Event row to the Events table
**And** logs: messageId, eventId, userId, eventType on receive
**And** logs: "Persisted event {EventId} to database"

**Given** an event with the same Id already exists in the database
**When** the function processes a duplicate message
**Then** it catches the unique constraint violation
**And** treats it as success (no exception thrown)
**And** logs: "Duplicate event {EventId} ignored (already exists)"

**Given** the database is unavailable
**When** the function processes a message
**Then** it lets the exception propagate
**And** the Service Bus runtime retries the message (or BackgroundService retries)
**And** after max delivery attempts the message is dead-lettered (Service Bus mode)

**Implementation Notes:**
- Processor project: EventProcessorFunction with ServiceBusTrigger attribute binding to queue
- EventPersistenceService: receives EventMessage, maps to Event entity, calls DbContext.SaveChangesAsync()
- Idempotency: catch DbUpdateException, check for unique constraint violation on Events.Id → log and return
- Program.cs: register EF Core DbContext with same provider-switching logic as API
- BackgroundService (in Api project): EventProcessorService reads from Channel<EventMessage>, calls same EventPersistenceService
- host.json: configure maxConcurrentCalls=1 for simplicity
- local.settings.json: placeholder for ServiceBus and DB connection strings
- Structured logs at receive, persist success, duplicate, and error

**Test Notes:**
- Verify event is persisted with correct Id, UserId, Type, Description, CreatedAt
- Verify duplicate insert is handled gracefully (no exception, logged)
- Verify all structured log entries are emitted
- Verify DB failure propagates (message will be retried)
- End-to-end: POST → queue → function → DB → GET returns the event

---

## Epic 3: Frontend SPA

Build the Angular application with event submission form and events display table.

### Story FE-01: Angular Reactive Form for Event Submission

**Goal:** Build the event submission form with userId, car selection, action selection, and description fields. Two buttons trigger ViewCar and ReserveCar actions via POST /api/events.

**Acceptance Criteria:**

**Given** the user opens the Angular app
**When** the form loads
**Then** it displays fields: userId (text), car (select: car-1 Toyota Corolla, car-2 VW Golf), description (text, auto-filled)
**And** two buttons: "View Selected Car" and "Reserve Selected Car"

**Given** the user fills in userId="user-1", selects car-1
**When** they click "View Selected Car"
**Then** the app sends POST /api/events with action=ViewCar, carId=car-1
**And** shows a success message with the response (published count and event IDs)

**Given** the user fills in userId="user-1", selects car-2
**When** they click "Reserve Selected Car"
**Then** the app sends POST /api/events with action=ReserveCar, carId=car-2
**And** shows a success message with the response

**Given** the userId field is empty
**When** the user clicks either button
**Then** the form shows a validation error and does not submit

**Given** the API returns an error (400, 503)
**When** the response is received
**Then** the form displays the error message to the user

**Implementation Notes:**
- Angular 19 with standalone components
- EventFormComponent with ReactiveFormsModule
- FormGroup: userId (required), carId (required), description (optional, auto-filled on car/action change)
- Auto-fill description: "Viewing car-1 Toyota Corolla" or "Reserving car-2 VW Golf" — editable by user
- EventService: HttpClient POST to /api/events with CreateEventRequest body
- Show success/error feedback (simple div or snackbar)
- Configure API base URL in environment.ts
- CORS: API must allow Angular dev server origin (http://localhost:4200)

**Test Notes:**
- Verify form validation prevents submission without userId
- Verify correct request payload for ViewCar vs ReserveCar
- Verify description auto-fills based on car selection
- Verify success and error messages display correctly
- Verify API call uses correct endpoint and method

---

### Story FE-02: Angular Events Table with Filters and Refresh

**Goal:** Build the events table component displaying Id, UserId, Type, Description, CreatedAt with filtering by UserId (text) and Type (select), and a manual refresh button.

**Acceptance Criteria:**

**Given** events exist in the database
**When** the events table loads
**Then** it calls GET /api/events and displays results in a table with columns: Id, UserId, Type, Description, CreatedAt
**And** events are sorted by CreatedAt descending by default

**Given** the user enters "user-1" in the UserId filter
**When** the filter is applied
**Then** the table shows only events where UserId="user-1"

**Given** the user selects "Purchase" from the Type filter dropdown
**When** the filter is applied
**Then** the table shows only events where Type="Purchase"

**Given** the user selects "All" from the Type filter dropdown
**When** the filter is applied
**Then** the table shows events of all types

**Given** the user clicks the refresh button
**When** the refresh occurs
**Then** the table reloads data from GET /api/events with current filters applied

**Implementation Notes:**
- EventsTableComponent with standalone component
- EventService: HttpClient GET to /api/events with query params
- Filter inputs: userId text input, type select dropdown (All, PageView, Click, Purchase)
- Refresh button calls the service method again with current filter values
- Display CreatedAt in a readable format (e.g., locale string or ISO)
- Table can be plain HTML table or Angular Material mat-table
- Filters apply on change (or on explicit "Apply" button — keep it simple)

**Test Notes:**
- Verify table displays all columns correctly
- Verify userId filter sends correct query param
- Verify type filter sends correct query param (omit param when "All")
- Verify refresh reloads data with current filters
- Verify empty state when no events match filters

---

## Epic 4: Quality & Polish

Cross-cutting concerns: structured logging, global error handling, and documentation.

### Story Q-01: Logging, Error Handling, and README

**Goal:** Ensure structured logging is consistent across all pipeline stages, global error handling returns ProblemDetails, and the README provides clear setup/run instructions for both runtime modes.

**Acceptance Criteria:**

**Given** any event is published by the API
**When** the publish succeeds
**Then** a structured log entry includes: EventId, UserId, EventType

**Given** any message is consumed by the function/BackgroundService
**When** processing occurs
**Then** structured log entries include: MessageId (if available), EventId, UserId, EventType, and the outcome (persisted/duplicate/error)

**Given** an unhandled exception occurs in the API
**When** the exception propagates
**Then** global exception middleware returns 500 ProblemDetails with traceId and generic message
**And** the exception is logged with full details

**Given** a new developer clones the repository
**When** they read the README
**Then** they find:
- Project overview and architecture diagram
- Prerequisites (.NET 8 SDK, Node.js, Angular CLI)
- How to run in local fallback mode (no Azure needed)
- How to run with real Azure (connection string setup)
- API endpoint documentation (POST and GET contracts)
- Project structure overview

**Implementation Notes:**
- API: GlobalExceptionMiddleware catches unhandled exceptions → logs → returns ProblemDetails with 500 and traceId from HttpContext.TraceIdentifier
- API: Use ILogger<T> with structured parameters everywhere (no string interpolation in log calls)
- Function: same structured logging pattern with ILogger<T>
- Verify all log points defined in architecture are present:
  - API publish: "Publishing {EventCount} events for user {UserId}"
  - API per-event: "Published event {EventId} type={EventType}"
  - Consumer receive: "Received message {MessageId} | eventId={EventId} userId={UserId} type={EventType}"
  - Consumer persist: "Persisted event {EventId} to database"
  - Consumer duplicate: "Duplicate event {EventId} ignored (already exists)"
- Optional: add Serilog for console sink with structured JSON output (not required, built-in ILogger is fine)
- README.md at repo root with sections: Overview, Architecture, Prerequisites, Quick Start (Local), Azure Mode, API Reference, Project Structure
- CORS configuration documented
- Include BMAD artifacts reference in README

**Test Notes:**
- Verify GlobalExceptionMiddleware returns ProblemDetails with traceId for unhandled errors
- Verify structured log entries contain all expected fields (spot check)
- Verify README steps work end-to-end on a clean clone
- Verify 503 response when Service Bus is unavailable (if testable)
