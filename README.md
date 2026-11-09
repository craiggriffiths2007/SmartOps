# SmartOps

SmartOps is a 4D integration and testing platform for field-service workflows. It provides a working 4D API server, an ASP.NET Core web interface, and prepopulated integration test data for exploring and validating end-to-end workflows. A companion Android survey application is distributed separately through Google Play.

> **Integration testing environment.** The included accounts and records are for local development and integration testing, not production use. Do not expose a server running the supplied test credentials to the public internet.

## Included projects

- `SmartOps.4D` — 4D database and API server.
- `SmartOps.4D.Web` — ASP.NET Core web frontend connected to 4D through a bridge.
- `SmartOps.Shared` — shared contracts, DTOs, and dynamic question definitions.

## Android survey application

The companion **SmartOps Survey** Android application is distributed on [Google Play](https://play.google.com/store/apps/details?id=com.authorityx.smartops). Its source code is **not included in this repository**. It communicates directly with the SmartOps 4D API, independently of the ASP.NET web bridge. Availability and device compatibility are determined by Google Play.

## Scope

Only the three projects listed above and the supporting solution, configuration and documentation files needed to develop and operate them belong in this repository. The Google Play survey application is a separate distribution.

## API integration guide

For developers integrating another system with SmartOps, see the **[4D API integration guide](SmartOps.4D/Documentation/API-INTEGRATION.md)**. It explains the 4D HTTP endpoints, direct Android API versus ASP.NET bridge architecture, authentication, example test requests, and useful methods such as `GO_StartLAN`, `SmartOpsHTTP.handle`, `SmartOpsService.dispatch`, and `SmartOpsSurveyService.sendJobs`.

## Download SmartOps 4D integration package

**[Download the latest SmartOps integration package (ZIP)](https://github.com/craiggriffiths2007/SmartOps/releases/latest/download/SmartOps-Integration-Windows-x64.zip)** — a ready-to-use download containing the editable 4D project, integration test data and optional prebuilt Windows web interface. You can also [view all releases](https://github.com/craiggriffiths2007/SmartOps/releases).

The single download contains:

- **`SmartOps.4D/`** — editable 4D project, API, documentation, and fictional integration test data.
- **`SmartOps-Web-Windows-x64.zip`** — optional prebuilt, self-contained ASP.NET Core browser interface for Windows x64. No Visual Studio or separately installed .NET runtime is required.
- **`README-START-HERE.txt`** — setup instructions for both options.

**4D-only quick start:** Extract the main ZIP, open `SmartOps.4D/Project/smartops4d.4DProject` in 4D, and create a **new empty data file** when prompted. The 4D `onStartup` method runs `GO_StartLAN`, starts HTTP on port **8045** and automatically imports the integration test records. You can now explore 4D methods and test the API **without installing or running the web frontend**. A compatible installation/licence for 4D is still required.

**Optional web frontend:** Extract the **inner** `SmartOps-Web-Windows-x64.zip` to its own folder, then double-click `SmartOps.Web.exe` while 4D is running. The executable automatically opens the web login page at `http://127.0.0.1:5245/Account/Login`. Keep the server console open while using the interface.

The web ZIP uses .NET single-file publishing to reduce separate DLLs, although some website assets and supporting files remain. The package is intended for **local integration testing**; packaging the web executable inside a second ZIP does **not** guarantee that antivirus software, Chrome or Windows SmartScreen will consider it safe. Validate the download and do not disable security protections.

**Developers who want to change the integration** can [download the source repository](https://github.com/craiggriffiths2007/SmartOps) and build the .NET frontend themselves. The proprietary Android application source code is not distributed.

### Building a release

Maintainers can run [Build and release SmartOps Web (Windows)](https://github.com/craiggriffiths2007/SmartOps/actions/workflows/smartops-web-windows-release.yml) using **Actions → Run workflow**. With `publish_release=false` (the default), it builds and smoke-tests the web app, bundles the 4D project with the optional web ZIP, and uploads an Actions artifact for testing. After validating the package, run with `publish_release=true` and leave the version blank for an automatically generated tag, or enter a **new** version tag such as `v1.0.1`. The workflow attaches `SmartOps-Integration-Windows-x64.zip` to a GitHub Release.

## Local integration testing setup

SmartOps includes fictional 4D integration test data so developers can exercise the API, web interface and mobile workflows without manually creating records. After cloning, open `SmartOps.4D/Project/smartops4d.4DProject` in 4D and create a **new empty** data file if prompted. The 4D `onStartup` method automatically invokes `GO_StartLAN`, which starts HTTP port **8045** and imports the integration test records only if the database is completely empty. Successful startups and seeds do **not** display confirmation dialogs. `GO_Start` is also available as a manual alias for the same startup.

The ASP.NET web bridge automatically discovers the active local Ethernet or Wi-Fi IPv4 address for its 4D API connection; no machine-specific IP address is stored in the source. On machines with unusual or multiple network adapters, set `FourD__BaseUrl=http://YOUR-PC-IP:8045/` in the web app's launch environment or your own local configuration.

Run `SmartOps.4D.Web` from Visual Studio and sign in with these **local integration-testing** credentials:

| Field | Value |
| --- | --- |
| Company code | `SMARTOPS` (configured automatically by the web app) |
| Username | `admin` |
| Password | `Password1!` |

The initial integration test dataset is modelled on the **smaller GlassOps registration demonstration data**, rather than the six-month reporting dataset. It includes **13 fictional SmartOps customers and contracts**, three branches (**Bolton**, Gloucester and Glasgow), eight agents, 20 vehicles, and **18 diary bookings** spanning recent past, today and upcoming dates. It also includes estimates, contract updates, insurers, test accounts and a separate tenant for isolation testing. The supplied test accounts share a known test password and are intended only for isolated development environments. The seed runs **only for a completely empty 4D database**; to try the new data when an older test database is already populated, create a new empty data file instead of deleting records you need.

For the [Android survey app on Google Play](https://play.google.com/store/apps/details?id=com.authorityx.smartops), set the tablet's **Server** to `http://YOUR-PC-IP:8045`, the **Agent Code** to an existing agent such as `ADMIN`, and the company code to `SMARTOPS`. **No device ID registration is needed when that agent's Serial Number field is blank.** If an agent has a registered serial number, the existing device checks still apply (privileged agents must use their own registered device). This agent-code-only fallback supports local integration testing and is not secure production authentication. On Windows, allow incoming TCP **8045** from the trusted local subnet on a **Private** network. Do not expose the API publicly.

**Security:** These credentials are published in the source code and README. Never use them for a deployed or internet-accessible instance. Configure unique private administrator credentials and disable automatic integration test data seeding before a real deployment. `GO_StartLAN` exposes the 4D API to the local network and should not be used with unchanged demonstration credentials. A production-grade first-run account registration process is still required.

If your existing data file is empty but was created before these changes, close 4D, pull the latest changes, and reopen the project. It will seed and start automatically. Do not delete an existing data file containing useful records.

## Checks before public or production use

- [ ] Import source using a **fresh Git history** after cleaning the working tree, not a Git mirror of private history.
- [ ] Rename all projects, solution files, namespaces, assemblies, resource references, workflows, branding, and application identifiers where safe.
- [ ] Implement secure production first-run administrator provisioning and remove the published test password from production deployments.
- [ ] Review all checked-in files and generated assets for secrets, private data, confidential information and proprietary dependencies.
- [ ] Confirm rights to distribute code originally developed for an employer/client, images, and third-party assets.
- [ ] Build and test a fresh 4D installation, the web bridge, and integration with the Google Play survey app.
- [ ] Choose and add a licence only after ownership review.
- [ ] Publish only after all checks pass.

## Architecture

The separately distributed Android survey app connects directly to the 4D API. The ASP.NET Core frontend uses the 4D bridge. This repository contains only the three listed source projects and their required supporting files.

Developers can explore and experiment with a **fully functioning online demonstration system** at **[glassops.co.uk](https://glassops.co.uk/)** to understand the workflows and capabilities before working with the SmartOps 4D integration and its local integration test data.
