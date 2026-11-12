# Cleanup validation — 29 October 2026

Work was performed in the existing `legacy-clear-up` branch. No commit, merge,
backend edit or application setting change was made.

## Build and source checks

- `dotnet restore SmartOps.4D.Web/SmartOps.4D.Web.csproj`: succeeded.
- `dotnet build SmartOps.4D.Web/SmartOps.4D.Web.csproj --configuration Release --no-restore --verbosity quiet`:
  succeeded, 69 warnings, 0 errors.
- `dotnet build SmartOps.4D.Web/SmartOps.4D.Web.csproj --no-restore --verbosity quiet`:
  succeeded, 69 warnings, 0 errors after the user stopped the running application.
- The initial baseline Debug build was blocked by DLLs held by Visual Studio
  and the running `SmartOps.Web` process (MSB3027/MSB3021). Baseline runtime
  inspection used a successful Release build instead. No user process was stopped
  by the cleanup.
- SHA-256 hashes of all five archived files match their originals byte for byte.
- Evaluated MSBuild `Compile` items contain zero `LegacyReference` sources;
  all five archived C# files are present as `None` items for Visual Studio.
- No active caller of an excluded mapping method or archived private helper
  remains. The media upload lock's declaration was retained in its own compiled
  partial class, without editing the original media file.
- `git diff --check` passed. Only the five source moves, project exclusion,
  retained upload lock and legacy documentation are part of this cleanup.

## Executed runtime checks

A temporary probe outside the repository started the real web entry point on an
ephemeral loopback port before and after the cleanup. It used the web assembly's
application name and actual content root, inspected endpoint metadata and MVC
controller discovery, resolved key scoped services, and stopped its own host.
It did not sign in, load a 4D snapshot, submit business changes or call a live
4D endpoint. It did not change the existing Visual Studio run configuration.

The following baseline and post-cleanup snapshots matched:

| Check | Result |
|---|---|
| Route patterns, HTTP methods and endpoint display names | All 312 entries unchanged |
| MVC discovered controller types | All 40 unchanged |
| Checked DI services | All 14 resolved before and after |
| Archived extension methods in compiled assembly | All five registration methods absent after cleanup |

The DI checks resolved `BridgeSession`, `AppDBContext`, `ITenantContext`,
`TenantQuestionSetService`, `Repo`, `ITenantDeletionService`, `TenantBackupService`,
`BuildInfoService`, `EmailLoginLinkService`, `IHttpClientFactory`, `IEmailSender`,
`UserManager<AppUser>`, `SignInManager<AppUser>` and `RoleManager<IdentityRole>`.
Resolution confirms construction, not correctness of business operations.

All 15 post-cleanup HTTP smoke checks passed:

| Request | Result |
|---|---|
| `GET /Account/Login` | 200 |
| `GET /Jobs` without authentication | 302 |
| `GET /images/probe.jpg` without authentication | 401 |
| POST to each of `/AgentLogin`, `/GetJobs`, `/SendJobs`, `/SendChecks`, `/GetVehicles`, `/GetDynamicQuestions`, `/GetUpdateContractNotes`, `/GetSpotCheckJobInfo`, `/GetAgentMessages`, `/GetAgentUnreadMessageCount`, `/MarkAgentMessageRead`, `/SurveyEnRoute` | 404 for all 12 paths |

The archived files contain minimal API extensions, not MVC controllers. Their
exclusion removes their registration methods from the assembly; the unchanged
controller discovery snapshot also confirms that no web controller was lost.
The active `HomeController.SurveyAppVersion` proxy and `Program.cs` media route
remain in the unchanged endpoint snapshot.

## Tests and limits

No .NET automated test project was found for this web project or its shared
dependency, so no existing .NET test suite was run. The repository's 4D test
methods/scripts and unrelated Flutter tests were not executed. The temporary
host/DI/HTTP checks above were executed; they do not establish 4D feature parity.

Manual authenticated runtime testing remains necessary for login/logout against
4D, bridge snapshot/commit, generated-key redirects, booking/diary flows,
customer/contract edits, surveys/reports, question editor draft/publication and
media retrieval/upload. The migration differences and uncertain routes are
documented in [README.md](README.md). In particular, notification email delivery,
dynamic questionnaire publication/validation, vehicle branch scope and check
child synchronisation need separate backend verification.

Existing audit code was left as found, including EF SaveChanges overrides and
the unregistered request middleware. This cleanup neither enables auditing nor
claims that previous saving errors have been diagnosed or resolved.
