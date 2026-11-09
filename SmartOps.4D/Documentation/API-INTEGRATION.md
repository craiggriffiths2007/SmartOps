# SmartOps 4D API integration guide

This guide helps developers integrate with **SmartOps.4D**, inspect the implementation in 4D, and test workflows against the automatically seeded **integration test data**.

SmartOps has two independent clients:

- **SmartOps Survey** (the [Android app on Google Play](https://play.google.com/store/apps/details?id=com.authorityx.smartops)) communicates **directly with the 4D HTTP API**. Its source code is not included in this repository.
- **SmartOps.4D.Web** is an ASP.NET Core frontend that communicates with 4D through a **.NET bridge**. The bridge is not used by the Android app.

The 4D server remains the authoritative data store. The web application's request-scoped SQLite database is a temporary query workspace, not a second business database.

> **Local development only:** The published integration test accounts, optional agent-code-only authentication, and HTTP port 8045 are for testing on a trusted private network. Do not expose the server with its supplied test credentials to the public internet.

## Getting started

1. Clone SmartOps and open `SmartOps.4D/Project/smartops4d.4DProject` in 4D.
2. Create a **new, empty 4D data file** when prompted.
3. The 4D `onStartup` method calls `GO_StartLAN`, which seeds fictional integration test records into an entirely empty database and starts HTTP on port **8045**. No success dialog is shown.
4. From the server PC, check [http://127.0.0.1:8045/api/health](http://127.0.0.1:8045/api/health). If 4D listens only on its LAN interface, use `http://YOUR-PC-IP:8045/api/health` instead.
5. Start `SmartOps.4D.Web` in Visual Studio to test the browser-facing workflow, or connect the Android app to `http://YOUR-PC-IP:8045` to test the mobile workflow.

The supplied web integration-testing account uses company `SMARTOPS`, username `admin` and password `Password1!`. Change these credentials before any non-local deployment.

## Useful 4D methods to explore

All links below are repository-relative, so they work both in GitHub and a local checkout.

| 4D method or class | What it does / why it matters |
| --- | --- |
| [`onStartup`](../Project/Sources/DatabaseMethods/onStartup.4dm) | Automatically calls the LAN startup method when the 4D database opens |
| [`GO_StartLAN`](../Project/Sources/Methods/GO_StartLAN.4dm) | Sets HTTP port 8045 and LAN binding, enables HTTPS only when certificates exist, and seeds an empty integration database |
| [`GO_Start`](../Project/Sources/Methods/GO_Start.4dm) | Manual entry point to the same startup sequence |
| [`GO_Stop`](../Project/Sources/Methods/GO_Stop.4dm) | Stops the API server |
| [`GO_Seed`](../Project/Sources/Methods/GO_Seed.4dm) | Imports fictional integration test records; protected against seeding nonempty mapped tables |
| [`GO_UpgradeCompanyCode`](../Project/Sources/Methods/GO_UpgradeCompanyCode.4dm) | Handles the integration test company's earlier code variants |
| [`HTTPHandlers.json`](../Project/Sources/HTTPHandlers.json) | Registers the API route patterns and routes requests to the HTTP handler |
| [`SmartOpsHTTP.handle`](../Project/Sources/Classes/SmartOpsHTTP.4dm) | Reads incoming requests and JSON, manages write transactions, dispatches calls and formats error responses |
| [`SmartOpsService.dispatch`](../Project/Sources/Classes/SmartOpsService.4dm) | Implements the general `/api/*` routes for the web frontend and other integrations |
| [`SmartOpsService.login` / `authenticate`](../Project/Sources/Classes/SmartOpsService.4dm) | Authenticates web/API users and checks their bearer sessions |
| [`SmartOpsService.row` / `rows` / `save` / `publicRow`](../Project/Sources/Classes/SmartOpsService.4dm) | Reusable tenant-scoped ORDA access, persistence and API serialization helpers |
| [`SmartOpsWebBridge.snapshot` / `commit`](../Project/Sources/Classes/SmartOpsWebBridge.4dm) | Reads data for the ASP.NET frontend and commits changes back to 4D |
| [`SmartOpsSurveyService.dispatch`](../Project/Sources/Classes/SmartOpsSurveyService.4dm) | Routes the Android app's survey, job, vehicle, media and messaging requests |
| [`SmartOpsSurveyService.login` / `authenticate`](../Project/Sources/Classes/SmartOpsSurveyService.4dm) | Issues and validates agent tokens for the mobile API |
| [`SmartOpsSurveyService.accessibleJobs` / `headForJob`](../Project/Sources/Classes/SmartOpsSurveyService.4dm) | Enforces agent job visibility and prepares job downloads |
| [`SmartOpsSurveyService.sendJobs` / `checks`](../Project/Sources/Classes/SmartOpsSurveyService.4dm) | Saves completed work and vehicle/safety check submissions |
| [`SmartOpsSurveyService.sendMedia` / `mediaFile`](../Project/Sources/Classes/SmartOpsSurveyService.4dm) | Handles media uploads, chunk assembly and tenant-scoped file storage |

The most useful starting point when adding an endpoint is usually **`SmartOpsService.dispatch`** for general API routes or **`SmartOpsSurveyService.dispatch`** for Android-compatible routes. Both are reached through **`SmartOpsHTTP.handle`**.

## HTTP routes and transport

Routes are registered in [`HTTPHandlers.json`](../Project/Sources/HTTPHandlers.json). Use `application/json` for requests with a JSON body.

### General 4D API (ASP.NET bridge and custom integrations)

| Route | Verb | Purpose |
| --- | --- | --- |
| `/api/health` | GET | Unauthenticated API health response |
| `/api/auth/login` | POST | Sign in with company code, username and password; returns a bearer token |
| `/api/auth/logout` | POST | Revoke the current bearer session |
| `/api/web/snapshot` | GET | Read the authenticated tenant's data for the ASP.NET frontend |
| `/api/web/commit` | POST | Persist the web bridge's tenant changes to 4D |
| `/api/auth/profile` | GET / POST | Read and update the authenticated user's profile |
| `/api/auth/preferences` | GET / POST | Read and update interface preferences |
| `/api/error-log` | GET | Read integration errors (admin role) |

The general API also contains resource endpoints for contracts, accounts and business workflows. The authoritative route definitions and access checks are in [`SmartOpsService.dispatch`](../Project/Sources/Classes/SmartOpsService.4dm).

### Example: check the 4D server

```powershell
Invoke-RestMethod -Uri "http://127.0.0.1:8045/api/health"
```

The current handler returns JSON including `service`, `version`, `surveyApi`, and `at`. This check requires no login.

### Example: authenticate to the general API

```powershell
$base = "http://YOUR-PC-IP:8045"
$login = Invoke-RestMethod -Method Post -Uri "$base/api/auth/login" `
  -ContentType "application/json" `
  -Body (@{
    TenantCode = "SMARTOPS"
    Username = "admin"
    Password = "Password1!"
  } | ConvertTo-Json)

$headers = @{ Authorization = "Bearer $($login.token)" }
$snapshot = Invoke-RestMethod -Method Get -Uri "$base/api/web/snapshot" -Headers $headers
$snapshot.tables.Keys
```

The token is a **web/API session token** with a limited lifetime (currently `expiresIn: 3600` seconds). The snapshot and commit endpoints are intended for the ASP.NET bridge; implementing a separate client should use the appropriate API resource routes and enforce tenant boundaries.

## Android survey API

The mobile app uses the **native 4D API**, not `/api/web/*`. Its main routes are HTTP **POST**:

| Endpoint | Main 4D method | Purpose |
| --- | --- | --- |
| `/AgentLogin` | `SmartOpsSurveyService.login` | Obtain an agent authentication token |
| `/GetJobs` | `accessibleJobs`, `headForJob` | Download permitted jobs and saved work |
| `/GetDynamicQuestions` | `publishedQuestions` | Retrieve the current dynamic question set |
| `/GetVehicles` | `dispatch` | Download vehicles for checks |
| `/SendJobs` | `sendJobs` | Upload job and survey results |
| `/SendChecks` | `checks` | Upload vehicle and safety checks |
| `/SendMedia` | `sendMedia` | Upload pictures, drawings and video |
| `/GetImage` | `validFilename`, `mediaFile` | Retrieve an uploaded media file |
| `/GetUpdateContractNotes` | `notes` | Read/update contract notes |
| `/GetAgentMessages` | `dispatch` | Retrieve messages |
| `/MarkAgentMessageRead` | `dispatch` | Mark a message as read |
| `/Home/SurveyAppVersion` | `appVersion` | Check app version and record version usage |

### Example: login as an integration test agent

```powershell
$base = "http://YOUR-PC-IP:8045"
$agentLogin = Invoke-RestMethod -Method Post -Uri "$base/AgentLogin" `
  -ContentType "application/json" `
  -Body (@{
    TenantCode = "SMARTOPS"
    AgentCode = "ADMIN"
    SerialNumber = ""
    CurrentVersion = "1.0"
    CurrentBuild = "1"
  } | ConvertTo-Json)

$jobs = Invoke-RestMethod -Method Post -Uri "$base/GetJobs" `
  -ContentType "application/json" `
  -Body (@{
    TenantCode = "SMARTOPS"
    AgentCode = "ADMIN"
    AuthenticationString = $agentLogin.authenticationString
  } | ConvertTo-Json)
```

An agent whose `SerialNumber` field is **blank** may currently sign in by company and agent code alone for integration testing. If a serial number is assigned, the existing device registration rules apply, including requiring privileged agents to use their own registered device. **Agent-code-only login is not suitable for production authentication.**

For complete request/response details and data shapes, see the existing [survey API reference](SURVEY-API-REFERENCE.md). Some historical examples in that reference refer to older mobile-authentication behaviour; the [current `SmartOpsSurveyService` source](../Project/Sources/Classes/SmartOpsSurveyService.4dm) is authoritative.

## Integrating or extending the API

1. **Choose the appropriate API surface:** mobile-compatible routes belong in `SmartOpsSurveyService`; general integration and web routes belong in `SmartOpsService`.
2. **Register the route** in `HTTPHandlers.json` if the existing patterns do not match it.
3. **Reuse `SmartOpsHTTP.handle`** for request parsing, transaction management and error handling.
4. **Enforce authentication, roles and tenant isolation** before reading or modifying records. The `GoTenantId` checks in `SmartOpsService` and `SmartOpsSurveyService` illustrate the approach.
5. **Use the ORDA helpers** such as `row`, `rows`, `save` and `publicRow` rather than trusting a client-supplied tenant identifier.
6. **Exercise the integration test data** from a fresh empty database, including successful requests, unauthorized requests and repeated uploads where applicable.

### Useful implementation references

- [4D schema and table mapping](SCHEMA.md)
- [Survey API reference and payloads](SURVEY-API-REFERENCE.md)
- [Local survey integration test setup](SURVEY-TRIAL-SETUP.md)
- [ASP.NET bridge code](../../SmartOps.4D.Web/Bridge/BridgeSession.cs)
- [ASP.NET API configuration](../../SmartOps.4D.Web/Program.cs)

The API contract and security policy are determined by the checked-in 4D source. Update the documentation and tests alongside API changes.
