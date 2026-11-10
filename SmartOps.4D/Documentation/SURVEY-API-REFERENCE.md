# SmartOps.Survey ↔ 4D API reference

**Audience:** Developers integrating with the 4D survey API  
**Scope:** The native 4D API used by the .NET MAUI Android survey app. **Not** the ASP.NET/Razor web bridge.  
**Source of truth:** `SmartOps.4D/Project/Sources/Classes/SmartOpsSurveyService.4dm`, `SmartOps.4D/Project/Sources/Classes/SmartOpsHTTP.4dm`, `SmartOps.4D/Project/Sources/HTTPHandlers.json` and the separately distributed Android survey app.  
**Status:** Current fresh-dataset integration contract (November 2026). Illustrative request snippets omit some DTO fields; the code and schema are authoritative.

## 1. Architecture at a glance

```text
SmartOps.Survey (.NET MAUI on Android)
  Settings: ServerURL, TenantCode, UserCode, AuthenticationString
        |
        | HTTP POST, JSON (except binary GetImage response)
        v
4D HTTP server (trial: port 8045)
  HTTPHandlers.json -> SmartOpsHTTP.handle()
        |
        +--> SmartOpsSurveyService.dispatch()
                 |-- login / authenticate
                 |-- jobs and surveys
                 |-- vehicle and safety checks
                 |-- photos, drawings and videos
                 |-- notes, messages, app versions, questions
                 v
             4D ORDA entities + SmartOpsMedia/<tenant-id> files
```

**Important:** `SmartOps.4D.Web` is a *separate* ASP.NET/Razor adapter using `/api/web/snapshot` and `/api/web/commit`. The MAUI app does **not** call this bridge.

### Files to open in 4D

| Role | File / class |
|---|---|
| HTTP route registration | `Project/Sources/HTTPHandlers.json` |
| HTTP method, JSON parsing, transaction, error response | `Classes/SmartOpsHTTP.4dm` → `handle` |
| Mobile API route dispatcher | `Classes/SmartOpsSurveyService.4dm` → `dispatch` |
| Mobile login & session checking | `SmartOpsSurveyService.login`, `authenticate` |
| Shared 4D database/schema helpers | `Classes/SmartOpsService.4dm` |
| App login | `SmartOps.Survey/SettingsPage.xaml.cs` |
| Job download | `SmartOps.Survey/Comms/JobsReceive.xaml.cs` |
| Job/media upload | `SmartOps.Survey/Comms/JobsSend.xaml.cs` |
| Checks upload | `SmartOps.Survey/Comms/ChecksSend.xaml.cs` |
| Vehicle download | `SmartOps.Survey/Comms/VanChecksReceive.xaml.cs` |
| DTO definitions | `SmartOps.Survey/Models/DTOs.cs` |
| Questions sync | `SmartOps.Survey/Comms/DynamicQuestionSync.cs` |

## 2. Transport and authentication

All paths in the following table are **POST** requests handled by the native 4D server. Request content type is `application/json`; JSON property names are normally **camelCase** on the wire. 4D's `This.value(body; "FieldName")` supports the source DTO naming.

Trial server: `http://YOUR-4D-SERVER:8045`. For a physical tablet, use the server's reachable LAN address, not `127.0.0.1`. Only use HTTP on a trusted development LAN; use HTTPS and appropriate server protections for production.

### Login

`POST /AgentLogin`

```json
{
  "tenantCode": "SMARTOPS",
  "agentCode": "AG01",
  "serialNumber": "A1B2C3D4E5",
  "currentVersion": "1.0",
  "currentBuild": "1"
}
```

The `serialNumber` field carries the Android application's persistent **10-character installation ID**, not a hardware serial number or Android ID. For local testing, an Agent record with a **blank** `SerialNumber` accepts any installation ID (including an empty value). When an Agent has a nonblank `SerialNumber`, its own configured installation ID must match, regardless of whether Privileged Access is enabled. This optional-ID mode is for trusted local integration testing and **must not be treated as production authentication**.

