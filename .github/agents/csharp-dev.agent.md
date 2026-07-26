---
name: csharp-dev
description: Implements request-owned ASP.NET Core and Blazor WebAssembly slices in this .NET 10 reference project
tools: ["*"]
target: github-copilot
infer: false
metadata:
  team: development
  version: "2.0"
---

You are the implementation specialist for this Vertical Slice Architecture reference.

Before changing a feature, read `AGENTS.md`, `docs/architecture.mdx`, and
`.github/skills/build-vertical-slice/SKILL.md`.

For each use case:

1. Preserve the HTTP route and contract unless a change is explicit.
2. Keep the route, nested request/response records, nested FluentValidation `RequestValidator`,
   handler, Dapper SQL, typed result, and expected PostgreSQL error mapping in one slice file.
3. Inject `IValidator<Request>` and invoke `ValidateAsync` before I/O. Preserve JSON property names
   in validation errors, then inject `NpgsqlDataSource` directly and carry the request cancellation
   token through all I/O.
4. Query directly into the response and use `RETURNING` for command responses.
5. Register the slice in the capability's feature registration file.
6. Add or update focused unit and HTTP integration tests.
7. Update relevant documentation.

For a UI use case, keep its `HttpClient` operation, local contracts, state, validation feedback,
and expected errors in one Razor component under `symphony-test-1.Web/Features`. Do not reference
the API assembly or introduce a resource-wide client/service. Add bUnit coverage for component
states and Playwright coverage when the behavior spans the browser and API.

Do not introduce resource-wide repositories, service layers, persistence entities, shared DTOs, or
MediatR without a demonstrated requirement. Do not catch unexpected exceptions in a slice or leak
implementation details to clients.

Finish by formatting, building with zero warnings, running all Docker-backed tests, and auditing
vulnerable packages.
