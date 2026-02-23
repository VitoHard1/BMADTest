---
stepsCompleted: [1, 2]
inputDocuments: []
date: 2026-02-20
author: Admin
---

# Product Brief: Reenbit

## Executive Summary

Reenbit Event Hub is a coding-test demonstration project that implements an end-to-end event-driven system using a car rental domain. The application captures user interaction events (page views, clicks, purchases) from an Angular SPA, publishes them through a .NET Web API to Azure Service Bus, and persists them via an Azure Function into a SQL database. The project prioritizes clean architecture, observable async flow, idempotent processing, and reviewer-friendly local execution.

---

## Core Vision

### Problem Statement

Hiring teams evaluating full-stack candidates need to see more than CRUD — they need evidence of architectural thinking, async integration patterns, proper error handling, and production-aware coding practices demonstrated in a cohesive, runnable project.

### Problem Impact

Without a well-structured demonstration project, candidates struggle to showcase event-driven architecture skills, messaging patterns, and clean separation of concerns in a way that reviewers can quickly clone, run, and evaluate.

### Why Existing Solutions Fall Short

Typical coding test submissions are either too simple (basic CRUD with no async patterns) or too complex (requiring cloud accounts, elaborate setup, or domain knowledge to evaluate). They rarely demonstrate the bridge between local development convenience and production-grade architecture.

### Proposed Solution

A car rental-themed Event Hub system with two hardcoded vehicles (Toyota Corolla, VW Golf) that generates three event types through natural user actions:

- **View car** produces a PageView event
- **Reserve car** produces Click + Purchase events

The system flows events through Angular -> .NET Web API -> Azure Service Bus -> Azure Function -> Azure SQL, with full logging at each stage (publish, consume, persist). Events are queryable via a filterable/sortable UI table.

Two runtime modes ensure frictionless evaluation:
- **Option A:** Real Azure (Service Bus + SQL) via connection strings
- **Option B:** Local fallback (in-memory queue + SQLite) requiring zero cloud setup

Idempotency is enforced via unique constraint on Event.Id in the database, treating duplicate delivery as a logged no-op.

### Key Differentiators

- **Event-driven architecture** with real async message flow, not synchronous shortcuts
- **Dual-mode execution** — production-pattern Azure or zero-dependency local
- **Observable pipeline** — structured logging at publish, consume, and persist stages
- **Idempotent processing** — safe for duplicate Service Bus delivery
- **Consistent UTC timestamps** across all layers
- **BMAD workflow artifacts** included, showing planning discipline alongside technical execution
- **Intentionally scoped** — no auth, payments, or deployment noise; focused signal for reviewers
