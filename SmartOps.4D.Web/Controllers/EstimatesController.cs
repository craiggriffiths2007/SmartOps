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
public class EstimatesController(AppDBContext db) : Controller
{
    public async Task<IActionResult> Create(int contractId)
    {
        var contract = await db.Contract.FirstOrDefaultAsync(c => c.Id == contractId);
        if (contract == null) return NotFound();
        if (contract.IsCancelled) return BadRequest("This contract is cancelled.");
        return View(new EstimateCreateViewModel { ContractId = contract.Id, ContractCode = contract.ContractCode });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EstimateCreateViewModel model)
    {
        var contract = await db.Contract.FirstOrDefaultAsync(c => c.Id == model.ContractId);
        if (contract == null) return NotFound();
        if (contract.IsCancelled) return BadRequest("This contract is cancelled.");
        // Also validate collection and money precision for requests made without client validation.
        if (model.Lines == null || model.Lines.Count is < 1 or > 100)
            ModelState.AddModelError(nameof(model.Lines), "Add between 1 and 100 estimate lines.");
        if (model.VatRate < 0 || model.VatRate > 100 || decimal.Round(model.VatRate, 2) != model.VatRate)
            ModelState.AddModelError(nameof(model.VatRate), "Enter a VAT rate between 0 and 100 with up to two decimal places.");
        foreach (var line in model.Lines ?? new())
            if (line == null || string.IsNullOrWhiteSpace(line.Details) || line.Details.Length > 2000 ||
                line.Cost < 0 || line.Cost > 999999999.99m || decimal.Round(line.Cost, 2) != line.Cost)
                ModelState.AddModelError(nameof(model.Lines), "Each line needs details and a non-negative cost with up to two decimal places.");
        if (string.IsNullOrWhiteSpace(model.ServiceLevel) || model.ServiceLevel.Length > 200)
            ModelState.AddModelError(nameof(model.ServiceLevel), "Enter a service level of up to 200 characters.");
        if (!ModelState.IsValid)
        {
            model.ContractCode = contract.ContractCode;
            model.Lines ??= new();
            return View(model);
        }

        var estimate = new Estimate
        {
            TenantId = contract.TenantId, ContractId = contract.Id,
            ServiceLevel = model.ServiceLevel.Trim(), VatRate = model.VatRate,
            CreatedBy = User.Identity?.Name ?? "Staff",
            Lines = model.Lines!.Select((line, index) => new EstimateLine
            {
                TenantId = contract.TenantId, Details = line.Details.Trim(), Cost = line.Cost, SortOrder = index
            }).ToList()
        };
        estimate.CalculateTotals();
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Estimates.Add(estimate);
        await db.SaveChangesAsync();
        contract.SelectedEstimateId ??= estimate.Id;
        var targetStatus = contract.Status < ContractStatus.EstimateCompleted ? ContractStatus.EstimateCompleted : contract.Status;
        var update = ContractUpdateHelper.Create(contract, targetStatus);
        update.Title = "Estimate saved";
        update.Description = $"Estimate #{estimate.Id} ({estimate.ServiceLevel}) saved. Total including VAT: {estimate.Total.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("en-GB"))}.";
        update.CustomerVisible = false;
        db.ContractUpdates.Add(update);
        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return Conflict("The contract changed while saving. Reload the contract and try again.");
        }
        return RedirectToAction("Details", "Contracts", new { id = contract.Id, tab = "estimation" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Choose(int contractId, int estimateId)
    {
        var contract = await db.Contract.FirstOrDefaultAsync(c => c.Id == contractId);
        if (contract == null) return NotFound();
        var estimate = await db.Estimates.FirstOrDefaultAsync(e => e.Id == estimateId && e.ContractId == contractId && e.TenantId == contract.TenantId);
        if (estimate == null) return NotFound();
        if (contract.IsCancelled) return BadRequest("This contract is cancelled.");
        if (contract.SelectedEstimateId != estimate.Id)
        {
            contract.SelectedEstimateId = estimate.Id;
            var status = contract.Status;
            if (estimate.IsAuthorized && status < ContractStatus.Authorised) status = ContractStatus.Authorised;
            else if (!estimate.IsAuthorized && status == ContractStatus.Authorised) status = ContractStatus.EstimateCompleted;
            var update = ContractUpdateHelper.Create(contract, status);
            update.Title = "Estimate chosen";
            update.Description = $"Estimate #{estimate.Id} ({estimate.ServiceLevel}) chosen for the contract.";
            update.CustomerVisible = false;
            db.ContractUpdates.Add(update);
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return Conflict("The estimate selection changed. Reload the contract and try again."); }
        }
        return RedirectToAction("Details", "Contracts", new { id = contractId, tab = "estimation" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Authorize(int contractId, int estimateId)
    {
        var contract = await db.Contract.FirstOrDefaultAsync(c => c.Id == contractId);
        if (contract == null) return NotFound();
        var estimate = await db.Estimates.FirstOrDefaultAsync(e => e.Id == estimateId && e.ContractId == contractId && e.TenantId == contract.TenantId);
        if (estimate == null) return NotFound();
        if (contract.IsCancelled) return BadRequest("This contract is cancelled.");
        try { await EstimateAuthorization.AuthorizeAsync(db, contract, estimate, User.Identity?.Name ?? "Staff"); }
        catch (DbUpdateConcurrencyException) { return Conflict("The estimate changed. Reload the contract and try again."); }
        return RedirectToAction("Details", "Contracts", new { id = contractId, tab = "estimation" });
    }
}
