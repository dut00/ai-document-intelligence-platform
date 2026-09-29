# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Design decisions and implementation order: `.claude/plans/implementation-plan.md` (summary in `SPEC.md`). All files are in English.

## Commands

```sh
cp .env.example .env                                  # once
docker compose up -d                                  # Postgres, RabbitMQ, SeaweedFS (S3), Aspire Dashboard
dotnet build DocumentIntelligence.slnx
dotnet run --project src/DocumentIntelligence.Api     # http://localhost:5080, Scalar UI at /scalar
dotnet run --project src/DocumentIntelligence.Worker
dotnet test --solution DocumentIntelligence.slnx
dotnet test --project tests/DocumentIntelligence.UnitTests --filter-class "*LayerDependencyTests"
```

Tests run on Microsoft.Testing.Platform: use `--solution`/`--project` and `--filter-class`/`--filter-method`, not VSTest syntax.

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

Package versions go only in `Directory.Packages.props` (central package management rejects a `Version` on `PackageReference`).

## Rule: verify before finishing

After every change, run `dotnet build DocumentIntelligence.slnx` and `dotnet test --solution DocumentIntelligence.slnx`, and fix every error before finishing. Warnings are errors (`TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`), so style violations fail the build too.
