using SmartOps.Web.Data;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;

namespace SmartOps.Web;

public static partial class EndpointMappings
{
    public static void MapSurveyEnRouteEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/SurveyEnRoute", async (SurveyEnRouteRequest request,
            AppDBContext db, IEmailSender emailSender, ILoggerFactory loggerFactory) =>
        {
            if (string.IsNullOrWhiteSpace(request.TenantCode) ||
                string.IsNullOrWhiteSpace(request.AgentCode) ||
                string.IsNullOrWhiteSpace(request.AuthenticationString))
                return Results.BadRequest(new { reasonPhrase = "Authentication Failed" });

            var tenant = await db.Tenant.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Code == request.TenantCode && t.IsActive);
            if (tenant == null)
                return Results.BadRequest(new { reasonPhrase = "Invalid tenant" });

            var agent = await db.Agent.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(a => a.TenantId == tenant.Id &&
                    a.Code == request.AgentCode &&
                    a.AuthenticationString == request.AuthenticationString);
            if (agent == null)
                return Results.BadRequest(new { reasonPhrase = "Authentication Failed" });

            if (!int.TryParse(request.ContractCode, out int contractNumber) ||
                contractNumber <= 0 || request.DiaryDate == default ||
                request.DiaryDate.Date == DateTime.MaxValue.Date)
                return Results.BadRequest(new { reasonPhrase = "Invalid survey details" });

            var contract = await db.Contract.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == tenant.Id &&
                    c.ContractNumber == contractNumber);
            if (contract == null)
                return Results.NotFound(new { reasonPhrase = "Survey was not found" });

            // ADMIN downloads all tenant bookings in /GetJobs, including those
            // assigned to other engineers. Apply the same access rule here.
            var isAdmin = agent.Code == "ADMIN";
            var date = request.DiaryDate.Date;
            var nextDate = date.AddDays(1);
            var job = await db.Job.IgnoreQueryFilters()
                .FirstOrDefaultAsync(j => j.TenantId == tenant.Id &&
                    j.ContractId == contract.Id && (isAdmin || j.AgentId == agent.Id) &&
                    j.JobType == enum_job_type.Survey &&
                    j.DiaryDate >= date && j.DiaryDate < nextDate);
            if (job == null)
                return Results.NotFound(new { reasonPhrase = "Survey is no longer assigned to you for this date" });

            if (job.EnRouteNotificationSent && !request.Resend)
                return Results.Conflict(new { reasonPhrase = "Notification already sent. Do you want to send it again?", job.EnRoutePressedAtUtc });

            var customer = await db.Customer.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == tenant.Id &&
                    c.Id == job.CustomerId);
            var address = customer?.Email?.Trim();
            if (string.IsNullOrWhiteSpace(address) || !MailAddress.TryCreate(address, out _))
                return Results.BadRequest(new { reasonPhrase = "The customer does not have a valid email address" });

            try
            {
                var pressedAtUtc = request.PressedAtUtc?.ToUniversalTime() ?? DateTime.UtcNow;
                await emailSender.SendEmailAsync(address, "Your engineer is on route",
                    "The engineer is on route and will be with you shortly.");
                job.EnRouteNotificationSent = true;
                job.EnRoutePressedAtUtc = pressedAtUtc;
                // A downloaded header may not have been uploaded yet. Update its
                // saved server copy when it exists; otherwise the next upload saves it.
                var header = await db.JobHeader.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(h => h.TenantId == tenant.Id && h.Guid == request.HeaderGuid &&
                        h.ContractId == contract.Id && h.AgentId == job.AgentId &&
                        h.JobType == enum_job_type.Survey && h.DiaryDate >= date && h.DiaryDate < nextDate);
                if (header != null)
                    header.EnRoutePressedAtUtc = pressedAtUtc;
                await db.SaveChangesAsync();
                return Results.Ok(new SurveyEnRouteResult { EnRoutePressedAtUtc = pressedAtUtc });
            }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger("SurveyEnRoute").LogError(ex,
                    "Unable to send en route email for survey job {JobId}", job.Id);
                return Results.Json(new { reasonPhrase = "Unable to send the customer email. Please try again." },
                    statusCode: StatusCodes.Status502BadGateway);
            }
        });
    }
}