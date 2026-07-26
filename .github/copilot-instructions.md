# Copilot Instructions

This is a .NET 10 reference implementation of request-oriented Vertical Slice Architecture using
ASP.NET Core Minimal APIs, Blazor WebAssembly, MudBlazor, PostgreSQL, Dapper, NUnit, and
Testcontainers.

## Non-negotiable architecture

- One HTTP request/use case is one vertical slice.
- Place slices in `src/symphony-test-1.Api/Features/<Capability>/<UseCase>.cs`.
- Keep route mapping, request/response records, validation, handler, tailored SQL, result mapping,
  and expected errors in that slice.
- Register the slice in `<Capability>Feature.cs`.
- Do not create CRUD repositories, generic repositories, application services, shared persistence
  models, or shared DTO files.
- Do not add MediatR just to dispatch handlers. VSA does not require it.
- Share platform mechanics only: database data source, exception handling, OpenTelemetry, and
  structured logging.
- Keep UI use cases under `src/symphony-test-1.Web/Features/<Capability>/`, with one Razor
  component per list/create/view/update/delete interaction.
- UI slices own the HTTP call, local request/response records, state, and error handling. Do not
  add resource-wide API clients/services or reference the API project from the WASM project.
- Share only stable presentation mechanics under `Components/` and transport mechanics under
  `Infrastructure/`.

Load `.github/skills/build-vertical-slice/SKILL.md` when adding, changing, reviewing, or refactoring
an endpoint.

## Code conventions

- Preserve public HTTP contracts unless the task explicitly changes them.
- Use typed Minimal API results with accurate OpenAPI metadata. Give every slice route a concise
  summary and behavioral description, and document request and response record fields for the
  generated schema.
- Define request rules in a nested internal FluentValidation `RequestValidator`, inject
  `IValidator<Request>` into the handler, and invoke `ValidateAsync` before I/O.
- Return validation problems using `ValidationResult.ToDictionary()`, preserving the JSON
  contract's lower-camel-case error keys, and use Problem Details for global failures.
- Catch specific PostgreSQL constraint errors only; never broadly catch and return `400`.
- Use `NpgsqlDataSource`, parameterized Dapper `CommandDefinition`, and cancellation tokens.
- Project queries directly to slice responses and use `RETURNING` for atomic command responses.
- Prefer deliberate local duplication to cross-slice coupling.

## Testing

- Mirror capability/slice names under `tests/`.
- Unit-test nested request validators or deterministic domain rules directly.
- Integration-test slice behavior through HTTP with Testcontainers PostgreSQL.
- Reserve end-to-end tests for multi-slice workflows.
- Use bUnit for fast component states and Playwright for browser workflows through the gateway
  against the API and Testcontainers PostgreSQL.
- Preserve the mechanical architecture tests that enforce client dependency direction and UI
  slice boundaries.
- Test relevant success, validation, conflict, not-found, and constraint paths.

Before completion, run a warning-free solution build, all tests with Docker available, formatting,
and `dotnet list ... package --vulnerable --include-transitive`.

## Documentation

Keep README, Mintlify docs, API reference, custom agents, and skills synchronized with source.
Describe `Languages` and `Greetings` as capabilities; the individual requests inside them are the
slices. Never document aspirational or unused features as implemented.
