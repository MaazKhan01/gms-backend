# Deploying the DMS API

One container image serves every host. `Dockerfile` at the repo root is the
deployable unit; each platform is only told where to find it and what port to
route to.

| File | Purpose |
|---|---|
| `Dockerfile` | Two-stage build — SDK 9.0 compiles, `aspnet:9.0-noble-chiseled` ships. |
| `.dockerignore` | Keeps Windows `bin/`/`obj/` out of the Linux build context. |
| `vercel.json` | Vercel: one `container` service, all paths rewritten to it. |

---

## Railway (current)

Railway auto-detects the `Dockerfile` at the repo root — `development` already
carries one, whose entrypoint is
`sh -c "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet API.dll"`. There is
no `railway.json`, and none is needed: the build and start are both correct by
default.

### Port

**Do not set `PORT` yourself.** Railway injects it and routes the public domain
to it; a hand-set value that disagrees with what Railway routes to presents as a
deploy that builds fine and then times out. The Dockerfile's entrypoint already
resolves it — `ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080}` — falling back to
8080 only when nothing injects one. `ASPNETCORE_ENVIRONMENT` is set there too,
so it does not belong in the variables either.

Generate the public URL under **Settings ▸ Networking ▸ Generate Domain**.

### Branch matters — the two lineages are not interchangeable

**Deploy `development`. Never point this database at `feature/gms-domain`.**

The two branches carry incompatible migration lineages:

| Branch | Migrations | Seeder |
|---|---|---|
| `development` | 9, starting `20260818170123_first migration` | none — `DataSeeder.cs` does not exist |
| `feature/gms-domain` | 1, `20260723163615_first migration` | `Program.cs:59` calls it, and it runs `MigrateAsync` |

`olympic-dms` records the `development` set. Booting `feature/gms-domain`
against it makes EF see its own lone migration as un-applied, run its `Up()`,
and fail on `There is already an object named 'AccountRequests'` — the symptom
looks like a broken database, but the database is correct and the branch is
wrong.

On `development` nothing migrates at all: there is no `Database.Migrate` call
anywhere, and the `DataSeeder` line in `Program.cs` is commented out. The schema
is managed deliberately, outside the app. Keep it that way — a deploy is the
wrong place to change a schema.

If a deploy fails this way again, check the commit SHA in the Railway deploy log
against `git rev-parse --short origin/development` before looking at the
database.

### Health check

None is configured. The `/health` endpoint exists only in an uncommitted local
edit, so pointing Railway at one would fail every deploy on a 404. Once that
endpoint is committed, add a `railway.json` with `healthcheckPath: "/health"`.

### Environment variables

Set under **Variables**. ASP.NET Core maps `__` (double underscore) onto the
config `:` separator, and `AddEnvironmentVariables()` is last in the chain, so
each of these overrides `appsettings.json`.

| Variable | Value |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string |
| `Authentication__Jwt__JwtSecretKey` | ≥32 chars — **not** the `dms-local-dev-only…` value that is in git |
| `Authentication__Jwt__Issuer` | The Railway URL, e.g. `https://dms-api.up.railway.app` |
| `Authentication__Jwt__Audience` | `https://dms-frontend-bice.vercel.app` |
| `AllowedOrigins` | `https://dms-frontend-bice.vercel.app` (comma-separated for more) |
| `FrontendUrl` | `https://dms-frontend-bice.vercel.app` — invitation links are built from this |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

Optional, all with working defaults in code: `AzureStorage__BlobContainerName`
(`media`), `AzureCommunicationServiceConfig__BccEmail`, `AppName`,
`Notifications__RetentionDays` (90), `Authentication__Jwt__ExpirationMinutes`
(60), `Authentication__Jwt__RefreshTokenExpirationDays` (30). `Firebase__*` too —
the push provider no-ops when unconfigured.

**Not needed:** `AzureStorage:IdentifierName`, `Seed:*`, `Caching:Redis` and
`Authentication:Adb2c` are all present in `appsettings.json` but read by no code
that runs (`AddJwtAuthentication` is never called).

### Two that look optional and are not

Both already have live values committed in `appsettings.json`, so **nothing will
fail today** — but if that file is ever removed from the image, these become
hard requirements:

- `AzureStorage__BlobConnectionString` — `BlobSasMiddleware` takes `IBlobService`
  per invocation and sits ahead of `MapControllers()`, so an empty value throws
  on **every request**, not just uploads.
- `AzureCommunicationServiceConfig__COMMUNICATION_SERVICES_CONNECTION_STRING`
  and `__EmailSenderInfo` — `EmailService`'s constructor throws without them, and
  `AuthController` → `IAuthService` → `IEmailService`, so **login** breaks.

### What works here that would not on a serverless host

Railway runs a long-lived container, so Hangfire background jobs (Events/Guests
bulk import, the daily notification cleanup) and SignalR's `/realtimehub` both
behave normally. Neither would on Vercel. Note that on the free/trial plan the
service stops when the credit runs out, which takes the queue with it.

---

## Frontend

Set `VITE_API_URL` on the frontend Vercel project to `https://<railway-host>/api`
and redeploy — it is a build-time variable, so a redeploy is required. Add the
same origin to `AllowedOrigins` above or CORS will reject it.

---

## Secrets

`API/appsettings.json` and `API/Configurations/firebase.json` are **tracked in
git** with live values — the production SQL connection string, the Azure Blob
account key, the ACS connection string and a Firebase private key — and the
build copies them into the image.

Setting the variables above means the running app does not *use* the committed
values, but it does not un-leak them. Before this is anything but temporary:
rotate those credentials, `git rm --cached` both files, add them to
`.gitignore`, and purge them from history.

---

## Vercel (deferred)

`vercel.json` is in place and points at the same `Dockerfile`. Before using it,
read the caveats: Hangfire jobs stop running between requests, `/realtimehub`
connections will not survive, request bodies cap at ~4.5 MB, and function egress
IPs are dynamic — which the SQL Server firewall has to allow.
