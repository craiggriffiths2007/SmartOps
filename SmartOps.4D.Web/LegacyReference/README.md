# Original C# API reference implementations

The 4D server in `SmartOps.4D` owns the API and authoritative business data.
This web project provides MVC/Razor screens, Identity compatibility and the 4D
bridge. Its SQLite database is a disposable request workspace populated from
`api/web/snapshot`; successful web changes are sent to `api/web/commit`.

The original C# API handlers are retained here because they are useful, readable
reference implementations. They are historical code, not a second API server.
The five files in `Endpoints/` were moved without changing their contents.
No controllers, services, models, views, configuration or backend files were moved.

## Runtime isolation and consulting the source

`SmartOps.4D.Web.csproj` removes `LegacyReference/**/*.cs` from `Compile` and
includes those files as `None` so they remain accessible in Visual Studio.
They are not part of the application assembly and cannot contribute handlers or
controllers to runtime discovery. Moving a controller directory alone would not
provide this isolation; the compilation exclusion is essential.

Before this cleanup, `Program.cs` already did not call any of the original
`MapAPIEndpoints`, `MapJobsEndpoints`, `MapChecksEndpoints`,
`MapAgentMessageEndpoints` or `MapSurveyEnRouteEndpoint` extensions. The cleanup
removes those methods from the compiled assembly as an additional safeguard.
The remaining customer/media mapping extensions also have no startup callers;
this cleanup does not register them.

Open these files normally to read or compare them with 4D. Do not add them to
`Compile`, copy them into an active source folder, or register their mapping
methods. Reusing any implementation requires a separate migration review,
including routes, authentication, storage and existing 4D behaviour. The original
namespace and partial class are deliberately preserved as historical reference;
the archive is not intended to compile independently.

## Endpoint ownership mapping

Paths in the first column were POST endpoints. All 4D source paths below are
relative to `SmartOps.4D/Project/Sources/`. `HTTPHandlers.json` routes the survey
paths to `Classes/SmartOpsHTTP.4dm` / `handle`, which calls
`Classes/SmartOpsSurveyService.4dm` / `dispatch`.

**Verified** here means the route and corresponding handler were verified in
source. It does not mean behavioural parity or live 4D execution was tested.
Differences below remain migration verification work; these C# handlers were
already unregistered, so retaining them in this archive changes no live routes.

| Original C# endpoint (file in `Endpoints/`) | Replacement 4D method/route | Migration status |
|---|---|---|
| `/AgentLogin` (`EndpointMappings.cs`) | `/AgentLogin` -> `SmartOpsSurveyService.login` | Verified ownership; credential/account model differs |
| `/GetUpdateContractNotes` (`EndpointMappings.cs`) | `/GetUpdateContractNotes` -> `dispatch` -> `notes` | Verified ownership; response/ordering parity unverified |
| `/SendJobs` (`EndpointMappings.Jobs.cs`) | `/SendJobs` -> `sendJobs` | Verified ownership; GUID/visit recovery and packet processing differ |
| `/GetJobs` (`EndpointMappings.Jobs.cs`) | `/GetJobs` -> `dispatch`, `accessibleJobs`, `headForJob` | Verified ownership; DTO and questionnaire parity unverified |
| `/GetDynamicQuestions` (`EndpointMappings.Jobs.cs`) | `/GetDynamicQuestions` -> `dispatch` reads `Resources/dynamic-questions.json` | Verified ownership; tenant editor publication parity unverified |
| `/SendChecks` (`EndpointMappings.Checks.cs`) | `/SendChecks` -> `checks`, `upsertCheck` | Verified ownership; validation and child deletion parity unverified |
| `/GetSpotCheckJobInfo` (`EndpointMappings.Checks.cs`) | `/GetSpotCheckJobInfo` -> `dispatch` -> `notes` | Verified ownership; original selected today's job, 4D uses contract notes/details |
| `/GetVehicles` (`EndpointMappings.Checks.cs`) | `/GetVehicles` -> `dispatch` | Verified ownership; original filtered by agent branch, 4D returns active tenant vehicles |
| `/GetAgentMessages` (`EndpointMappings.AgentMessages.cs`) | `/GetAgentMessages` -> `dispatch` | Verified ownership; original ordered newest first, 4D has no explicit ordering |
| `/GetAgentUnreadMessageCount` (`EndpointMappings.AgentMessages.cs`) | `/GetAgentUnreadMessageCount` -> `dispatch` | Verified ownership; runtime parity unverified |
| `/MarkAgentMessageRead` (`EndpointMappings.AgentMessages.cs`) | `/MarkAgentMessageRead` -> `dispatch` | Verified ownership; original only stamped first read, 4D stamps each call |
| `/SurveyEnRoute` (`EndpointMappings.SurveyEnRoute.cs`) | `/SurveyEnRoute` -> `dispatch`, `queue` | Verified ownership; 4D records state and queues `TestEnRouteNotification`, rather than sending the original SMTP email |

