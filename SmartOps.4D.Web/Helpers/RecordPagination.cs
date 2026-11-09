using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web.Helpers;

public record RecordPageInfo(int Page, int TotalItems, int PageSize = 10)
{
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
}

public static class RecordPagination
{
    // Call after applying authorization, filters and a stable ordering.
    public static async Task<List<T>> ToRecordPageAsync<T>(this IQueryable<T> query,
        int page, ViewDataDictionary viewData, string prefix = "", int pageSize = 10)
    {
        var count = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(count / (double)pageSize)));
        viewData[prefix + "RecordPagination"] = new RecordPageInfo(page, count, pageSize);
        return await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
    }
}
