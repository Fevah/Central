# libs/

Shared .NET libraries. Anything that at least two apps, services, or modules depend on lives here.

## What's here

| Folder | Assembly | What it is |
|--------|----------|------------|
| [engine/](engine/) | `Central.Engine` | The platform kernel — auth primitives, models, widgets, core services, ribbon config, messaging bus. Dependency for nearly everything. |
| [persistence/](persistence/) | `Central.Persistence` | PostgreSQL repositories via Npgsql + `AppLogger`. The only place that speaks SQL directly. |
| [api-client/](api-client/) | `Central.ApiClient` | Typed HTTP + SignalR client for `services/api`. Apps talk to the API through this; no raw `HttpClient` in app code. |
| [workflows/](workflows/) | `Central.Workflows` | Elsa 3.5.3 integration — custom activities, workflow definitions, PostgreSQL persistence. |
| [security/](security/) | `Central.Security` | ABAC policy engine, security headers, column-whitelist validators. |
| [tenancy/](tenancy/) | `Central.Tenancy` | Multi-tenant `ITenantContext` + `TenantConnectionResolver` (zoned vs dedicated). |
| [licensing/](licensing/) | `Central.Licensing` | License key issuing/validation, subscription service, module license checks. |
| [observability/](observability/) | `Central.Observability` | Correlation IDs, Serilog enrichers, Prometheus metrics helpers. |
| [collaboration/](collaboration/) | `Central.Collaboration` | Presence tracking (who's viewing what, live). |
| [protection/](protection/) | `Central.Protection` | Client-side integrity checks + tamper detection. |
| [update-client/](update-client/) | `Central.UpdateClient` | Desktop auto-update flow. |

## Conventions

- **Assembly name matches folder.** `libs/engine/` → `Central.Engine.csproj`, namespace `Central.Engine`. If a folder is `foo-bar/`, the assembly is still `Central.FooBar` (kebab-to-pascal).
- **No upward references.** `libs/` can reference `libs/` but never `apps/`, `services/`, or `modules/`. If a lib needs something from those, the dependency is inverted (the app/service calls the lib, not the other way around).
- **Low coupling.** A lib should be useful to something that doesn't exist yet. If your lib's only consumer is one specific app, it probably belongs in that app's folder instead.
- **Test coverage lives in `tests/dotnet/`.** Per-lib test projects are permitted if the lib is large enough to warrant them; consolidated is the default.
- **Avoid circular references.** `libs/engine` is the base; most other libs depend on it. Avoid A→B→A loops by refactoring the common surface up into `engine/` or down into a new shared lib.

## Adding a new library

1. `libs/<short-name>/` — folder.
2. `dotnet new classlib -n Central.<Name>` inside it.
3. `dotnet sln Central.sln add libs/<short-name>/Central.<Name>.csproj`.
4. Reference from consumers with `<ProjectReference Include="..\..\libs\<short-name>\Central.<Name>.csproj" />`.
5. Add to this README's table.
