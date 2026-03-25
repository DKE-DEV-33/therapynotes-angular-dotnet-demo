# TherapyNotes Demo (Angular + ASP.NET Core)

This is a small, portfolio-grade demo app meant to mirror the TherapyNotes job description:

- Angular client (responsive UI)
- ASP.NET Core Scheduling API (robust, typed endpoints)
- Event-based design
  - Domain events are handled inside the API to produce an audit log
  - Integration events are written to an outbox and consumed by a separate worker service
- SOA principles (separate processes with explicit HTTP contract)

All data is fake demo data. No PHI.

## Quickstart

1. Start the backend API:
   - `./scripts/run-api.sh`
   - API default: `http://localhost:5070`

2. Start the worker (integration event consumer):
   - `./scripts/run-worker.sh`
   - Writes to `src/server/TherapyNotesDemo.Notifications.Worker/data/notifications.log`

3. Start the Angular client:
   - `./scripts/run-client.sh`
   - Client default: `http://localhost:4200`
   - Uses `src/client/proxy.conf.json` to proxy `/api` to the backend.

## What To Demo In An Interview

1. Create a client in `Clients`.
2. Schedule an appointment in `Appointments`.
3. Show `Audit Log` (domain event handler).
4. Show the outbox being consumed by the worker (tail the `notifications.log` file).

## API Endpoints

- `GET /api/clients`
- `POST /api/clients` body: `{ "displayName": "Alex Morgan" }`
- `GET /api/appointments?from=...&to=...`
- `POST /api/appointments` body: `{ "clientId": "...", "startsAt": "2026-03-25T14:00:00Z", "durationMinutes": 50 }`
- `GET /api/audit`
- `POST /internal/outbox/claim` body: `{ "max": 25, "leaseSeconds": 30 }`
- `POST /internal/outbox/complete` body: `{ "messageIds": ["..."] }`

## Architecture Notes

- `src/server/TherapyNotesDemo.Scheduling.Api`
  - `Domain/` contains the model and domain events.
  - `Eventing/` contains an in-process domain event bus + dispatcher.
  - `Infrastructure/OutboxStore.cs` implements a simple outbox with claim/complete semantics.
- `src/server/TherapyNotesDemo.Notifications.Worker`
  - Polls `POST /internal/outbox/claim` and then `POST /internal/outbox/complete`.
- `src/server/TherapyNotesDemo.Contracts`
  - Shared integration event contracts (`ClientCreatedIntegrationEvent`, `AppointmentScheduledIntegrationEvent`).

This intentionally avoids external infrastructure (Kafka/RabbitMQ) to keep the demo runnable locally, but keeps the boundaries and patterns that would translate to a real message bus + outbox processor.
