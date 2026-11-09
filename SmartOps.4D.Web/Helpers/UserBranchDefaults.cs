using System.Security.Claims;
using SmartOps.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web.Helpers;

public static class UserBranchDefaults
{
    public static bool IsLocked(AppDBContext context, ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return context.Users.Where(u => u.Id == userId).Select(u => u.LockBranch).FirstOrDefault();
    }

    public static bool AllowsSelection(AppDBContext context, ClaimsPrincipal user, int? selected, int? expected) =>
        !IsLocked(context, user) || selected == expected;

    public static string? Code(AppDBContext context, ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var code = context.Users.Where(u => u.Id == userId).Select(u => u.BranchCode).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(code)) return null;
        code = code.Trim();
        return context.Branches.Any(b => b.Code != null && b.Code.Trim() == code) ? code : null;
    }

    public static int? Id(AppDBContext context, ClaimsPrincipal user)
    {
        var code = Code(context, user);
        return code == null ? null : context.Branches.Where(b => b.Code != null && b.Code.Trim() == code)
            .Select(b => (int?)b.Id).FirstOrDefault();
    }

    public static string? Filter(AppDBContext context, HttpContext http, string? selected)
    {
        // Unlocked users can explicitly choose All instead of their default.
        return !IsLocked(context, http.User) && http.Request.Query.ContainsKey("branchCode")
            ? (string.IsNullOrWhiteSpace(selected) ? null : selected.Trim())
            : Code(context, http.User);
    }
}
