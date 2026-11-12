# SmartOps 4D database schema

Native 4D table mapping for the fresh SmartOps integration dataset. The schema was initially derived from an EF model snapshot, but SmartOps creates a new 4D data file; it does not migrate SQL Server records.

- **Agent**: 12 fields; key `Id`; 1 source foreign keys; generic editor: True
- **AgentMessages**: 8 fields; key `Id`; 0 source foreign keys; generic editor: False
- **AgentMessageRecipients**: 8 fields; key `Id`; 2 source foreign keys; generic editor: False
- **AuditLogs**: 18 fields; key `Id`; 0 source foreign keys; generic editor: False
- **BookingReservations**: 9 fields; key `Id`; 0 source foreign keys; generic editor: False
- **Branches**: 7 fields; key `Id`; 0 source foreign keys; generic editor: True
- **Contract**: 21 fields; key `Id`; 4 source foreign keys; generic editor: True
- **ContractNotes**: 8 fields; key `Id`; 0 source foreign keys; generic editor: False
- **ContractUpdates**: 10 fields; key `Id`; 1 source foreign keys; generic editor: False
- **CustomItems**: 78 fields; key `Id`; 0 source foreign keys; generic editor: True
- **Customer**: 18 fields; key `Id`; 0 source foreign keys; generic editor: True
- **Estimates**: 14 fields; key `Id`; 1 source foreign keys; generic editor: False
- **EstimateLines**: 8 fields; key `Id`; 1 source foreign keys; generic editor: False
- **FAccidents**: 18 fields; key `Id`; 0 source foreign keys; generic editor: True
- **GarageDoorMotor**: 5 fields; key `Id`; 0 source foreign keys; generic editor: True
- **InsuranceClaims**: 21 fields; key `Id`; 1 source foreign keys; generic editor: False
- **InsuranceClaimPhotos**: 7 fields; key `Id`; 1 source foreign keys; generic editor: False
- **InsuranceClaimRequests**: 7 fields; key `Id`; 1 source foreign keys; generic editor: False
- **InsuranceClaimUpdates**: 9 fields; key `Id`; 1 source foreign keys; generic editor: False
- **Insurers**: 12 fields; key `Id`; 0 source foreign keys; generic editor: True
- **InsurerMessages**: 12 fields; key `Id`; 3 source foreign keys; generic editor: False
- **Invoices**: 26 fields; key `Id`; 2 source foreign keys; generic editor: False
- **InvoiceLines**: 8 fields; key `Id`; 1 source foreign keys; generic editor: False
- **Job**: 27 fields; key `Id`; 3 source foreign keys; generic editor: False
- **JobHeader**: 97 fields; key `Id`; 0 source foreign keys; generic editor: True
- **LadderChecks**: 35 fields; key `Id`; 0 source foreign keys; generic editor: True
- **MileageSheets**: 40 fields; key `Id`; 0 source foreign keys; generic editor: True
- **NewsArticles**: 10 fields; key `Id`; 0 source foreign keys; generic editor: True
- **PageHits**: 8 fields; key `Id`; 0 source foreign keys; generic editor: False
- **Images**: 10 fields; key `Id`; 0 source foreign keys; generic editor: False
- **SecuritySurveys**: 25 fields; key `Id`; 0 source foreign keys; generic editor: True
- **SpotChecks**: 32 fields; key `Id`; 0 source foreign keys; generic editor: True
- **SupportTickets**: 10 fields; key `Id`; 2 source foreign keys; generic editor: False
- **SupportTicketMessages**: 11 fields; key `Id`; 2 source foreign keys; generic editor: False
- **SurveyAppRequests**: 17 fields; key `Id`; 0 source foreign keys; generic editor: False
- **Tenant**: 22 fields; key `Id`; 0 source foreign keys; generic editor: False
- **TenantQuestionSets**: 9 fields; key `TenantId`; 1 source foreign keys; generic editor: False
- **ToolChecks**: 21 fields; key `Id`; 0 source foreign keys; generic editor: True
- **VAccidents**: 17 fields; key `Id`; 0 source foreign keys; generic editor: True
- **Vehicles**: 9 fields; key `Id`; 1 source foreign keys; generic editor: True
- **VehicleChecks**: 38 fields; key `Id`; 0 source foreign keys; generic editor: True
- **VehicleCheckHeaders**: 20 fields; key `Id`; 0 source foreign keys; generic editor: True
- **SystemSettings**: 5 fields; key `Id`; 0 source foreign keys; generic editor: True
- **GoAccount**: 13 fields; key `Id`; 0 source foreign keys; generic editor: False
- **GoSession**: 6 fields; key `Id`; 0 source foreign keys; generic editor: False
- **GoAudit**: 8 fields; key `Id`; 0 source foreign keys; generic editor: False
- **GoSequence**: 5 fields; key `Id`; 0 source foreign keys; generic editor: False
- **GoTicket**: 7 fields; key `Id`; 0 source foreign keys; generic editor: False
- **GoTicketMessage**: 7 fields; key `Id`; 0 source foreign keys; generic editor: False
- **GoMessage**: 8 fields; key `Id`; 0 source foreign keys; generic editor: False
- **GoSurveySubmission**: 11 fields; key `Id`; 0 source foreign keys; generic editor: False
- **GoMediaFile**: 10 fields; key `Id`; 0 source foreign keys; generic editor: False
- **GoMediaChunk**: 8 fields; key `Id`; 0 source foreign keys; generic editor: False
- **GoCheckPacket**: 7 fields; key `Id`; 0 source foreign keys; generic editor: False
- **GoIntegrationOutbox**: 9 fields; key `Id`; 0 source foreign keys; generic editor: False

Dates/timestamps and durations use ISO text to preserve date AND time. Decimal properties map to 4D Real; financial workflow calculations round integer pence and cap input. Full SQL decimal(18,2) range is not supported. GoTenantId scopes all records, including source tables that were global (insurers/claims). GoRevision is an API concurrency counter. Source Identity tables are structural only; GoAccount is the separate bcrypt login store. Composite Identity keys use a GoKey surrogate; original composite uniqueness is not implemented. Foreign-key relation definitions do not cascade deletes; API validates referenced rows within the tenant. This is a new empty database, not a SQL Server migration.


## ASP.NET Identity compatibility tables

The following mapped ASP.NET Identity tables are retained for structural compatibility with the optional web frontend. They are not the survey app's login mechanism; Android agents authenticate using the native `Agent` record and the `SmartOpsSurveyService`. The main 4D web/API account store is `GoAccount`.

- **AspNetUsers**: 26 fields; key `Id`; 2 source foreign keys; generic editor: False
- **AspNetRoles**: 6 fields; key `Id`; 0 source foreign keys; generic editor: False
- **AspNetRoleClaims**: 6 fields; key `Id`; 1 source foreign keys; generic editor: False
- **AspNetUserClaims**: 6 fields; key `Id`; 1 source foreign keys; generic editor: False
- **AspNetUserLogins**: 7 fields; key `GoKey`; 1 source foreign keys; generic editor: False
- **AspNetUserRoles**: 5 fields; key `GoKey`; 2 source foreign keys; generic editor: False
- **AspNetUserTokens**: 7 fields; key `GoKey`; 1 source foreign keys; generic editor: False