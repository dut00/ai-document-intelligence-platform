# SPEC: AI Document Intelligence Platform

## Goal
A full-stack, production-oriented portfolio project, published as a public GitHub repository. An authenticated user uploads a PDF/TXT document. A background worker extracts the text, analyzes it with Claude (structured, validated output) and verifies the detected dates against the Nager.Date public-holiday calendar. The UI shows live processing status and the analysis. Focus areas: architecture, reliability, AI integration quality, tests, Docker and documentation.

## MVP scope
- **Auth:** register, login, refresh, logout (JWT access token in memory, rotated refresh token in an httpOnly cookie).
- **Documents:** upload (PDF/TXT, max 10 MB, magic-byte check), paginated list, details, download, delete, dashboard stats. Owner-only access (another user's document → 404).
- **Processing:** file in MinIO, metadata in PostgreSQL, `DocumentUploaded` via outbox → RabbitMQ → Worker. Status flow `Pending → Processing → Completed | Failed`.
- **AI analysis:** document type, summary, entities, important dates (type + meaning), financial info, risks. Claude via forced tool use + FluentValidation, one corrective retry. Fake analyzer when no API key.
- **Date insights:** backend (not AI) checks weekends/holidays and the next business day via Nager.Date. An API failure does not fail the document ("Calendar check unavailable").
- **Reliability:** retries, delayed redelivery, DLQ (`_error`), idempotency (inbox + `xmin` concurrency), permanent vs transient errors.
- **Extras:** SignalR live status, rate limiting, health checks, Serilog, OpenTelemetry + Aspire Dashboard, GitHub Actions CI.

## Out of scope
- OCR for scanned PDFs (they end as `Failed`: "no extractable text").
- File formats other than PDF/TXT (DOCX, images, etc.).
- Separate read store / event sourcing, multi-tenancy, roles/admin panel.
- External identity providers, email confirmation, password reset.
- Document sharing, editing, versioning, search across documents.
- Cloud deployment / Kubernetes (local Docker only).

## Technical decisions
- **Backend:** .NET, ASP.NET Core Minimal API with OpenAPI + Scalar UI, Clean Architecture + DDD + CQRS with custom handlers and decorators (no MediatR — license), `Result<T>` → ProblemDetails, EF Core + PostgreSQL, ASP.NET Core Identity (core) + JWT.
- **Messaging:** MassTransit v8 (OSS; v9 is commercial) + RabbitMQ, EF Core transactional outbox/inbox.
- **AI:** official `Anthropic` NuGet package, default model `claude-haiku-4-5-20251001` (configurable).
- **Other:** MinIO (storage), PdfPig (PDF text), Nager.Date (holidays, cached per year/country, default `PL`).
- **Frontend:** React + Vite + TypeScript, React Router, TanStack Query, Tailwind, `@microsoft/signalr`.
- **Infra:** `docker compose up` for Postgres, RabbitMQ, MinIO, Aspire Dashboard; `full` profile also runs api, worker, ui.

**Repository layout**
```
src/        Domain, Application, Infrastructure, Contracts, Api, Worker, ServiceDefaults
frontend/   Vite React TS
tests/      UnitTests, IntegrationTests
docs/       architecture.md, decisions.md (ADRs), ai-development.md
.github/workflows/ci.yml, docker-compose.yml, Directory.Packages.props (CPM)
```

**Tests:** xUnit, NSubstitute, Shouldly.
- Unit: `Document` aggregate and value objects, validators, upload validation, JWT/refresh rotation, consumer (idempotency, error types, Nager.Date failure), `DateInsightsService`, holiday provider, Claude response parsing.
- Integration: `WebApplicationFactory` + Testcontainers (Postgres, RabbitMQ, MinIO), fake AI — auth flow, authorization (401/404), full upload → processing → download → delete.
- CI: `dotnet build/test` and `npm ci && npm run lint && npm run build`.
