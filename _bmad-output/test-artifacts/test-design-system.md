---
stepsCompleted: [step-01-detect-mode, step-02-load-context, step-03-risk-assessment, step-04-coverage, step-05-generate]
lastStep: step-05-generate
lastSaved: 2026-02-20
mode: system-level
---

# Test Design — Reenbit Event Hub

**Date:** 2026-02-20
**Project:** Reenbit Event Hub (Car Rental Demo — Coding Test)
**Scope:** Lightweight test plan covering unit tests, integration tests, and manual verification

---

## Risk Assessment

| ID | Risk | Probability | Impact | Score | Mitigation | Test Coverage |
|----|------|-------------|--------|-------|------------|---------------|
| R1 | Eventual consistency — UI reads before function processes | Medium (2) | Low (1) | 2 | Manual refresh; document async nature | Manual verification |
| R2 | Duplicate Service Bus delivery | Medium (2) | High (3) | 6 | Unique constraint on Event.Id; no-op with log | Unit test + integration test |
| R3 | Invalid input accepted by API | Medium (2) | Medium (2) | 4 | Request validation + ProblemDetails | Unit tests |
| R4 | Event generation logic wrong (wrong count/types) | Low (1) | High (3) | 3 | Deterministic logic, easily tested | Unit tests |
| R5 | Service Bus publish failure unhandled | Low (1) | Medium (2) | 2 | 503 ProblemDetails + structured log | Unit test (mock failure) |
| R6 | GET filters/sort return wrong results | Medium (2) | Medium (2) | 4 | EF Core query building | Integration test (in-memory DB) |

**High-priority (R2):** Idempotency is the only score-6 risk. Covered by unit test on `EventPersistenceService` and integration test with duplicate insert.

---

## Test Strategy

### Approach

| Layer | Framework | What to Test | Estimated Count |
|-------|-----------|-------------|-----------------|
| **Unit Tests** | xUnit + Moq | Event generation logic, validation, idempotency handling | ~8-12 tests |
| **Integration Tests** | xUnit + WebApplicationFactory + SQLite | POST/GET endpoints end-to-end through API | ~4-6 tests |
| **Manual Verification** | Swagger UI + Angular app | Full pipeline flow, UI behavior, logs | Checklist |

### What NOT to Test (Out of Scope)

- Azure Service Bus SDK internals (trust the SDK)
- EF Core framework behavior (trust the ORM)
- Angular framework rendering (no E2E/Cypress for a coding test)
- Azure Function trigger binding (tested manually)

---

## Unit Tests

### 1. Event Generation Logic

**Location:** `ReenbitEventHub.Api.Tests/`

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| U1 | `ViewCar_Generates_One_PageView_Event` | ViewCar action produces exactly 1 event with Type=PageView |
| U2 | `ReserveCar_Generates_Two_Events_Click_And_Purchase` | ReserveCar action produces exactly 2 events: Click + Purchase (in order) |
| U3 | `Generated_Events_Have_Unique_Guids` | Each event gets a distinct Guid for Id |
| U4 | `Generated_Events_Have_Utc_Timestamps` | CreatedAt is UTC (Kind == Utc) |
| U5 | `Description_AutoFilled_When_Not_Provided` | Default description includes carId and carName |

### 2. Request Validation

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| U6 | `Missing_UserId_Returns_400` | Empty/null userId → 400 ProblemDetails |
| U7 | `Invalid_CarId_Returns_400` | carId not car-1/car-2 → 400 ProblemDetails |
| U8 | `Invalid_Action_Returns_400` | Action not ViewCar/ReserveCar → 400 ProblemDetails |

### 3. Idempotency (EventPersistenceService)

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| U9 | `Persist_New_Event_Succeeds` | New event inserted into DB |
| U10 | `Persist_Duplicate_Event_Is_NoOp` | Duplicate Event.Id → no exception, returns success |

### 4. Publisher Failure Handling

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| U11 | `ServiceBus_Failure_Returns_503` | Mock publisher throws → API returns 503 ProblemDetails |

---

## Integration Tests

**Setup:** Use `WebApplicationFactory<Program>` with SQLite in-memory + `InMemoryPublisher` (Channel\<T\>) + `BackgroundService` consumer. This tests the full local pipeline without any Azure dependencies.

