using SmartOps.Web.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web.Helpers;

public static class BranchIndexFilters
{
    // Build choices from the tenant's complete data before filtering or pagination.
    public static async Task PopulateAsync(AppDBContext context, ViewDataDictionary viewData,
        string? branchCode, IQueryable<string?> recordedCodes)
    {
        var branches = await context.Branches.AsNoTracking()
            .Where(branch => branch.Code != null && branch.Code != "")
            .Select(branch => new { branch.Code, branch.Name }).ToListAsync();
        var codes = await recordedCodes.Where(code => code != null && code != "")
            .Distinct().ToListAsync();
        var options = branches.Select(branch => new SelectListItem
        {
            Value = branch.Code!.Trim(),
            Text = string.IsNullOrWhiteSpace(branch.Name) ? "Unnamed branch" : branch.Name.Trim()
        }).Concat(codes.Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => new SelectListItem { Value = code!.Trim(), Text = "Unknown branch" }))
            .Where(option => !string.IsNullOrWhiteSpace(option.Value))
            .DistinctBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
            .OrderBy(option => option.Text, StringComparer.OrdinalIgnoreCase).ToList();
        if (branchCode != null && !options.Any(option => string.Equals(option.Value, branchCode, StringComparison.OrdinalIgnoreCase)))
            options.Add(new SelectListItem { Value = branchCode, Text = "Unknown branch" });
        foreach (var option in options)
            option.Selected = string.Equals(option.Value, branchCode, StringComparison.OrdinalIgnoreCase);
        viewData["BranchOptions"] = options;
    }
}