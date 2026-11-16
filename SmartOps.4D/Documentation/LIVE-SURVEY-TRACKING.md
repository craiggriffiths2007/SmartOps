# Live surveyor tracking — 4D integration (experimental)

This is an optional port of GlassOps live surveyor tracking to the SmartOps 4D integration and **MartOps.Survey** (the SmartOps Android app). It uses **no destination pin**.

## How the parts connect

1. The surveyor logs into the **4D** server in MartOps.Survey as before. Job download, upload and SurveyEnRoute remain direct-to-4D.
2. If a **Live Tracking Web URL (HTTPS)** has been supplied in MartOps.Survey Settings, selecting **En Route** prompts for Android location permission. The app first calls the normal 4D `/SurveyEnRoute` endpoint.
3. Once 4D records En Route, the app calls `POST /api/survey-tracking/start` on the configured `SmartOps.4D.Web` (or `MartOps.4D.Web`) host.
4. The web host calls 4D `/SurveyTrackingVerify`, verifying the **existing authenticated 4D agent**, assigned survey and date, and reading the **customer email from 4D**. The app cannot choose an arbitrary recipient.
5. The web host sends an HTML email with a unique HTTPS link to `/Track/{token}`. The Android foreground location service uploads the latest GPS position to the host roughly every 20 seconds. The public customer page polls about every 20 seconds.
6. **Arrived**, a resend, or the one-hour maximum stops the appropriate session. The map displays **only the surveyor's small blue vehicle marker**.

## Configuration

The feature is **opt-in** and is disabled until the tracking web host and its email configuration have been set up.

### Web host

The .NET 4D bridge requires:

- `FourD__BaseUrl`: the reachable base URL of the 4D server (for example, `http://127.0.0.1:8045/`). The web host must be able to call `/SurveyTrackingVerify` on 4D.
- `SurveyTracking__PublicBaseUrl`: the **public HTTPS** base URL of the bridge, without a trailing slash (for example, `https://tracking.example.org`).
- `SurveyTracking__DatabasePath`: an optional writable path for the **separate persistent** tracking SQLite database. Default is `App_Data/survey-tracking.db` under the web application's content root. Do **not** point it at the bridge's disposable, in-memory EF/SQLite workspace. Back up/protect this file appropriately.
- `EmailSettings__SmtpServer`, `EmailSettings__SmtpPort`, `EmailSettings__SenderName`, `EmailSettings__SenderEmail`, `EmailSettings__Username`, `EmailSettings__Password`, `EmailSettings__UseSsl`: SMTP sender settings. Keep credentials in environment variables / secrets rather than source control.

The bridge defaults to listening only on `127.0.0.1:5245`. To make the public URL work, **run it behind a properly configured HTTPS reverse proxy** and ensure its hostname is permitted in ASP.NET Core `AllowedHosts`. TLS termination, routing, firewall configuration and externally reachable DNS are deployment responsibilities. Do not publicly expose the 4D database HTTP server.

The public proxy needs to expose `/Track/*` and `/api/survey-tracking/*` to the browser/tablet. The bridge uses a separate permanent SQLite file for tracking sessions because the normal 4D web front-end imports a temporary SQLite workspace for each authenticated request.

### Mobile

In **MartOps.Survey → Settings**:

- **Server:** keep the current 4D URL (for example, `http://192.168.0.15:8045`) for normal app operations.
- **Live Tracking Web URL (HTTPS):** enter the publicly accessible URL configured in `SurveyTracking__PublicBaseUrl`.

Leave **Live Tracking Web URL** empty to keep the old 4D-only En Route workflow. A missing/broken tracking host **does not undo** En Route recorded in 4D; the app displays a warning rather than claiming tracking started.

### Google Play

This adds `ACCESS_FINE_LOCATION`, `ACCESS_COARSE_LOCATION`, `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_LOCATION` and `POST_NOTIFICATIONS` to the Android manifest. Before publishing the SmartOps app to Google Play, complete the foreground-service **user-initiated location sharing** declaration and any required data safety / prominent-disclosure steps. Test on actual tablets and Android versions.

## Security and retention

- Server verifies the agent's live 4D authentication token and assigned survey before issuing a customer link.
- Customer viewer token and mobile upload token are separate random 256-bit secrets; **only SHA-256 hashes are stored** in the SQLite file.
- The browser receives no login credentials or upload token.
- No GPS route history is stored, only the latest coordinate. Arrived removes the saved point immediately, and an automatic cleanup clears expired locations roughly every 15 minutes.
- The web API rejects new coordinates after the one-hour expiry. Expired session metadata is deleted after about 24 hours.
- Resending the link closes earlier sessions for the same visit.
- Location updates depend on Android GPS, connectivity, permissions and battery management; do not promise uninterrupted service.

## Test plan

1. Build and launch the feature branches of **4D**, **4D.Web**, and **MartOps.Survey**. Start 4D and verify `/SurveyTrackingVerify` returns only the assigned survey after a successful En Route.
2. Test the public HTTPS tracking URL and SMTP email delivery with a **test customer**; do not test against live customer data.
3. On Android, grant location permission and select **En Route**. Confirm the email link, tracking notification and blue marker.
4. Lock the tablet and open navigation; check updates continue.
5. Select **Arrived**; confirm tracking ends and the map no longer shows coordinates.
6. Verify a resend invalidates previous links and expiry stops updating after one hour.
7. Test with tracking URL blank, invalid credentials, a different tenant, no customer email, no GPS and unavailable network.
8. Verify `SmartOps.4D.Web` and `MartOps.4D.Web` builds before merging; these branches have **not** been compiled or field-tested in this environment.

No changes to the 4D database catalog or an EF migration are required. The standalone bridge creates its own tracking SQLite table when started.
