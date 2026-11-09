using Microsoft.EntityFrameworkCore;
//using System.Text.Json;
using Newtonsoft.Json;
using SmartOps.Web.Data;

using SmartOps.Web.Models;

using System.Collections.Concurrent;

namespace SmartOps.Web
{
    public static partial class EndpointMappings
    {
        static ConcurrentDictionary<string, object> _uploadLocks = new();

        public static void MapAPIEndpoints(this IEndpointRouteBuilder app)
        {

            app.MapPost("/AgentLogin", async (AgentLoginDTO gs, AppDBContext db) =>
            {
                // Find the tenant
                var tenant = await db.Tenant
                    .FirstOrDefaultAsync(t =>
                        t.Code == gs.TenantCode &&
                        t.IsActive);

                if (tenant == null)
                {
                    return Results.BadRequest(
                        new
                        {
                            ReasonPhrase = "Tenant Code Not Found : " + gs.TenantCode
                        });
                }

                // Find the agent within this tenant
                var agent = await db.Agent
                    .IgnoreQueryFilters()
                    .Include(x => x.Branch)
                    .FirstOrDefaultAsync(x =>
                        x.TenantId == tenant.Id &&
                        x.Code == gs.AgentCode);

                if (agent == null)
                {
                    return Results.BadRequest(
                        new
                        {
                            ReasonPhrase = "Agent Code Not Found : " + gs.AgentCode
                        });
                }

                if (!string.Equals(
                    agent.Password,
                    gs.Password,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return Results.BadRequest(
                        new
                        {
                            ReasonPhrase = "Password Incorrect"
                        });
                }

                // Generate a new authentication string
                var send_data = new AgentLoginDTO
                {
                    TenantCode = tenant.Code,
                    AgentCode = agent.Code,
                    AgentType = agent.Type,
                    BranchCode = agent.Branch?.Code,
                    AuthenticationString = Guid.NewGuid().ToString(),
                    CurrentVersion = gs.CurrentVersion,
                    CurrentBuild = gs.CurrentBuild
                };

                agent.AuthenticationString =
                    send_data.AuthenticationString;

                if (!string.IsNullOrWhiteSpace(gs.CurrentVersion) ||
                    !string.IsNullOrWhiteSpace(gs.CurrentBuild))
                {
                    agent.CurrentAppVersion = string.IsNullOrWhiteSpace(gs.CurrentBuild)
                        ? gs.CurrentVersion
                        : $"{gs.CurrentVersion} ({gs.CurrentBuild})";
                }

                agent.LoggedInDateTime =
                    DateTime.Now;

                await db.SaveChangesAsync();

                return Results.Ok(send_data);
            });
            

            app.MapPost("/GetUpdateContractNotes", async (GetDataDTO gs, AppDBContext db) =>
            {
                if (string.IsNullOrWhiteSpace(gs.TenantCode) || string.IsNullOrWhiteSpace(gs.AgentCode)
                    || string.IsNullOrWhiteSpace(gs.AuthenticationString))
                    return Results.BadRequest(new { ReasonPhrase = "Authentication Failed. Please sign in again." });

                var auth = await AuthenticateAgentAsync(gs, db);
                if (auth == null)
                    return Results.BadRequest(new { ReasonPhrase = "Authentication Failed. Please sign in again." });

                int tenantId = auth.Value.TenantId;
                if (db.CurrentTenantId.HasValue && db.CurrentTenantId != tenantId)
                    return Results.BadRequest(new { ReasonPhrase = "Tenant mismatch. Please sign in again." });

                if (!int.TryParse(gs.ContractCode?.Trim(), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int contractNumber) || contractNumber <= 0)
                    return Results.BadRequest(new { ReasonPhrase = "Enter a valid contract number." });

                // Mobile requests authenticate in the payload, not with a web login.
                // ContractCode is a calculated display property; query the stored number.
                var contract = await db.Contract.IgnoreQueryFilters().Include(c => c.Insurer)
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ContractNumber == contractNumber);
                if (contract == null)
                    return Results.NotFound(new { ReasonPhrase = "Contract not found." });

                var customer = await db.Customer.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == contract.CustomerId);
                if (customer == null)
                    return Results.NotFound(new { ReasonPhrase = "Customer details not found for this contract." });

                if (!string.IsNullOrWhiteSpace(gs.Note))
                {
                    db.ContractNotes.Add(new ContractNote {
                        TenantId = tenantId,
                        ContractCode = contract.ContractCode,
                        DateAdded = DateTime.Now,
                        Note = gs.Note.Trim(),
                        AddedBy = "PDA : " + gs.AgentCode
                    });
                    await db.SaveChangesAsync();
                }

                var sendData = new JobContractDTO(contract, customer) {
                    ContractNotes = await db.ContractNotes.IgnoreQueryFilters()
                        .Where(n => n.TenantId == tenantId && n.ContractCode == contract.ContractCode)
                        .OrderByDescending(n => n.DateAdded).ThenByDescending(n => n.Id).ToListAsync()
                };
                return Results.Ok(sendData);
            });






            
        }
    }
}
