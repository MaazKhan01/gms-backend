# Deploying the DMS API

One container image serves every host. `Dockerfile` at the repo root is the
deployable unit; each platform is only told where to find it and what port to
route to.

| File | Purpose |
|---|---|
| `Dockerfile` | Two-stage build — SDK 9.0 compiles, `aspnet:9.0-noble-chiseled` ships. |
| `.dockerignore` | Keeps Windows `bin/`/`obj/` out of the Linux build context. |
| `railway.json` | Railway: Dockerfile builder + health check. |
| `vercel.json` | Vercel: one `container` service, all paths rewritten to it. |

---

## Railway (current)

Railway auto-detects the `Dockerfile` at the repo root — the branch already
carries one, whose entrypoint is
`sh -c "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet API.dll"`. So
`railway.json` only overrides the Pre-Deploy Command; the build needs nothing.

### Port

**Do not set `PORT` yourself.** Railway injects it and routes the public domain
to it; a hand-set value that disagrees with what Railway routes to presents as a
deploy that builds fine and then times out. The Dockerfile's entrypoint already
resolves it — `ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080}` — falling back to
8080 only when nothing injects one. `ASPNETCORE_ENVIRONMENT` is set there too,
so it does not belong in the variables either.

Generate the public URL under **Settings ▸ Networking ▸ Generate Domain**.

### Migrations are NOT run on deploy

`railway.json` overrides the Pre-Deploy Command with a no-op, and that is
deliberate. **Nothing in this codebase applies migrations** — there is no
`Database.Migrate` call anywhere and `DataSeeder` does not exist, only a
commented-out reference to it in `Program.cs`. The schema is managed by hand.

A pre-deploy `dotnet ef database update` against `olympic-dms` fails with
`There is already an object named 'AccountRequests'`. That table is created by
the FIRST migration, so EF starting there means `__EFMigrationsHistory` does not
record the migrations that actually built the schema — history and schema
disagree. Re-running migrations cannot fix that; it can only be fixed by
baselining the history table, which is a deliberate database change, not
something a deploy should do on its own.

Railway's config-as-code takes precedence over dashboard settings, so this file
is what decides it. If the `dotnet ef database update` is in a **Custom Start
Command** rather than Pre-Deploy, this will not catch it — clear it in the
dashboard instead. Do not override `startCommand` here: the Dockerfile's
entrypoint already resolves `$PORT`, and replacing it risks a container that
builds and then never listens.

### Health check

None is configured. The `/health` endpoint exists only in an uncommitted local
edit, so pointing Railway at it would fail every deploy on a 404. Add
`healthcheckPath` to `railway.json` once that endpoint is actually committed.

### Environment variables

Set under **Variables**. ASP.NET Core maps `__` (double underscore) onto the
config `:` separator, and `AddEnvironmentVariables()` is last in the chain, so
each of these overrides `appsettings.json`.

| Variable | Value |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string |
| `Authentication__Jwt__JwtSecretKey` | ≥32 chars — **not** the `dms-local-dev-only…` value that is in git |
| `Authentication__Jwt__Issuer` | The Railway URL, e.g. `https://dms-api.up.railway.app` |
| `Authentication__Jwt__Audience` | `https://dms-ashen-three.vercel.app` |
| `AllowedOrigins` | `https://dms-ashen-three.vercel.app` (comma-separated for more) |
| `FrontendUrl` | `https://dms-ashen-three.vercel.app` — invitation links are built from this |
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
