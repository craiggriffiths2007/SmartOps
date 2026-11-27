using System.Globalization;
using SmartOps.Web.Data;
using SmartOps.Web.Helpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace SmartOps.Web.TagHelpers;

[HtmlTargetElement("select", Attributes = "lock-branch-value")]
public sealed class LockedBranchTagHelper(AppDBContext context) : TagHelper
{
    public override int Order => 10000;
    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;
    public object? LockBranchValue { get; set; }

    public override void Process(TagHelperContext tagContext, TagHelperOutput output)
    {
        output.Attributes.RemoveAll("lock-branch-value");
        if (!UserBranchDefaults.IsLocked(context, ViewContext.HttpContext.User)) return;
        output.Attributes.SetAttribute("disabled", "disabled");
        // Disabled controls are not submitted, so preserve the displayed value.
        var hidden = new TagBuilder("input");
        hidden.TagRenderMode = TagRenderMode.SelfClosing;
        hidden.Attributes["type"] = "hidden";
        hidden.Attributes["name"] = output.Attributes["name"]?.Value?.ToString() ?? "";
        hidden.Attributes["value"] = Convert.ToString(LockBranchValue, CultureInfo.InvariantCulture) ?? "";
        output.PostElement.AppendHtml(hidden);
    }
}
