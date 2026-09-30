---
name: precommit
description: Build the solution, run all unit and integration tests, lint and build the frontend, build the Docker images when their inputs changed, audit dependencies for known vulnerabilities, scan the change for secrets, and check the working tree before a commit. Use before every commit, and whenever the user asks to check the build and tests before committing.
context: fork
---

# Pre-commit check

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

## 3b. Frontend

If anything under `frontend/` changed, run in `frontend/` (after `npm ci` when `node_modules` is missing or `package-lock.json` changed):

```sh
npm run lint
npm run build
```

Both must pass with no errors or warnings.

## 3c. Docker

If a `Dockerfile`, `.dockerignore`, `docker-compose.yml`, `frontend/nginx/` or a project file changed, build the images (the build only, never `up`: the user's containers stay as they are):

```sh
docker compose --profile full build
```

## 3d. Security checks

Mechanical checks only; the judgment part is the `security-reviewer` agent.

**Vulnerable packages.** Always run both:

```sh
dotnet list DocumentIntelligence.slnx package --vulnerable --include-transitive   # reads obj/, so a locked bin/ does not matter
(cd frontend && npm audit --omit=dev --audit-level=high)
```

- Any project reported as "has the following vulnerable packages" is a finding: quote the package, version, severity and advisory. The command exits 0 either way, so read its output.
- `npm audit` must exit 0. Also run the full `npm audit` (dev dependencies included) and report what it finds for information; dev-only advisories do not block.

**Secret scan** of the files this change touches (changed tracked files and untracked files, whole files, so hits carry `path:line`):

```sh
secrets='sk-ant-[A-Za-z0-9_-]{10,}|-----BEGIN [A-Z ]*PRIVATE KEY-----|AKIA[0-9A-Z]{16}|eyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}'
# A literal value of 8+ characters under a secret-like key (Storage__SecretKey, S3_SECRET_KEY, "Password": …),
# or credentials in a URL. ${VAR}, <placeholder>, calls and member access do not match; bare identifiers in code can.
settings='(password|passwd|signing_?key|api_?key|secret_?key|access_?key)["'\'']?[[:space:]]*[:=][[:space:]]*["'\'']?[^[:space:]"'\''$<{(.][^[:space:]"'\''(),;.]{7,}([^.(A-Za-z0-9_-]|$)|://[^/:@[:space:]]+:[^@$[:space:]{]{6,}@'
known=':(exclude)**/appsettings.Development.json :(exclude).env.example :(exclude)tests/** :(exclude)docker/seaweedfs/s3.json'
changed() { git diff HEAD --name-only --diff-filter=d -z -- "$@"; git ls-files --others --exclude-standard -z -- "$@"; }

changed . | xargs -0 -r grep -HInE "$secrets"
changed . $known | xargs -0 -r grep -HIniE "$settings"
```

- No output means clean; the exit code is then 123 (from `xargs`, because `grep` found nothing), which is not a failure.
- Every hit is a finding unless it is a placeholder, a test value or code (a secret-like key assigned from a plain variable); say which it is.
- The scan is a safety net, not proof: a value containing a dot, or a secret under another key name, slips through. Look at any new configuration value by eye as well.
- The known development values (dev connection strings, the dev JWT key, SeaweedFS dev credentials, test keys) live only in the excluded files. The same value anywhere else is a finding.

## 4. Working tree

- `git status --short`: flag unexpected files (build output, `.env`, scratch files, anything under `.my-notes/`).
- `git diff --check` and `git diff --cached --check`: no whitespace errors.
- Line endings: the repo uses `eol=lf`. Changed or new text files must not contain CRLF. Check with `git ls-files --eol` on the changed paths (`w/crlf` in the working tree is a finding) and fix with `sed -i 's/\r$//' <file>`.
- New files follow the conventions in `CLAUDE.md`: English only, one top-level type per file, snake_case test names, package versions only in `Directory.Packages.props`.

## 5. Report

Reply with a short summary:

- build: warnings / errors;
- tests: passed / failed / skipped, for unit and integration tests separately;
- security: vulnerable packages (NuGet, npm) and secret scan hits, or "clean";
- working tree findings and anything you fixed (for example line endings);
- a clear verdict: **ready to commit** or **not ready**, with the reasons.

Quote the exact error for every failure. Do not paste full build or test logs.
