# Registration settings

Configure the Registration section in appsettings.json (or override it in appsettings.Production.json):

```json
"Registration": {
  "AutoApprove": true,
  "RequireEmailConfirmation": false
}
```

- Defaults match the immediate registration flow in every environment: activate the tenant, approve the administrator and sign in immediately.
- Set AutoApprove=false to create an inactive tenant and unapproved administrator. A notification goes to the registration reviewer; a SuperAdmin approves through RegistrationApproval/Review.
- Set RequireEmailConfirmation=true to require an email link before sign-in. Auto-approved applicants receive the link after registration; manually approved applicants receive it after approval. Both Identity confirmation checks follow this setting. Working SMTP and EmailSettings:Enabled=true are needed to deliver these messages.
- With confirmation disabled, new accounts are marked EmailConfirmed=true, matching the previous development behaviour. Enabling confirmation later does not undo existing confirmed states.
- Changes require an application restart. Existing approval and tenant states are retained; changing AutoApprove affects new registrations only.
- ApprovalSignInManager still checks approval and tenant activity. The settings apply equally in Development and Production; email delivery settings do not bypass approval.
- Unique email validation, password validation, tenant isolation, and demo-data creation are retained.
