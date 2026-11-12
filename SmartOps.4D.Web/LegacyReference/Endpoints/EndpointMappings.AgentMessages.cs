using SmartOps.Web.Data;
using SmartOps.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web
{
    public static partial class EndpointMappings
    {
        public static void MapAgentMessageEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/GetAgentMessages", async (GetDataDTO request, AppDBContext db) =>
            {
                var auth = await AuthenticateAgentAsync(request, db);
                if (auth == null)
                    return Results.BadRequest(new { ReasonPhrase = "Authentication Failed" });

                var messages = await db.AgentMessageRecipients
                    .IgnoreQueryFilters()
                    .Where(x => x.TenantId == auth.Value.TenantId && x.AgentId == auth.Value.AgentId)
                    .Include(x => x.AgentMessage)
                    .OrderByDescending(x => x.AgentMessage.Created)
                    .Select(x => new AgentMessageDTO
                    {
                        Id = x.AgentMessageId,
                        Subject = x.AgentMessage.Subject,
                        BodyHtml = x.AgentMessage.BodyHtml,
                        Created = x.AgentMessage.Created,
                        IsRead = x.IsRead,
                        ReadDateTime = x.ReadDateTime
                    })
                    .ToListAsync();

                return Results.Ok(messages);
            });

            app.MapPost("/GetAgentUnreadMessageCount", async (GetDataDTO request, AppDBContext db) =>
            {
                var auth = await AuthenticateAgentAsync(request, db);
                if (auth == null)
                    return Results.BadRequest(new { ReasonPhrase = "Authentication Failed" });

                var count = await db.AgentMessageRecipients
                    .IgnoreQueryFilters()
                    .CountAsync(x => x.TenantId == auth.Value.TenantId &&
                                     x.AgentId == auth.Value.AgentId && !x.IsRead);

                return Results.Ok(new { UnreadCount = count });
            });

            app.MapPost("/MarkAgentMessageRead", async (MarkAgentMessageReadDTO request, AppDBContext db) =>
            {
                var auth = await AuthenticateAgentAsync(request, db);
                if (auth == null)
                    return Results.BadRequest(new { ReasonPhrase = "Authentication Failed" });

                var recipient = await db.AgentMessageRecipients
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x => x.TenantId == auth.Value.TenantId &&
                                              x.AgentId == auth.Value.AgentId &&
                                              x.AgentMessageId == request.MessageId);

                if (recipient == null)
                    return Results.NotFound();

                if (!recipient.IsRead)
                {
                    recipient.IsRead = true;
                    recipient.ReadDateTime = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }

                return Results.Ok(new { status = "success" });
            });
        }

        private static async Task<(int TenantId, int AgentId)?> AuthenticateAgentAsync(GetDataDTO request, AppDBContext db)
        {
            var tenant = await db.Tenant
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Code == request.TenantCode && x.IsActive);

            if (tenant == null)
                return null;

            var agent = await db.Agent
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.TenantId == tenant.Id &&
                                          x.Code == request.AgentCode &&
                                          x.AuthenticationString == request.AuthenticationString);

            return agent == null ? null : (tenant.Id, agent.Id);
        }
    }
}