4D looks up the active company and agent, checks the configured installation ID when present, issues an authentication token, and stores it against that agent. The response includes `authenticationString`, company and agent details, plus version fields.

The app persists that token and sends the following common fields for later API calls:

```json
{
  "tenantCode": "SMARTOPS",
  "agentCode": "AG01",
  "authenticationString": "<token>"
}
```

`SmartOpsSurveyService.authenticate` validates the company, agent code and stored token. An installation ID is checked at login, not on each later token-authenticated request. Logging out in the app clears its local token.

## 3. Endpoint catalogue

| POST endpoint | Purpose | Main 4D implementation / entities |
|---|---|---|
| `/AgentLogin` | Sign in and issue Agent token | `login`; Tenant, Agent, GoAccount |
| `/GetJobs` | Download today's/tomorrow's assigned visits, previously saved surveys and media filenames | `dispatch`, `accessibleJobs`, `headForJob`; Job, Contract, Customer, JobHeader, CustomItems, GoSurveySubmission |
| `/GetDynamicQuestions` | Download published tenant question set | `publishedQuestions`; TenantQuestionSets or bundled default |
| `/GetVehicles` | Download active fleet vehicles for checks | `dispatch`; Vehicles, Branches |
| `/SendJobs` | Upload completed survey/fitting and item data | `sendJobs`; Job, JobHeader, CustomItems, GoSurveySubmission |
| `/SendMedia` | Upload base64 photo/drawing/video files or chunks | `sendMedia`; GoMediaFile, GoMediaChunk and SmartOpsMedia files |
| `/GetImage` | Download one media file (binary response) | `dispatch`, `validFilename`, `mediaFile`; GoMediaFile |
| `/SendChecks` | Upload van, ladder, tool, spot, accident and mileage checks | `checks`, `upsertCheck`; check tables, GoCheckPacket |
| `/GetUpdateContractNotes` | Retrieve contract/customer information; optionally add note | `notes`; Contract, Customer, ContractNotes |
| `/GetSpotCheckJobInfo` | Same notes/info handler used for spot checks | `notes` |
| `/GetAgentMessages` | Retrieve messages for Agent | `dispatch`; AgentMessageRecipients, AgentMessages |
| `/GetAgentUnreadMessageCount` | Retrieve unread count | `dispatch`; AgentMessageRecipients |
| `/MarkAgentMessageRead` | Mark a message recipient as read | `dispatch`; AgentMessageRecipients |
| `/SurveyEnRoute` | Mark survey en route / enqueue notification | `dispatch`, `queue`; Job, integration outbox |
| `/Home/SurveyAppVersion` | Check latest Android build, record app/device stats | `appVersion`; SystemSettings, SurveyAppRequests, Agent |

`GET /api/health` is a separate **server health** endpoint implemented on the general 4D API, not an authenticated survey POST route.

## 4. Download workflows

### GetJobs

Request: common authentication fields.

Response: JSON **array** of `JobHeaderDTO` objects, each containing `head` (JobHeader), `customItems` (array) and `images` (array of filenames).

```json
[
  {
    "head": {
      "contractCode": "00000123",
      "guid": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
      "jobType": 0,
      "diaryDate": "2026-10-31"
    },
    "customItems": [],
    "images": ["00000123_c_example.jpg"]
  }
]
```

This is an **abbreviated** illustration, not a complete JobHeader. Consult `SmartOps.Survey/Models/JobHeader.cs` and `SmartOps.4D/Resources/survey-head-default.json` for all fields.

4D selects accessible, non-cancelled bookings for **today and tomorrow** in diary date order. `GoAllJobs` controls broader access; otherwise only the account's assigned jobs are returned. `headForJob` preserves or creates a stable visit GUID; previously submitted job data/items can be included. It converts the compact 4D GUID to the hyphenated .NET GUID format.

The `X-Dynamic-Questions-Version` response header tells the app whether it needs to download updated questions. The app saves headers and items locally, then calls `/GetImage` for missing files. Media failures are reported separately; an individual missing file need not discard the entire job.

