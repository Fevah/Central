---
name: Always use latest stable versions
description: When picking image tags, package versions, or tool releases — query the registry/repo for the latest stable, never guess
type: feedback
originSessionId: b843b558-8aff-4405-a09b-8537ec6eb980
---
When choosing a version (Docker image tag, npm/cargo/pip/nuget package, Flutter/Dart SDK, K8s release), **always query for the latest stable** before writing it into config.

**Why:** Guessing tags wastes time on `ImagePullBackOff` errors and security debt from old versions. The user has had multiple cases where my hard-coded version (e.g. `pgbouncer:1.23.1`, `bitnami/pgbouncer:1.23.1`, `edoburu/pgbouncer:1.25.1`) didn't exist on Docker Hub.

**How to apply** (before committing any version):

1. **Docker images**: `curl -s "https://hub.docker.com/v2/repositories/<owner>/<image>/tags/?page_size=20" | jq -r '.results[].name' | head` — pick the highest stable (skip `-rc`, `-alpha`, `-beta`).

2. **GitHub releases**: `gh release list --repo owner/name --limit 5` or `curl -s https://api.github.com/repos/owner/name/releases/latest | jq -r .tag_name`.

3. **npm packages**: `npm view <pkg> version` for latest, `npm view <pkg> versions --json | tail -10` for recent.

4. **Cargo crates**: `cargo search <crate>` shows latest, or `curl https://crates.io/api/v1/crates/<crate>` returns `max_stable_version`.

5. **NuGet packages**: `dotnet package search <pkg>` or check https://www.nuget.org/packages/<pkg>.

6. **Flutter/Dart packages**: `dart pub outdated` after declaring, or `https://pub.dev/api/packages/<pkg>` returns `latest.version`.

Maintain a **version matrix** in `docs/CREDENTIALS.md` showing every external dep with current version + last-checked date. Update it whenever a service is rebuilt.

When upgrading, bump to the latest stable that's compatible with the workspace constraints. Don't pin to old versions just because they "worked before" — the user wants current.
