# OpenFGA authorization model

`authorization-model.fga` defines the authorization vocabulary. It contains no environment data
and no application code. `authorization-model.json` is the generated OpenFGA API representation
used by the provisioning project. OpenFGA models are immutable once published; the provisioner
reuses its named store and publishes a new version only when this committed model has changed.

The model remains separate from relationship data. For local Aspire and Compose environments, the
provisioner reconciles bootstrap role tuples for the committed Keycloak test-user subject IDs. The
comma-separated `OpenFga__BootstrapSuperuserSubjects` and
`OpenFga__BootstrapStandardUserSubjects` values are the complete desired set of direct assignments
on `system:global`: provisioning adds missing assignments and removes stale assignments for those
two relations without touching resource tuples. A deployment that runs the provisioner must
therefore supply its complete role assignment set; a direct role-tuple change made elsewhere is
removed on the next provisioning run unless it is also present in this configuration. At runtime,
the API represents an authenticated caller as `user:{jwt sub}` and checks the relevant `can_*`
relation.

The starter model has two global roles. `system:global#superuser` grants every permission represented
by the current Languages and Greetings routes. `system:global#standard_user` grants read-only access
to every Language and Greeting. The model intentionally defines no ownership, tenant, delegation, or
language-scoped authorization rules.

Request slices first use `system:global#can_read_catalog` or
`system:global#can_manage_catalog` as a stable role boundary. This distinguishes an unassigned
authenticated user from an assigned user querying a catalog that happens to be empty, while
resource checks and `ListObjects` continue to decide which individual records are visible.

| API operation | Implemented authorization check |
| --- | --- |
| Search/list languages | `system:global#can_read_catalog`, then `ListObjects` using `language#can_view` |
| Create a language | `system:global#can_create_language` |
| Get a language | `system:global#can_read_catalog`, then `language:{id}#can_view` |
| Update/delete a language | `system:global#can_manage_catalog`, then `language:{id}#can_update` / `can_delete` |
| Search/list greetings | `system:global#can_read_catalog`, then `ListObjects` using `greeting#can_view` |
| Create a greeting | `system:global#can_create_greeting` |
| Get a greeting | `system:global#can_read_catalog`, then `greeting:{id}#can_view` |
| Update/delete a greeting | `system:global#can_manage_catalog`, then `greeting:{id}#can_update` / `can_delete` |

Tuple writes and deletes are committed to a PostgreSQL outbox with their corresponding row change.
Enqueueing takes a transaction-scoped lock derived from the tuple identity, so committed operations
have an unambiguous order even when requests overlap. The API drains that order through its own
operation before returning success, and a background worker replays interrupted work one tuple at a
time so a failing head is not retried once per successor. Writes ignore duplicates and deletes
ignore missing tuples, making replay idempotent. Permission checks and object searches request
higher consistency because UI and API
workflows commonly read immediately after a successful command. Each API process resolves and pins
the latest model ID after provisioning, so all of its decisions use one immutable model version.
A failed or cancelled discovery attempt is not cached: the next operation retries discovery, while
the first successful result remains pinned for the lifetime of that API process.
Processed outbox operations are retained for seven days for diagnosis, then pruned in bounded
batches of at most 1,000 rows during the five-second recovery cycle. Pending operations are never
pruned. A partial index on processed rows keeps retention scans independent of the pending-work
queue.
Catalog searches use the streamed `ListObjects` API so authorized results are not truncated at the
non-streaming endpoint's result cap. Local Aspire and Compose environments set a five-second
`OPENFGA_LIST_OBJECTS_DEADLINE`; deployments should tune that explicit deadline from observed
catalog size and latency.

Run the model checks from the repository root:

```powershell
pwsh tools/test-openfga-model.ps1
```

The script runs the pinned OpenFGA CLI container and does not require a running OpenFGA server.
It also ensures `authorization-model.json` matches the readable `.fga` source.

Docker Compose creates the dedicated `openfga` database through an idempotent one-shot resource;
Aspire creates the same database from its application model.
