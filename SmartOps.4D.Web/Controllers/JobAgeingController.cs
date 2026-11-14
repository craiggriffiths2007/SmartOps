using SmartOps.Web.Data;
using SmartOps.Web.Helpers;
using SmartOps.Web.Models;
using SmartOps.Web.Services;
using SmartOps.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web.Controllers;

[Authorize(Roles = StaffRoles.ManageDiary)]
public sealed class JobAgeingController(AppDBContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        var contracts = await db.Contract.AsNoTracking()
            .Include(c => c.Customer)
            .Where(c => !c.IsCancelled && c.Status != ContractStatus.Invoiced)
            .ToListAsync();
        var health = await ContractAgeingService.CalculateAsync(db, contracts);
        var rows = contracts.Select(contract =>
        {
            var action = ActionFor(contract.Status);
            return new JobAgeingRow(
                contract.Id, contract.ContractNumber.ToString("D8"),
                contract.CustomerId, contract.Customer?.Name ?? "",
                ItemDisplayNameHelper.GetDisplayName(contract.Status.ToString()),
                action.Label, action.Controller, action.Action, action.Parameter, action.Tab,
                health[contract.Id]);
        })
        .OrderByDescending(row => row.Health.Days ?? -1)
        .ThenBy(row => row.ContractNumber)
        .ToList();

        const int pageSize = 20;
        var pages = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)pageSize));
        page = Math.Clamp(page, 1, pages);
        return View(new JobAgeingViewModel
        {
            Rows = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            Page = page,
            TotalPages = pages
        });
    }

    private static (string Label, string Controller, string Action, string Parameter, string? Tab)
        ActionFor(ContractStatus status) => status switch
        {
            ContractStatus.Reported => ("Book survey", "Jobs", "Create", "contractId", null),
            ContractStatus.SurveyBooked => ("View survey booking", "Contracts", "Details", "id", "bookings"),
            ContractStatus.SurveyCompleted => ("Prepare estimate", "Estimates", "Create", "contractId", null),
            ContractStatus.EstimateCompleted => ("Authorise estimate", "Contracts", "Details", "id", "estimation"),
            ContractStatus.Authorised => ("Book fitting", "Jobs", "Create", "contractId", null),
            ContractStatus.FittingBooked => ("View fitting booking", "Contracts", "Details", "id", "bookings"),
            ContractStatus.FittingCompleted => ("Create invoice", "Invoices", "Create", "contractId", null),
            _ => ("Review contract", "Contracts", "Details", "id", "updates")
        };
}
