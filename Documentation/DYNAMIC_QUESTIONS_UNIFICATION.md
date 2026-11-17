# DynamicQuestions unification

Ported from the GlassOps `dynamic-questions-unification` implementation.

## Scope

- Shared `DynamicQuestionSession`: state, dependency visibility, options, images, validation and report projection.
- Shared `DynamicQuestionDocuments`: backwards-compatible JSON snapshot/answer handling.
- Explicit allow-list of widget keys for time pickers, star ratings and named device actions.
- Question JSON includes optional `widgetKey` metadata; older published questionnaires and snapshots remain supported.
- Web item details and spot-check reports use shared question report formatting and conditional rules.
- This repository contains the 4D server, shared question definitions and web integration; the SmartOps Android app source lives in the private MartOps repository under `MartOps.Survey`. The 4D API remains unchanged.

## Verification

No database schema or 4D API change is required. The original tenant question set, revision, and historical visit snapshots are preserved.

```powershell
dotnet run --project tests/SmartOps.DynamicQuestionChecks/SmartOps.DynamicQuestionChecks.csproj
dotnet build SmartOps.Shared/SmartOps.Shared.csproj
dotnet build SmartOps.4D.Web/SmartOps.4D.Web.csproj
```

Test an existing visit and new visit, offline question sync, conditional options,
required/number validation, spot-check photos/signatures, vehicle/tool checks,
fitting/remedial signatures/drawings, and web reporting. Compare old JSON
snapshots and new ones before deployment.

**Important:** These builds and device checks have not been run by the
automated port. They should be completed before publishing server or app updates.
The existing question editor's role checks and draft/publish concurrency controls
remain unchanged.
