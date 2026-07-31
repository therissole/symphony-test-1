# ASP.NET Core Vertical Slice Architecture Reference

An executable .NET 10 reference for request-oriented Vertical Slice Architecture (VSA), inspired
by Jimmy Bogard's approach: treat each request as a distinct use case, maximize cohesion inside the
slice, and minimize coupling between slices.

This is intentionally not a layered `Controller → Service → Repository` application. Every API
operation owns its route, contract, validation, handler, SQL, mapping, and expected errors.

## What the reference demonstrates

- ASP.NET Core Minimal APIs with typed results and generated OpenAPI
- A responsive MudBlazor administration UI running as Blazor WebAssembly
- A same-origin gateway that proxies the Web development server and API independently
- Dashboard and list/sort/filter/create/view/update/delete workflows for both capabilities
- One request/use case per slice
- Slice-local FluentValidation rules with explicit asynchronous handler invocation
- Request-specific Dapper SQL against PostgreSQL
- Atomic command responses with PostgreSQL `RETURNING`
- RFC 7807 Problem Details and validation problems
- Cancellation-aware database I/O
- Trace-correlated structured logging through `ILogger` and OpenTelemetry
- Aspire service defaults for OpenTelemetry, health checks, resilience, and service discovery
- Keycloak OIDC authentication with authorization code + PKCE in the WebAssembly client
- JWT bearer validation and authenticated administration API boundaries
- Aspire 13.4 AppHost orchestration for the gateway, Web client, API, Keycloak, PostgreSQL, OpenFGA, and database migrations
- NUnit architecture, unit, integration, bUnit component, and Playwright browser tests
- Build-enforced .NET, security, banned-API, and repository-specific Roslyn analyzers
- Strong `LanguageId` and `GreetingId` types with unchanged UUID JSON and OpenAPI contracts
- Build-time OpenAPI 3.1 generation and deterministic contract linting
- Exact SDK selection, locked NuGet dependency graphs, NuGet audit, and CodeQL security analysis
- Request-slice LightBDD acceptance tests using API and browser protocol drivers by default
- Scenario-isolated synthetic data and deterministic business time through `TimeProvider`
- Real PostgreSQL tests through Testcontainers
- Versioned, checksum-verified SQL migrations and a complete Docker Compose fallback
- Repository instructions, custom agents, and a reusable VSA agent skill

## Quick start

Prerequisites: Docker, the .NET 10 SDK, the Aspire CLI, and Node.js 20.17 or later.

```bash
git clone https://github.com/therissole/symphony-test-1.git
cd symphony-test-1
aspire run
```

The Aspire dashboard shows the dynamically assigned **Symphony administration** endpoint. Open it
and sign in as `symphony-admin` with the temporary local password `ChangeMe!12345`; Keycloak
requires a new password at first sign-in. These credentials exist only in the disposable local
realm. Use `/api/health` without authentication to verify database-aware health, or
`/openapi/v1.json` for the OpenAPI document.

To run beside another copy without port or user-secret collisions:

```bash
aspire run --isolated
```

Aspire assigns resource endpoints for each run. The API receives that run's Keycloak authority
from the AppHost, and the WebAssembly client obtains the same non-secret authority through the
gateway at startup, so no Keycloak or application port is compiled into the Aspire path.

## Project structure

