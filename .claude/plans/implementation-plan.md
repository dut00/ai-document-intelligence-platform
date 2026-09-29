# Plan: AI Document Intelligence Platform

## Context
A portfolio project: build a full-stack, production-oriented application published as a public GitHub repository. A user signs in (JWT) and uploads a PDF or TXT file. The file goes to SeaweedFS and its metadata to PostgreSQL. A message travels through RabbitMQ to a Worker, which extracts the text, analyzes it with Claude (structured output + validation), checks the detected dates against the Nager.Date public-holiday calendar and stores the result. The React UI shows live status (SignalR) and the analysis. General documents are supported (contracts, invoices, letters, etc.). The repo is empty; the branch stays `master`.

The project focuses on architecture, reliability (retries, DLQ, idempotency), AI integration quality, tests, Docker and documentation (including `docs/ai-development.md`).

## Agreed decisions
- **Language:** all project files are in **English**: code, comments, README, `docs/`, commit messages, UI, plan files and everything under `.claude`. The user writes in Polish; memory and all saved files are in English.
- **Architecture:** Clean Architecture + **DDD** + **CQRS**, with custom handlers (no MediatR).
  - **DDD (Domain):**
    - Aggregates: `Document` is the root and owns `DocumentAnalysis` as an internal entity. User accounts are owned by ASP.NET Core Identity (`ApplicationUser` in Infrastructure, behind the `IUserAccountService` port); the domain refers to users only by `UserId`, so there is no separate domain `User` class duplicating Identity.
    - Value objects: `DocumentId` and `UserId` (strongly-typed ids), `FileName`, `FileSize`, `ContentType` (PDF/TXT only), `StorageKey`, `Money` (Amount + Currency), `ImportantDate` (Date, Type, Description, `CalendarCheck?`), `CalendarCheck` (IsWeekend, HolidayName, NextBusinessDay) and `Risk`.
    - Rich model: `Document.Upload(...)`, `StartProcessing()`, `Complete(analysis)`, `Fail(reason)` and `MarkForDeletion()` enforce the allowed status transitions. Invalid transitions throw `DomainException`.
    - Domain events: `DocumentUploadedDomainEvent`, `DocumentProcessingCompletedDomainEvent`, `DocumentProcessingFailedDomainEvent` and `DocumentDeletedDomainEvent` (raised by `MarkForDeletion()`, carries the storage key for blob cleanup). They are collected on the aggregate and dispatched by a `SaveChanges` interceptor. Their handlers publish integration events (Contracts) through the outbox.
    - One repository per aggregate (`IDocumentRepository`) plus `IUnitOfWork`.
  - **CQRS:**
    - Commands (`RegisterUser`, `Login`, `RefreshToken`, `Logout`, `UploadDocument`, `DeleteDocument`, `ProcessDocument`) go through repositories and aggregates.
    - Queries (`GetDocuments`, `GetDocumentDetails`, `GetDocumentStats`, `DownloadDocument`) use `IReadDbContext` with `AsNoTracking` and project directly to DTOs, without loading aggregates.
    - One shared database with no separate read store (justified in an ADR).
    - `ICommandHandler<TCommand,TResult>` and `IQueryHandler<TQuery,TResult>` with decorators for FluentValidation and logging, registered via Scrutor.
    - Business errors return `Result<T>` / `Error` instead of throwing, mapped to ProblemDetails.
- **Messaging:** MassTransit **v8** (OSS; v9 is commercial) with RabbitMQ, EF Core transactional outbox/inbox, retry, redelivery and a DLQ (`_error` queue).
- **AI:** Anthropic Claude via the official `Anthropic` NuGet package. The model is configurable and defaults to the lightest one, `claude-haiku-4-5-20251001`. Structured output uses forced tool use with a JSON schema. Without `ANTHROPIC_API_KEY`, a deterministic `FakeDocumentAnalyzer` runs and a warning is logged.
- **External API:** **Nager.Date** (`https://date.nager.at/api/v3/PublicHolidays/{year}/{countryCode}`, no key required).
  - Split of responsibility: the AI **only identifies** dates and their meaning. It returns a `type` (StartDate, EndDate, EffectiveDate, SigningDate, PaymentDeadline, TerminationDeadline, ExpiryDate or Other) and a `description`, e.g. "Invoice payment due date".
  - The backend's `IDateInsightsService` does the calendar check deterministically, not the AI. It reports whether a date falls on a weekend or a public holiday (with the holiday name) and gives the next business day.
  - The country comes from config (`Holidays:CountryCode`, default `PL`). Holidays are cached per (year, country).
  - A document with no dates gets an empty section ("No dates detected") and no API call is made. Many dates mean one call per distinct year.
