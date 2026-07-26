# Acceptance Test-Driven Development approach

This reference project uses acceptance tests as executable specifications for business behaviour. They are written from an external user's perspective, use domain language, and interact with a deployed system only through public protocols.

## Layers

```text
LightBDD acceptance test -> feature DSL -> feature protocol driver -> deployed system
```

`Core/` contains only protocol-neutral mechanics: scenario lifecycle and cleanup, synthetic-data
generation, the protocol-fixture matrix, and generic HTTP/browser transports. It contains no
feature vocabulary. Each request slice owns its acceptance test, readable DSL, protocol-driver
interface, and API and/or web implementations under `Features/<Capability>/<Request>/`.

The DSL owns the business language; its protocol drivers translate it into that channel's public
details. Thus the test contains no routes, JSON, selectors, database identifiers, or application
request/response types. The API driver owns HTTP and JSON; the web driver owns browser navigation
and selectors. Replacing the application stack requires changing a driver only when its public
protocol differs.

An acceptance fixture runs against API and web by default. Add
`[AcceptanceProtocols(AcceptanceProtocol.Api)]` or `Web` only when a scenario deliberately proves
a channel-specific public capability. The creation-range scenario is API-only because the
controlled test-environment clock is an API contract; it is not a user-facing web workflow.

The acceptance project has no production-project references. It receives a base URL and credentials, so it can verify another implementation of the same public contract.

## Scenario data and isolation

Specifications use stable logical aliases. A scenario-scoped data context resolves them to unique, constraint-aware physical values and records server-generated identifiers. Each execution has a fresh isolation token and a recorded deterministic seed. Scenarios do not read or write the database directly, do not depend on global list counts, and clean up through public APIs. Correctness comes from isolation rather than cleanup succeeding.

## Time

Application behaviour obtains the current instant from an injected `TimeProvider`. Business timestamps are UTC instants stored as `TIMESTAMPTZ` and represented as `DateTimeOffset`. Tests use `FakeTimeProvider` below the public boundary; acceptance scenarios use an explicitly enabled test-environment clock protocol. Clock-changing scenarios have exclusive use of that capability and never wait for wall-clock time.


## Test responsibilities

- Acceptance tests prove valuable user behaviour through API and browser protocol drivers.
- Slice integration tests prove HTTP binding, validation, SQL, constraints, and error mapping.
- Component and browser tests prove presentation and accessibility behaviour.
- Architecture tests enforce dependency direction, slice boundaries, and the `TimeProvider` rule.

See `docs/architecture.mdx` for the vertical-slice conventions that apply to the production code.