```text
src/symphony-test-1.AppHost/       # Aspire application model and orchestration
src/symphony-test-1.Gateway/       # same-origin UI/API boundary and production WASM host
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
└── Program.cs

src/symphony-test-1.Web/
├── Components/                    # layout and shared presentation mechanics
├── Features/
│   ├── Dashboard/
│   ├── Languages/                 # one Razor component per UI use case
│   └── Greetings/                 # one Razor component per UI use case
├── Infrastructure/                # RFC 7807 response parsing only
└── wwwroot/                       # WASM host page and application styles

src/symphony-test-1.DatabaseMigrations/
└── Program.cs                     # one-shot, versioned SQL migration resource

openfga/
├── authorization-model.fga         # versioned authorization model
├── authorization-model.json         # generated API model used for provisioning
└── authorization-model.tests.fga.yaml # model assertions and representative tuples

src/symphony-test-1.OpenFgaProvisioning/
└── Program.cs                       # idempotently creates the named store and model version

src/symphony-test-1.ServiceDefaults/
└── Extensions.cs                  # telemetry, health, resilience, discovery

tests/
├── unit/          # deterministic rules and mechanical architecture checks
├── acceptance/    # deployed-system LightBDD specifications and protocol drivers
├── ui/            # fast bUnit component states
├── integration/   # every slice through HTTP + real PostgreSQL
└── e2e/           # API and Playwright workflows spanning multiple slices

.github/
├── agents/        # project-specific Copilot agents
├── skills/        # reusable VSA implementation workflow
└── workflows/     # build, test, audit, and container verification

tools/
├── SymphonyTest1.Analyzers/      # compile-time vertical-slice rules
└── lint-openapi.ps1              # generated API-contract policy
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
| Health (public) | `GET /api/health` |
| Authentication bootstrap (public) | `GET /api/authentication/configuration` |
| Languages | `GET /api/languages`, `GET /api/languages/{id}`, `POST /api/languages`, `PUT /api/languages/{id}`, `DELETE /api/languages/{id}` |
| Greetings | `GET /api/greetings` (optional `languageId`, `formal`, `createdFrom`, `createdTo` filters), `GET /api/greetings/{id}`, `GET /api/greetings/by-language/{code}`, `POST /api/greetings`, `PUT /api/greetings/{id}`, `DELETE /api/greetings/{id}` |

Language and greeting endpoints require a Keycloak access token whose audience is
`symphony-api`. The dashboard and both management pages initiate the OIDC sign-in flow when the
visitor is anonymous. Health deliberately remains anonymous for orchestrators and diagnostics.

The local Mintlify site generates its interactive API reference from the API's live Development
OpenAPI document.

The administration UI intentionally excludes Health. Health remains an operational API and
orchestrator concern rather than a catalog management use case.

## Local development and verification

Aspire is the primary local-development path:

```bash
aspire run
```

The AppHost creates Keycloak with the committed local `symphony` realm and PostgreSQL, runs every
unapplied `db/migrations/V*.sql` file, verifies
checksums for migrations already applied, starts the API only after migration success, starts the
Web development server and same-origin gateway, and starts
the local Mintlify documentation preview. The Aspire dashboard shows links for the administration
application and the documentation. The application executes in WebAssembly and calls the gateway's
relative `/api` routes. Aspire allocates Keycloak and application endpoints per run; the API
publishes the current non-secret OIDC authority and client ID to the browser through
`/api/authentication/configuration`. In Mintlify, open **API Reference** to inspect contracts, status codes, and
send requests to the running API from the generated endpoint pages.

### Fast edit/verify loop

For source changes, start the Aspire topology once. The AppHost runs the API and Web resources
with their native non-interactive `dotnet watch` loops while preserving isolated, dynamically
allocated endpoints:

```powershell
aspire run
```

If a Windows agent has not inherited the Aspire CLI PATH entry, use
`& "$env:USERPROFILE\.aspire\bin\aspire.exe" run`.

Leave that command running for the task. The dashboard and startup output identify the isolated
**Symphony administration** endpoint for this run. The `web` and `api` resources watch their own
source independently; the gateway remains running. Do not manually tear down and recreate PostgreSQL,
Keycloak, or migrations after an application or UI edit. If a native watcher cannot apply an edit,
restart only that resource from the dashboard or CLI:

```powershell
aspire resource api stop
aspire resource api start
aspire resource web stop
aspire resource web start
```

Then verify the result with the endpoint allocated for the current run:

```powershell
Invoke-RestMethod <Symphony-administration-endpoint>/api/health
```

The gateway serves the browser client at that same endpoint. In development it proxies UI requests
to the Web development server; published deployments serve the compiled WebAssembly assets from
the gateway. Navigate there (or reload the page after an edit) to verify a UI slice against the
same live API. When Aspire restarts or rebuilds a
resource, wait for the health endpoint to respond before retrying a browser or API check.

Repository-local `defaultWatchEnabled` is disabled deliberately. AppHost-wide watch is
restart-based and re-creates the application topology; it is appropriate while editing
`AppHost.cs`, not for the normal API/UI inner loop. Stop and rerun `aspire run` after an AppHost
model change.

`docker compose up --build --wait` remains the reproducible, headless fallback. It builds an image
from a source snapshot, so it is not the edit/verify command and should not be rerun after routine
source changes. Stop the watcher with Ctrl+C when finished; do not use this fixed-port fallback as
an isolated-development environment.

Aspire PostgreSQL storage is intentionally environment-local and ephemeral so isolated runs can
use randomized credentials safely. The Docker Compose fallback provides a named PostgreSQL volume
when persistence across restarts is wanted.

Docker Compose remains available as a headless fallback and uses the same .NET migration resource:

```bash
docker compose up --build --wait
curl http://localhost:8081/api/health
docker compose down -v
```

Run the quality checks:

```bash
dotnet restore symphony-test-1.slnx --locked-mode
dotnet build symphony-test-1.slnx --configuration Release --no-restore
pwsh tools/lint-openapi.ps1 src/symphony-test-1.Api/obj/openapi/symphony-test-1.json
pwsh tools/test-openfga-model.ps1
dotnet format symphony-test-1.slnx style --verify-no-changes --no-restore
pwsh tests/e2e/symphony-test-1.E2ETests/bin/Release/net10.0/playwright.ps1 install chromium
dotnet test symphony-test-1.slnx --configuration Release --no-build --no-restore
dotnet list symphony-test-1.slnx package --vulnerable --include-transitive --no-restore
```

Docker must be running for integration and end-to-end tests.

See [deterministic quality enforcement](docs/quality-enforcement.mdx) for the rule catalogue and
the boundary between compiler diagnostics, architecture tests, API-contract checks, and CI
security analysis.

Run black-box API and browser acceptance specifications against a disposable Compose deployment:

```powershell
pwsh tests/acceptance/run.ps1
```

The acceptance project has no production-project references. Its generic core contains only
scenario, protocol-matrix, and transport mechanics. Each capability keeps request-level fixtures
under `AcceptanceTests/` and shares only that capability's business DSL and API/browser protocol
drivers through its `Dsl/` and `ProtocolDrivers/` folders. It receives deployment details through
environment variables, so the same tests can run against another implementation of the public API.
The runner assigns independently configurable gateway, Keycloak, PostgreSQL, and OpenFGA host ports
and restores every pre-existing environment value after the disposable deployment is removed.

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
- **Gateway-hosted WASM:** Aspire's Blazor hosting integration serves the browser client from the
  gateway and preserves one browser origin for API and telemetry routes.
- **Observable browser boundary:** the WebAssembly client exports OpenTelemetry through the
  gateway's same-origin `/_otlp` route, so browser HTTP, gateway, API, and PostgreSQL spans share
  trace context without exposing dashboard credentials. A browser-safe Razor class library owns
  the Aspire runtime-config initializer and client telemetry defaults; API calls use Aspire service
  discovery through `IHttpClientFactory`, matching Microsoft's standalone Blazor reference setup.
- **Defense in depth:** route authorization prevents anonymous navigation in the client, while
  the API independently validates issuer, audience, signature, and token lifetime. Client-side
  visibility is never treated as the security boundary.
- **OpenFGA authorization:** OpenFGA runs against its own database in the shared PostgreSQL
  instance. Every Languages and Greetings slice enforces the versioned model: superusers can read
  and manage the catalog, standard users have read-only access, and anonymous callers cannot enter
  the administration API. Catalog searches use streamed `ListObjects` results to filter SQL,
  while creates and deletes commit tuple intent with the business row in a transactional outbox,
  dispatch before returning success, and recover idempotently after interruption. Aspire and
  Compose run a one-shot provisioner that reuses the named store, publishes only changed models,
  reconciles the configured direct bootstrap roles as an authoritative set, and applies resource
  tuples. The readable model, generated API
  representation, CLI assertions, and request-level API/browser acceptance specifications are
  verified in CI.
- **Local realm only:** Aspire imports the committed realm for development. Production deployments
  must provide a managed Keycloak instance, HTTPS authority, and exact environment-specific client
  redirect and logout URIs. The disposable local realm's wildcard redirects and web origins exist
  only to support Aspire-assigned ports.
- **UI slices:** components own their HTTP contracts and operation flow. There is no shared
  language or greeting client/service layer.

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
