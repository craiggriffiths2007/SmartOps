# Native 4D question editor

This is the first step toward removing the .NET web site's dependency for questionnaire editing only. It does **not** migrate legacy schemas, reproduce .NET bookings/contracts, or implement native 4D printouts.

## Access

Start the 4D HTTP server with `GO_Start` or `GO_StartLAN` and open:

`http://<4d-host>:8045/questions`

Use the same company code, username and password used by the `SmartOps.4D.Web` login. These credentials are verified by the existing `POST /api/auth/login` endpoint; the standalone editor does not implement another password database. Only Admin/Staff web accounts can edit questions. Resetting all questions requires Admin.

## Operation

The existing HTML question editor, JavaScript and CSS have been bundled into `SmartOps.4D/Resources/question-editor`. The Razor view has been converted to static markup, and the browser uses the existing 4D bearer authentication for the following endpoints:

| Endpoint | Method | Purpose |
|---|---|---|
| `/api/questions/editor` | GET | Read published/draft JSON and revisions for the signed-in company |
| `/api/questions/validate` | POST | Validate imported question JSON without saving |
| `/api/questions/draft` | POST | Save a draft without changing the mobile version |
| `/api/questions/discard` | POST | Discard draft |
| `/api/questions/publish` | POST | Publish and advance the version received by devices |
| `/api/questions/reset` | POST | Admin-only reset to bundled defaults |

The existing `TenantQuestionSets` records remain the sole persistence store. Requests are company-scoped using the authenticated 4D principal. The 4D handler is a shared singleton and checks version/draft revision before writes to avoid silently overwriting edits. Mobile download continues to use its existing published question endpoint.

The browser token is stored for the current browser tab in `sessionStorage`, not in the URL or local storage. Use HTTPS and the existing trusted network boundary for any non-local installation.

## Verification before deployment

1. Run `GO_TestNativeQuestionEditor` inside a copy of the 4D database. It rolls its changes back and writes `native-question-editor-test-result.json` to the database folder.
2. Start the 4D HTTP server; visit `/questions`, sign in and edit a question. Verify draft save and browser reload.
3. Confirm that an unpublished change does not appear in SmartOps.Survey.
4. Publish and confirm that a fresh mobile download receives the updated questionnaire and version.
5. Check that stale browser tabs receive a conflict, and accounts in another company cannot see or modify the first company's questions.
6. Test login expiry, invalid credentials, imports, adding/removing categories, export and discard.

## Current limitations

The .NET controller performs richer shared question validation, especially retention of shared question IDs. The 4D implementation has basic JSON, category-name, duplicate ID and empty-label validation; before treating it as production-ready, the full rules should be ported and tested with the real questionnaire set. Diagram options remain empty, matching the currently empty `DiagramAssets.Names` list in the web project. The per-category Restore and global Reset buttons are hidden in the standalone editor until their behaviour and permissions can be verified end to end; import/export, drafting and publishing are included.

No 4D runtime test has been executed from this environment. Do not merge/publish to production until the 4D smoke test and browser/mobile checks pass.
