
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web.Services;

public interface ITenantDeletionService
{
    Task DeleteTenantAsync(int tenantId);
}

public sealed class TenantDeletionService : ITenantDeletionService
{
    private readonly AppDBContext _context;
    private readonly UserManager<AppUser> _userManager;

    public TenantDeletionService(
        AppDBContext context,
        UserManager<AppUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task DeleteTenantAsync(int tenantId)
    {
        var tenant = await _context.Tenant
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (tenant == null)
            throw new InvalidOperationException(
                "Company account not found.");

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        await DeleteTenantDataAsync(tenantId);

        _context.Tenant.Remove(tenant);
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();
    }

    private async Task DeleteTenantDataAsync(int tenantId)
    {
        // Messaging and support
        await _context.AuditBulkAsync("AgentMessageRecipients", tenantId, "Deleted", () => _context.AgentMessageRecipients
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        await _context.AuditBulkAsync("AgentMessages", tenantId, "Deleted", () => _context.AgentMessages
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        await _context.AuditBulkAsync("SupportTicketMessages", tenantId, "Deleted", () => _context.SupportTicketMessages
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        await _context.AuditBulkAsync("SupportTickets", tenantId, "Deleted", () => _context.SupportTickets
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        // Insurer-related tenant records
        // Shared insurers and claims are not deleted.
        await _context.AuditBulkAsync("InsurerMessages", tenantId, "Deleted", () => _context.InsurerMessages
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        await _context.AuditBulkAsync("InsuranceClaimRequests", tenantId, "Deleted", () => _context.InsuranceClaimRequests
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        // Contract history
        await _context.AuditBulkAsync("ContractUpdates", tenantId, "Deleted", () => _context.ContractUpdates
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        await _context.AuditBulkAsync("ContractNotes", tenantId, "Deleted", () => _context.ContractNotes
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        // Survey and job items
        await _context.AuditBulkAsync("CustomItems", tenantId, "Deleted", () => _context.CustomItems.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync());

        // Images
        await _context.AuditBulkAsync("Images", tenantId, "Deleted", () => _context.Images
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        // Shared-primary-key job-header children
        await _context.AuditBulkAsync("SecuritySurveys", tenantId, "Deleted", () => _context.SecuritySurveys
            .Where(x => _context.JobHeader
                .IgnoreQueryFilters()
                .Any(h =>
                    h.Id == x.Id &&
                    h.TenantId == tenantId))
            .ExecuteDeleteAsync());

        await _context.AuditBulkAsync("GarageDoorMotor", tenantId, "Deleted", () => _context.GarageDoorMotor
            .Where(x => _context.JobHeader
                .IgnoreQueryFilters()
                .Any(h =>
                    h.Id == x.Id &&
                    h.TenantId == tenantId))
            .ExecuteDeleteAsync());

        // Job headers and diary
        await _context.AuditBulkAsync("JobHeader", tenantId, "Deleted", () => _context.JobHeader
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        await _context.AuditBulkAsync("Job", tenantId, "Deleted", () => _context.Job
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        // Vehicle checks
        await _context.AuditBulkAsync("VehicleChecks", tenantId, "Deleted", () => _context.VehicleChecks
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        await _context.AuditBulkAsync("VehicleCheckHeaders", tenantId, "Deleted", () => _context.VehicleCheckHeaders
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        // Other operational checks
        await _context.AuditBulkAsync("ToolChecks", tenantId, "Deleted", () => _context.ToolChecks
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        await _context.AuditBulkAsync("LadderChecks", tenantId, "Deleted", () => _context.LadderChecks
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        await _context.AuditBulkAsync("SpotChecks", tenantId, "Deleted", () => _context.SpotChecks
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        await _context.AuditBulkAsync("MileageSheets", tenantId, "Deleted", () => _context.MileageSheets
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        // Accident records
        await _context.AuditBulkAsync("VAccidents", tenantId, "Deleted", () => _context.VAccidents
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        await _context.AuditBulkAsync("FAccidents", tenantId, "Deleted", () => _context.FAccidents
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        await _context.BookingReservations.IgnoreQueryFilters().Where(r => r.TenantId == tenantId).ExecuteDeleteAsync();

        // Agents and vehicles
        await _context.AuditBulkAsync("Agent", tenantId, "Deleted", () => _context.Agent
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        await _context.AuditBulkAsync("Vehicles", tenantId, "Deleted", () => _context.Vehicles
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        await _context.AuditBulkAsync("InvoiceLines", tenantId, "Deleted", () => _context.InvoiceLines
            .IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync());
        await _context.AuditBulkAsync("Invoices", tenantId, "Deleted", () => _context.Invoices
            .IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync());

        // Clear the chosen-estimate reference before deleting the estimate and its lines.
        await _context.AuditBulkAsync("Contract", tenantId, "Updated", () => _context.Contract
            .IgnoreQueryFilters().Where(x => x.TenantId == tenantId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.SelectedEstimateId, (int?)null)));
        await _context.AuditBulkAsync("EstimateLines", tenantId, "Deleted", () => _context.EstimateLines
            .IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync());
        await _context.AuditBulkAsync("Estimates", tenantId, "Deleted", () => _context.Estimates
            .IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ExecuteDeleteAsync());

        // Contracts and customers
        await _context.AuditBulkAsync("Contract", tenantId, "Deleted", () => _context.Contract
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        await _context.AuditBulkAsync("Customer", tenantId, "Deleted", () => _context.Customer
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        // Branches
        await _context.AuditBulkAsync("Branches", tenantId, "Deleted", () => _context.Branches
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync());

        // Identity users
        var users = await _userManager.Users
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync();

        foreach (var user in users)
        {
            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    result.Errors.Select(e => e.Description));

                throw new InvalidOperationException(
                    $"Could not delete user '{user.Email}': {errors}");
            }
        }
    }
}
