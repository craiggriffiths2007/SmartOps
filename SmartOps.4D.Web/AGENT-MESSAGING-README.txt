Mart Ops - Agent Messaging
===========================

Implemented in this source:
- Diary > Agent Messages menu option.
- Quill rich-text message composer using the Quill files already in wwwroot/lib.
- Messages are sent ONLY to explicitly checked Agent records.
- Message history with sent date, sender, recipient count and read count.
- Message detail page showing each selected recipient and read/unread state.
- TenantEntity used for both messages and recipients, so existing tenant filtering applies.
- MAUI-facing endpoints:
    POST /GetAgentMessages
    POST /GetAgentUnreadMessageCount
    POST /MarkAgentMessageRead
  These use TenantCode + AgentCode + AuthenticationString, matching the existing mobile authentication approach.

Database migration
------------------
The uploaded ZIP contains SmartOps.Web but does not contain its referenced ../SmartOps.Shared project,
so this environment cannot safely run `dotnet ef migrations add` against the complete solution.

After putting this Web project back beside your existing SmartOps.Shared project, run in Package Manager Console:

    Add-Migration AddAgentMessaging
    Update-Database

Or CLI:

    dotnet ef migrations add AddAgentMessaging
    dotnet ef database update

MAUI work still required
------------------------
The uploaded source is the Web project only, so I have added the server API needed by the phone but have not
modified the MAUI app itself. The phone can call /GetAgentUnreadMessageCount for the bottom-left envelope badge,
/GetAgentMessages for the inbox, and /MarkAgentMessageRead when a message is opened.
