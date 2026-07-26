# Administration portal UI prototype

This is a self-contained, interactive prototype for reviewing the proposed Symphony
administration experience before adding a production Blazor project or changing any API
contracts.

Open `index.html` in a browser. The prototype uses in-memory sample data and does not call the
running API.

## What to review

- Persistent top application bar with a placeholder signed-in profile
- Responsive left navigation for Dashboard, Languages, and Greetings
- Business-oriented density, colour, typography, and page hierarchy
- Dashboard summary metrics, recent updates, and common tasks
- Sortable and filterable language and greeting grids
- Create, view, edit, validation, delete confirmation, and success feedback
- Language deletion warning for the existing cascading greeting behaviour

The prototype intentionally omits authentication, authorization, live API integration, loading
states, unexpected error states, pagination implementation, and automated tests. Those belong to
the production phase after the interaction and visual direction are approved.

## Proposed production architecture

Add a separate `symphony-test-1.Web` .NET 10 Blazor Web App using Interactive Server rendering.
MudBlazor requires an interactive render mode, and server interactivity keeps this administrative
reference simple: one UI project, no WebAssembly client project, no browser-to-API CORS setup, and
server-to-server API calls through Aspire service discovery.

```text
src/symphony-test-1.Web/
├── Components/
│   ├── App.razor
│   ├── Routes.razor
│   └── Layout/
│       ├── MainLayout.razor
│       └── NavMenu.razor
├── Features/
│   ├── Dashboard/
│   │   └── DashboardPage.razor
│   ├── Languages/
│   │   ├── ListLanguages.razor
│   │   ├── CreateLanguageDialog.razor
│   │   ├── ViewLanguageDialog.razor
│   │   ├── UpdateLanguageDialog.razor
│   │   └── DeleteLanguageDialog.razor
│   └── Greetings/
│       ├── ListGreetings.razor
│       ├── CreateGreetingDialog.razor
│       ├── ViewGreetingDialog.razor
│       ├── UpdateGreetingDialog.razor
│       └── DeleteGreetingDialog.razor
└── Infrastructure/
    └── API HTTP configuration and cross-cutting Problem Details handling
```

Each UI use case owns its API request/response records, interaction state, validation display, and
MudBlazor component. A configured `HttpClient` and safe RFC 7807 parsing are shared platform
concerns. Do not add a generic repository, generic CRUD client, or shared API DTO collection.

The first implementation can use `MudDataGrid` client-side sorting and filtering because the
existing list endpoints deliberately return the complete small reference catalog. If the sample
later needs server paging, filtering, or large data volumes, add purpose-built list query slices
with explicit query contracts instead of hiding query behaviour in a generic data service.

## Existing API mapping

| UI use case | API request |
| --- | --- |
| List/filter/sort languages | `GET /api/languages` |
| View language | `GET /api/languages/{id}` |
| Create language | `POST /api/languages` |
| Update language | `PUT /api/languages/{id}` |
| Delete language | `DELETE /api/languages/{id}` |
| List/filter/sort greetings | `GET /api/greetings` |
| View greeting | `GET /api/greetings/{id}` |
| Create greeting | `POST /api/greetings` |
| Update greeting | `PUT /api/greetings/{id}` |
| Delete greeting | `DELETE /api/greetings/{id}` |

The dashboard can load both list requests concurrently and derive its counts and recent-update
summary from the existing timestamped responses. This is appropriate at the sample's current data
size and avoids a dashboard-only API contract until one has a demonstrated need.
