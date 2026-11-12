using SmartOps.Web.Helpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace SmartOps.Web.TagHelpers;

[HtmlTargetElement("record-pagination")]
public class RecordPaginationTagHelper : TagHelper
{
    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;
    public string Prefix { get; set; } = "";
    public string PageParameter { get; set; } = "page";
    public string Fragment { get; set; } = "";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var data = ViewContext.ViewData;
        var info = data[Prefix + "RecordPagination"] as RecordPageInfo;
        if (info == null)
        {
            var page = data["PageNumber"] as int? ?? 1;
            var size = data["PageSize"] as int? ?? 10;
            var count = data["TotalItems"] as int? ?? data["TotalCount"] as int? ?? 0;
            info = new RecordPageInfo(page, count, size);
        }
        output.TagName = "div";
        output.Attributes.Clear();
        output.Attributes.SetAttribute("class", "record-pagination");
        if (info.TotalItems == 0) return;
        var summary = new TagBuilder("p");
        summary.AddCssClass("text-muted small");
        summary.InnerHtml.Append($"Showing {(info.Page - 1) * info.PageSize + 1}–{Math.Min(info.Page * info.PageSize, info.TotalItems)} of {info.TotalItems}");
        output.Content.AppendHtml(summary);
        if (info.TotalPages <= 1) return;
        var nav = new TagBuilder("nav");
        nav.Attributes["aria-label"] = string.IsNullOrEmpty(Prefix) ? "Record pages" : Prefix + " pages";
        var list = new TagBuilder("ul");
        list.AddCssClass("pagination flex-wrap");
        void Add(int target, string label, bool disabled = false, bool current = false)
        {
            var li = new TagBuilder("li");
            li.AddCssClass("page-item" + (disabled ? " disabled" : current ? " active" : ""));
            var item = new TagBuilder(disabled || current ? "span" : "a");
            item.AddCssClass("page-link");
            item.InnerHtml.Append(label);
            if (current) item.Attributes["aria-current"] = "page";
            if (!disabled && !current)
            {
                var request = ViewContext.HttpContext.Request;
                var query = request.Query.ToDictionary(x => x.Key, x => x.Value);
                query[PageParameter] = target.ToString();
                item.Attributes["href"] = request.PathBase + request.Path + QueryString.Create(query)
                    + (string.IsNullOrEmpty(Fragment) ? "" : "#" + Fragment);
            }
            li.InnerHtml.AppendHtml(item);
            list.InnerHtml.AppendHtml(li);
        }
        Add(info.Page - 1, "Previous", info.Page <= 1);
        // Keep large lists manageable while always offering the first and last pages.
        var pages = Enumerable.Range(Math.Max(1, info.Page - 2),
            Math.Min(info.TotalPages, info.Page + 2) - Math.Max(1, info.Page - 2) + 1)
            .Append(1).Append(info.TotalPages).Distinct().Order().ToArray();
        int previous = 0;
        foreach (var page in pages)
        {
            if (previous > 0 && page > previous + 1) Add(0, "…", true);
            Add(page, page.ToString(), current: page == info.Page);
            previous = page;
        }
        Add(info.Page + 1, "Next", info.Page >= info.TotalPages);
        nav.InnerHtml.AppendHtml(list);
        output.Content.AppendHtml(nav);
    }
}
