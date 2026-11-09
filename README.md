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

## Local integration testing setup

SmartOps includes fictional 4D integration test data so developers can exercise the API, web interface and mobile workflows without manually creating records. After cloning, open `SmartOps.4D/Project/smartops4d.4DProject` in 4D and create a **new empty** data file if prompted. The 4D `onStartup` method automatically invokes `GO_StartLAN`, which starts HTTP port **8045** and imports the integration test records only if the database is completely empty. Successful startups and seeds do **not** display confirmation dialogs. `GO_Start` is also available as a manual alias for the same startup.

The ASP.NET web bridge automatically discovers the active local Ethernet or Wi-Fi IPv4 address for its 4D API connection; no machine-specific IP address is stored in the source. On machines with unusual or multiple network adapters, set `FourD__BaseUrl=http://YOUR-PC-IP:8045/` in the web app's launch environment or your own local configuration.

Run `SmartOps.4D.Web` from Visual Studio and sign in with these **local integration-testing** credentials:

| Field | Value |
| --- | --- |
| Company code | `SMARTOPS` (configured automatically by the web app) |
| Username | `admin` |
| Password | `Password1!` |

The initial integration test dataset contains 54 fictional records, including branches, agents, insurers, customers, contracts, estimates, bookings and messages. The supplied test accounts share a known test password and are intended only for isolated development environments.

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