The archived agent authentication helper has callers only in the archived files.
The original `_uploadLocks` field was also used by the retained media partial
class. Its identical declaration is retained in
`../EndpointMappings.Media.UploadLocks.cs`; this is the only supporting source
separation. It adds no route registration or behaviour.

## Active code retained

The entire project's source inventory, startup registrations, controller actions,
view/page references, service callers, EF metadata and reflection sites were
reviewed for the archive boundary. No broad unused-code deletion was attempted.

| Area | Classification and reason to retain |
|---|---|
| `Program.cs`, `Bridge/BridgeSession.cs`, `Bridge/BridgeMiddleware.cs` | **Active.** 4D login, cookie claims, media proxy, request import/commit and redirect key translation. Unchanged. |
| `Controllers/` and `Areas/Insurer/Controllers/` | **Active or mixed/uncertain; all retained.** Conventional MVC routes and views still use them, including JSON/AJAX actions. A 4D route with similar business logic does not supersede a web action. |
| `Controllers/HomeController.cs` | **Active.** Includes anonymous `POST /Home/SurveyAppVersion`, which forwards to the same 4D route, alongside web actions and media diagnostics. |
| `Controllers/FourDErrorLogController.cs` | **Active.** Calls the 4D integration error log; not a legacy database-only API. |
| `Controllers/DynamicQuestionsController.cs`, `Services/TenantQuestionSetService.cs`, `Services/DiagramAssets.cs`, `Models/TenantQuestionSet.cs` | **Active.** Question editor, draft/version operations and rendering. Keep regardless of the mobile question resource difference. |
| `Controllers/JobsController.cs`, `Services/BookingAvailability.cs`, `Services/BookingReservations.cs` | **Active.** Diary, availability, booking holds, save validation and views use these directly. |
| Customer/contract/job/header/item/check/vehicle controllers, insurer area, estimates/invoices and messaging controllers | **Active.** Web CRUD, reports, surveys, finance, claims and messages operate against the staging workspace. |
| Users/roles/registration/profile/tenant/company/news/statistics/log/backup controllers | **Active or mixed/uncertain.** Still exposed through conventional routing; no complete replacement of their web interface was established. |
| `Data/SmartOpsServiceContext.cs` (`AppDBContext`), `Data/SmartOpsServiceUser.cs` (`AppUser`), `Models/Tennancy.cs` | **Active.** EF/Identity metadata, tenant claims and filters, and user compatibility are required by the bridge/web interface. |
| `Data/Repo.cs`, `Services/TenanatDeletionService.cs`, `Services/TenantBackupService.cs` | **Active.** DI registrations and web controller consumers remain. Their backend/storage suitability is outside this structural cleanup. |
| `Data/DbInitializer.cs`, `Data/DbInitializer.Demo.cs` | **Active/mixed.** Still called by tenant creation and Identity registration; do not infer obsolescence from absence of startup seeding. |
| `Services/EstimateAuthorization.cs`, `BuildInfoService.cs`, `EmailLoginLinkService.cs` | **Active.** Called by estimate/insurer flows, layout/build display and user email login links respectively. |
| `Models/`, `Models/SurveyApp/`, `Models/CustomerApp/`, `ViewModels/`, `Areas/Insurer/ViewModels/` | **Active or mixed/uncertain; all retained.** Entities are reachable through EF metadata and bridge reflection; DTOs share files with active models. No model was excluded just because its obvious API caller was archived. |
| `Areas/Identity/Pages/`, `Pages/`, `Views/`, area views | **Active.** Razor page discovery, conventional controller views, partials and Identity dependencies. |
| `Helpers/`, `ViewComponents/`, `TagHelpers/`, `Options/`, `CustomViewLocationExpander.cs`, `GlobalUsing.cs` | **Active or mixed/uncertain; all retained.** View rendering, routing, pagination, display, shared types and form options have indirect consumers. |
| `wwwroot/`, resources, templates, project references and settings | **Retained.** Static/media assets, scaffolding and configuration were not altered. |

