namespace SmartOps.Web.Models;

/// <summary>One complete, versioned question snapshot per tenant.</summary>
public class TenantQuestionSet
{
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = default!;
    public int Version { get; set; } = 1;
    public string QuestionsJson { get; set; } = "";
    // Draft changes never affect the snapshot delivered to devices.
    public string? DraftQuestionsJson { get; set; }
    public DateTime? DraftSavedUtc { get; set; }
    public int DraftRevision { get; set; }
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
