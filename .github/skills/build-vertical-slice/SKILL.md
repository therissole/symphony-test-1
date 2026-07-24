---
name: build-vertical-slice
description: Add, change, review, or refactor ASP.NET Core use cases in this repository using request-owned Vertical Slice Architecture. Use for endpoint work under src/symphony-test-1.Api/Features, request and response contracts, validation, Dapper SQL, slice registration, slice-focused tests, or architecture documentation.
---

# Build Vertical Slice

Treat one HTTP request as one use case and one slice. Keep the route, request and response
contracts, validation, handler, SQL, result mapping, and expected failure handling together in
one named slice file.

## Workflow

1. Read `docs/architecture.mdx`, the feature registration file, and one neighboring slice.
2. Record the existing HTTP route, payload, response, and status-code contract before editing.
3. Create or update one `<Verb><UseCase>.cs` file under the relevant feature capability.
4. Define request-specific `Request` and `Response` records inside the slice. Do not reuse a
   database entity or another slice's DTO merely because their current fields match.
5. For validated input, add an
   `internal sealed RequestValidator : AbstractValidator<Request>` inside the slice file. Inject
   `IValidator<Request>` into the handler, call `ValidateAsync` before I/O, and return
   `ValidationProblem(validationResult.ToDictionary())` for invalid input. Use
   `OverridePropertyName` where needed so error keys match the JSON contract.
6. Inject `NpgsqlDataSource`, open a connection with the request cancellation token, and execute
   tailored parameterized SQL through a Dapper `CommandDefinition` carrying that token.
7. Project query results directly to the slice response. For commands, prefer PostgreSQL
   `RETURNING` so the write and returned representation are atomic.
8. Catch only expected database conditions such as unique or foreign-key violations and map
   them deliberately. Let unexpected failures reach the global Problem Details handler.
9. Return typed Minimal API results and declare matching OpenAPI response metadata. Give every
   route a concise `WithSummary` and behavioral `WithDescription`, and document request and
   response record parameters with XML comments so generated schemas explain their JSON fields.
10. Register the slice in `<Capability>Feature.cs`; keep `Program.cs` as the composition root.
11. Unit-test the nested validator directly when rules warrant it, add HTTP integration tests for
    every outcome, and use end-to-end tests only for workflows crossing multiple slices.
12. Update user and architecture documentation when behavior or conventions change.
13. Run formatting, a warning-free build, the full Docker-backed test suite, and the vulnerable
    package audit.

## Boundaries

- Do not create resource-wide repositories, services, DTO collections, or handler classes.
- Do not introduce MediatR solely to claim VSA; request dispatch technology is optional.
- Do not move SQL into a shared data-access layer. Share platform setup, not use-case behavior.
- Do not catch `Exception` inside business slices or expose exception messages to clients.
- Do not omit `CancellationToken` from database or network I/O.
- Do not change an established HTTP contract accidentally. Make intentional contract changes
  explicit in tests and API documentation.
- Do not add FastEndpoints or an automatic validation filter merely to hide validation dispatch.
  Explicit asynchronous FluentValidation invocation keeps the Minimal API request flow visible.
- Duplicate small mappings or rules when that keeps slices independent. Extract shared behavior
  only after a stable cross-cutting concept has emerged.

Shared infrastructure belongs under `Infrastructure/`. Capability route registration belongs in
the feature folder. Everything specific to fulfilling one request belongs in that request's slice.