- **Auth:** ASP.NET Core Identity (core, `AddIdentityCore`) with custom JWT endpoints. Refresh tokens are stored in a table with hashing, rotation and reuse detection, and are revoked on logout.
- **Tokens in the UI:** the access token is kept in memory and the refresh token in an httpOnly cookie (`SameSite=Strict`, path `/api/auth`). The Vite proxy keeps both on the same origin.
- **PDF:** PdfPig. TXT files are read directly as UTF-8.
  - Uploads are limited to 10 MB and validated by extension, content type and magic bytes (`%PDF-`).
  - A PDF with no text layer ends as `Failed` with the reason "no extractable text".
- **File storage:** **SeaweedFS** (Apache 2.0) through its S3 API, accessed with `AWSSDK.S3` behind `IFileStorage`, so any S3-compatible store can replace it. MinIO was dropped because its container images are no longer published.
- **Frontend:** React + Vite + TypeScript, React Router, TanStack Query, Tailwind, `@microsoft/signalr`.
- **Optional features in scope:** retry/DLQ, health checks, Serilog, GitHub Actions CI, pagination, rate limiting, SignalR, and OpenTelemetry with the **Aspire Dashboard** container.
- **Tests:** xUnit v3 on Microsoft.Testing.Platform (opted in via `global.json`), NSubstitute, Shouldly. Integration tests use `WebApplicationFactory` with Testcontainers (Postgres, RabbitMQ, SeaweedFS) and a fake AI.
- **Running locally:**
  - `docker compose up` starts the infrastructure: Postgres, RabbitMQ, SeaweedFS and the Aspire Dashboard.
  - The API, the Worker and Vite run locally.
  - The `full` profile also builds and runs api, worker and ui.

## Repository layout
```
DocumentIntelligence.slnx
Directory.Build.props / Directory.Packages.props (central package management)
.editorconfig, .gitignore, .env.example, global.json
docker-compose.yml
src/
  DocumentIntelligence.Domain/          aggregates, value objects, domain events (no dependencies)
  DocumentIntelligence.Application/     command/query handlers, ports, validators, DTOs
  DocumentIntelligence.Infrastructure/  EF Core, Identity, JWT, SeaweedFS, MassTransit, Claude, Nager.Date, PdfPig
  DocumentIntelligence.Contracts/       integration messages (DocumentUploaded, DocumentStatusChanged)
  DocumentIntelligence.Api/             Minimal API endpoints, SignalR hub, middleware
  DocumentIntelligence.Worker/          MassTransit consumer host
  DocumentIntelligence.ServiceDefaults/ shared Serilog, OpenTelemetry, health checks
frontend/                               Vite React TS
tests/
  DocumentIntelligence.UnitTests/
  DocumentIntelligence.IntegrationTests/
docs/ architecture.md, decisions.md (ADRs), ai-development.md
.github/workflows/ci.yml
README.md
```

## Data model (PostgreSQL, EF Core, migrations in Infrastructure)
- `Users`: Identity's `AspNetUsers`, renamed to `Users`.
- `RefreshTokens`: UserId, TokenHash, ExpiresAt, RevokedAt, ReplacedByHash.
- `Documents`: Id, OwnerId, FileName, ContentType, SizeBytes, StorageKey, Status (Pending/Processing/Completed/Failed), FailureReason, UploadedAt, ProcessedAt, with `xmin` as the concurrency token.
- `DocumentAnalyses` (1:1 with `Documents`): DocumentType, Summary, Entities (jsonb), ImportantDates (jsonb, including the `CalendarCheck` or null when the API was unavailable), FinancialInformation (jsonb), PotentialRisks (jsonb), Model, CreatedAt.
- MassTransit outbox/inbox tables: `OutboxMessage`, `OutboxState`, `InboxState`.

## Flow and reliability
1. **Upload** (`POST /api/documents`, multipart):
   - Validate the file, then store it in SeaweedFS at `users/{userId}/{docId}`.
   - In one transaction, save `Document(Pending)` and publish `DocumentUploaded` through the outbox.
   - If the DB write fails, delete the blob (compensation).
   - Return `202 Accepted` with a Location header.
