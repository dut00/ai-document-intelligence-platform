# Architecture decision records

Each record is short: the context, the decision, what it costs, and what else was considered. All of them are **accepted**. The later ones were made while the system was being built, when a problem showed up.

| # | Decision |
| --- | --- |
| [001](#001-clean-architecture-with-ddd-one-aggregate) | Clean Architecture with DDD, and one aggregate |
| [002](#002-own-cqrs-handlers-instead-of-mediatr) | Own CQRS handlers instead of MediatR |
| [003](#003-one-database-for-commands-and-queries) | One database for commands and queries |
| [004](#004-masstransit-8-with-rabbitmq-and-the-ef-core-outbox) | MassTransit 8 with RabbitMQ and the EF Core outbox |
| [005](#005-in-process-exponential-retries-no-delayed-redelivery) | In-process exponential retries, no delayed redelivery |
| [006](#006-processing-is-never-committed-on-its-own) | `Processing` is never committed on its own |
| [007](#007-claude-with-forced-tool-use-validation-and-one-correction) | Claude with forced tool use, validation and one correction |
| [008](#008-haiku-as-the-default-model-and-a-fake-analyzer) | Haiku as the default model, and a fake analyzer |
| [009](#009-the-ai-identifies-dates-the-backend-checks-them) | The AI identifies dates, the backend checks them |
| [010](#010-text-extraction-with-pdfpig-no-ocr) | Text extraction with PdfPig, no OCR |
| [011](#011-seaweedfs-as-the-object-store) | SeaweedFS as the object store |
| [012](#012-identity-core-with-own-jwt-endpoints) | Identity (core) with own JWT endpoints |
| [013](#013-access-token-in-memory-refresh-token-in-an-httponly-cookie) | Access token in memory, refresh token in an httpOnly cookie |
| [014](#014-refresh-token-rotation-with-reuse-detection-and-a-grace-period) | Refresh-token rotation with reuse detection and a grace period |
| [015](#015-signalr-without-a-backplane) | SignalR without a backplane |
| [016](#016-files-are-deleted-after-the-commit) | Files are deleted after the commit |
| [017](#017-aspire-dashboard-for-telemetry) | Aspire Dashboard for telemetry |
| [018](#018-integration-tests-against-real-infrastructure) | Integration tests against real infrastructure |
| [019](#019-infrastructure-in-docker-apps-on-the-host-plus-a-full-profile) | Infrastructure in Docker, apps on the host, plus a `full` profile |
| [020](#020-one-origin-behind-nginx-with-trusted-forwarded-headers) | One origin behind nginx, with trusted forwarded headers |
| [021](#021-abuse-and-cost-limits-from-the-security-audit) | Abuse and cost limits, from the security audit |

---

## 001. Clean Architecture with DDD, one aggregate

**Context.** The project is meant to show how a maintainable backend is structured. The core logic, the document lifecycle, is small but has real rules.

**Decision.**
- Layers: Domain, Application, Infrastructure, and the hosts. A test checks that the inner layers (Domain, Contracts, Application) reference nothing outward.
- `Document` is the only aggregate root. It owns `DocumentAnalysis` and guards its status transitions.
- User accounts belong to ASP.NET Core Identity in Infrastructure. The domain refers to users only by `UserId`, so no `User` entity duplicates Identity.

**Consequences.** There are more projects and mapping code than in a single-project API. In return, the rules live in one place, and the domain has no framework dependencies and is trivial to unit-test.

**Alternatives.**
- Vertical slices: a good fit for feature-heavy apps, and less telling for this goal.
- A domain `User` aggregate: it would only mirror Identity.

## 002. Own CQRS handlers instead of MediatR

**Context.** MediatR switched to a commercial license. The project needs only in-process dispatch with a couple of cross-cutting behaviors.

**Decision.**
- `ICommandHandler<TCommand, TResult>` and `IQueryHandler<TQuery, TResult>`, with `ValidationDecorator` and `LoggingDecorator` registered through Scrutor.
- Handlers return `Result<T>`. Expected business errors are values, not exceptions, and the API maps them to ProblemDetails.

**Consequences.** There is no mediator indirection: endpoints inject the handler they call, and there is no request/response pipeline to learn. Adding a new cross-cutting behavior takes one more decorator.

**Alternatives.**
- MediatR: its license.
- Plain application services: they give no uniform place for validation and logging.

## 003. One database for commands and queries

**Context.** The read side is lists, details and counts of the user's own documents.

**Decision.** Commands load aggregates through the repository. Queries use `IReadDbContext` with `AsNoTracking` and project straight to DTOs. Both use one PostgreSQL database.

**Consequences.** Reads are always consistent, and there is nothing to synchronize. The Application layer references EF Core for query projections (`ToListAsync` and the like). The layer test checks only project references, so this package reference is a deliberate, documented exception rather than an enforced one.

**Alternatives.** A separate read store or projections: more infrastructure and eventual consistency, without a real problem to solve.

## 004. MassTransit 8 with RabbitMQ and the EF Core outbox

**Context.** Uploading and processing must not lose work. A document must never be saved without its message, and a message must never be sent for a document that was not saved.

**Decision.**
- MassTransit 8 on RabbitMQ.
- The EF Core bus outbox in the API, and the consumer outbox and inbox in the Worker, in the same transaction as the business data.
- Kebab-case endpoints with `_error` queues as the dead-letter queues.

**Consequences.** Delivery is at least once, and duplicates are handled by the inbox and by idempotent handlers ([006](#006-processing-is-never-committed-on-its-own)). The outbox tables live in the application database. Messages leave with a short delay, which is also why the tests poll for them.

**Alternatives.**
- MassTransit 9: commercial.
- `RabbitMQ.Client` with a hand-written outbox: a lot of subtle code.
- Wolverine: capable, but MassTransit is the more established choice, with a mature EF Core outbox.

## 005. In-process exponential retries, no delayed redelivery

**Context.** Transient errors such as AI rate limits or network failures deserve backoff. On RabbitMQ, MassTransit's delayed redelivery needs the delayed-message-exchange plugin, which the stock image does not include.

**Decision.** `UseMessageRetry` with exponential intervals (`Messaging:Retry`, by default 5 retries from 1 s to 30 s), on top of the Anthropic SDK's own retries. Once the retries run out, a `Fault<DocumentUploaded>` consumer marks the document `Failed`.

**Consequences.** A retrying message keeps its consumer slot for the whole sequence: the backoff adds about a minute on top of the attempts themselves, and each attempt can include a slow Claude call. That is acceptable at this scale, and it works with a stock broker.

A crash of the Worker process never reaches this policy. The endpoints therefore use quorum queues, which count unacknowledged deliveries. A redelivered document is handed to an isolated endpoint that processes one document at a time in a process of its own, and is failed there after 3 interrupted deliveries, instead of killing Workers forever ([021](#021-abuse-and-cost-limits-from-the-security-audit)).

**Alternatives.** Delayed redelivery with a custom RabbitMQ image, or a scheduler (Quartz or Hangfire): more moving parts.

## 006. `Processing` is never committed on its own

**Context.** Saving `Processing` before the AI call would hold a row lock inside the consumer's transaction for the whole call, which can take seconds or minutes, and block a concurrent delete. Committing it separately instead would leave a half-finished state behind a crash, which redelivery then has to untangle.

**Decision.**
- The whole attempt runs inside the consumer's outbox transaction. `StartProcessing()` changes the state in memory, and only the final `Completed` or `Failed` is saved.
- The `xmin` concurrency token rejects the save if the document changed or was deleted meanwhile, and the retry then skips it.
- A document stuck in `Processing` for 15 minutes counts as stale and can be picked up again.

**Consequences.** Other readers never see `Processing`. The UI shows "In progress" for anything not finished, and SignalR pushes only final statuses ([015](#015-signalr-without-a-backplane)).

**Alternatives.** Commit `Processing` early and add a lease or heartbeat: more state to get wrong, and it does not show more.

## 007. Claude with forced tool use, validation and one correction

**Context.** The analysis feeds a typed UI and the database, so free-form text is not enough. Models occasionally return malformed or out-of-range values.

**Decision.**
- Force the `record_analysis` tool (`tool_choice`). Its JSON schema is the output contract, and its enums are generated from the domain.
- Validate the input with FluentValidation. An invalid answer goes back once as an `is_error` tool result listing the errors; a second invalid answer fails the document.
- The system prompt marks the document as untrusted data, and closing tags inside the text are defused.
- The model does not compute anything, such as dates or business days.
- No `temperature` is sent, because newer models accept only the default.

**Consequences.** The output is always schema-shaped and validated before it reaches the domain. One correction round costs one extra request, only when needed.

**Alternatives.**
- Free-text JSON in the prompt: it breaks more often.
- Unlimited correction loops: an unbounded cost.

## 008. Haiku as the default model, and a fake analyzer

**Context.** Structured extraction from business documents does not need the largest model. Cost and latency matter for every upload, and the project must run without a key: for reviewers, in CI and in tests.

**Decision.**
- The default model is `claude-haiku-4-5-20251001`, configurable through `Anthropic:Model`.
- The input is capped at 60,000 characters, about 15k tokens.
- Without an API key, a deterministic `FakeDocumentAnalyzer` runs (regular expressions for dates and amounts) and logs a warning.

**Consequences.** Anyone can clone the project and run the whole pipeline offline. The analysis quality then depends on the fake, which is clearly flagged. Switching to a larger model is a configuration change.

**Alternatives.** Requiring a key: nobody could try the project without paying.

## 009. The AI identifies dates, the backend checks them

**Context.** The project needed an external API next to the AI. An exchange-rate API (NBP) was considered first. A public-holiday calendar fits general documents better, because deadlines matter in contracts, invoices and letters alike.

**Decision.**
- The AI returns only the date, its type (payment deadline, termination deadline, …) and its meaning.
- `DateInsightsService` decides in code whether a date falls on a weekend or a public holiday (Nager.Date, no key needed), and which day is the next business day.
- Holidays are cached per year and country (`Holidays:CountryCode`, default `PL`).
- A calendar outage never fails a document: the dates are saved without a check, and the UI says so.

**Consequences.** The facts in the result are deterministic and testable, and each call does what it is good at. The trade-off is one outbound HTTP dependency, handled with resilience, a cache and graceful degradation.

**Alternatives.**
- Asking the model about holidays: it can be wrong, and it cannot be verified.
- An exchange-rate API: less useful for non-financial documents.

## 010. Text extraction with PdfPig, no OCR

**Context.** Uploads are PDF or TXT, up to 10 MB.

**Decision.**
- PdfPig extracts the PDF text layer, and TXT is read as UTF-8.
- A PDF without text (a scan) fails with "no extractable text".
- Uploads are validated by extension, content type and magic bytes (`%PDF-`).

**Consequences.** Extraction is cheap, local and predictable. Scanned documents are out of scope.

**Alternatives.** Sending the PDF itself to Claude, or OCR: more cost and more complexity, for a case the scope excludes.

## 011. SeaweedFS as the object store

**Context.** The files need S3-compatible storage that runs locally in Docker. MinIO, the usual choice, no longer publishes container images.

**Decision.** SeaweedFS (Apache 2.0), used through its S3 API with `AWSSDK.S3`, behind `IFileStorage`. The bucket is created on startup.

**Consequences.** Any S3-compatible service, including AWS S3, can replace it through configuration alone.

**Alternatives.** MinIO: no images. The local file system: not realistic for several instances.

## 012. Identity (core) with own JWT endpoints

**Context.** The SPA needs register, login, refresh and logout, with a refresh token the browser's JavaScript cannot read.

**Decision.**
- `AddIdentityCore` provides password hashing and user storage. Its account lockout is off: a lockout lets anyone lock a known email out. Failed logins are throttled per client IP address and account instead ([021](#021-abuse-and-cost-limits-from-the-security-audit)).
- JWT access tokens are issued by `JwtAccessTokenIssuer`, and refresh tokens are handled by `RefreshTokenStore`. There are no roles.

**Consequences.** There is full control over token lifetimes, rotation and the cookie, at the cost of owning that code and its tests.

**Alternatives.**
- `MapIdentityApi`: opaque tokens, and no httpOnly refresh cookie.
- A custom user entity with `PasswordHasher`: it re-implements Identity.
- An external identity provider: out of scope.

## 013. Access token in memory, refresh token in an httpOnly cookie

**Context.** Tokens in `localStorage` can be read by any script that runs on the page, so a single XSS bug leaks the long-lived session.

**Decision.**
- The access token (15 min) lives only in JavaScript memory.
- The refresh token (7 days) lives in an httpOnly, `Secure`, `SameSite=Strict` cookie with path `/api/auth`.
- After a reload, the app restores the session by calling `refresh`.
- The dev server (Vite) and production (nginx) serve the UI and the API from one origin.

**Consequences.** Script injection can still act while the page is open, but cannot steal a long-lived credential. `SameSite=Strict` with a narrow path limits CSRF to the refresh endpoint, which returns a token only to the page itself.

**Alternatives.** Both tokens in `localStorage`: simpler, but it trades away the main protection.

## 014. Refresh-token rotation with reuse detection and a grace period

**Context.** A stolen refresh token should be discovered. Normal browsers must not trip the detection, though: several tabs refresh at once, and a page reloads while a refresh is in flight and never stores the rotated cookie.

**Decision.**
- **Rotation.** Every refresh revokes the presented token and issues a successor. `xmin` makes sure only one parallel refresh wins.
- **Reuse detection.** A rotated token that comes back revokes every session of the user.
- **Grace period** (`Jwt:RefreshTokenReuseGracePeriod`, 10 s). A token rotated within the window whose successor is still unused is accepted once more, and the successor is rotated in its place.
- **Logout** follows grace rotations to the live token. Logging out with a token that someone else rotated outside the window counts as reuse.
- **In the UI,** refreshes are deduplicated within a tab and serialized across tabs with the Web Locks API.

**Consequences.** A copy used within those 10 seconds goes undetected. Two parallel refreshes whose responses arrive out of order could leave an already-rotated cookie behind; the UI's serialization prevents that.

**Alternatives.**
- No rotation: theft goes unnoticed for 7 days.
- Strict rotation without a window: real users get logged out everywhere.

## 015. SignalR without a backplane

**Context.** Status changes happen in the Worker, but browsers are connected to an API instance, and there may be several of those.

**Decision.**
- The Worker publishes `DocumentStatusChanged`. Every API instance consumes it on its own temporary queue (exclusive and auto-deleted) and pushes it to the group `user:{id}`.
- That endpoint skips the inbox, the outbox and retries, and discards faults.
- Only final statuses are pushed.
- On the hub path only, the token is accepted from the query string. Connections close when the token expires.

**Consequences.** It scales out without a Redis backplane. A missed push only delays the UI until its next refetch, which also happens after every reconnect.

**Alternatives.** A Redis or Azure SignalR backplane with competing consumers: more infrastructure for the same result.

## 016. Files are deleted after the commit

**Context.** Deleting the blob in the request can lose the file of a document whose delete then rolls back. Deleting it before the commit races with the Worker, which may still be reading it.

**Decision.** `MarkForDeletion()` raises an event that puts `DocumentDeleted` in the outbox. The Worker deletes the object after the row is gone. Deleting a missing object succeeds, so redelivery is harmless.

**Consequences.** A file is never deleted for a document that still exists, but it may outlive its row by moments. A worker that later receives the document's upload message finds no row, then acknowledges and skips it.

**Alternatives.** A synchronous delete with compensation: it cannot be made atomic across the database and S3.

## 017. Aspire Dashboard for telemetry

**Context.** OpenTelemetry was requested with a simple dashboard in a container.

**Decision.** The standalone Aspire Dashboard receives OTLP traces, metrics and logs from both hosts, with custom metrics for processing.

**Consequences.** One container gives one view of a request across the API, RabbitMQ, the Worker, Claude and Nager.Date. It keeps data in memory only, which is fine for development. Production would point the same OTLP exporter elsewhere.

**Alternatives.**
- Grafana LGTM (`otel-lgtm`): more capable, and heavier.
- Jaeger: traces only.
- A Collector with Prometheus and Grafana: much more setup.

## 018. Integration tests against real infrastructure

**Context.** Much of the behavior lives in PostgreSQL (`xmin` concurrency, jsonb, the outbox tables) and in S3. An in-memory database would pass tests that fail in production.

**Decision.**
- `WebApplicationFactory` hosts the real API, with PostgreSQL and SeaweedFS started by Testcontainers and shared by all tests.
- MassTransit's in-memory test harness replaces RabbitMQ and runs the Worker's consumers in-process.
- The AI is the fake, and holidays come from a stub.
- xUnit v3 runs on Microsoft.Testing.Platform.

**Consequences.** The tests need Docker, both locally and in CI (ubuntu runners provide it). They exercise real SQL, real concurrency and real S3 calls. Refresh-token rotation is tested only this way, because it depends on `xmin`.

**Alternatives.** The EF in-memory provider or SQLite: they behave differently where it matters.

## 019. Infrastructure in Docker, apps on the host, plus a `full` profile

**Context.** Development needs fast edit-run cycles and a debugger. Reviewers want a single command.

**Decision.**
- `docker compose up` starts Postgres, RabbitMQ, SeaweedFS and the Aspire Dashboard, and the API, the Worker and Vite run on the host.
- `docker compose --profile full up --build` builds and runs everything from source, the API in the Production environment with migrations and Scalar switched on explicitly.

**Consequences.** There are two configurations to keep in step: the Development appsettings for local runs, and the compose environment for containers. `.env.example` documents the shared values.

**Alternatives.** A .NET Aspire AppHost: attractive, but it hides the plain Docker setup this project wants to show.

## 020. One origin behind nginx, with trusted forwarded headers

**Context.** In containers, nginx serves the UI and proxies the API, so every request reaches the API from nginx's address. That would put every user in one per-IP rate-limit bucket.

**Decision.**
- nginx overwrites `X-Forwarded-For` with the client address.
- The API reads it only from the proxies or networks listed in `ForwardedHeaders`, and only the last hop.
- In the `full` profile the API has no published port, so trusting Docker's private address ranges is safe.
- nginx also leaves query strings out of its access log (the hub's token), applies CSP to the UI only, and exposes only `/health/live`.

**Consequences.** Each client keeps its own rate limit, and a client cannot spoof its address. Publishing the API port directly would require narrowing the trusted networks.

**Alternatives.**
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED`: it trusts everyone.
- A fixed compose subnet: it can collide with existing networks.

## 021. Abuse and cost limits, from the security audit

**Context.** The first whole-repository run of the `security-reviewer` agent found no critical or high issues. It did find several ways an ordinary registered user, or anyone on the same network, could cost the operator money or availability:
- the infrastructure ports were published on all interfaces, with public development credentials;
- a hostile PDF could exhaust the Worker, and a crash would be redelivered forever;
- free accounts multiplied the per-user upload limit;
- five failed logins locked any known account;
- a published JWT key was accepted in Production;
- model output could reach the logs, and could drive dozens of calendar requests per document;
- the document delimiter was defused only in its exact spelling;
- a huge page number caused a 500.

**Decision.**
- **Network and keys.** Every published port is bound to `127.0.0.1`. `.env.example` leaves `JWT_SIGNING_KEY` empty, and outside Development the API refuses to start with any key published in the repository (`JwtOptions.PublishedSigningKeys`).
- **Hostile files.**
  - Text extraction stops after 500 pages or once past the 60,000-character budget, and gives up after one minute (the document fails, with no retry). The parser cannot be interrupted inside a page, so it runs on its own thread and the Worker stops waiting for it; the abandoned parse ends at its next page check.
  - The Worker containers have memory (1 GB) and CPU limits, a Worker processes (and prefetches) at most 4 documents at a time, and at most 8 PDF parses (abandoned ones included) run per process; a parse still running 2 minutes after being abandoned makes the Worker exit so its container restarts.
  - Quorum queues count unacknowledged deliveries. A crash returns every message the Worker held, the innocent ones included, so a redelivered document is handed to a separate endpoint that processes one at a time, in a Worker process of its own (`Worker:Role`, the `worker-isolated` container). Only there, after 3 interrupted deliveries, is a document failed.
- **Cost.**
  - Registration is limited to 10 per IP address per day.
  - A user keeps at most 200 documents (`Documents:MaxDocumentsPerUser`).
  - At most 20 documents per user and 500 across all users are analyzed in any 24 hours; with the registration limit, one address can use at most 200 of those (`Documents:DailyAnalysisLimitPerUser`, `Documents:DailyAnalysisLimit`). Beyond that, a document fails with a request to upload it again later.
  - The caps count an append-only usage log, which deleting documents does not shrink, and which includes analyses that failed after invalid answers.
  - Only the 5 years closest to today are checked against the holiday calendar.
- **Logins.** The Identity lockout is replaced by `LoginAttemptThrottle`.
  - Each attempt is counted before the password is checked, so concurrent guesses cannot slip past it. After 5 attempts without a success for an account from one IP address within 15 minutes, that address is refused; the owner, on another address, is not affected.
  - The key is a hash of the email as Identity normalizes it, so Unicode look-alikes share a budget and no attacker-sized string is kept.
  - IPv6 clients are counted per /64 prefix, here and in the rate limits.
  - Across all addresses, an account gets 50 attempts without a success per 15 minutes; beyond that each attempt is delayed by 3 seconds rather than refused, so the owner can still sign in.
- **AI output.** Any tag variant that could open or close the `<document>` block is defused with a non-backtracking regular expression (linear time on hostile input). Logs record only where the analysis was invalid, not the values.
- **Input.** `page` has an upper bound, so the row offset cannot overflow.

**Consequences.**
- The limits are approximate: two uploads, or two Workers, at the same moment can both pass. They bound cost and storage; they are not accounting.
- The login throttle and the rate limits live in each API instance's memory, so N instances allow N times the attempts.
- Existing local RabbitMQ volumes still hold the classic queues created before this change. They must be deleted once (or the volume recreated, see the README's "Upgrading an existing setup"), because a queue's type cannot change.
- A deploy or restart counts as an interruption only if it kills a Worker mid-document: Workers finish the documents in hand on shutdown (2.5 minutes), and fetch no more than they process. A broker connection drop still counts.
- A process-level crash inside the PDF parser (e.g. a stack overflow) still kills the Worker, but now only 3 times per document, and documents that were merely in flight are processed again rather than failed.
- An abandoned PDF parse keeps a thread busy until its next page check. At most 8 parses run at once per Worker process (more PDFs wait, without that wait counting against their timeout), and a stuck one restarts the Worker: documents in flight then take the isolated path. The containers have CPU and memory limits.
- Without a lockout, an attacker with very many addresses can still guess in parallel: the account-wide delay slows each attempt, not the number running at once. A stronger defense (MFA, CAPTCHA) is out of scope.
- A busy user can exhaust their own daily allowance, and many accounts from many addresses can still exhaust the overall one: that is the price of a hard cost ceiling.

**Alternatives.**
- Running the PDF parser in a separate sandboxed process: stronger isolation, much more complexity.
- A distributed store (Redis) for the rate limits and the login throttle: needed only with several API instances.
- Email verification before an account can upload: out of scope ([SPEC](../SPEC.md)).
