# WEB_API_INTEGRATION — optional ASP.NET frontend

This secondary guide documents the **ASP.NET Core web frontend** and its 4D API bridge. For Android surveying integrations, start with **[APP_API_INTEGRATION.md](APP_API_INTEGRATION.md)**; the tablet communicates directly with the 4D HTTP server and does **not** require this bridge.

## Architecture

```text
ASP.NET Core MVC (SmartOps.4D.Web)
        |
        v
.NET bridge (BridgeSession; temporary request-scoped SQLite workspace)
        |
        | HTTP JSON, bearer token
        v
4D HTTPHandlers.json -> SmartOpsHTTP.handle -> SmartOpsService
        |
        v
4D ORDA database (authoritative data store)
```

The SQLite workspace is temporary; data is read via a snapshot and written back to 4D through commit. This frontend is optional for integration testing.

## Local setup

1. Open `SmartOps.4D/Project/smartops4d.4DProject` in 4D and create a **new empty data file**. `onStartup` invokes `GO_StartLAN`, seeds the fictional test records only for an empty database, and starts port **8045** on the local network.
2. Check `http://YOUR-PC-IP:8045/api/health` (or `http://127.0.0.1:8045/api/health` when the host permits it).
3. Start `SmartOps.4D.Web` from Visual Studio, or use the optional prebuilt web release. The browser login is at `http://127.0.0.1:5245/Account/Login`.
4. The local test account is company `SMARTOPS`, username `admin`, password `Password1!`. These public test credentials are **not suitable for production**.
5. The web bridge normally discovers the local IPv4 API endpoint automatically. If needed, configure `FourD__BaseUrl=http://YOUR-PC-IP:8045/` for the web process.

Do not expose this test API and its known credentials to the public internet.

## Web-specific API routes

| Route | Verb | Purpose |
| --- | --- | --- |
| `/api/health` | GET | Check the native 4D API health without a session |
| `/api/auth/login` | POST | Authenticate a web/API user and receive a bearer token |
| `/api/auth/logout` | POST | Revoke the bearer session |
| `/api/auth/profile` | GET / POST | Read and modify the web user's profile |
| `/api/auth/preferences` | GET / POST | Web UI user preferences |
| `/api/web/snapshot` | GET | Read authenticated tenant data into the web bridge workspace |
| `/api/web/commit` | POST | Persist authorised tenant changes from the web frontend to 4D |
| `/api/error-log` | GET | Read integration errors (administrator role) |

Additional business resource endpoints live in `SmartOpsService.dispatch`. Consult the implementation for the currently supported routes and access rules.

## Authentication example

```powershell
$base = "http://YOUR-PC-IP:8045"
$login = Invoke-RestMethod -Method Post -Uri "$base/api/auth/login" \`
  -ContentType "application/json" \`
  -Body (@{
    TenantCode = "SMARTOPS"
    Username = "admin"
    Password = "Password1!"
  } | ConvertTo-Json)

$headers = @{ Authorization = "Bearer $($login.token)" }
$snapshot = Invoke-RestMethod -Method Get -Uri "$base/api/web/snapshot" -Headers $headers
$snapshot.tables.Keys
```

The general API login token is not the Android agent authentication token. Sessions are time-limited (currently 3,600 seconds); perform authorised snapshot/commit operations within an active session.

## Where to look in the code

| Component | Location |
| --- | --- |
| HTTP routing | [`HTTPHandlers.json`](../Project/Sources/HTTPHandlers.json) |
| Request handler, errors and transactions | [`SmartOpsHTTP.4dm`](../Project/Sources/Classes/SmartOpsHTTP.4dm) |
| Web/general API routes and access checks | [`SmartOpsService.4dm`](../Project/Sources/Classes/SmartOpsService.4dm) |
| 4D snapshot and commit | [`SmartOpsWebBridge.4dm`](../Project/Sources/Classes/SmartOpsWebBridge.4dm) |
| .NET bridge | [`BridgeSession.cs`](../../SmartOps.4D.Web/Bridge/BridgeSession.cs) |
| ASP.NET configuration | [`Program.cs`](../../SmartOps.4D.Web/Program.cs) |
| Schema and tenant mapping | [`SCHEMA.md`](SCHEMA.md) |

**Tenant isolation:** `GoTenantId` and server-side authentication/authorization must be enforced for all data access and writes; never accept tenant ownership solely from client-supplied IDs. The web bridge must preserve transaction safety and concurrency handling.

**Compatibility policy:** SmartOps starts from a new 4D test dataset; it does not upgrade historical SQL Server or demo databases. Reopen existing test data files without reseeding them. Create a new empty 4D data file when you want to reset test data.
