# SmartOps

SmartOps is a 4D integration and testing platform for field-service workflows. It provides a working 4D API server, an ASP.NET Core web interface, and prepopulated integration test data for exploring and validating end-to-end workflows. A companion Android survey application is distributed separately through Google Play.

> **Integration testing environment.** The included accounts and records are for local development and integration testing, not production use. Do not expose a server running the supplied test credentials to the public internet.

## Included projects

- `SmartOps.4D` — 4D database and API server.
- `SmartOps.4D.Web` — ASP.NET Core web frontend connected to 4D through a bridge.
- `SmartOps.Shared` — shared contracts, DTOs, and dynamic question definitions.

## API integration guide

For developers integrating another system with SmartOps, see the **[4D API integration guide](SmartOps.4D/Documentation/API-INTEGRATION.md)**. It explains the 4D HTTP endpoints, direct Android API versus ASP.NET bridge architecture, authentication, example test requests, and useful methods such as `GO_StartLAN`, `SmartOpsHTTP.handle`, `SmartOpsService.dispatch`, and `SmartOpsSurveyService.sendJobs`.

## SmartOps 4D Integration Kit

**[Download SmartOps Integration Kit (ZIP)](https://github.com/craiggriffiths2007/SmartOps/releases/latest/download/SmartOps-Integration-Windows-x64.zip)** — a ready-to-use download containing the editable 4D project, integration test data and optional prebuilt Windows web interface. You can also [view all releases](https://github.com/craiggriffiths2007/SmartOps/releases).

The single download contains:

- **`SmartOps.4D/`** — editable 4D project, API, documentation, and fictional integration test data.
- **`SmartOps-Web-Windows-x64.zip`** — optional prebuilt, self-contained ASP.NET Core browser interface for Windows x64. No Visual Studio or separately installed .NET runtime is required.
- **`README-START-HERE.txt`** — setup instructions for both options.

**4D-only quick start:** Extract the main ZIP, open `SmartOps.4D/Project/smartops4d.4DProject` in 4D, and create a **new empty data file** when prompted. The 4D `onStartup` method runs `GO_StartLAN`, starts HTTP on port **8045** and automatically imports the integration test records. You can now explore 4D methods and test the API **without installing or running the web frontend**. A compatible installation/licence for 4D is still required.

**Optional web frontend:** Extract the **inner** `SmartOps-Web-Windows-x64.zip` to its own folder, then double-click `SmartOps.Web.exe` while 4D is running. The executable automatically opens the web login page at `http://127.0.0.1:5245/Account/Login`. Keep the server console open while using the interface.

The web ZIP uses .NET single-file publishing to reduce separate DLLs, although some website assets and supporting files remain. The package is intended for **local integration testing**; packaging the web executable inside a second ZIP does **not** guarantee that antivirus software, Chrome or Windows SmartScreen will consider it safe. Validate the download and do not disable security protections.

**Developers who want to change the integration** can [download the source repository](https://github.com/craiggriffiths2007/SmartOps) and build the .NET frontend themselves. The proprietary Android application source code is not distributed.

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

Developers can explore and experiment with a **fully functioning online demonstration system** at **[glassops.co.uk](https://glassops.co.uk/)** to understand the workflows and capabilities before working with the SmartOps 4D integration and its local integration test data.
