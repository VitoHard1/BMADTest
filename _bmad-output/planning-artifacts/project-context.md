# Project Context — Car Rental Event Hub (Coding Test)

## Purpose
Demonstrate an end-to-end event-driven flow:
Angular SPA → .NET Web API → Azure Service Bus → Azure Function → DB,
with clean structure, logging, validation, and BMAD artifacts.

## Tech Stack
- Frontend: Angular SPA, Reactive Forms
- Backend: .NET 10 Web API (Controllers), Swagger/OpenAPI enabled
- Messaging: Azure Service Bus (Queue)
- Processor: Azure Function (.NET isolated), ServiceBusTrigger
- Database target: Azure SQL (production-like)
- Local dev fallback (no Azure account): SQLite via EF Core

## Domain / Demo Theme
Car rental UI with 2 hardcoded cars:
- car-1: Toyota Corolla
- car-2: VW Golf

User actions:
- ViewCar → creates 1 event: Type=PageView
- ReserveCar → creates 2 events: Type=Click + Type=Purchase

## Event Model
Event:
- Id (GUID)
- UserId (string, required)
- Type (PageView | Click | Purchase)
- Description (string, required)
- CreatedAt (DateTime UTC)

## API Contracts
POST /api/events
- Request: { userId, action: ViewCar|ReserveCar, carId: car-1|car-2, description? }
- Behavior:
  - ViewCar → generate 1 Event (PageView)
  - ReserveCar → generate 2 Events (Click + Purchase)
- Publish each generated Event as a separate Service Bus message
- Return: 202 Accepted + published ids/count

GET /api/events
- Query: userId?, type?, from?, to?, sort? (default createdAt_desc), page?, pageSize?
- Returns: filtered/sorted events from DB

## Persistence & Idempotency
- Single table `Events`
- PK on Id (unique)
- Indexes (optional): CreatedAt; (UserId, CreatedAt); (Type, CreatedAt)
- Function must be idempotent:
  - duplicate Event.Id is ignored (log as no-op)

## Error Handling & Logging
- Validation: 400 ProblemDetails (field errors)
- Unhandled errors: global exception handler returns ProblemDetails (500) with traceId
- Service Bus publish failures: 503 + log
- Function: rely on runtime retries; log failures; DLQ after max delivery
- Structured logs for publish/consume/persist with correlation (messageId + Event.Id)

## Definition of Done
- Swagger shows POST/GET and they work
- UI can View/Reserve and list events in table
- Function persists events
- Filters by UserId/Type work
- README includes run steps + architecture + trade-offs