2. **Worker** (`DocumentUploadedConsumer`):
   - Idempotency: the MassTransit inbox plus a conditional `Pending → Processing` transition (concurrency via `xmin`). A `Completed` document is skipped. A deleted document is acked and skipped.
   - Download the file from SeaweedFS and extract the text (`ITextExtractor` per content type), truncated to a token budget.
   - `IDocumentAnalyzer` (Claude): forced `record_analysis` tool with a JSON schema, validated by FluentValidation (`AnalysisResultValidator`). An invalid response gets one retry that includes the validation errors, then the document is marked `Failed`.
   - `IDateInsightsService` with `IPublicHolidayProvider` (Nager.Date typed HttpClient with resilience and IMemoryCache per year/country). Weekends are computed locally. A Nager.Date failure does **not** fail the document: dates are saved with `CalendarCheck = null`, a warning is logged, and the UI shows "Calendar check unavailable".
   - Save the analysis, mark the document `Completed`, and publish `DocumentStatusChanged` through the outbox.
3. **Transient failures** (AI 429/5xx, network):
   - `Microsoft.Extensions.Http.Resilience` on the HttpClients, plus MassTransit `UseMessageRetry` (exponential) and `UseDelayedRedelivery`.
   - Once retries are exhausted, a `Fault<DocumentUploaded>` consumer sets `Failed` and the message lands in the `_error` queue (DLQ).
   - Permanent errors (no text, unsupported file) fail the document immediately, without retries.
4. **Worker crash:** RabbitMQ redelivers the unacked message. A document stuck in `Processing` can be reclaimed once its last update is older than a timeout.
5. **Real-time:** the API consumes `DocumentStatusChanged` and pushes it through `DocumentsHub` to the `user:{id}` group. The hub reads the JWT from the query string. The UI invalidates the affected TanStack Query queries.

## API (Minimal API, OpenAPI + Scalar)
- `POST /api/auth/register | login | refresh | logout`, `GET /api/auth/me`
- `GET /api/documents?page=&pageSize=&status=` (paginated), `GET /api/documents/{id}`, `GET /api/documents/{id}/download` (streamed from SeaweedFS), `DELETE /api/documents/{id}` (DB + blob), `GET /api/documents/stats` (dashboard)
- Ownership checks live in the handlers: queries are filtered by `OwnerId`, and another user's document returns **404** so its existence is not leaked.
- OpenAPI document (`Microsoft.AspNetCore.OpenApi`) with the **Scalar UI** at `/scalar` for browsing and trying the API, including a JWT bearer security scheme. Enabled in Development and in the `full` Docker profile.
- ProblemDetails and a global exception handler.
- Rate limiting: fixed window on auth, token bucket on upload.
- Health checks: `/health/live` and `/health/ready` (Postgres, RabbitMQ, SeaweedFS).
- A 10 MB upload limit in Kestrel/FormOptions, plus validation in the handler.

## Frontend (`frontend/`)
- **Pages:**
  - Login and Register.
  - Dashboard: stats and recent documents.
  - Documents: paginated list.
  - Upload: drag and drop with client-side validation.
  - Document Details: info, status, summary, entities, financial information, risks, download and delete. Dates are shown with their type, meaning and "Weekend" / "Public holiday: …" / "Next business day: …" badges.
- `api/client.ts`: a fetch wrapper holding the in-memory access token. On a 401 it runs a single, deduplicated `refresh` and retries the request. On app start it restores the session by calling refresh.
- `AuthProvider` + `ProtectedRoute`, and a `useDocumentStatusHub` hook for SignalR.
- Vite proxy: `/api` and `/hubs` → API.

## Observability
`ServiceDefaults.AddServiceDefaults()` sets up:
- Serilog: console output, JSON format, TraceId enrichment.
- OpenTelemetry traces, metrics and logs sent over OTLP to the Aspire Dashboard (`:18888`).
- Instrumentation for ASP.NET Core, HttpClient, EF Core/Npgsql and MassTransit (`MassTransit` ActivitySource).
- Custom metrics: `documents.processed`, `ai.analysis.duration`.