| # | Test Name | What It Verifies |
|---|-----------|-----------------|
| I1 | `Post_ViewCar_Returns_202_With_One_EventId` | POST with ViewCar → 202 Accepted, response has publishedCount=1 |
| I2 | `Post_ReserveCar_Returns_202_With_Two_EventIds` | POST with ReserveCar → 202 Accepted, response has publishedCount=2 |
| I3 | `Post_Then_Get_Returns_Persisted_Events` | POST ViewCar → wait briefly → GET /api/events → response contains the PageView event |
| I4 | `Get_Filter_By_UserId_Returns_Only_Matching` | Seed events for 2 userIds → GET ?userId=user-1 → only user-1 events returned |
| I5 | `Get_Filter_By_Type_Returns_Only_Matching` | Seed mixed events → GET ?type=Purchase → only Purchase events |
| I6 | `Get_Default_Sort_Is_CreatedAt_Desc` | Seed events with different timestamps → GET → first item has latest CreatedAt |

**Note on I3:** Since the local pipeline uses Channel\<T\> + BackgroundService, there may be a short delay. Use a polling assertion (retry GET for up to 2 seconds) rather than a fixed Thread.Sleep.

---

## Manual Verification Checklist

### Via Swagger UI (API)

| # | Action | Expected Result |
|---|--------|----------------|
| M1 | POST /api/events with `{ "userId": "test-1", "action": "ViewCar", "carId": "car-1" }` | 202 Accepted with publishedCount=1 |
| M2 | POST /api/events with `{ "userId": "test-1", "action": "ReserveCar", "carId": "car-2" }` | 202 Accepted with publishedCount=2 |
| M3 | GET /api/events | Returns all events, sorted by CreatedAt desc |
| M4 | GET /api/events?userId=test-1 | Returns only test-1's events |
| M5 | GET /api/events?type=Purchase | Returns only Purchase events |
| M6 | GET /api/events?sort=createdAt_asc | Returns events oldest-first |
| M7 | POST with empty userId | 400 ProblemDetails with "userId" error |
| M8 | POST with carId="car-99" | 400 ProblemDetails with "carId" error |
| M9 | Check console/terminal logs | Structured log entries for publish, consume, persist |

### Via Angular App

| # | Action | Expected Result |
|---|--------|----------------|
| M10 | Open app, leave userId empty, click any button | Validation error shown, no API call |
| M11 | Enter userId, select car-1, click "View Selected Car" | Success message with 1 published event |
| M12 | Click refresh on events table | PageView event appears in table |
| M13 | Select car-2, click "Reserve Selected Car" | Success message with 2 published events |
| M14 | Click refresh | Click + Purchase events appear in table |
| M15 | Enter "test-1" in UserId filter | Table shows only test-1's events |
| M16 | Select "Purchase" in Type filter | Table shows only Purchase events |
| M17 | Select "All" in Type filter | All events shown again |

### Idempotency (Manual — Optional)

| # | Action | Expected Result |
|---|--------|----------------|
| M18 | Note an event Id from GET, manually re-insert via DB tool or repeat POST with same data | Function logs "duplicate ignored", no duplicate row in DB |

---

## Test Project Structure

```
tests/
└── ReenbitEventHub.Api.Tests/
    ├── Unit/
    │   ├── EventGenerationTests.cs      (U1-U5)
    │   ├── ValidationTests.cs           (U6-U8)
    │   ├── EventPersistenceTests.cs     (U9-U10)
    │   └── PublisherFailureTests.cs     (U11)
    ├── Integration/
    │   ├── EventsControllerPostTests.cs (I1-I2)
    │   ├── EventsPipelineTests.cs       (I3)
    │   └── EventsControllerGetTests.cs  (I4-I6)
    ├── Fixtures/
    │   └── TestWebApplicationFactory.cs
    └── ReenbitEventHub.Api.Tests.csproj
```

**Dependencies:** xUnit, Moq, Microsoft.AspNetCore.Mvc.Testing, Microsoft.EntityFrameworkCore.Sqlite

---

## Quality Gate (Definition of Done for Testing)

- [ ] All unit tests pass (U1-U11)
- [ ] All integration tests pass (I1-I6)
- [ ] Manual Swagger checklist completed (M1-M9)
- [ ] Manual Angular checklist completed (M10-M17)
- [ ] No unhandled exceptions in logs during manual testing
- [ ] Structured log entries visible for publish, consume, persist stages
