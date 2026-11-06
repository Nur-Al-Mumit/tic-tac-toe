# CI/CD

*The GitHub Actions delivery pipeline for Multiplayer Tic-Tac-Toe: trunk-based branching with required PR checks, the CI workflow (backend, frontend, E2E, security), Docker image publishing to GHCR, and the ordered CD rollout to the VPS with health verification and a safe rollback path.*

The pipeline implements the PRD's fixed CI shape — restore → build → test (containerless — no Docker needed for the test step) → lint client → build client → build Docker images — and the deployment contract described in [docker-and-environments](docker-and-environments.md). Job names match the summary table in [testing-strategy §12](../05-quality/testing-strategy.md#12-ci-integration).

## 1. Branching Model

Trunk-based development:

- `main` is the only long-lived branch; it is always deployable.
- All work happens on short-lived branches (hours to ~2 days) named `feat/<slug>`, `fix/<slug>`, `chore/<slug>`, `docs/<slug>`, pushed and opened as a PR to `main`.
- Every PR runs the full CI gate (§2, §11); **all required checks must pass before merge** — no admin overrides, no "merge anyway".
- Squash-merge keeps `main` linear (no merge commits); the branch is deleted on merge.
- Feature flags are not needed at MVP scope — incomplete features stay on their branch until done, because branches are short.

## 2. CI Pipeline

One workflow, `.github/workflows/ci.yml`, triggered by every PR and every push to `main`/tag `v*`. Trimmed to the essential lines:

```yaml
name: ci

on:
  pull_request:
  push:
    branches: [main]
    tags: ["v*"]

concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true

permissions:
  contents: read

jobs:
  backend-unit:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v5
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
          cache: true                                   # NuGet cache keyed on lock files
          cache-dependency-path: '**/packages.lock.json'
      - run: dotnet restore --locked-mode
      - run: dotnet build -c Release --no-restore
      - run: dotnet format --verify-no-changes
      - name: Fail if the model has no committed migration
        run: |
          dotnet tool install --global dotnet-ef --version 10.*
          dotnet ef migrations has-pending-model-changes \
            --project src/TicTacToe.Infrastructure \
            --startup-project src/TicTacToe.Api
      - run: >
          dotnet test test/TicTacToe.Application.UnitTests
          -c Release --no-build --collect:"XPlat Code Coverage"
          --results-directory artifacts/backend-unit
      - uses: actions/upload-artifact@v4
        with: { name: backend-unit, path: artifacts/backend-unit }

  frontend-unit:
    runs-on: ubuntu-latest
    defaults: { run: { working-directory: client } }
    steps:
      - uses: actions/checkout@v5
      - uses: actions/setup-node@v5
        with: { node-version: 24, cache: npm, cache-dependency-path: client/package-lock.json }
      - run: npm ci
      - run: npx prettier --check .
      - run: npx eslint .
      - run: npx vitest run --coverage          # thresholds enforced in vitest.config.ts
      - run: npm run build -- --configuration production
      - uses: actions/upload-artifact@v4
        with: { name: frontend-coverage, path: client/coverage }

  security-audit:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v5
        with: { fetch-depth: 0 }
      - uses: gitleaks/gitleaks-action@v2         # no secrets in the repo, ever
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: 10.0.x }
      - run: dotnet audit                         # NuGet vulnerability scan (.NET 10 SDK)
      - uses: actions/setup-node@v5
        with: { node-version: 24, cache: npm, cache-dependency-path: client/package-lock.json }
      - run: npm audit --audit-level=high
        working-directory: client

  docker:
    if: github.event_name == 'push'               # images only for main and tags, not PRs
    runs-on: ubuntu-latest
    needs: [backend-unit, frontend-unit, security-audit]
    permissions: { contents: read, packages: write }
    steps:
      - uses: actions/checkout@v5
      - uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}   # short-lived, scoped to this run
      - uses: docker/metadata-action@v5
        id: meta-api
        with:
          images: ghcr.io/${{ github.repository_owner }}/tictactoe-api
          tags: |
            type=sha,prefix=sha-
            type=ref,event=tag                    # v1.2.3 -> 1.2.3
            type=raw,value=latest,enable={{is_default_branch}}
      - uses: docker/build-push-action@v6
        with:
          context: .
          file: src/TicTacToe.Api/Dockerfile
          push: true
          tags: ${{ steps.meta-api.outputs.tags }}
          cache-from: type=gha
          cache-to: type=gha,mode=max
      - uses: docker/metadata-action@v5            # same tag scheme for the client image
        id: meta-client
        with:
          images: ghcr.io/${{ github.repository_owner }}/tictactoe-client
          tags: |
            type=sha,prefix=sha-
            type=ref,event=tag
            type=raw,value=latest,enable={{is_default_branch}}
      - uses: docker/build-push-action@v6
        with:
          context: ./client
          push: true
          tags: ${{ steps.meta-client.outputs.tags }}
          cache-from: type=gha
          cache-to: type=gha,mode=max
```

