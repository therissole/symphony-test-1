# OpenFGA authorization model

`authorization-model.fga` defines the authorization vocabulary. It contains no environment data
and no application code. `authorization-model.json` is the generated OpenFGA API representation
used by the provisioning project. OpenFGA models are immutable once published; the provisioner
reuses its named store and publishes a new version only when this committed model has changed.

The model remains separate from relationship data. For local Aspire and Compose environments, the
provisioner idempotently writes bootstrap role tuples for the committed Keycloak test-user subject
IDs. At runtime, the API represents an authenticated caller as `user:{jwt sub}` and checks the
relevant `can_*` relation. Deployment environments must supply their own bootstrap assignments or
write role tuples through their identity-administration process.

The starter model has two global roles. `system:global#superuser` grants every permission represented
by the current Languages and Greetings routes. `system:global#standard_user` grants read-only access
to every Language and Greeting. The model intentionally defines no ownership, tenant, delegation, or
language-scoped authorization rules.

| API operation | Authorization check for a future integration |
| --- | --- |
| Search/list languages | `ListObjects` using `language#can_view` |
| Create a language | `system:global#can_create_language` |
| Get/update/delete a language | `language:{id}#can_view` / `can_update` / `can_delete` |
| Search/list greetings | `ListObjects` using `greeting#can_view` |
| Create a greeting | `system:global#can_create_greeting` |
| Get/update/delete a greeting | `greeting:{id}#can_view` / `can_update` / `can_delete` |

Run the model checks from the repository root:

```powershell
pwsh tools/test-openfga-model.ps1
```

The script runs the pinned OpenFGA CLI container and does not require a running OpenFGA server.
It also ensures `authorization-model.json` matches the readable `.fga` source.

Docker Compose creates the dedicated `openfga` database through an idempotent one-shot resource;
Aspire creates the same database from its application model.