`BridgeSession` enumerates `db.Model.GetEntityTypes()`, creates entities with
`Activator.CreateInstance`, assigns mapped properties, and uses EF table/foreign
key metadata for commits. Models therefore cannot be classified by direct text
references alone. MVC, Razor, components and tag helpers also use framework
discovery. No custom assembly scan of endpoint mapping extensions, dynamic call
to those extensions, or hosted/background service registration was found in this
web project or its shared project.

## Uncertain code left compiled and unchanged

"Compiled" does not imply "registered" or "working with the 4D backend".

| Code / original route | Known 4D area | Reason left untouched |
|---|---|---|
| `EndpointMappings.Customer.cs`: `/Customer/CreateTicket`, `/Customer/Tickets` | `POST`/`GET /api/tickets` in `SmartOpsService.dispatch` | Related functionality exists, but payload, authentication and response compatibility are unverified. |
| Same file: `/Customer/Ticket`, `/Customer/ReplyToTicket` | `/api/ticket-messages` in `SmartOpsService.dispatch` | Related operations; no verified drop-in mapping for ticket detail/reply contracts. |
| Same file: `/Customer/Login`, `/Customer/Account`, `/Customer/ChangePassword` | `/api/auth/login`, `/api/auth/password` and account APIs | Different account/authentication contracts; exact mapping unverified. |
| Same file: `/Customer/CurrentRepair` | `GET /api/portal` and contract APIs | Related portal functionality, not a proven equivalent response. |
| Same file: `/Customer/CreateTestAccount` | No exact replacement established | Unverified. Entire mixed customer file remains compiled, unregistered. |
| `EndpointMappings.Media.cs`: `/GetImage`, `/SendMedia` | `SmartOpsSurveyService.dispatch`, `sendMedia` | Verified source route ownership, but file also contains the uncertain routes below. Entire file retained to avoid splitting media behaviour. |
| Same file: `/images/{filename}` | Active `Program.cs` proxy -> `/api/integration/media/{id}` | Web proxy already replaces local disk serving. Original mapping remains unregistered with the rest of this mixed file. |
| Same file: `/Customer/GetImage`, `/ExportImages`, `/debug-paths` | No exact replacements established | Customer authentication, export tooling and diagnostic routes need verification. No routes enabled/disabled by this cleanup. |
| `Services/ApprovalSignInManager.cs`, `Services/EmailSender.cs`, `Services/EmailSettings.cs`, `Models/Tennancy.cs` claims factory | No complete migration established | Not selected by current startup in the same way as the old app. Retain Identity/email alternatives pending a separate review. Current `LocalEmailDisabled` stays unchanged. |
| `Services/AuditRequestMiddleware.cs`, `Services/AuditIdentity.cs`, `Data/AppDBContext.Audit*.cs`, `Data/AuditTransaction.cs`, audit model/view models/helpers and `LogController` | Existing web auditing code | Mixed EF/controller dependencies remain. Audit request middleware is not registered. Existing SaveChanges audit overrides are still present in this checkout. Nothing here reintroduces auditing or changes the prior save/error handling behaviour. |

Do not interpret a verified route owner as proof that dynamic questionnaire
validation, SMTP delivery, branch filtering, DTO projection, child deletion,
message ordering or timestamp semantics have complete C#/4D parity. Those are
existing migration questions, not changes introduced by archiving dormant code.

## Validation and remaining runtime checks

See [VALIDATION.md](VALIDATION.md) for executed checks and their limits.
Manual integration testing should cover 4D login/logout, authenticated bridge
load/save and key redirects, diary/bookings/customers/contracts, surveys and
reports, question draft/publish/reload and media. Verify the migration differences
above directly against the running 4D server and actual clients before any
further archival or behavioural work.