## Docker
- `docker-compose.yml` runs postgres:17, rabbitmq:4-management, chrislusf/seaweedfs (S3 API, credentials in `docker/seaweedfs/s3.json`; the app creates the bucket on startup) and aspire-dashboard.
- The `full` profile adds api, worker and ui (nginx serves the UI build and proxies to the API).
- Multi-stage Dockerfiles live in `src/…Api`, `src/…Worker` and `frontend`.
- Config comes from `.env` (copied from `.env.example`). Local secrets live in `dotnet user-secrets` or `.env`.
- The API applies migrations on startup in Development.

## Tests
- **Unit:**
  - The `Document` aggregate (valid and invalid transitions, raised domain events) and the value objects.
  - The handler validation decorator.
  - `AnalysisResultValidator`.
  - Upload validation (size, type, magic bytes).
  - `JwtTokenService` and refresh-token rotation.
  - `DocumentUploadedConsumer`: idempotency, permanent vs transient errors, and a Nager.Date failure still ending `Completed` without a CalendarCheck.
  - `DateInsightsService`: weekend, holiday, next business day skipping a weekend plus a holiday, no dates → no API call, multiple years → one call per year.
  - `NagerDateHolidayProvider`, using a fake HttpMessageHandler.
  - `ClaudeDocumentAnalyzer`: tool_use parsing.
- **Integration:**
  - Auth: register → login → refresh → logout.
  - Authorization: 401 without a token, and user B gets 404 for user A's document.
  - Processing: upload → worker (in-process MassTransit, fake analyzer) → `Completed` → GET analysis → download → delete.
- **CI** (`.github/workflows/ci.yml`): `dotnet build/test` (Testcontainers runs on ubuntu-latest) and `npm ci && npm run lint && npm run build` for the frontend.

## Implementation order
0. Housekeeping: save an English feedback memory ("all project files, plans and memory in English; user writes in Polish"), then copy this plan to `.claude/plans/implementation-plan.md` in the repo (committed; the source of truth from then on).
1. Skeleton: solution, projects, CPM, `.gitignore`, docker-compose with the infrastructure, ServiceDefaults.
2. Domain (aggregates, value objects, domain events), CQRS building blocks (handler interfaces, decorators, `Result`), Infrastructure (DbContext, Identity, migration), Auth (JWT, refresh, endpoints) and auth tests. Refresh-token rotation and reuse detection are tested end to end against Postgres (they rely on `xmin`), not with unit tests.
3. Documents: EF mapping of `Document`/`DocumentAnalysis` and its migration, domain event dispatch interceptor, `IFileStorage` (SeaweedFS), upload/list/details/download/delete/stats, outbox, and tests.
4. Worker: consumer, text extraction, `IDocumentAnalyzer` (Claude + Fake), validation, Nager.Date date insights, retry/DLQ/Fault, and tests.
5. SignalR, rate limiting, health checks, OpenTelemetry with the Aspire Dashboard.
6. Frontend: auth flow, dashboard, upload, list, details, SignalR.
7. Dockerfiles, the `full` profile, CI.
8. Documentation:
   - README: overview, mermaid diagram, stack, running, configuration, design decisions.
   - `docs/architecture.md` and `docs/decisions.md` (ADRs).
   - `docs/ai-development.md`: prompts and decisions from this session. Examples:
     - MediatR and MassTransit v9 rejected because of their licenses.
     - Cookie vs localStorage for the refresh token.
     - Nager.Date chosen over a currency API, with the "AI identifies, backend verifies" split.
     - Haiku as the lightest sufficient model.

Commit to `master` after each step.

## Verification
- Run `docker compose up -d`, then `dotnet run` for the Api and the Worker, and `npm run dev` in `frontend/`.
- End-to-end in the browser:
  - Register, log in and upload a PDF/TXT.
  - Status updates live: Pending → Processing → Completed.
  - Details show the analysis and calendar badges, e.g. a contract deadline on 11 Nov shows "Public holiday: National Independence Day".
  - Download, then delete.
- Without an Anthropic key the fake analyzer runs. With a key, real Claude responds.
- Failure scenarios:
  - Stop the Worker mid-processing (the message is redelivered).
  - An invalid file returns 400.
  - A scanned PDF ends as `Failed`.
  - A wrong Nager.Date URL in config still ends `Completed`, with "Calendar check unavailable".
  - Messages in `_error` are visible in the RabbitMQ UI (`:15672`).
- Traces API → RabbitMQ → Worker → Claude/Nager.Date are visible in the Aspire Dashboard (`:18888`).
- `dotnet test` passes (requires Docker) and the CI pipeline is green.
- `docker compose --profile full up --build` runs everything without a local SDK.
