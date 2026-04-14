# TaskFlow API

A task management REST API built with ASP.NET Core 8, PostgreSQL, and Docker.

---

## Table of Contents

1. [Overview](#1-overview)
2. [Architecture Decisions](#2-architecture-decisions)
3. [Running Locally](#3-running-locally)
4. [Running Migrations](#4-running-migrations)
5. [Test Credentials](#5-test-credentials)
6. [API Reference](#6-api-reference)
7. [Integration Tests](#7-integration-tests)
8. [What I'd Do With More Time](#8-what-id-do-with-more-time)

---

## 1. Overview

TaskFlow is a minimal task management system that lets users register, log in, create projects, add tasks to those projects, and assign tasks to team members.

**Tech stack:**

| Layer | Technology |
|---|---|
| Language | C# / .NET 10.0 |
| Framework | ASP.NET Core Web API |
| Database | PostgreSQL 16 |
| ORM / Query | Dapper (raw SQL) |
| Auth | JWT Bearer (HS256, 24h expiry) |
| Password hashing | BCrypt.Net-Next (cost 12) |
| Migrations | Flyway-style versioned SQL files (V001, V002, V003) and Evolve Package |
| Validation | FluentValidation |
| Logging | Serilog (structured JSON) |
| Containerisation | Docker + Docker Compose |

> **Note on language choice:** The assignment recommends Go, but this submission uses .NET 8 as permitted by the spec ("use a language you know well"). The architecture maps directly — handlers → controllers, middlewares → middleware pipeline, goroutines → async/await.

---

## 2. Architecture Decisions

### Layered architecture

```
Controllers  →  Services  →  Repositories  →  PostgreSQL
     ↓               ↓
  DTOs/Validation   Models
```

- ## Backend Architecture

This project uses a streamlined N-Tier architecture, utilizing "Fat Repositories" to reduce boilerplate and keep things simple:

* **Controllers:** Handle HTTP concerns only—request routing, extracting authentication tokens, and formatting HTTP responses.
* **Fat Repositories:** Serve as both the business logic and data access layer. They implement domain interfaces (e.g., `IProjectService`) and handle both raw SQL queries (via Dapper) and business rules (ownership checks, permission enforcement).
* **DTOs:** Completely isolated from domain models, allowing the API contract and database shape to evolve independently.
* **Infrastructure Services:** The `Services` folder is strictly reserved for infrastructural helpers (e.g., JWT generation, password hashing) rather than domain business logic.


### Why Dapper over EF Core
Raw SQL gives explicit control over query shape. For this project, several queries require JOINs and GROUP BY that would generate inefficient SQL through EF Core's LINQ translation. Dapper keeps the queries readable and reviewable.

### Why versioned SQL migrations (not auto-migrate)
The spec explicitly disqualifies auto-migrate. SQL migration files (V001__create_users.sql, etc.) are version-controlled, deterministic, and strictly forward-moving. The migration runner applies them in order on container startup.

### JWT claims
Token payload: `user_id` (UUID) and `email`. The `user_id` claim is extracted in a shared middleware and injected into `HttpContext.Items` so controllers never re-parse the token.

### Intentionally left out

- Refresh tokens — a 24-hour expiry is sufficient for the scope of this assignment
- Rate limiting — would add in production (`AspNetCoreRateLimit`)
- Soft deletes — hard deletes are simpler and the spec does not require an audit trail
- HTTPS termination — handled by the reverse proxy layer in production; not needed for local Docker

---

## 3. Running Locally

**Prerequisites:** Docker and Docker Compose only. Nothing else needs to be installed.

```bash
# 1. Clone the repo
git clone https://github.com/amalprasad0/taskflow_amalprasad.git
cd taskflow-amal-prasad

# 2. Copy environment file
cp .env.example .env

# 3. Start the full stack (postgres + api)
docker compose up --build

# API is available at:
#   http://localhost:5000
#
# Migrations and seed data run automatically on startup.
# No manual steps required.
```

To stop and clean up:

```bash
docker compose down -v   # -v removes the postgres volume (full reset)
```

---

## 4. Running Migrations

Migrations run **automatically** when the container starts. The startup sequence is:

1. API waits for PostgreSQL to be ready (up to 30s, retries every 2s)
2. Migration runner scans `/Migrations/` for `V*.sql` files and applies any that haven't run yet
3. Seed data is inserted if the users table is empty (`R__seed_data.sql`)
4. API begins serving requests

**To run migrations manually** (if needed for development):

```bash
# Connect to the running api container
docker compose exec api dotnet run --migrate-only

# Or apply directly via psql
docker compose exec postgres psql -U postgres -d taskflow -f /migrations/V001__create_users.sql
```

**Migration files:**

| File | Description |
|---|---|
| `V001__create_users.sql` | users table, unique index on email |
| `V002__create_projects.sql` | projects table, FK to users |
| `V003__create_tasks.sql` | tasks table, FKs to projects + users, enum constraints |
| `R__seed_data.sql` | Repeatable seed — 1 user, 1 project, 3 tasks |

---

## 5. Test Credentials

The seed script creates a ready-to-use test account:

```
Email:    test@example.com
Password: password123
```

Use `POST /auth/login` with the above credentials to get a JWT token, then include it as `Authorization: Bearer <token>` on all subsequent requests.

---

## 6. API Reference

**Base URL:** `http://localhost:5000`

All responses are `Content-Type: application/json`. All non-auth endpoints require `Authorization: Bearer <token>`.

### Authentication

#### POST /auth/register

```json
// Request
{
    "Username":"jane.doe",
    "Email":"jane@example.com",
    "Password":"j@ne1243"
}
// Response 201
{
    "message": "User registered successfully",
    "status": true,
    "data":{
      "id": "a1b2c3d4-...",
      "name": "Jane Doe",
      "email": "jane@example.com",
      "createdAt": "2026-04-14T10:00:00Z"
          }
}

```

#### POST /auth/login

```json
// Request
{
  "email": "jane@example.com",
  "password": "secret123"
}

// Response 200
{
    "message": "Login successful",
    "status": true,
    "data": {
        "accessToken": "eyJhbGciOiJIUzI1N......o"
    }
}
```

---

### Projects

#### GET /projects

Returns projects the current user owns **or** has tasks assigned in.

```json
// Response 200
{
    "message": "Projects retrieved successfully",
    "status": true,
    "data": [
        {
            "id": "770046fa-18e6-4803-95af-81d74fcfa1bf",
            "name": "Backend Project3",
            "description": "Development of taskflow",
            "createdAt": "2026-04-13T20:36:20.123678Z",
            "ownerId": "f4550ae0-babd-40b4-a1f2-4d6f630dc132"
        }
    ]
}
```

#### POST /projects

```json
// Request
{ "name": "New Project", "description": "Optional" }

// Response 201
{
    "message": "Project created successfully",
    "status": true,
    "data": {
        "projectId": "770046fa-18e6-4803-95af-81d74fcfa1bf"
    }
}
```

#### GET /projects/:id

Returns project details + all tasks. Returns 403 if user has no access, 404 if not found.
```json
// Response 200
{
    "message": "Project with tasks retrieved successfully",
    "status": true,
    "data": {
        "id": "770046fa-18e6-4803-95af-81d74fcfa1bf",
        "name": "Backend Project3",
        "description": "Development of taskflow",
        "createdAt": "2026-04-13T20:36:20.123678Z",
        "ownerId": "f4550ae0-babd-40b4-a1f2-4d6f630dc132",
        "ownerName": "jane.doe",
        "tasks": [
            {
                "id": "45b44b76-c135-4871-a180-e709f25f7f06",
                "name": "Test Tasks",
                "description": "Test Task 12344",
                "status": "todo",
                "priority": "high",
                "projectId": "770046fa-18e6-4803-95af-81d74fcfa1bf",
                "assigneeId": "a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11",
                "createdAt": "2026-04-13T20:36:42.943689Z",
                "updatedAt": "2026-04-13T20:36:42.94369Z",
                "dueDate": "2026-04-20T00:00:00",
                "assigneeName": "Test User",
                "ownerName": null
            }
        ]
    }
}
```
#### PATCH /projects/:id

Owner only. Accepts partial updates to `name` and/or `description`. Returns 403 if not owner.
```json
// Request 
{
   "Name": "{{projectName}}",
   "Description": "{{projectDescription}}"
}

// Response 200
{
    "message": "Project updated successfully",
    "status": true,
    "data": {
        "isUpdated": true
    }
}
```
#### DELETE /projects/:id

Owner only. Cascades to delete all tasks. Returns 204 on success.
```json
Response 200
{
    "message": "Project deleted successfully",
    "status": true,
    "data": {
        "isDeleted": true
    }
}
```
---

### Tasks

#### GET /projects/:id/tasks

Supports query filters:

| Param | Values | Example |
|---|---|---|
| `status` | `todo`, `in_progress`, `done` | `?status=todo` |
| `assignee` | UUID | `?assignee=a1b2c3...` |

#### POST /projects/:id/tasks

```json
// Request
{
  "title": "Test Tasks",
  "description": "Test Task 1234",
  "assigneeId": "uuid",
  "dueDate": "2026-04-20T12:00:00Z",
  "priority": "high",
  "status": "todo",
  "createdAt": "2026-04-12T10:00:00Z",
  "updatedDateAt": "2026-04-12T10:00:00Z"
}

// Response 201 — returns task id
{
    "message": "Task created successfully",
    "status": true,
    "data": {
        "taskId": "45b44b76-c135-4871-a180-e709f25f7f06"
    }
}
```

#### PATCH /tasks/:id

All fields are optional (true PATCH semantics):

```json
// Request
{ "status": "done", "priority": "low" }

// Response 200 — returns updated task
{
    "message": "Task updated successfully",
    "status": true,
    "data": {
        "taskId": "49737ae1-c945-426e-bafd-681902a1f9d7"
    }
}
```

#### DELETE /tasks/:id

Allowed for: project owner OR the user who created the task. Returns 204.
```json
Response 200
{
    "message": "Project deleted successfully",
    "status": true,
    "data": {
        "isDeleted": true
    }
}
```
#### GET /project/{{projectId}}/stats

Retunrs the stats of project Returns 200.
```json
Response 200
{
    "message": "Project statistics retrieved successfully",
    "status": true,
    "data": {
        "tasksPerStatus": {
            "todo": 1 // Status
        },
        "tasksPerPriority": {
            "high": 1 //Priority
        },
        "tasksPerAssignee": {
            "Test User": 1 //Assignee
        }
    }
}
```
---

### Error responses

All errors follow a consistent shape:

```json
// 400 — Validation failure
{
    "status": false,
    "message": "Email and password are required",
    "error": {
        "type": "ValidationException",
        "title": "Validation failed",
        "detail": "Email and password are required"
    },
    "traceId": "0HNKPRAQVGBRB:00000001"
}
// 401 — Missing or invalid token
{ "error": "unauthorized" }

// 403 — Valid token, insufficient permission
{ "error": "forbidden" }

// 200 — Resource does not exist
{
    "status": false,
    "message": "Task not found",
    "error": {
        "type": "KeyNotFoundException",
        "title": "Resource not found",
        "detail": "Task not found"
    },
    "traceId": "0HNKPIG19KFGG:00000017"
}


```

> A full Postman collection is included at `/postman/TaskFlow.postman_collection.json`. Import it and set the `baseUrl` (default: `http://localhost:5000`) and `token` environment variables.

---

## 7. Integration Tests

Tests run against a real PostgreSQL instance spun up via **Testcontainers** — no mocks, no in-memory database.

### Prerequisites

- **Docker** must be running (Testcontainers pulls `postgres:16-alpine` automatically)

### Run

```bash
dotnet test taskflow.IntegrationTests
```

### Stack

| Tool | Purpose |
|------|---------|
| **xUnit** | Test framework |
| **Testcontainers.PostgreSql** | Disposable Postgres container per test collection |
| **WebApplicationFactory** | In-process ASP.NET Core server (no network, no port binding) |
| **FluentAssertions** | Readable assertion syntax |
| **Coverlet** | Code coverage collection |

### How it works

`TaskFlowFactory` extends `WebApplicationFactory<Program>`. On startup it:
1. Spins up a Postgres container
2. Injects the container's connection string via environment variables
3. The app's Evolve migrations run automatically, creating the schema from scratch

All test classes share the same container via `[Collection("Integration")]` and `ICollectionFixture<TaskFlowFactory>`.

### Test suites

| File | Covers |
|------|--------|
| `AuthFlowTests` | Register → Login → JWT return, duplicate email rejection, wrong password 401 |
| `AuthorizationTests` | 401 without JWT, 403 when non-owner deletes a task, owner delete succeeds |
| `ProjectFlowTests` | Create project → create task → verify via GET, 404 for unknown project ID |

---

## 8. What I'd Do With More Time

**Shortcuts taken:**

- No pagination on list endpoints — `GET /projects` and `GET /projects/:id/tasks` return all results. In production this would cause issues at scale.
- Error messages from the DB layer (e.g. unique constraint violations) are caught generically rather than mapped to specific user-facing messages.

**What I'd add:**

- **Refresh tokens** — short-lived access tokens (15m) + long-lived refresh tokens stored in the DB, revocable on logout
- **Pagination** — cursor-based pagination for tasks (more stable than offset for frequently-updated lists)
- **Rate limiting** — `AspNetCoreRateLimit` on auth endpoints to prevent brute-force
- **Soft deletes** — `deleted_at` timestamp instead of hard deletes, for audit trail
- **Assignee validation** — currently accepts any UUID as assignee_id; should validate that the user is a member of the project
- **Role-based access** — project members vs owners with explicit membership table
- **OpenAPI / Swagger UI** — auto-generated from controller attributes, replaces the manual Postman collection

---
