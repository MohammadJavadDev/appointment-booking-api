# Appointment Booking API

A REST API for booking time slots with service providers (clinics, salons, consultants).
It is a reference implementation of **Clean Architecture** with **CQRS** on **.NET 8**, built to show
how to keep business rules isolated, testable and safe under concurrent load.

## Highlights

- **Clean Architecture** – dependencies point inward only: `Api → Infrastructure → Application → Domain`.
- **CQRS with MediatR** – one command/query per use case, with a validation pipeline behavior (FluentValidation).
- **Rich domain model** – invariants (slot grid, working hours, ownership, cancellation rules) live in the entities, not in controllers.
- **Result pattern** – expected business failures are values, not exceptions; the API maps them to RFC 7807 problem responses.
- **No double booking, guaranteed by the database** – a filtered unique index on `(ProviderId, StartUtc)` for active
  appointments; a violation is translated into `409 Conflict`. A concurrency test fires 8 simultaneous requests for one slot and asserts exactly one wins.
- **JWT authentication and role-based authorization** (`Customer`, `Admin`), password hashing via ASP.NET Core Identity's hasher.
- **PostgreSQL** in production, **SQLite** for integration tests – same EF Core model, no mocks of the database.
- Docker, docker-compose, GitHub Actions CI, health check endpoint, Swagger UI.

## Structure

```
src/
  AppointmentBooking.Domain          Entities, Result/Error, business rules. No dependencies.
  AppointmentBooking.Application     Use cases (commands/queries), validators, abstractions (repositories, token generator…).
  AppointmentBooking.Infrastructure  EF Core, repositories, JWT, password hashing. Implements Application abstractions.
  AppointmentBooking.Api             Minimal API endpoints, auth setup, error handling, composition root.
tests/
  AppointmentBooking.Domain.Tests              Fast unit tests for the domain rules.
  AppointmentBooking.Api.IntegrationTests      End-to-end tests through HTTP against a real (SQLite) database.
```

## Run it

Requirements: .NET 8 SDK and Docker.

```bash
docker compose up --build
```

Swagger UI: <http://localhost:8080/swagger> · Health: <http://localhost:8080/health>

In the `Development` environment an admin is seeded from `appsettings.Development.json`
(`admin@example.com` / `ChangeMe123!`) – **change these and the JWT signing key for any real deployment.**
Outside Development, provide `Jwt__SigningKey` (32+ characters) via environment variables or a secret store.

Without Docker, start a PostgreSQL instance matching `ConnectionStrings:Default` and run:

```bash
dotnet run --project src/AppointmentBooking.Api
```

## Tests

```bash
dotnet test
```

## API overview

| Method | Route | Access | Description |
| --- | --- | --- | --- |
| POST | `/api/auth/register` | public | Create a customer account, returns a JWT |
| POST | `/api/auth/login` | public | Exchange credentials for a JWT |
| GET | `/api/providers` | public | List providers |
| POST | `/api/providers` | admin | Create a provider (working hours + slot length) |
| GET | `/api/providers/{id}/slots?date=YYYY-MM-DD` | public | Slots for a day with availability |
| POST | `/api/appointments` | customer | Book a slot (`providerId`, `startUtc`) |
| GET | `/api/appointments/mine` | customer | My appointments |
| DELETE | `/api/appointments/{id}` | owner/admin | Cancel an appointment |

## Design notes and trade-offs

- **Times are UTC.** Working hours and slots are stored and exchanged in UTC to keep the sample focused; a production system would store a provider time zone.
- **Schema creation uses `EnsureCreated`.** Chosen to keep the sample zero-setup; production would use EF Core migrations.
- **Repositories + Unit of Work** are kept thin on purpose; they make the Application layer independent of EF Core and trivially substitutable in tests.
- **Optimistic approach to concurrency.** The app does a cheap availability check for a friendly fast path, but the unique index is the source of truth.
