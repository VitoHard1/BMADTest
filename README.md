# Reenbit Event Hub

A full-stack event-driven car rental portal that demonstrates clean architecture, async messaging with Azure Service Bus, and dual-mode execution (cloud or local). Users submit car-browsing actions through an Angular SPA; the API generates domain events, publishes them to a message queue, and an async consumer persists them to a database for later querying.

## Table of Contents

- [Architecture](#architecture)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [How to Run](#how-to-run)
- [API Endpoints](#api-endpoints)
- [Testing Strategy](#testing-strategy)
- [CI/CD](#cicd)
- [Trade-offs](#trade-offs)
- [Switching to Azure SQL](#switching-to-azure-sql)
- [Important Notes](#important-notes)

## Architecture

```
┌──────────────────┐   HTTP    ┌──────────────────────────────────────────────┐
│  Angular SPA     │ ────────► │  ASP.NET Core Web API (.NET 10)              │
│  (Port 4200)     │ ◄──────── │  (Port 5113)                                │
└──────────────────┘           │                                              │
                               │  EventsController                           │
                               │    └─► EventApplicationService              │
                               │          └─► IMessagePublisher              │
                               └──────────────┬───────────────────────────────┘
                                              │
                        ┌─────────────────────┴─────────────────────┐
                        │                                           │
                   [Cloud Mode]                              [Local Mode]
                        │                                           │
              ┌─────────▼──────────┐                ┌───────────────▼──────────┐
              │ Azure Service Bus  │                │ In-Memory Channel<T>      │
              │ Queue ("events")   │                │ + BackgroundService       │
              └─────────┬──────────┘                └───────────────┬──────────┘
                        │                                           │
              ┌─────────▼──────────┐                                │
              │ Azure Function     │                                │
              │ (ServiceBusTrigger)│                                │
              └─────────┬──────────┘                                │
                        │                                           │
                        └───────────────┬───────────────────────────┘
                                        │
                              ┌─────────▼──────────┐
                              │ Database            │
                              │ SQLite (local) or   │
                              │ Azure SQL (cloud)   │
                              └────────────────────┘
```

The system operates in two modes determined by configuration:

- **Cloud Mode** — the API publishes events to an Azure Service Bus queue; a separate Azure Function consumes messages and persists them.
- **Local Mode** — when `ServiceBus:ConnectionString` is empty (the default), the API uses an in-memory `Channel<T>` as the queue and an in-process `BackgroundService` as the consumer. No Azure account is needed.

### Layered Architecture (Backend)

```
CarRentalApi (Host)
  └─► ReenbitEventHub.Application  (use-cases, validation, no framework deps)
        └─► ReenbitEventHub.Domain       (entities, enums, repository interfaces — pure)
  └─► ReenbitEventHub.Infrastructure  (EF Core, Service Bus / in-memory messaging)
        └─► ReenbitEventHub.Application
        └─► ReenbitEventHub.Domain
```

Controllers never touch infrastructure directly — they call `IEventApplicationService`, which coordinates validation, event generation, and publishing through abstractions.

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Angular 21, TypeScript 5.9, RxJS, Vitest |
| Backend API | .NET 10, ASP.NET Core, EF Core, OpenAPI |
| Azure Function | .NET 8 Isolated Worker, ServiceBusTrigger |
| Messaging | Azure Service Bus (cloud) / `System.Threading.Channels` (local) |
| Database | SQLite (local) / Azure SQL Server (cloud) |
| Monitoring | Application Insights |

## Project Structure

```
src/
├── api/CarRentalApi/                 # Backend API solution
│   ├── CarRentalApi/                 #   ASP.NET Core host, controllers, middleware
│   ├── ReenbitEventHub.Domain/       #   Entities, enums, repository interfaces
│   ├── ReenbitEventHub.Application/  #   Services, commands, DTOs, validation
│   ├── ReenbitEventHub.Infrastructure/ # EF Core, messaging implementations
│   └── ReenbitEventHub.Application.Tests/ # Unit tests
│
├── web/car-rental-portal/            # Angular SPA
│   └── src/app/
│       ├── components/
│       │   ├── event-form/           #   Reactive form for submitting events
│       │   └── events-table/         #   Table displaying persisted events
│       ├── services/                 #   HTTP communication with API
│       └── models/                   #   TypeScript interfaces
│
└── functions/CarRentalFunction/      # Azure Function (event consumer)
    ├── CarRentalFunction/            #   ServiceBusTrigger function
    └── CarRentalFunction.Tests/      #   Unit tests
```

## Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 10.0+ (API), 8.0+ (Function) |
| Node.js | 20+ |
| npm | 11+ |
| Angular CLI | 21+ (`npm install -g @angular/cli`) |

For **local mode** that is all you need. No Azure account required.

For **cloud mode** you additionally need:
- Azure Service Bus namespace with a queue named `events`
- Azure Functions Core Tools (`func`)
- Azurite or an Azure Storage account (for Function trigger)

## How to Run

### 1. Backend API (local mode — recommended for development)

```bash
cd src/api/CarRentalApi
dotnet build
dotnet run --project CarRentalApi/CarRentalApi.csproj
```

The API starts on **http://localhost:5113**. With the default empty `ServiceBus:ConnectionString`, it automatically falls back to in-memory messaging + BackgroundService — events are published, consumed, and persisted entirely in-process using a SQLite database (`events.dev.db`).

### 2. Frontend

```bash
cd src/web/car-rental-portal
npm install
npm start
```

The Angular dev server starts on **http://localhost:4200** and proxies API calls to `localhost:5113`.

### 3. Azure Function (cloud mode only)

```bash
cd src/functions/CarRentalFunction
func start
```

The Function App listens on the Service Bus queue and persists incoming events to the database.

> **Note:** The Azure Function **cannot run locally out of the box** because the `ServiceBus__ConnectionString` in `local.settings.json` is set to an empty string (a placeholder). The Functions runtime requires a valid connection string to create the Service Bus listener — without it the host fails to start. This is intentional — local development uses the API's built-in BackgroundService consumer instead.

## API Endpoints

| Method | Path | Description | Response |
|---|---|---|---|
| `POST` | `/api/events` | Submit a user action (ViewCar / ReserveCar) | `202 Accepted` with `{ publishedCount, eventIds }` |
| `GET` | `/api/events` | Query persisted events with filtering, sorting, pagination | `200 OK` with `{ items, totalCount, page, pageSize }` |

### POST request body

```json
{
  "userId": "user-1",
  "action": 1,
  "carId": "car-1",
  "description": "optional override"
}
```

**Event generation logic:**
- `ViewCar` produces **1** event (PageView)
- `ReserveCar` produces **2** events (Click + Purchase)

### GET query parameters

`userId`, `type`, `from`, `to`, `sort` (e.g. `createdAt:desc`), `page`, `pageSize`

## Testing Strategy

Because the Azure Service Bus connection strings are placeholders (empty), not every component can be run locally. The testing approach differs per project:

| Component | How to verify | Why |
|---|---|---|
| **API** | Run locally (`dotnet run`) | Works end-to-end in local mode — `InMemoryPublisher` + `BackgroundService` replace Azure Service Bus, so events are published, consumed, and persisted entirely in-process |
| **Azure Function** | Run unit tests (`dotnet test`) | The Function **cannot start** without a real Service Bus connection string — the `ServiceBusTrigger` binding requires a valid connection to create the queue listener. Unit tests verify the processing logic by mocking `ServiceBusReceivedMessage` directly |
| **Frontend** | Run locally (`npm start`) against the API | Connects to the running API on `localhost:5113`; no Azure dependency |

### Running tests

```bash
# API unit tests
cd src/api/CarRentalApi
dotnet test

# Azure Function unit tests
cd src/functions/CarRentalFunction
dotnet test

# Frontend unit tests
cd src/web/car-rental-portal
npm test
```

> **Key point:** The Azure Function is the only component that cannot be launched locally. Its business logic (deserialization, idempotent persistence) is validated exclusively through unit tests. To run the Function for real, you would need to provide a valid Azure Service Bus connection string in `local.settings.json`.

## CI/CD

The project uses **GitHub Actions** with a single workflow file at `.github/workflows/ci.yml`. The pipeline runs on every push and pull request to `main`.

### Pipeline Structure

Three jobs run **in parallel** to keep feedback fast:

| Job | Runtime | What it does |
|---|---|---|
| **build-api** | .NET 10 | Restore, build, and test the API solution (`src/api/CarRentalApi`) |
| **build-function** | .NET 8 | Restore, build, and test the Azure Function (`src/functions/CarRentalFunction`) |
| **build-web** | Node.js 20 | Install, build, and test the Angular frontend (`src/web/car-rental-portal`) |

```
push / PR to main
       │
       ├──► build-api ──► dotnet restore → build → test
       │
       ├──► build-function ──► dotnet restore → build → test
       │
       └──► build-web ──► npm ci → build → test
```

### CD (Continuous Deployment)

The workflow currently covers **CI only** (build + test). There are no deployment steps because the project has no provisioned Azure infrastructure — the Service Bus connection string is a placeholder and the database is local SQLite. Once real Azure resources are set up (App Service, Function App, Static Web App), deploy jobs can be added after the build/test jobs with the appropriate `AZURE_CREDENTIALS` GitHub Secret.

## Trade-offs

### Dual-mode messaging (Azure Service Bus vs. in-memory Channel)
The API ships with two `IMessagePublisher` implementations selected at startup based on configuration. When `ServiceBus:ConnectionString` is present, the DI container registers `ServiceBusPublisher` which talks to real Azure Service Bus. When the connection string is empty (the default), it registers `InMemoryPublisher` which writes to an in-memory `Channel<T>` instead.

**Why `InMemoryPublisher` exists:** Without it, the application would attempt to create a `ServiceBusClient` with an empty/fake connection string and throw an authentication exception on every publish — making the API unusable locally. `InMemoryPublisher` prevents this by replacing the real transport with an in-memory queue while preserving the same async pipeline (events still flow through a queue and get consumed by the `BackgroundService`). The controller and application service are unaware of the switch — they only depend on the `IMessagePublisher` abstraction.

**The trade-off:** The local path does not exercise the real Service Bus SDK, so bugs specific to serialization, retry policies, dead-lettering, or connection handling will only surface in cloud mode.

### SQLite for local development
Using SQLite instead of SQL Server locally removes the need for a database server and keeps setup to zero dependencies. However, SQLite lacks features like row-level locking and some SQL Server-specific types, so queries that work locally could behave differently in production.

Switching to Azure SQL is a one-line config change — see [Switching to Azure SQL](#switching-to-azure-sql) below.

### 202 Accepted (fire-and-forget publish)
The API returns `202 Accepted` immediately after publishing to the queue. It does **not** wait for the consumer to persist the event. This keeps the API fast and decoupled, but it means the client has no guarantee that the event has been saved — only that it has been accepted for processing. Temporary queue or consumer failures could delay persistence.

### BackgroundService as Function stand-in
In local mode, a `BackgroundService` replaces the Azure Function as the event consumer. This simplifies local development but doesn't replicate Function-specific behaviors like trigger-based scaling, poison-message handling, or Application Insights integration.

### Shared domain library vs. duplicated contracts
The API's domain/application projects define the canonical event models. The Azure Function project currently has its own copy of entity and contract types instead of referencing the shared Domain project. This avoids a cross-framework-version dependency (.NET 10 API vs .NET 8 Function) but means model changes must be synchronized manually.

### .NET 10 API + .NET 8 Function
The API targets .NET 10 while the Function targets .NET 8 (the latest LTS supported by Azure Functions Isolated Worker). This is a pragmatic split — the API uses the latest framework features, and the Function stays on the most stable supported runtime — but it prevents direct project references between them.

## Switching to Azure SQL

Both the API and the Azure Function auto-detect the database provider from the connection string. If the value contains `Server=`, EF Core uses **SQL Server**; otherwise it falls back to **SQLite**. No code changes are needed — just update the configuration.

**API** — edit `src/api/CarRentalApi/CarRentalApi/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "EventsDb": "Server=your-server.database.windows.net;Database=EventsDb;User Id=your-user;Password=your-password;Encrypt=True;TrustServerCertificate=False;"
  }
}
```

**Azure Function** — edit `src/functions/CarRentalFunction/CarRentalFunction/local.settings.json`:

```json
{
  "Values": {
    "ConnectionStrings__EventsDb": "Server=your-server.database.windows.net;Database=EventsDb;User Id=your-user;Password=your-password;Encrypt=True;TrustServerCertificate=False;"
  }
}
```

That's it. Both projects already include the `Microsoft.EntityFrameworkCore.SqlServer` NuGet package, so the provider switch is purely config-driven.

## Important Notes

- **The Azure Function cannot run locally without a real Azure Service Bus connection string.** The value in `local.settings.json` is an empty placeholder. For local development, use the API in its default local mode which handles the full event pipeline in-process.
- All error responses use the RFC 9457 **ProblemDetails** format.
- All timestamps are **UTC**.
- All JSON uses **camelCase** serialization.
- Events are **idempotent** — duplicate event IDs are detected and skipped during persistence.
