# SmartOps 4D — Survey tablet trial

This project is **SmartOps**, not GlassOps. It is a standalone proof of concept intended to host the existing **SmartOps.Survey** mobile API before eventual integration into Martindales' 4D system. No SQL Server migration is attempted.

## First run

1. Install a compatible 4D 21 trial and open `SmartOps.4D/Project/smartops4d.4DProject` from the cloned SmartOps repository.
2. Create/select an **empty** 4D data file. The 4D startup method seeds fictional example data automatically only if the database is empty.
3. The 4D startup method automatically starts `GO_StartLAN` on HTTP port 8045. Run `GO_Open` to explore the native browser workspace if desired.
4. The automatic `GO_StartLAN` binds to the PC's LAN interfaces on TCP port 8045. Allow inbound TCP 8045 in Windows Defender Firewall **only on the Private profile**, only from your trusted tablet/LAN. Never forward the port or deploy trial HTTP to the public Internet.
5. Find your PC's **IPv4 LAN address** with `ipconfig`. Set SmartOps.Survey Settings → Server URL to `http://YOUR-PC-IP:8045` (without a trailing slash). PC and tablet must be on the same reachable LAN/Wi-Fi network; the tablet must not use localhost/127.0.0.1.
6. For tablet login, use company `SMARTOPS` and agent code `ADMIN` (the app does not send an agent password). A device ID is **optional only when the selected agent has no Serial Number assigned in 4D**. Once a serial is registered for that agent, the existing device validation applies. For web login, use company `SMARTOPS`, username `admin`, and the demo password `Password1!`. Agent-code-only mobile login is intended exclusively for isolated developer demos.
7. From the tablet, sign in then **receive jobs**. Check dynamic questions, complete an assigned job, send survey data and media. Examine them in the 4D browser workspace or the native 4D data editor.

## Files and API

Existing data files are upgraded from company code `DEMOWIND` to `SMARTOPS` when `GO_Start` or `GO_StartLAN` runs. The existing company ID, accounts, passwords and linked records are retained. You can also run `GO_UpgradeCompanyCode` directly. The upgrade refuses to overwrite a separate company already using `SMARTOPS`. Update any saved Survey company code to `SMARTOPS`.

- HTTP server provides `/AgentLogin`, `/GetJobs`, `/GetDynamicQuestions`, `/GetVehicles`, `/SendJobs`, `/SendChecks`, `/SendMedia`, `/GetImage`, messages and the other compatibility routes in `Project/Sources/HTTPHandlers.json`.
- When a media upload completes, 4D writes it to `SmartOps.4D/Project/SmartOpsMedia/<tenant-ID>/<filename>` (location relative to the **4D project database folder**). Tenant folders isolate file names. The data file retains media metadata, SHA-256 digest, and completion state. In-progress chunks remain in 4D until assembled; completed chunks are deleted.
- **Back up the media folder alongside the 4D data file.** Deleting or moving the folder will break previously uploaded media. Do not expose it as a static public web directory.
- In the browser, **Photos & videos** lists files and offers authenticated downloads. **Survey uploads** shows submitted payload JSON.
- Media is replaced by company and filename when resent. Chunk 1 starts/restarts the transfer, clearing stale chunks and allowing a changed chunk count. Repeated chunks replace their bytes. An existing completed file stays available until the replacement is assembled, and shorter replacements are truncated correctly. Run `Tools/test_survey_send.ps1 -Suite MediaOverwrite` for isolated disk-file regression tests.
- `/api/health` returns a basic status JSON. Use `http://YOUR-PC-IP:8045/api/health` to check network connectivity from your tablet browser.

## Statistics and Survey app release

In the ASP.NET 4D interface, sign in as the SMARTOPS company administrator and open **Statistics → App**. Set **Current release build** to the Android build number (`versionCode` / `ApplicationVersion`), rather than the display version. The setting is stored in 4D for the current company. Tablets whose installed build is lower are offered an update; build `0` disables update prompts. The existing app update link remains unchanged.

Both the native 4D and ASP.NET `Home/SurveyAppVersion` routes use the same 4D setting and retain version-check/device history for the Statistics page. Run `Tools/test_survey_send.ps1 -Suite AppVersion` for isolated persistence, build comparison and company isolation tests.

## 4D API error log

API exceptions are recorded without a modal 4D alert. Failed writes are still rolled back, and callers still receive the appropriate HTTP error. In the ASP.NET interface, administrators can open **4D Error Log** beside **Log** to view their company's latest 200 errors from the past seven days, displayed in UK local time (GMT/BST). The page can open independently of the normal data snapshot.

The persistent JSON-lines files are in the data file's 4D `Logs` folder, named `smartops-errors-YYYY-MM-DD-TENANT.jsonl`. Each entry includes a reference, UTC timestamp, request path/method, stage and native error details. Request bodies, headers and credentials are not recorded. Failures before a company is identified are retained in the tenant `0` file for local diagnosis. Existing startup/seed notices are unchanged.

Run `Tools/test_survey_send.ps1 -Suite ErrorLog` for isolated native logging tests. Reopen the 4D project and restart the ASP.NET app after updating the sources.

## Native survey submission regression test

Run `Tools/test_survey_send.ps1` from PowerShell, passing `-FourDPath` if 4D 21 R4 is installed somewhere other than the script's default path. The runner creates a separate project and a new data file, imports fictional sample data there, runs native 4D headlessly, and retains its test folder and JSON results. It never opens the live data file or media folder. All test submissions are rolled back.

Coverage includes new survey headers, two linked custom items, dynamic answer JSON/version/snapshot fields, server-issued GUIDs, legacy GUID recovery, identical retries without duplicates, and rejection of changed legacy retries.

Use `Tools/test_survey_send.ps1 -Suite Booking` for fitting-booking tests. These cover valid times, invalid time formats, duration limits, midnight boundaries, fitting authorisation and appointment clashes, also using a separate data file and rolled-back transactions.

Use `Tools/test_survey_send.ps1 -Suite FittingRecovery` to test an older tablet retaining the survey agent when switching to fitting. Recovery requires an all-jobs account, its own stale agent ID, a previously saved survey, matching contract/customer and fitting date/time, and one eligible booking. Restricted accounts, unrelated agents, missing survey evidence, wrong times and ambiguous bookings remain rejected. Identical retries create no duplicate submission. Future mobile builds copy the incoming tenant/contract/customer/agent IDs in `UpdateJobHeader`; recover the pending fitting before receiving replacement jobs.

The error 54 regression occurs when a header/item lookup returns `Null` and a variable declared with inferred typing is later assigned a new entity. These nullable lookup variables are explicitly declared `4D.Entity` in `sendJobs()`. A native runtime test is required; catalog validation alone cannot detect this error.

After changing the source in an already running installation, reopen SmartOps in 4D, run `GO_StartLAN`, and resend the existing completed survey. Do not receive replacement jobs over a pending completed survey. The isolated test verifies submission persistence; the actual tablet payload and media must still be checked end to end.

## Important limitations before integration

- This is a trial adapter, **not yet an accepted equivalent of SmartOps.Web**. Only the survey-centric paths are targeted, and tablet-to-4D round-trip and 4D compile/runtime testing must be performed locally.
- Media uploads and the integration outbox are not automatically delivered to Martindales' existing 4D database. No production data or credentials should be used.
- Full-scale 4D web service hardening, HTTPS, authentication lifecycle, image size/load testing, database migration and production backup procedures are future work.
- Existing `SmartOps.Web` remains untouched. Survey changes are limited to login UI compatibility.
