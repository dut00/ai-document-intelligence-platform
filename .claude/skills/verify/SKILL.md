---
name: verify
description: Build the solution, run all unit and integration tests, and check the working tree before a commit. Use before every commit, and whenever the user asks to verify, test or check the application.
context: fork
---

# Verify before commit

Run the steps in order and stop at the first step that cannot be fixed. Do not commit, stage or push anything: this skill only verifies. Do not kill processes you did not start (the user often runs the API from Visual Studio).

## 1. Prerequisites

- `docker info` must succeed. Integration tests start PostgreSQL and SeaweedFS with Testcontainers. If Docker is not running, stop and report it: skipping integration tests is not a pass.

## 2. Build

```sh
dotnet build DocumentIntelligence.slnx
```

Warnings are errors (`TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`), so the build must end with 0 warnings and 0 errors.

If the build fails with MSB3021/MSB3027 (a file in `bin/` is locked by a running API or Worker), do not stop the process. Build into a separate folder in the scratchpad directory instead and use the same folder for the tests:

```sh
dotnet build DocumentIntelligence.slnx --artifacts-path <scratchpad>/artifacts
```

## 3. Tests

```sh
dotnet test --solution DocumentIntelligence.slnx
```

Add `--artifacts-path <scratchpad>/artifacts` if step 2 needed it. Tests run on Microsoft.Testing.Platform: filter with `--filter-class` / `--filter-method`, never VSTest `--filter`.

If a test fails, re-run only that class to tell a real failure from a flaky one. A test that fails only sometimes is still a finding: report it, do not hide it by re-running until it passes.

## 4. Working tree

- `git status --short`: flag unexpected files (build output, `.env`, scratch files, anything under `.my-notes/`).
- `git diff --check` and `git diff --cached --check`: no whitespace errors.
- Line endings: the repo uses `eol=lf`. Changed or new text files must not contain CRLF. Check with `git ls-files --eol` on the changed paths (`w/crlf` in the working tree is a finding) and fix with `sed -i 's/\r$//' <file>`.
- New files follow the conventions in `CLAUDE.md`: English only, one top-level type per file, snake_case test names, package versions only in `Directory.Packages.props`.

## 5. Report

Reply with a short summary:

- build: warnings / errors;
- tests: passed / failed / skipped, for unit and integration tests separately;
- working tree findings and anything you fixed (for example line endings);
- a clear verdict: **ready to commit** or **not ready**, with the reasons.

Quote the exact error for every failure. Do not paste full build or test logs.
