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
7. [What I'd Do With More Time](#7-what-id-do-with-more-time)

---

## 1. Overview

TaskFlow is a minimal task management system that lets users register, log in, create projects, add tasks to those projects, and assign tasks to team members.

**Tech stack:**

| Layer | Technology |
|---|---|
| Language | C# / .NET 8 |
| Framework | ASP.NET Core Web API |
| Database | PostgreSQL 16 |
| ORM / Query | Dapper (raw SQL) |
| Auth | JWT Bearer (HS256, 24h expiry) |
| Password hashing | BCrypt.Net-Next (cost 12) |
| Migrations | Flyway-style versioned SQL files (V001, V002, V003) |
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

- **Controllers** handle HTTP concerns only — request parsing, response shaping, status codes.
- **Services** own business logic — ownership checks, permission enforcement, cascade operations.
- **Repositories** own all SQL — one repository per aggregate root (UserRepository, ProjectRepository, TaskRepository).
- **DTOs** are separate from domain models so API shape and DB shape can evolve independently.

### Why Dapper over EF Core

Raw SQL gives explicit control over query shape. For this project, several queries require JOINs and GROUP BY that would generate inefficient SQL through EF Core's LINQ translation. Dapper keeps the queries readable and reviewable.

### Why versioned SQL migrations (not auto-migrate)

The spec explicitly disqualifies auto-migrate. SQL migration files (V001__create_users.sql, etc.) are version-controlled, deterministic, and include both up and down directions. The migration runner applies them in order on container startup.

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
git clone https://github.com/your-username/taskflow-amal-prasad
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
  "name": "Jane Doe",
  "email": "jane@example.com",
  "password": "secret123"
}

// Response 201
{
  "id": "a1b2c3d4-...",
  "name": "Jane Doe",
  "email": "jane@example.com",
  "createdAt": "2026-04-14T10:00:00Z"
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
  "token": "<jwt>",
  "user": {
    "id": "a1b2c3d4-...",
    "name": "Jane Doe",
    "email": "jane@example.com"
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
  "projects": [
    {
      "id": "uuid",
      "name": "Website Redesign",
      "description": "Q2 project",
      "ownerId": "uuid",
      "createdAt": "2026-04-01T10:00:00Z"
    }
  ]
}
```

#### POST /projects

```json
// Request
{ "name": "New Project", "description": "Optional" }

// Response 201
{ "id": "uuid", "name": "New Project", "description": "Optional", "ownerId": "uuid", "createdAt": "..." }
```

#### GET /projects/:id

Returns project details + all tasks. Returns 403 if user has no access, 404 if not found.

#### PATCH /projects/:id

Owner only. Accepts partial updates to `name` and/or `description`. Returns 403 if not owner.

#### DELETE /projects/:id

Owner only. Cascades to delete all tasks. Returns 204 on success.

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
  "title": "Design homepage",
  "description": "Wireframes first",
  "priority": "high",
  "assigneeId": "uuid",
  "dueDate": "2026-04-30"
}

// Response 201 — returns full task object
```

#### PATCH /tasks/:id

All fields are optional (true PATCH semantics):

```json
// Request
{ "status": "done", "priority": "low" }

// Response 200 — returns updated task
```

#### DELETE /tasks/:id

Allowed for: project owner OR the user who created the task. Returns 204.

---

### Error responses

All errors follow a consistent shape:

```json
// 400 — Validation failure
{ "error": "validation failed", "fields": { "email": "is required" } }

// 401 — Missing or invalid token
{ "error": "unauthorized" }

// 403 — Valid token, insufficient permission
{ "error": "forbidden" }

// 404 — Resource does not exist
{ "error": "not found" }
```

> A full Postman collection is included at `/postman/TaskFlow.postman_collection.json`. Import it and set the `baseUrl` (default: `http://localhost:5000`) and `token` environment variables.

---

## 7. What I'd Do With More Time

**Shortcuts taken:**

- No pagination on list endpoints — `GET /projects` and `GET /projects/:id/tasks` return all results. In production this would cause issues at scale.
- No integration tests — only manual testing via Postman. The spec awards bonus points for integration tests (xUnit + WebApplicationFactory).
- The stats endpoint (`GET /projects/:id/stats`) is not implemented — it was a bonus item that ran out of time budget.
- Error messages from the DB layer (e.g. unique constraint violations) are caught generically rather than mapped to specific user-facing messages.

**What I'd add:**

- **Refresh tokens** — short-lived access tokens (15m) + long-lived refresh tokens stored in the DB, revocable on logout
- **Pagination** — cursor-based pagination for tasks (more stable than offset for frequently-updated lists)
- **Integration tests** — `WebApplicationFactory<Program>` + TestContainers for a real Postgres in CI
- **Rate limiting** — `AspNetCoreRateLimit` on auth endpoints to prevent brute-force
- **Soft deletes** — `deleted_at` timestamp instead of hard deletes, for audit trail
- **Assignee validation** — currently accepts any UUID as assignee_id; should validate that the user is a member of the project
- **Role-based access** — project members vs owners with explicit membership table
- **OpenAPI / Swagger UI** — auto-generated from controller attributes, replaces the manual Postman collection

---

*Questions? See the assignment email or open a GitHub issue.*
