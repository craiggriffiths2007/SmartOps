namespace SmartOps.Shared.Models;
public static class CustomItemLinks
{
    public static bool IsRequested(CustomItemBase child, IEnumerable<CustomItemBase> items)
    {
        if (!child.ParentItemGuid.HasValue) return true;
        var parent = items.FirstOrDefault(p => p.Guid == child.ParentItemGuid && p.HeaderId == child.HeaderId && p.bComplete);
        var type = child.QuestionSetKey.ToLowerInvariant() switch {
            "glass" => DynamicQuestionType.ReplaceGlass, "panel" => DynamicQuestionType.ReplacePanel,
            "lockmech" => DynamicQuestionType.ReplaceLocking, _ => (DynamicQuestionType?)null
        };
        return parent != null && type.HasValue && DynamicWorkflowQuestions.SelectedType(parent, type.Value);
    }
}