### GetDynamicQuestions

Request: common fields. Response: published tenant question-set JSON, including its version and questions/options/rules. 4D reads `TenantQuestionSets.QuestionsJson` or the embedded `dynamic-questions.json` fallback. Draft changes are not published to tablets.

### GetVehicles

Request: common fields. Response: JSON array of active `VehicleDTO` objects, with `id`, `branchId`, `branchCode`, `name`, `registration`, `description` and `type`. Also sets `X-Dynamic-Questions-Version`. The mobile app imports vehicle entries for van checks.

### GetImage

Request: common fields plus `filename` (string). Response: **raw bytes** (`application/octet-stream`), not JSON/base64. Requires completed media metadata and a file in the tenant's media directory. Returns 404 if missing. Do not expose the disk directory publicly.

## 5. Upload workflows

### SendJobs

Request uses `JobSendDTO`:

```json
{
  "tenantCode": "SMARTOPS",
  "agentCode": "AG01",
  "authenticationString": "<token>",
  "jobHeaders": [
    {
      "head": { "contractCode": "00000123", "guid": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" },
      "customItems": [],
      "images": []
    }
  ]
}
```

The nested `head` above is abbreviated. Up to **100** jobs per batch. For every job, 4D validates the contract, visit GUID and relevant booking/agent/date details. The submitted visit GUID, agent and booking details must match the current booking; mismatches are rejected. It persists submitted header/item data and submission records; retries are checked to avoid duplicate or conflicting submissions. The submission also feeds the 4D integration outbox. See `sendJobs` for exact validation and business rules. Media files are uploaded separately via `SendMedia`; a completed job payload alone does not transfer image/video bytes.

### SendMedia

Request: common fields plus:

```json
{
  "filename": "example-photo.jpg",
  "data": "<base64-encoded-chunk>",
  "chunkIndex": 1,
  "totalChunks": 1
}
```

- Filename is validated by `validFilename`, within the authenticated tenant.
- `data` is **base64 text**; decoded bytes are written to disk.
- `chunkIndex` is **1-based**. Omitting/zeroing total chunks defaults to one part.
- At most **512** chunks; each decoded part must be non-empty and at most **8,000,000 bytes**.
- Chunk 1 starts/restarts an upload, replacing stale parts. Repeated chunk numbers replace their bytes.
- Once all parts exist, 4D assembles an output file, updates `GoMediaFile` and removes temporary chunks. Media lives in `SmartOpsMedia/<tenant-id>/<filename>`.
- Reusing a filename replaces that tenant's file. Back up **both** the 4D data file and `SmartOpsMedia`.

### SendChecks

Request: common fields plus arrays: `ladderChecks`, `toolChecks`, `spotChecks`, `fAccidents`, `mileageSheets`, `accidentsVehicleDTOs`, `vehicleCheckDTOs`.

Vehicle check records include `vehicleCheckHeader` and `vehicles`; vehicle accident records include `accident`. 4D permits up to **1,000 records total** per batch, upserts check rows, records a deduplication `GoCheckPacket`, and queues a `ChecksReceived` integration event. Successful response:

```json
{ "status": "success" }
```

Check photos are uploaded separately via `SendMedia`. In the app, `ChecksSend` sends images and then the check data.

## 6. Other operations

| Endpoint | Additional request fields | Response / behaviour |
|---|---|---|
| `GetUpdateContractNotes` | `contractCode`; optional `note` | Contract/customer details and `contractNotes` array; a nonblank note creates ContractNotes |
| `GetSpotCheckJobInfo` | `contractCode`; optional `note` | Same `notes` implementation |
| `GetAgentMessages` | None | Array containing `id`, `subject`, `bodyHtml`, `created`, `isRead`, `readDateTime` |
| `GetAgentUnreadMessageCount` | None | `{ "unreadCount": 3 }` (illustrative) |
| `MarkAgentMessageRead` | `messageId` | `{ "status": "success" }`; only messages addressed to this Agent can be marked |
| `SurveyEnRoute` | `contractCode`, `diaryDate`, optional `resend` | Records en-route time and queues an event; returns `enRoutePressedAtUtc`. Duplicate call without resend can return **409** |
| `Home/SurveyAppVersion` | `currentVersion`, `currentBuild`, `deviceManufacturer`, `deviceModel`, `deviceName`, `platform`, `osVersion` | `{ "latestBuild": "500", "updateAvailable": false }` (illustrative). Records SurveyAppRequests and updates Agent app info |

