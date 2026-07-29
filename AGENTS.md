# Repository Agent Instructions

## Purpose

This repository is an executable reference for request-oriented Vertical Slice Architecture in
ASP.NET Core. Preserve both its behavior and its teaching value.

## Architecture

- Treat one HTTP request as one slice.
- Keep each slice in one descriptively named file under
  `src/symphony-test-1.Api/Features/<Capability>/`.
- A slice owns its route, request and response records, validation, handler, Dapper SQL, mapping,
  typed results, and expected failure handling.
- Register slices in `<Capability>Feature.cs`. Keep `Program.cs` as the composition root.
- Share platform concerns such as `NpgsqlDataSource`, exception handling, telemetry, and logging
  under `Infrastructure/` or application startup.
- Do not add resource-wide repositories, application services, shared persistence entities, or
  shared API DTOs.
- MediatR is optional and is not evidence of VSA. Do not add it without a concrete need.
- Prefer small duplication over coupling unrelated requests. Extract a shared concept only after
  it has stable cross-cutting meaning.
- Keep UI use cases in one descriptively named Razor component under
  `src/symphony-test-1.Web/Features/<Capability>/`.
- A UI slice owns its HTTP call, local request/response records, state, validation feedback, and
  expected failure handling. Do not add resource-wide API clients/services or share server DTOs.
- The WebAssembly project must not reference the API project or persistence packages. Share only
  stable presentation and transport mechanics under `Components/` and `Infrastructure/`.

Use `.github/skills/build-vertical-slice/SKILL.md` for feature work.

## Implementation Standards

- Preserve established routes and success response JSON unless a change is explicitly requested.
- Use typed Minimal API results and matching OpenAPI metadata.
- Put request rules in an `internal sealed RequestValidator : AbstractValidator<Request>` nested
  in the slice. Inject `IValidator<Request>` into the handler, call `ValidateAsync` before I/O,
  and return RFC 7807 validation problems from `ValidationResult.ToDictionary()`.
- Override FluentValidation property names when necessary so validation keys match the JSON
  contract's lower-camel-case names.
- Catch only exceptions the slice can translate deliberately. Unexpected failures belong to the
  global Problem Details handler.
- Use parameterized Dapper SQL and pass `CancellationToken` through `CommandDefinition` and
  connection opening.
- Use `LanguageId` and `GreetingId` instead of raw `Guid` entity identifiers inside the API.
- Project query results directly into the slice response. Prefer PostgreSQL `RETURNING` for
  commands that return a representation.
- Never expose exception, database, connection, or secret details to API clients.

## Tests

- Mirror production capability and slice names under `tests/`.
- Unit-test slice-local FluentValidation validators directly; do not mock internal layers that do
  not exist.
- Integration-test each slice through HTTP against the Testcontainers PostgreSQL database.
- Use end-to-end tests for workflows spanning multiple slices.
- Use bUnit for component states and Playwright for browser workflows through the gateway.
- Preserve the mechanical architecture tests that protect client dependency direction and slice
  boundaries.
- Cover success, validation, not-found, conflict, and database-constraint behavior where relevant.
- Keep tests independent and avoid execution-order assumptions or arbitrary sleeps.

Run:

```bash
dotnet restore symphony-test-1.slnx --locked-mode
dotnet build symphony-test-1.slnx --configuration Release --no-restore
pwsh tools/lint-openapi.ps1 src/symphony-test-1.Api/obj/openapi/symphony-test-1.json
pwsh tests/e2e/symphony-test-1.E2ETests/bin/Release/net10.0/playwright.ps1 install chromium
dotnet test symphony-test-1.slnx --configuration Release --no-build --no-restore
dotnet list symphony-test-1.slnx package --vulnerable --include-transitive --no-restore
```

Docker must be available for integration and end-to-end tests.

## Hot-reload development and live verification

Use Aspire with resource-native watch loops for an edit/verify loop. The AppHost keeps each
development environment isolated: it allocates the application and dependency endpoints and
injects their values rather than relying on fixed ports.

```powershell
aspire run
```

If the installed CLI is not visible on PATH in a Windows agent process, invoke
`& "$env:USERPROFILE\.aspire\bin\aspire.exe" run` instead.

Keep the watcher open for the task and use the dashboard or startup output to obtain that run's
**Symphony administration** endpoint. The AppHost starts `api` and `web` with non-interactive
`dotnet watch` commands. Do not
manually tear down and recreate PostgreSQL, Keycloak, or migrations after an application or UI
edit. If an expected change is not visible, stop and start only its resource with
`aspire resource <resource-name> stop` and `aspire resource <resource-name> start`.

With the watcher running, request `<Symphony administration endpoint>/api/health` for an immediate
unauthenticated API check and navigate or reload the same endpoint in a browser to verify
WebAssembly UI changes through the gateway. When Aspire reports a restart or rebuild, wait for the
endpoint to respond before
checking; do not restart the stack. Stop the watcher with Ctrl+C when the task ends; leave the
dependency containers running if another agent may continue the task.

Repository-local AppHost-wide watch is disabled because it is restart-based and re-creates the
whole topology. After an AppHost model change, stop and rerun `aspire run`; do not use that
topology-edit behavior as the API/UI inner loop.

## Documentation and Guidance

- Keep `README.md`, `docs/`, `.github/copilot-instructions.md`, custom agents, and repository
  skills consistent with the source.
- Describe only behavior verified in the current code and tests.
- Call a request/use case a slice; call `Languages` and `Greetings` capabilities or feature areas.
- Update API status-code documentation whenever endpoint behavior changes.

## Safety

- Do not modify `.git/`.
- Do not commit real credentials. Fixed `postgres/postgres` values are disposable local Docker
  development credentials only.
- Do not remove tests or compatibility behavior merely to simplify a refactor.
