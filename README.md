# SmartOps

SmartOps is an **open-source 4D integration and testing platform** for field-service workflows. It provides a native 4D database, an HTTP/JSON API, and a browser-based workspace for testing jobs, appointments, questionnaires and survey submissions.

An optional **ASP.NET Core web frontend and 4D bridge** are included for developers who want to explore an alternative web integration approach. They are **not required** to run the native 4D workspace, Questions Editor or mobile API.

A companion [SmartOps Android survey app](https://play.google.com/store/apps/details?id=com.authorityx.smartops) is available on Google Play. Its source code is not included in this repository.

> **For local development and evaluation only.** The bundled accounts and records are fictional. The supplied credentials, plain HTTP access and optional serial-number fallback are not secure production defaults.

## What's included

- **4D database and API:** Native records, business logic, test-data seeding and HTTP endpoints for survey devices.
- **Browser workspace:** Contracts, diary, records, uploads, claims and administration served directly by 4D.
- **Questions Editor:** Full questionnaire editor with live preview, conditional rules, import/export, drafts and publishing. Published questions are downloaded by SmartOps.Survey.

**Architecture:** SmartOps.Survey (Android) → 4D HTTP/JSON API → 4D database. The browser workspace talks to the same 4D API.

## Quick start

1. Clone the repository or download the [SmartOps Integration Kit](https://github.com/craiggriffiths2007/SmartOps/releases/latest/download/SmartOps-Integration-Windows-x64.zip).
2. In 4D, open `SmartOps.4D/Project/smartops4d.4DProject` and create a new, empty data file if prompted.
3. The `onStartup` method runs `GO_StartLAN`, starts the web server on port **8045**, and opens the browser at [http://127.0.0.1:8045/](http://127.0.0.1:8045/). It seeds fictional records **only when the mapped database tables are empty**.
4. Sign in to the SmartOps workspace using the demo account below.
5. Verify the API at [http://127.0.0.1:8045/api/health](http://127.0.0.1:8045/api/health).

| Demo login (newly seeded data) | Value |
| --- | --- |
| Username | `admin` |
| Password | `Password1!` |

An existing 4D data file may have different credentials. Opening a populated database does **not** reset its records. To start over, create a separate empty data file rather than deleting data you need.

## Questions Editor

From the signed-in workspace, select **Questions editor** in the sidebar. The original multi-pane editor opens in a **new browser tab**, reusing your current login. Allow the new tab if your browser blocks it.

Use the editor to manage question categories, edit conditional rules, preview questions, import/export question sets, and save drafts. **Publishing** updates the version available to SmartOps.Survey during synchronization; saving a draft alone does not.

The legacy `/questions` URL redirects to the dedicated editor. For the normal single-login workflow, enter through the main workspace first.

## Optional ASP.NET Core web application

The repository also contains `SmartOps.4D.Web` (an ASP.NET Core MVC web interface and 4D bridge) and `SmartOps.Shared` (shared contracts and models). These projects demonstrate another way to integrate with 4D. They are entirely optional: **SmartOps.4D can serve its own browser interface and mobile API without running the .NET application**.

For developers wishing to explore this alternative, see the [web integration guide](SmartOps.4D/Documentation/WEB_API_INTEGRATION.md). The optional frontend normally runs at `http://127.0.0.1:5245/`.

## Connect SmartOps.Survey

Install the [SmartOps Android survey app](https://play.google.com/store/apps/details?id=com.authorityx.smartops) and connect it to your 4D server using the local test settings:

| Setting | Local test value |
| --- | --- |
| Server | `http://YOUR-PC-IP:8045` |
| Agent | An existing agent code such as `ADMIN` |

A blank agent Serial Number in 4D permits sign-in without device registration for local integration testing. When a serial number is registered, the device checks apply; privileged agents require their own registered device. This fallback is **not suitable for production authentication**.

On Windows, allow inbound TCP **8045** only on your trusted **Private** network. Do not expose the demonstration server directly to the public internet.

## Test data

The included seed provides fictional contracts and customers, branches, agents, vehicles, diary bookings, estimates, insurer details and updates for exercising the workflow.

## Documentation

- [Android Survey API](SmartOps.4D/Documentation/APP_API_INTEGRATION.md) — mobile-to-4D integration
- [4D database schema and ER diagram](SmartOps.4D/Documentation/SCHEMA.md) — tables and relationships
- [All releases](https://github.com/craiggriffiths2007/SmartOps/releases) — available integration packages and documentation
- [GlassOps live demonstration](https://glassops.co.uk/) — example field-service workflows

