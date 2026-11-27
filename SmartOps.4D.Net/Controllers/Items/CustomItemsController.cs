using SmartOps.Web.Data;
using SmartOps.Shared.Models;
using SmartOps.Web.ViewModels;
using SmartOps.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = SmartOps.Web.Helpers.StaffRoles.ManageDiary)]
public sealed class CustomItemsController(Repo repo) : Controller
{
    public async Task<IActionResult> Details(int id)
    {
        var item = await repo.Db.CustomItems.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        var children = await repo.Db.CustomItems.Where(c => c.HeaderId == item.HeaderId && c.ParentItemGuid == item.Guid).ToListAsync();
        children.RemoveAll(child => !CustomItemLinks.IsRequested(child, [item]));
        return View(new ItemDetailsViewModel { CustomItem = item, CustomChildren = children,
            Images = repo.GetSurveyItemImages(item.ContractCode, item.item_number) });
    }

    public Task<IActionResult> LockDetails(int id) => SpecialistDetails(id, true);

    public Task<IActionResult> LeadingDetails(int id) => SpecialistDetails(id, false);

    private async Task<IActionResult> SpecialistDetails(int id, bool isLock)
    {
        // All queries retain the same staff authorization and tenant filters as item details.
        var item = await repo.Db.CustomItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (item is null || !(isLock ? SpecialistPrintBuilder.CanPrintLock(item) : SpecialistPrintBuilder.CanPrintLeading(item)))
            return NotFound();
        var header = await repo.Db.JobHeader.AsNoTracking().FirstOrDefaultAsync(x => x.Id == item.HeaderId);
        if (header is null || repo.Db.CurrentTenantId is not int tenantId) return NotFound();
        var tenant = await repo.Db.Tenant.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tenantId);
        if (tenant is null) return NotFound();

        var model = SpecialistPrintBuilder.Build(item, isLock);
        model.ItemId = item.Id;
        model.CompanyName = tenant.Name;
        model.CustomerName = header.CustomerName;
        model.PropertyAddress = string.Join(", ", new[] { header.CustomerAddressLine1, header.CustomerAddressLine2, header.CustomerPostcode }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        // Resolve this item's exact specialist image, including when it is a child item.
        string filename = $"{item.ContractCode}_{(isLock ? "dLO" : "dLI")}{item.item_number:000}00.jpg";
        if (System.Text.RegularExpressions.Regex.IsMatch(filename, @"^[A-Za-z0-9_-]+\.jpg$"))
            model.ImageFilename = await repo.Db.Images.AsNoTracking().Where(x => x.Filename == filename)
                .Select(x => x.Filename).FirstOrDefaultAsync();
        return View("TechnicalDetails", model);
    }

    public async Task<IActionResult> SignedItemChange(int id)
    {
        var item = await repo.Db.CustomItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (item is null || (item.bDifferentFromOriginal != true && !item.bDifferentFromOriginalSigned))
            return NotFound();

        var header = await repo.Db.JobHeader.AsNoTracking().FirstOrDefaultAsync(x => x.Id == item.HeaderId);
        if (header is null || repo.Db.CurrentTenantId is not int tenantId)
            return NotFound();

        var tenant = await repo.Db.Tenant.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tenantId);
        if (tenant is null)
            return NotFound();

        // Use the same per-item filename as the survey signature capture page.
        string filename = $"{item.ContractCode}_sCU{item.item_number:000}00.jpg";
        string signature = item.bDifferentFromOriginalSigned
            ? await repo.Db.Images.Where(x => x.Filename == filename).Select(x => x.Filename).FirstOrDefaultAsync() ?? ""
            : "";

        return View(new SignedItemChangeViewModel
        {
            Item = item,
            JobHeader = header,
            SignatureFilename = signature,
            CompanyName = tenant.Name,
            CompanyAddress1 = tenant.Address1 ?? "",
            CompanyAddress2 = tenant.Address2 ?? "",
            CompanyAddress3 = string.Join(", ", new[] { tenant.Town, tenant.County }.Where(x => !string.IsNullOrWhiteSpace(x))),
            CompanyPostcode = tenant.Postcode ?? "",
            CompanyTelephone = tenant.Telephone ?? "",
            CompanyEmail = tenant.Email ?? "",
            LogoUrl = tenant.Logo is { Length: > 0 }
                ? Url.Action("Logo", "Company", new { v = tenant.LogoUpdated?.Ticks ?? 0 }) ?? ""
                : ""
        });
    }
}