A second workflow, `.github/workflows/load.yml`, runs the k6 load/NFR scenarios on a `schedule` (nightly) and `workflow_dispatch` (pre-release); it is never a PR gate (rationale and gates in [testing-strategy §11](../05-quality/testing-strategy.md#11-performance-and-load-verification)).

Notes on the workflow:

- **No Docker needed in CI:** every shipped job is containerless (ADR-0015) — the backend suite is pure unit, the frontend runs vitest, and image builds happen in the deploy job.
- `dotnet restore --locked-mode` + `packages.lock.json` makes restores reproducible and is what `setup-dotnet`'s `cache: true` keys on (§4).
- `dotnet ef migrations has-pending-model-changes` enforces the migration discipline from [migrations-and-indexes §1.1](../03-database/migrations-and-indexes.md#11-tooling-and-commands).
- The `docker` job builds only after all five gates pass, and never on PRs — PR builds are validated by the compile/build steps in `backend unit (NUnit + Shouldly)`/`frontend-unit` and the `docker compose up -d --build` in `e2e`.

## 3. Test Reporting, Coverage and Artifacts

- Every `dotnet test` run publishes TRX results and coverlet `coverage.cobertura.xml` under `artifacts/<job>`; each job uploads its folder with `actions/upload-artifact@v4`, so a failed PR has downloadable results without reading raw logs.
- ReportGenerator merges the three test projects' coverage into one HTML report (`backend unit (NUnit + Shouldly)` artifact); frontend coverage is the `vitest --coverage` HTML report (`frontend-coverage` artifact). Uploaded artifacts are retained per repository settings; they are for humans — the gates below are what block merges.
- Coverage gates (fail the job, summarized in [testing-strategy §10.2](../05-quality/testing-strategy.md#102-gates-ci-fails-below)): Domain ≥ 90% line, Application ≥ 80% line, backend combined ≥ 70% line, frontend core ≥ 70% statements.
- The E2E job uploads Playwright's HTML report and failure traces on red runs (`actions/upload-artifact@v4` with `if: always()`).
- The deploy job (§5) additionally uploads the exact `migrations.sql` it applied — the auditable record of every schema change that reached a server.

## 4. Caching

| Cache | Mechanism | Key / notes |
| --- | --- | --- |
| NuGet packages | `actions/setup-dotnet@v4` with `cache: true` | keyed on `**/packages.lock.json`; requires `--locked-mode` restores to be meaningful |
| npm packages | `actions/setup-node@v5` with `cache: npm` | keyed on `client/package-lock.json`; `npm ci` then installs from cache instead of the registry |
| Docker layer cache | `docker/build-push-action@v6` with `type=gha` | layer cache stored in the GitHub Actions cache service; the publish layer re-runs only when csproj/package files change |

## 5. CD Pipeline

Deployments are jobs in the same `ci.yml`, running only after the `docker` job succeeds: `main` → staging automatically; tag `v*` → production after an approval (configured as a required reviewer on the GitHub `production` environment). The VPS pulls pre-built images from GHCR and applies migrations **before** new code starts.

```yaml
  deploy-staging:
    if: github.event_name == 'push' && github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    needs: [docker]
    environment: staging
    steps:
      - uses: actions/checkout@v5
      - uses: ./.github/actions/deploy            # shared composite action (below)
        with:
          ssh-host: ${{ secrets.SSH_HOST }}
          ssh-user: ${{ secrets.SSH_USER }}
          ssh-key: ${{ secrets.SSH_PRIVATE_KEY }}
          deploy-dir: /opt/tictactoe-staging
          image-tag: sha-${{ github.sha }}
          public-url: https://staging.play.tictacttoe.example

  deploy-production:
    if: startsWith(github.ref, 'refs/tags/v')
    runs-on: ubuntu-latest
    needs: [docker]
    environment: production                        # requires manual approval in repo settings
    steps:
      - uses: actions/checkout@v5
      - uses: ./.github/actions/deploy
        with:
          ssh-host: ${{ secrets.SSH_HOST }}
          ssh-user: ${{ secrets.SSH_USER }}
          ssh-key: ${{ secrets.SSH_PRIVATE_KEY }}
          deploy-dir: /opt/tictactoe
          image-tag: ${{ github.ref_name }}        # e.g. 1.2.3 (metadata-action strips "v")
          public-url: https://play.tictacttoe.example
```

The composite action `.github/actions/deploy/action.yml` (runner side):

```yaml
runs: using: "composite"
steps:
  - uses: actions/setup-dotnet@v4
    with: { dotnet-version: 10.0.x, cache: true, cache-dependency-path: '**/packages.lock.json' }
  - shell: bash
    run: |
      dotnet tool install --global dotnet-ef --version 10.*
      dotnet ef migrations script --idempotent \
        --project src/TicTacToe.Infrastructure \
        --startup-project src/TicTacToe.Api \
        --output artifacts/migrations.sql
  - uses: actions/upload-artifact@v4
    with: { name: migrations-${{ inputs.image-tag }}, path: artifacts/migrations.sql }
  - uses: appleboy/scp-action@v0.1.7              # ship the script to the VPS
    with:
      host: ${{ inputs.ssh-host }}
      username: ${{ inputs.ssh-user }}
      key: ${{ inputs.ssh-key }}
      source: artifacts/migrations.sql
      target: /tmp/deploy-${{ inputs.image-tag }}
  - uses: appleboy/ssh-action@v1                  # ordered rollout
    with:
      host: ${{ inputs.ssh-host }}
      username: ${{ inputs.ssh-user }}
      key: ${{ inputs.ssh-key }}
      script: |
        set -euo pipefail
        cd ${{ inputs.deploy-dir }}
        export API_TAG=${{ inputs.image-tag }} CLIENT_TAG=${{ inputs.image-tag }}
        COMPOSE="docker compose -f docker-compose.yml -f docker-compose.prod.yml"

        # 0) pre-deploy backup (last resort safety net)
        $COMPOSE exec -T postgres pg_dump -U tictactoe -Fc tictactoe \
          > backups/pre-deploy-$(date +%F-%H%M).dump

        # 1) database first — idempotent script, re-runnable
        $COMPOSE pull postgres && $COMPOSE up -d --no-deps postgres
        $COMPOSE exec -T postgres psql -v ON_ERROR_STOP=1 \
          -U tictactoe -d tictactoe < /tmp/deploy-${{ inputs.image-tag }}/artifacts/migrations.sql

        # 2) api next — new code must find the schema already in place
        $COMPOSE pull api && $COMPOSE up -d --no-deps api
        timeout 90 bash -c \
          'until docker compose -f docker-compose.yml -f docker-compose.prod.yml \
             exec -T api curl -fsS http://localhost:8080/health >/dev/null; do sleep 3; done'

        # 3) client last — SPA rollout is invisible until the backend serves
        $COMPOSE pull client && $COMPOSE up -d --no-deps client

        # 4) post-deploy verification through the public edge
        curl -fsSk ${{ inputs.public-url }}/health
        curl -fsSk -o /dev/null ${{ inputs.public-url }}/
```

Ordering rationale: **migrate → api → client**. The idempotent script can run against the old app version because migrations are backwards compatible (below); the new API is health-verified before nginx (the only public surface) is replaced; the SPA follows last so no user ever gets new static assets talking to an old API.

### Rollback

| Layer | Procedure |
| --- | --- |
| App/SPA (routine) | Redeploy the previous immutable tag: `API_TAG=sha-<previous> CLIENT_TAG=sha-<previous> docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d api client` — every `sha-*` tag stays in GHCR, so this is a two-minute operation with no rebuild |
| Database (last resort) | Restore the `backups/pre-deploy-*.dump` taken in step 0 of the deploy (procedure in [docker-and-environments §10](docker-and-environments.md#10-postgresql-backups)); expect downtime — this exists for data corruption, not for code regressions |

Backwards-compatibility rule that makes app-level rollback safe: a release's migrations must work with the **previous** API version (expand/contract). Add the nullable column, dual-write, *then* remove the old path in a later release; destructive operations (`DropColumn`, `DropTable`) never ship in the same release as the code that stops using the object — and EF Core generates no automatic "down", so there is no `dotnet ef database update` undo to tempt anyone.

## 6. Environment Secrets

All values below are **examples/placeholders only — never real values**; every real secret lives in GitHub environment secrets or the VPS `.env`, never in the repository (gitleaks-enforced).

| Secret | Stored where | Used by | Example placeholder |
| --- | --- | --- | --- |
| `GITHUB_TOKEN` | automatic | `docker` job push to GHCR | (provided by Actions; no configuration) |
| `SSH_HOST` | GitHub environments `staging` / `production` | deploy jobs | `203.0.113.10` |
| `SSH_USER` | same | deploy jobs | `deploy` |
| `SSH_PRIVATE_KEY` | same | scp/ssh actions | `-----BEGIN OPENSSH PRIVATE KEY-----b3BlbnNzaC1rZXk…` |
| `GHCR_PULL_TOKEN` | VPS (`docker login ghcr.io`, bootstrap only) | `docker compose pull` on the VPS | `github_pat_11AB2C…` (fine-grained, `read:packages`) |
| `POSTGRES_PASSWORD` | VPS `/opt/tictactoe/.env` (chmod 600) | `postgres` container | `s9f2K…` (`openssl rand -base64 24`) |
| `ConnectionStrings__Default` | VPS `.env` | `web` container | `Host=postgres;Port=5432;Database=tictactoe;Username=tictactoe;Password=…` |
| `Jwt__Key` | VPS `.env` | `web` container | 64 hex chars (`openssl rand -hex 32`); rotation procedure in [auth-identity-jwt](../02-backend/auth-identity-jwt.md) |
| `Jwt__Issuer` / `Jwt__Audience` | VPS `.env` | `web` container | `https://play.tictacttoe.example` / `tic-tac-toe-client` |
| `Cors__Origins` | VPS `.env` | `web` container | `https://play.tictacttoe.example` |

Split by design: GitHub holds only SSH coordinates (so a leaked Actions secret yields nothing but a shell to a box that reads its real secrets from a `chmod 600` file), and the deploy user on the VPS has rights limited to the compose project — not root.

## 7. Versioning and Image Tags

**SemVer from git tags** (`vMAJOR.MINOR.PATCH`), chosen over CalVer because the API (`/api/v1`) and the shared contracts version semantically and a breaking-change signal in the tag is worth more than a release date:

| Event | Image tags produced | Consumed by |
| --- | --- | --- |
| Push to `main` | `sha-<full-sha>` (immutable), `latest` (rolling) | staging deploy pinned to `sha-<sha>` |
| Tag `v1.2.3` | `sha-<sha>`, `1.2.3` (immutable) | production deploy pinned to `1.2.3` |
| Nightly / dispatch | — (load workflow builds nothing; it uses compose images) | — |

Only immutable tags (`sha-*`, semver) are ever referenced by deployments; `latest` exists as a convenience alias for humans and is never what production runs. Rollback (§5) is therefore just "reference the previous immutable tag".

## 8. Release Checklist

Security items live in the canonical [pre-release security checklist](../05-quality/security.md#8-pre-release-security-checklist); the deployment-side items are:

| # | Item | Verified by |
| --- | --- | --- |
| 1 | CI green on the release commit, including `security-audit` and coverage gates | workflow run summary |
| 2 | `migrations.sql` for the release reviewed in the PR (no destructive ops — §5 rule) | PR review + artifact |
| 3 | Load workflow green against the release build (200 ms p95, 500 games) | [testing-strategy §11](../05-quality/testing-strategy.md#11-performance-and-load-verification) |
| 4 | Nightly backup verified restorable on staging in the last 30 days | backup drill log |
| 5 | Tag `vX.Y.Z` pushed; `deploy-production` approved and completed | environment deployment log |
| 6 | `curl https://play.tictacttoe.example/health` returns `200`; smoke login + one game played manually | operator |
| 7 | Rollback tag identified and written into the release notes | release notes |

## 9. Pipeline Diagram

```mermaid
flowchart TD
    dev["Developer<br/>branch feat/slug"]
    dev --> pr["PR to main"]
    pr --> gate{"Required CI checks"}

    gate --> bu["backend unit (NUnit + Shouldly)<br/>restore/build/format<br/>NUnit + coverage gates<br/>migration drift check"]
    gate --> bu["backend-unit<br/>NUnit + Shouldly<br/>(containerless)"]
    gate --> fu["frontend-unit<br/>prettier + eslint + vitest<br/>ng build production"]
    gate --> e2e["e2e<br/>Playwright vs compose stack"]
    gate --> sec["security-audit<br/>gitleaks · dotnet audit · npm audit"]

    bu --> merge
    bi --> merge["Squash merge to main"]
    fu --> merge
    e2e --> merge
    sec --> merge

    merge --> img["docker job<br/>build+push api & client to GHCR<br/>sha-* + latest (+ semver on tags)"]
    img --> stg["deploy-staging<br/>auto on main"]
    img --> prod{"deploy-production<br/>tag v* + approval"}
    stg & prod --> roll["Ordered rollout"]

    subgraph roll["Ordered rollout on the VPS"]
        m["1 · pg_dump backup"] --> s["2 · apply idempotent<br/>migrations.sql via psql"]
        s --> a["3 · up -d api<br/>poll /health"]
        a --> c["4 · up -d client<br/>public smoke check"]
    end

    roll --> live["Release live"]
    a -. "health fails" .-> rb["Rollback: redeploy previous<br/>immutable image tag<br/>(DB untouched — expand/contract rule)"]
```

## 10. Badges and Status Conventions

README badges (owner/repo placeholders resolve to the real repository):

```markdown
[![ci](https://github.com/OWNER/tic-tac-toe/actions/workflows/ci.yml/badge.svg)](https://github.com/OWNER/tic-tac-toe/actions/workflows/ci.yml)
[![load](https://github.com/OWNER/tic-tac-toe/actions/workflows/load.yml/badge.svg)](https://github.com/OWNER/tic-tac-toe/actions/workflows/load.yml)
```

- Check names in branch protection are the job ids — `backend-unit`, `frontend-unit`, `security-audit` — so the badge, the required-checks list and the [testing-strategy §12](../05-quality/testing-strategy.md#12-ci-integration) table use one vocabulary.
- Load workflow has a badge but is explicitly **not** a required check; its red badge blocks a release (checklist item 3), not a merge.
- Deployments surface as GitHub environment entries (`staging`, `production`) with the deployed image tag and run URL — that page is the "what is live right now" source of truth.

## 11. PR Validation

| Rule | Setting |
| --- | --- |
| Required status checks | `backend-unit`, `frontend-unit`, `security-audit` — all must pass; PRs cannot be merged while any is failing or missing |
| Branches up to date | required ("strict") — the branch must be current with `main` at merge time |
| History | squash merge only → `main` stays linear; each squash commit is the PR title (`feat: …` / `fix: …` conventional prefixes) |
| PR template | includes the secrets self-check ("no credentials, keys or connection strings in this diff") and links to the checklists above |
| Draft PRs | allowed for early CI signal; the same gates run, blocking applies when marked ready |
| Runner note | All shipped jobs are containerless; an `e2e`/integration job (Docker-based) may return post-V1 per ADR-0015 |

## Assumptions

- Deployment target is a single VPS reached over SSH with compose (PRD single-node MVP); staging and production are separate directories on their own hosts (or one host with two compose project names and volumes) — not Kubernetes.
- GHCR is the registry (native `GITHUB_TOKEN` push, no extra account); the VPS logs in once with a fine-grained `read:packages` PAT whose rotation is a manual bootstrap task.
- `.github/actions/deploy` is a composite action both deploy jobs share; the YAML shown is its contract (inputs trimmed to the essential lines).
- `appleboy/scp-action@v0.1.7` and `appleboy/ssh-action@v1` are the SSH transport; any equivalent transport (e.g. `ssh-agent` + plain `ssh`) is acceptable as long as the ordered-rollout script is preserved verbatim.
- Action major versions (`checkout@v5`, `setup-dotnet@v4`, `setup-node@v5`, `build-push-action@v6`, `metadata-action@v5`, `upload-artifact@v4`, `gitleaks-action@v2`) reflect current stable majors at writing; Dependabot bumps them via routine PRs.
- Production deploys require a manual approval configured once in GitHub environment settings (not expressible in the workflow file itself).
