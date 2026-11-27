using SmartOps.Web.Data;
using SmartOps.Web.Helpers;
using SmartOps.Web.Models;
using SmartOps.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web.Controllers;

[Authorize(Roles = StaffRoles.ManageDiary)]
public class InvoicesController(AppDBContext db) : Controller
{
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var query = db.Invoices.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            var number = search.StartsWith("INV-", StringComparison.OrdinalIgnoreCase) ? search[4..] : search;
            int.TryParse(number, out var invoiceId);
            query = query.Where(i => i.Id == invoiceId || i.ContractCode.Contains(search) || i.BillToName.Contains(search) || i.CustomerName.Contains(search));
        }
        ViewBag.Search = search;
        return View(await query.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id).ToRecordPageAsync(page, ViewData));
    }

    public async Task<IActionResult> Details(int id)
    {
        var invoice = await db.Invoices.AsNoTracking().Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id);
        return invoice == null ? NotFound() : View(invoice);
    }

    public async Task<IActionResult> Print(int id)
    {
        var invoice = await db.Invoices.AsNoTracking().Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id);
        return invoice == null ? NotFound() : View(invoice);
    }

    private Task<Contract?> LoadContractAsync(int contractId) => db.Contract
        .Include(c => c.Customer).Include(c => c.Insurer).Include(c => c.Tenant)
        .FirstOrDefaultAsync(c => c.Id == contractId);

    private Task<Estimate?> ChosenEstimateAsync(Contract contract) => db.Estimates.AsNoTracking().Include(e => e.Lines)
        .FirstOrDefaultAsync(e => e.Id == contract.SelectedEstimateId && e.ContractId == contract.Id && e.TenantId == contract.TenantId && e.AuthorizedUtc != null);

    private static string Address(params string?[] parts) => string.Join("\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    private static string CustomerAddress(Contract c) => Address(c.Customer?.Add1, c.Customer?.Add2, c.Customer?.Add3, c.Customer?.Postcode);
    private static string BillingName(Contract c) => c.InsurerId.HasValue ? c.Insurer?.Name ?? "" : c.Customer?.Name ?? "";
    private static string BillingAddress(Contract c) => c.InsurerId.HasValue
        ? Address(c.Insurer?.Address1, c.Insurer?.Address2, c.Insurer?.Address3, c.Insurer?.Postcode) : CustomerAddress(c);

    private static void PopulateDisplay(InvoiceCreateViewModel model, Contract contract, Estimate? estimate)
    {
        model.ContractCode = contract.ContractCode;
        model.BillingPartyText = contract.InsurerId.HasValue ? "Insurer" : "Customer (Domestic)";
        model.BillToName = BillingName(contract);
        model.BillToAddress = BillingAddress(contract);
        model.EstimateReference = estimate == null ? null : $"Estimate #{estimate.Id}: {estimate.ServiceLevel}";
    }

    public async Task<IActionResult> Create(int contractId)
    {
        var contract = await LoadContractAsync(contractId);
        if (contract == null) return NotFound();
        var existing = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.ContractId == contractId);
        if (existing != null) return RedirectToAction(nameof(Details), new { id = existing.Id });
        if (contract.IsCancelled || contract.Status != ContractStatus.FittingCompleted)
            return BadRequest("An invoice can be created once fitting is completed.");
        var estimate = await ChosenEstimateAsync(contract);
        var model = new InvoiceCreateViewModel
        {
            ContractId = contract.Id,
            Description = estimate?.ServiceLevel ?? (string.IsNullOrWhiteSpace(contract.DamageDescription) ? "Glazing work" : contract.DamageDescription),
            VatRate = estimate?.VatRate ?? 20m,
            Lines = estimate?.Lines.OrderBy(l => l.SortOrder).Select(l => new EstimateLineInput { Details = l.Details, Cost = l.Cost }).ToList() ?? new() { new() }
        };
        PopulateDisplay(model, contract, estimate);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InvoiceCreateViewModel model)
    {
        var contract = await LoadContractAsync(model.ContractId);
        if (contract == null) return NotFound();
        var existing = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.ContractId == contract.Id);
        if (existing != null) return RedirectToAction(nameof(Details), new { id = existing.Id });
        if (contract.IsCancelled || contract.Status != ContractStatus.FittingCompleted)
            return BadRequest("An invoice can be created once fitting is completed.");
        var estimate = await ChosenEstimateAsync(contract);
        if (model.Lines == null || model.Lines.Count is < 1 or > 100)
            ModelState.AddModelError(nameof(model.Lines), "Add between 1 and 100 invoice lines.");
        foreach (var line in model.Lines ?? new())
            if (line == null || string.IsNullOrWhiteSpace(line.Details) || line.Details.Length > 2000 || line.Cost < 0 || line.Cost > 999999999.99m || decimal.Round(line.Cost, 2) != line.Cost)
                ModelState.AddModelError(nameof(model.Lines), "Each line needs details and a non-negative cost with up to two decimal places.");
        if (model.VatRate < 0 || model.VatRate > 100 || decimal.Round(model.VatRate, 2) != model.VatRate)
            ModelState.AddModelError(nameof(model.VatRate), "Enter a VAT rate between 0 and 100 with up to two decimal places.");
        if (string.IsNullOrWhiteSpace(model.Description) || model.Description.Length > 200)
            ModelState.AddModelError(nameof(model.Description), "Enter a description of up to 200 characters.");
        if (string.IsNullOrWhiteSpace(BillingName(contract)))
            ModelState.AddModelError("", "The contract's billing recipient needs a name before issuing an invoice.");
        if (!ModelState.IsValid)
        {
            PopulateDisplay(model, contract, estimate);
            model.Lines ??= new();
            return View(model);
        }

        var invoice = new Invoice
        {
            TenantId = contract.TenantId, ContractId = contract.Id, EstimateId = estimate?.Id,
            ContractCode = contract.ContractCode, PolicyNumber = contract.PolicyNumber,
            BillingParty = contract.InsurerId.HasValue ? InvoiceBillingParty.Insurer : InvoiceBillingParty.Customer,
            BillToName = BillingName(contract), BillToAddress = BillingAddress(contract),
            CustomerName = contract.Customer?.Name ?? "", ServiceAddress = CustomerAddress(contract),
            SellerName = contract.Tenant?.Name ?? "", SellerAddress = Address(contract.Tenant?.Address1, contract.Tenant?.Address2, contract.Tenant?.Town, contract.Tenant?.County, contract.Tenant?.Postcode),
            SellerEmail = contract.Tenant?.Email ?? "", SellerTelephone = contract.Tenant?.Telephone ?? "",
            VatRegistrationNumber = model.VatRegistrationNumber?.Trim() ?? "",
            Description = model.Description.Trim(), InvoiceDate = model.InvoiceDate.Date,
            IssuedBy = User.Identity?.Name ?? "Staff", VatRate = model.VatRate,
            Lines = model.Lines!.Select((l, index) => new InvoiceLine { TenantId = contract.TenantId, SortOrder = index, Details = l.Details.Trim(), Cost = l.Cost }).ToList()
        };
        invoice.CalculateTotals();
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();
            var update = ContractUpdateHelper.Create(contract, ContractStatus.Invoiced);
            update.Title = "Invoice issued";
            update.Description = $"{invoice.InvoiceNumber} issued to {invoice.BillToName}. Total including VAT: {invoice.Total.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("en-GB"))}.";
            update.CustomerVisible = true;
            db.ContractUpdates.Add(update);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return Conflict("The contract changed while invoicing. Reload the contract and try again.");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            db.ChangeTracker.Clear();
            var issued = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.ContractId == model.ContractId);
            if (issued != null) return RedirectToAction(nameof(Details), new { id = issued.Id });
            throw;
        }
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }
}
