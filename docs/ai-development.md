# AI-assisted development

The whole project was built in conversation with Claude Code: the plan, the code, the tests, the Docker setup and these documents. The author steered in Polish; every file in the repository is in English. This page describes the workflow, the prompts that drove it, the decisions made along the way, and what the automated review caught.

## Workflow

1. **Clarifying questions before a plan.** The first prompt asked Claude to ask questions before planning anything, a few at a time:
   > I want to build an application […]. Stack: .NET 10 Web API, PostgreSQL (Docker), React + Vite + TypeScript. Before you plan anything, ask me clarifying questions (architecture, repo structure, tests, how to run locally). Ask several at a time.

   Several rounds of multiple-choice questions followed, each option with a recommendation. The answers are summarized [below](#decisions-made-while-planning).
2. **A plan as the source of truth.** The agreed plan lives in [`.claude/plans/implementation-plan.md`](../.claude/plans/implementation-plan.md). It is committed and was updated whenever a step changed a decision. A one-page [`SPEC.md`](../SPEC.md) summarizes it.
3. **`CLAUDE.md` as standing instructions.** It holds the commands, the structure, the naming conventions and one rule: after every change, build and run all tests, and fix everything before finishing. The conventions are enforced by `.editorconfig` as build errors wherever an analyzer can check them.
4. **One step per prompt, one commit per step.** The implementation followed the plan's order, each step started with a short prompt ("go to step 3 of the plan"), and it was committed only after the checks below.
5. **A check skill and a reviewer agent** (added after step 4, on the author's suggestion):
   - [`/precommit`](../.claude/skills/precommit/SKILL.md) builds the solution, runs all unit and integration tests, lints and builds the frontend, builds the Docker images when their inputs changed, and checks the working tree (line endings, stray files). It runs in a forked context, so its long logs stay out of the conversation.
   - The [`reviewer`](../.claude/agents/reviewer.md) agent then reviews the change read-only, with fresh context, for design, correctness and test gaps. It must back every finding with a concrete scenario.
   - Security was split out later, once the project was finished. The mechanical checks (the NuGet and npm vulnerability audits and a secret scan of the change) run in `/precommit` and in CI, and Dependabot keeps dependencies current. The judgment part went to a separate [`security-reviewer`](../.claude/agents/security-reviewer.md) agent. It has a threat-model checklist written for this repository (tokens, ownership, uploads, prompt injection, SSRF, DoS, data exposure, containers) and runs on security-relevant changes and as a whole-repository audit.
6. **Manual verification** where tests cannot reach:
   - the real stack in local Docker (RabbitMQ, Nager.Date);
   - the UI driven in Chrome, including two tabs signing in and out;
   - the `full` compose profile end to end through nginx.

## Prompts

These are the author's prompts in order, translated from Polish.

| Stage | Prompt |
| --- | --- |
| Planning | "…before you plan anything, ask me clarifying questions…" (above); "What other options are there for an OpenTelemetry dashboard?" |
| Spec | "Based on the plan, write SPEC.md: the goal, the MVP scope, what is out of scope, and the technical decisions (stack, repo structure, tests). Short, one page at most. Create nothing else." |
| Spec | "Describe it as a project for my GitHub portfolio. Also add to the plan that the API gets a Scalar UI." |
| Skeleton | "Based on SPEC.md and the plan, prepare the application skeleton." / "Why is there an empty TestResults folder in the root?" |
| Conventions | "Shorten CLAUDE.md to what matters: build/test/run commands, the directory structure, naming conventions, and the rule that after every change you run the tests and fix errors before finishing." / "Make sure the rules in .editorconfig match CLAUDE.md." |
| Conventions | "Adopt the rule 'one type per file, with two exceptions: use-case files (command + validator + handler) and an enum used by only one type', and add it to CLAUDE.md." |
| Steps 2–8 | "Go to step N of the plan." (step 4: "…but hold off on the commit") |
| Tooling | "Before every commit you test the application. Maybe that should be a skill or an agent?" / "Would we benefit from some agent?" |
| Tooling | "Run /verify and the reviewer." / "Rename our skill so it does not collide." (with the built-in `/verify`) |
| Review | "Yes, fix 1–3 and document 4." (the step-5 findings, [below](#what-the-review-loop-caught)) |
| Auth | "Add a grace period for the rotation." (after Claude flagged the reload-during-refresh problem, [ADR 014](decisions.md#014-refresh-token-rotation-with-reuse-detection-and-a-grace-period)) |

## Decisions made while planning

Claude proposed options with a recommendation. The author accepted most of them and overrode a few.

| Question | Options | Choice |
| --- | --- | --- |
| Backend architecture | Clean Architecture, vertical slices, a mix | Clean Architecture (+ DDD, CQRS) |
| Mediator | own handlers, MediatR, application services | **own handlers**: MediatR is now commercial |
| Messaging | MassTransit + outbox, RabbitMQ.Client + own outbox, Wolverine | **MassTransit, version 8**: v9 is commercial |
| AI provider | Claude, OpenAI, an abstraction over both | Claude, behind `IDocumentAnalyzer` |
| Without an API key | fake analyzer, key required | fake analyzer |
| Repository | monorepo `src/ frontend/ tests/`, `backend/ + frontend/` | monorepo |
| Branch | rename to `main`, keep `master` | **keep `master`** (author's override) |
| External API | NBP exchange rates (recommended), ECB, VAT registry, KRS | **Nager.Date public holidays, with the AI only identifying dates and their meaning** (author's own answer) |
| Frontend libraries | React Router + TanStack Query + Tailwind, + shadcn/ui, minimal | React Router + TanStack Query + Tailwind |
| Tests | unit + integration with Testcontainers, in-memory, + frontend tests | unit + Testcontainers |
| Running locally | infrastructure in Docker + apps on the host + `full` profile, all in compose, Aspire AppHost | infrastructure in Docker + `full` profile |
| Optional features | retry/DLQ/health/Serilog, CI, pagination + rate limiting, SignalR | all of them, **plus OpenTelemetry with a dashboard** (author's addition) |
| Telemetry dashboard | Aspire Dashboard, Grafana LGTM, Collector + Jaeger + Prometheus + Grafana, Jaeger | Aspire Dashboard |
| Users and JWT | Identity (core) + own JWT, own entity + `PasswordHasher`, `MapIdentityApi` | Identity (core) + own JWT |
| Tokens in the browser | access in memory + refresh in an httpOnly cookie, both in `localStorage` | memory + httpOnly cookie |
| PDF text | PdfPig, the PDF sent to Claude, PdfPig with a Claude fallback | PdfPig, 10 MB limit |
| Plan location | `docs/`, `.claude/plans/`, outside git | `.claude/plans/` |

These are the decisions the plan singled out:

- **MediatR and MassTransit v9 were rejected because of their licenses.** Both moved to commercial licensing. Replacing MediatR costs two interfaces and two decorators ([ADR 002](decisions.md#002-own-cqrs-handlers-instead-of-mediatr)). MassTransit 8 is still open source and has everything this project needs ([ADR 004](decisions.md#004-masstransit-8-with-rabbitmq-and-the-ef-core-outbox)).
- **A cookie rather than `localStorage` for the refresh token.** An XSS bug should not be able to steal a seven-day credential. The access token therefore lives only in memory, and the refresh token in an httpOnly, `SameSite=Strict` cookie scoped to `/api/auth`, on a single origin in development (Vite proxy) and in Docker (nginx) ([ADR 013](decisions.md#013-access-token-in-memory-refresh-token-in-an-httponly-cookie)).
- **Nager.Date rather than an exchange-rate API, with "the AI identifies, the backend verifies".** Claude recommended NBP exchange rates, which suit invoices. The author chose general documents and a public-holiday calendar instead, and limited the AI to finding dates and saying what they mean. Claude then pointed out that the scope also expected a summary, entities, financial information and risks. The final split: the AI does the full analysis, but on dates it only identifies them, and the weekend, holiday and next-business-day checks are deterministic code ([ADR 009](decisions.md#009-the-ai-identifies-dates-the-backend-checks-them)).
- **Haiku as the lightest sufficient model.** Extracting fields from a single document into a fixed schema does not need a large model. `claude-haiku-4-5-20251001` keeps each upload cheap and fast, the model is one setting away, and the forced tool with validation and a correction round makes up for the occasional sloppy answer ([ADR 007](decisions.md#007-claude-with-forced-tool-use-validation-and-one-correction), [ADR 008](decisions.md#008-haiku-as-the-default-model-and-a-fake-analyzer)).

Later decisions, from facts discovered during implementation:

- **SeaweedFS instead of MinIO.** MinIO no longer publishes container images, which surfaced while writing the compose file ([ADR 011](decisions.md#011-seaweedfs-as-the-object-store)).
- **No delayed redelivery.** It needs a RabbitMQ plugin the stock image lacks ([ADR 005](decisions.md#005-in-process-exponential-retries-no-delayed-redelivery)).
- **No `temperature`,** because newer models reject values other than the default.
- **Financial items are copied, never calculated.** While the README screenshots were being taken with the real model, Haiku recorded "a penalty of 1% of the monthly fee" as *PLN 1.00*, and a 10% cap as an amount it had computed itself. The tool schema now asks for figures as stated, never calculated, and leaves percentages, rates without a currency and quantities to the summary and the risks. Only a run against the real model could surface this; the fake analyzer and the validator both accepted the output.
- **`Processing` is never committed on its own** ([ADR 006](decisions.md#006-processing-is-never-committed-on-its-own)). Because of that, SignalR pushes only final statuses, a documented deviation from the plan.

## What the review loop caught

The reviewer found real problems in every step that went through it. Some examples:

**Step 5 (SignalR, rate limiting, health, telemetry)**
- **(major)** The per-instance SignalR notification queue inherited the global retry and outbox settings. Every status change would have written an inbox row in every API instance, and a short database outage would have left an orphaned `_error` queue behind each restart. Fixed: that endpoint skips the outbox and retries.
- A hub connection outlived its token's expiry and the user's logout. Fixed with `CloseOnAuthenticationExpiration`.
- The test "another user gets nothing" checked too early to prove anything. Fixed: it now waits for that user's own notification first.
- `documents.processed` can count a document twice if the commit fails after the counter was incremented. Documented rather than restructured; this was the author's call.

**Step 6 (frontend)**
- Signing in as another user in one tab left other tabs showing the previous user's data. Fixed with a `BroadcastChannel`, and a check of the token's subject after every refresh.
- A logout racing an in-flight refresh could resurrect the session. Fixed: logout waits for the refresh.
- A 429 from the refresh endpoint was treated as a sign-out, and requests were sent without a token. Fixed.
- After a sign-out, the next user was redirected to the previous user's last page. Fixed.
- A logout failure was silent, a stale page number survived a filter change, and a registration whose automatic login failed looked like a failed registration. All three fixed.

**Grace period**
- A logout carrying a token that someone else had rotated long ago was not treated as reuse. It now revokes the whole session, just like a refresh would.
- A multi-hop chain and the "no mass revocation after logout" case were untested. Tests were added.
- Repeated concurrency conflicts could turn a logout into a 500. It now retries, then gives up quietly.

**Step 7 (Docker, CI)**
- nginx's *error* log would still write the hub's access token from the query string. Lowered to `crit` on `/hubs`.
- `/health/ready`, a list of every dependency and its state, was public through nginx. Only `/health/live` is proxied now.
- The trusted-network path the compose file actually uses (`KnownNetworks`) had no test. The test fixture now trusts a network rather than a single address.

**First security audit** (the `security-reviewer` agent, whole repository)

The audit found no critical or high issues. It did find 3 medium and 6 low ones, all fixed in one change ([ADR 021](decisions.md#021-abuse-and-cost-limits-from-the-security-audit)):

- **(medium) Infrastructure ports and credentials.** The infrastructure ports were published on all interfaces, with public development credentials.
- **(medium) Hostile PDFs.** A hostile PDF could exhaust the Worker, and a crash would be redelivered forever. The fix is a quorum-queue delivery count.
  - The first implementation read the count from `context.Headers`, which in MassTransit holds the message envelope's headers. The test harness passed, but a manual run against a real RabbitMQ showed the count never arrived. It is a transport header (`ReceiveContext.TransportHeaders`).
  - The reviewer and the security reviewer then found that the guard also failed innocent documents in flight next to a crashing one, which led to the isolation endpoint.
  - The real broker caught one more problem the in-memory transport could not. A `queue:` send address made the sender declare a classic queue, which RabbitMQ refused for the quorum one, so the hand-over now goes through the endpoint's exchange.
- **(follow-up) The fixes' own review** found three more problems:
  - the daily cap could be bypassed by deleting analyzed documents (now an append-only usage log, with a per-user cap);
  - the new login throttle kept attacker-sized emails in memory (now a hash of the normalized email);
  - the defusal regex could backtrack quadratically (now non-backtracking).
- **(medium) Free accounts.** Free accounts multiplied the per-user upload limit.
- **(low) Lockout.** An account lockout that anyone could trigger.
- **(low) JWT key.** A published JWT key that Production accepted.
- **(low) Paging.** An unbounded page number.
- **(low) Delimiter.** A document delimiter defused only in its exact spelling.
- **(low) Logs.** Model output in the logs.
- **(low) Calendar calls.** Dates driving dozens of calendar calls.

## What worked, and what needed a human

- **Asking before planning paid off.** The question rounds brought up constraints a one-shot plan would have guessed wrong: the licenses, the branch name, the external API, and the date split.
- **A written plan and `CLAUDE.md` kept the context across sessions.** The conversation was compacted several times. The plan, the commits and the conventions carried the state, not the model's memory.
- **Mechanical checks beat reminders.** Naming rules became build errors, layer boundaries became a test, and the pre-commit routine became a skill. Each fact is written down once and not repeated in every prompt.
- **A separate reviewer with no context found what the author of the change missed.** Most findings were about concurrency, sessions and data leaking into logs: things that pass every test until the right race happens.
- **The human made the product and scope decisions,** accepted or overrode trade-offs (for example, documenting the metric double count instead of rebuilding it), and noticed the process gaps that became the skill and the agent.
