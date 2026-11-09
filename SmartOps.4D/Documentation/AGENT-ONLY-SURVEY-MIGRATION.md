# Agent-only Survey API test branch

This branch switches native mobile API authentication to the Agent entity. The Razor web login remains on GoAccount and tenant isolation is still based on GoTenantId.

## Behaviour

- AgentLogin looks up the selected tenant and Agent Code, checks the supplied Android ID exists on **any Agent in that tenant**, and for Agents with PrivilegedAccess requires that it matches **that Agent's own SerialNumber**.
- No GoAccount lookup, creation, password or GoAllJobs dependency in SmartOpsSurveyService.
- Agent.PrivilegedAccess (default false) controls whether accessibleJobs returns all company bookings. Existing ADMIN codes are **not** automatically privileged: tick the checkbox explicitly for ADMIN in the web Agent Edit page.
- authenticate checks the current Agent token directly and returns that Agent.
- SendJobs, SendChecks, SendMedia, notifications and messages use the authenticated Agent's Id and GoTenantId. **For the disposable trial database only**, legacy fields named AccountId and GoOwnerAccountId on mobile-related rows now contain **Agent.Id**, not GoAccount.Id. The field names have been retained to avoid a wider schema rewrite. Do **not** apply this branch over production/history that depends on the old ownership meaning.
- The GoAccount table and browser login remain unchanged.

## Before testing

1. Back up your local 4D database and SmartOpsMedia directory.
2. Check out the branch, update the 4D project, reload the native source and reopen it if the 4D editor still has cached methods.
3. 4D must add the new Agent.PrivilegedAccess boolean field to the database structure.
4. In the 4D web Agent page, tick Privileged Access for ADMIN or any supervisor who should download all jobs, and enter their own SerialNumber. Ordinary Agents keep it unchecked and can borrow authorised devices.
5. Start with **new jobs and new submissions**, since the meaning of mobile ownership fields changes on this branch.

## Full test matrix

- Ordinary Agent on own authorised device, on another Agent's authorised device, and on an unregistered device.
- Privileged Agent on their own device; reject them on another authorised device.
- Job download: ordinary Agent gets assigned jobs; privileged Agent gets all company jobs. Reject cross-tenant access.
- Download questions and vehicle checks.
- Send completed survey and fitting with custom items, retry same job, handle invalid GUID or stale booking.
- Send image and chunked video, resend media filename; ensure downloaded binary images open.
- Send vehicle/ladders/tools/mileage/accident checks; retry and confirm no cross-agent overwrite.
- Notes, messages, unread counts, mark read, en route, update/version requests.
- Web login, Agent editing, diary and existing customer/contract pages still work.

## Validation status

Source-level change only. **4D compilation, SQLite bridge, and Android device round-trip have not been run by the assistant.** This branch is meant for isolated full-device testing.
