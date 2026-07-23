# ASP.NET Core Vertical Slice Architecture Reference

An executable .NET 10 reference for request-oriented Vertical Slice Architecture (VSA), inspired
by Jimmy Bogard's approach: treat each request as a distinct use case, maximize cohesion inside the
slice, and minimize coupling between slices.

This is intentionally not a layered `Controller → Service → Repository` application. Every API
operation owns its route, contract, validation, handler, SQL, mapping, and expected errors.

## What the reference demonstrates

- ASP.NET Core Minimal APIs with typed results and generated OpenAPI
- One request/use case per slice
- Slice-local FluentValidation rules with explicit asynchronous handler invocation
- Request-specific Dapper SQL against PostgreSQL
- Atomic command responses with PostgreSQL `RETURNING`
- RFC 7807 Problem Details and validation problems
- Cancellation-aware database I/O
- Structured request and command logging with Serilog
- OpenTelemetry tracing for ASP.NET Core, HTTP clients, and Npgsql
- NUnit unit, integration, and end-to-end tests
- Real PostgreSQL tests through Testcontainers
- Flyway migrations and a complete Docker Compose environment
- Repository instructions, custom agents, and a reusable VSA agent skill

## Quick start

Prerequisites: Docker with Compose. The .NET 10 SDK is also required for local development.

```bash
git clone https://github.com/therissole/symphony-test-1.git
cd symphony-test-1
docker compose up --build -d
docker compose ps
curl http://localhost:8080/api/health
```

The API is available at `http://localhost:8080`. In Development, its OpenAPI document is at
`http://localhost:8080/openapi/v1.json`.

If those host ports are already occupied, set `SYMPHONY_API_PORT` and
`SYMPHONY_POSTGRES_PORT` before running Compose; container-to-container ports remain unchanged.

Stop and remove the local containers and data volume with:

```bash
docker compose down -v
```

## Project structure

```text
src/symphony-test-1.Api/
├── Features/
│   ├── Languages/
│   │   ├── LanguagesFeature.cs
│   │   ├── ListLanguages.cs
│   │   ├── GetLanguage.cs
│   │   ├── CreateLanguage.cs
│   │   ├── UpdateLanguage.cs
│   │   └── DeleteLanguage.cs
│   ├── Greetings/
│   │   ├── GreetingsFeature.cs
│   │   └── <one file per greeting request>
│   └── Health/
│       ├── HealthFeature.cs
│       └── GetHealth.cs
├── Infrastructure/
│   └── Database.cs
└── Program.cs

tests/
├── unit/          # deterministic validation rules
├── integration/   # every slice through HTTP + real PostgreSQL
└── e2e/           # workflows spanning multiple slices

.github/
├── agents/        # project-specific Copilot agents
├── skills/        # reusable VSA implementation workflow
└── workflows/     # build, test, audit, and container verification
```

`Languages` and `Greetings` are capabilities. `CreateLanguage`, `GetGreetingByLanguage`, and the
other individual requests are the vertical slices.

## Adding a use case

1. Add one descriptively named slice file to the appropriate capability.
2. Keep its request and response records, nested FluentValidation validator, handler, SQL, and
   expected errors together.
3. Register it in the capability's `*Feature.cs` file.
4. Add integration tests for its observable outcomes.
5. Update the feature and API documentation.

Do not add a generic repository or shared service by default. Share platform mechanics, not
request behavior. See [the architecture guide](docs/architecture.mdx) and the
[`build-vertical-slice` skill](.github/skills/build-vertical-slice/SKILL.md).

## API

| Capability | Endpoints |
| --- | --- |
| Health | `GET /api/health` |
| Languages | `GET /api/languages`, `GET /api/languages/{id}`, `POST /api/languages`, `PUT /api/languages/{id}`, `DELETE /api/languages/{id}` |
| Greetings | `GET /api/greetings`, `GET /api/greetings/{id}`, `GET /api/greetings/by-language/{code}`, `POST /api/greetings`, `PUT /api/greetings/{id}`, `DELETE /api/greetings/{id}` |

See [the API reference](docs/api-reference/languages.mdx) for payloads and status codes.

## Local development and verification

Start only the database and migration runner, then run the API:

```bash
docker compose up -d postgres flyway
dotnet run --project src/symphony-test-1.Api
```

Run the quality checks:

```bash
dotnet format symphony-test-1.slnx --verify-no-changes
dotnet build symphony-test-1.slnx --configuration Release
dotnet test symphony-test-1.slnx --configuration Release --no-build
dotnet list symphony-test-1.slnx package --vulnerable --include-transitive
```

Docker must be running for integration and end-to-end tests.

## Design choices

- **No MediatR:** it is a valid dispatch option, not a requirement for VSA. Minimal API delegates
  keep this sample explicit.
- **No repositories:** each slice executes the SQL suited to its request.
- **No shared API models:** similar-looking contracts may evolve independently.
- **Explicit validation:** handlers invoke their slice-local FluentValidation validators so the
  Minimal API request flow remains visible and supports asynchronous rules.
- **Limited shared infrastructure:** the Npgsql data source and application pipeline are genuine
  platform concerns.
- **Intentional duplication:** a small repeated mapping is cheaper than coupling unrelated slices.

The architecture guide explains how slices can evolve toward richer domain patterns when business
complexity justifies them.

## Documentation

- [Introduction](docs/introduction.mdx)
- [Quick start](docs/quickstart.mdx)
- [Architecture](docs/architecture.mdx)
- [Languages capability](docs/features/languages.mdx)
- [Greetings capability](docs/features/greetings.mdx)

## License

Licensed under the [Apache License 2.0](LICENSE).

## Acknowledgment

The architecture is based on Jimmy Bogard's
[Vertical Slice Architecture](https://www.jimmybogard.com/vertical-slice-architecture/).