## 7. Errors, transactions, and diagnostics

- `SmartOpsHTTP.handle` parses incoming JSON and dispatches mobile paths to `SmartOpsSurveyService`. Non-JSON write requests can return **415**; invalid JSON **400**.
- Invalid Agent/token or unregistered device: typically **401**. Missing record/media: **404**. Repeated/stale/conflicting submission or transfer: **409**. Locked account: **429**. Unexpected server exception: **5xx**.
- Some failure bodies contain `error`, others `reasonPhrase`; mobile callers must check the HTTP status and handle both. Do not assume every error response has the same shape.
- Native write requests are transaction-controlled by `SmartOpsHTTP.handle`; login is handled separately so its account failure/lockout writes can persist.
- 4D records errors to tenant-scoped `Logs/smartops-errors-YYYY-MM-DD-TENANT.jsonl`, including request path, stage and reference, but not request bodies/credentials.
- HTTP timeouts and missing media should leave the app with an actionable error rather than a stuck progress popup.
- To diagnose: test `GET /api/health`, then login, then a read endpoint, then upload. Inspect the server's error log and the `This.service.debugStage` value where relevant.

## 8. A normal end-to-end tablet journey

1. Admin registers Android ID in **an Agent.SerialNumber** through SmartOps.4D.Web.
2. Tablet calls `AgentLogin` with company, agent and Android ID; 4D issues token.
3. Tablet calls `GetJobs` and optionally `GetDynamicQuestions`; calls `GetImage` for individual media files.
4. Tablet calls `GetVehicles` for van-check forms.
5. User completes survey/fitting or vehicle/safety checks offline.
6. Tablet sends images/videos with `SendMedia` (sometimes many chunks), then business records using `SendJobs` or `SendChecks`.
7. 4D stores canonical records and enqueues the appropriate integration events.
8. Tablet uses notes, messages, en-route and version endpoints as needed.

## 9. Guidance for integrating into Martindales' existing 4D system

The purpose of the interface is to keep the **mobile wire contract** stable even if Martindales changes 4D table names or internal architecture. David can implement equivalent 4D methods behind these routes and translate DTOs to existing entities.

- Keep **endpoint names**, JSON casing, GUID/date conventions, return structures and HTTP statuses compatible with the deployed MAUI app.
- Preserve company/agent boundaries and the device registration check. Do not rely on an Android ID alone as a secure proof of device ownership.
- Keep visit GUIDs and retry/idempotency semantics stable; duplicate upload rejection is important on unreliable mobile links.
- Preserve media chunk order, filename scoping and file retention on incomplete replacement.
- Map `JobHeader`, `CustomItems` and the check DTOs to the existing 4D model; the mobile app should not need to know those native table names.
- Run existing isolated regression tests (`SmartOps.4D/Tools/test_survey_send.ps1`) and then perform real-device round-trip tests before deployment.
- Production requires HTTPS, a reviewed authentication model, durable backups for database **and** media, and appropriate network access controls.

### Quick code map

```text
SmartOpsHTTP.handle
   -> SmartOpsSurveyService.dispatch
       -> login / authenticate
       -> headForJob / accessibleJobs
       -> sendJobs
       -> sendMedia / mediaFile / validFilename
       -> checks / upsertCheck
       -> notes
       -> appVersion
       -> questionVersion / publishedQuestions
   -> SmartOpsService for schema, row, save, errors and shared helpers
```

**Maintenance note:** Update this document whenever an endpoint, request field, security rule or response changes; it documents the present implementation rather than a permanent external standard.
