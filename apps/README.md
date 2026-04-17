# apps/

End-user surfaces. Things a human (or an API consumer) directly launches.

## What's here

| Folder | Stack | What it is |
|--------|-------|------------|
| [desktop/](desktop/) | .NET 10 / WPF / DevExpress 25.2 | `Central.Desktop` — the WPF shell. Hosts the ribbon, dock layout, and every WPF feature module from `modules/`. |
| [web/](web/) | Angular 21 + DevExtreme | Web client — SSE, auth, tasks, admin surfaces. |

## Conventions

- **Naming.** Each app lives in its own folder with a short, lowercase name (`desktop/`, `web/`, `mobile/`). The folder name matches the runtime surface, not the language.
- **.NET apps** keep the `Central.<Name>` assembly convention. Folder name is the short form (`apps/desktop/` → `Central.Desktop.csproj`).
- **No cross-app dependencies.** If two apps need the same code, it belongs in `libs/`. Apps can only reference `libs/`, `modules/`, and (for the .NET apps) `services/api` via `libs/api-client/`.
- **Apps don't talk to the DB directly.** They go through `libs/api-client/` to `services/api/`, which owns persistence via `libs/persistence/`. The WPF desktop retains a direct-DB mode (`libs/persistence/`) for offline / LAN scenarios — that's the exception, not the rule.
- **Tests.** App-level integration or E2E tests land under `tests/` (e.g. `tests/dotnet/` covers the WPF + API stack). Pure-logic unit tests for an app's view-models are fine inline if small, but the convention is consolidated tests.

## Adding a new app

1. Create `apps/<name>/` with your entry-point project.
2. For .NET: `dotnet new` your project, name it `Central.<Name>`, add to `Central.sln` via `dotnet sln Central.sln add apps/<name>/Central.<Name>.csproj`.
3. Reference libs via `<ProjectReference Include="..\..\libs\<lib>\Central.<Lib>.csproj" />`.
4. Never reference `services/` directly — go through `libs/api-client/`.
