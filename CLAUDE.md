# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Design decisions and implementation order: `.claude/plans/implementation-plan.md` (summary in `SPEC.md`). All files are in English.

## Commands

```sh
cp .env.example .env                                  # once
docker compose up -d                                  # Postgres, RabbitMQ (UI :15672), SeaweedFS (S3), Aspire Dashboard (:18888)
dotnet build DocumentIntelligence.slnx
dotnet run --project src/DocumentIntelligence.Api     # http://localhost:5080: Scalar UI /scalar, hub /hubs/documents, /health/ready
dotnet run --project src/DocumentIntelligence.Worker # fake analyzer unless a Claude key is set (below)
dotnet test --solution DocumentIntelligence.slnx
dotnet test --project tests/DocumentIntelligence.UnitTests --filter-class "*LayerDependencyTests"
dotnet user-secrets set Anthropic:ApiKey <key> --project src/DocumentIntelligence.Worker  # or ANTHROPIC_API_KEY
```

Migrations (`dotnet-ef` is a local tool: run `dotnet tool restore` once); the API applies them on startup in Development:

```sh
dotnet ef migrations add <Name> --project src/DocumentIntelligence.Infrastructure --startup-project src/DocumentIntelligence.Api --output-dir Persistence/Migrations
```

Integration tests need Docker running (Testcontainers starts Postgres and SeaweedFS; RabbitMQ is replaced by MassTransit's in-memory test harness, which also runs the Worker's consumers). Tests run on Microsoft.Testing.Platform: use `--solution`/`--project` and `--filter-class`/`--filter-method`, not VSTest syntax.

## Structure

```
src/
  DocumentIntelligence.Domain/          aggregates, value objects, domain events (no dependencies)
  DocumentIntelligence.Application/     CQRS handlers, ports, validators, DTOs (-> Domain)
  DocumentIntelligence.Contracts/       integration messages (no dependencies)
  DocumentIntelligence.Infrastructure/  EF Core, Identity/JWT, S3, MassTransit, Claude, Nager.Date
  DocumentIntelligence.ServiceDefaults/ Serilog, OpenTelemetry, health checks
  DocumentIntelligence.Api/             Minimal API host
  DocumentIntelligence.Worker/          MassTransit consumer host
tests/                                  UnitTests, IntegrationTests
docker/                                 container config (SeaweedFS S3 credentials)
```

## Naming conventions

Enforced by `.editorconfig` (build errors):
- Namespaces match project + folder (`DocumentIntelligence.<Layer>.<Folder>`), file-scoped.
- Private fields `_camelCase`; constants `PascalCase`.

Not enforceable by the analyzer, follow by hand:
- Test methods: `Subject_behavior_in_snake_case` (e.g. `Health_endpoint_returns_ok`).
- One top-level type per file, named after the type. Two exceptions:
  - a use-case file holds the command/query, its validator and its handler (e.g. `Application/Authentication/Login.cs`);
  - an enum used only by one type may live in that type's file (e.g. `RiskSeverity` in `Risk.cs`). Once another type uses it, move it to its own file.

Package versions go only in `Directory.Packages.props` (central package management rejects a `Version` on `PackageReference`).

## Rule: verify before finishing

After every change, run `dotnet build DocumentIntelligence.slnx` and `dotnet test --solution DocumentIntelligence.slnx`, and fix every error before finishing. Warnings are errors (`TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`), so style violations fail the build too.

Before every commit, run the `/precommit` skill (`.claude/skills/precommit/SKILL.md`; not the built-in `/verify`, which checks a running app instead): it builds, runs all tests, checks the working tree (line endings, stray files) and reports whether the change is ready to commit. Then run the `reviewer` agent (`.claude/agents/reviewer.md`) on the change: a read-only review of design, correctness, security and test gaps that the build cannot catch.
