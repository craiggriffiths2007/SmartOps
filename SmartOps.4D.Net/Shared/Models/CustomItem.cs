using SmartOps.Shared.Enums;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
namespace SmartOps.Shared.Models;
// The only survey item record. Questions live in SurveyAnswersJson.
public class CustomItemBase : SurveyItemBase
{
    [Browsable(false)] public string QuestionSetKey { get; set; } = "";
    [Browsable(false)] public override enum_item_type item_type => enum_item_type.custom;
    [Browsable(false)] public override string ItemTitle =>
        QuestionSetKey.Equals(ItemAssemblyQuestions.Key, StringComparison.OrdinalIgnoreCase)
            ? Material switch {
                enum_material_type.UPVC => "UPVC Assembly",
                enum_material_type.Aluminium => "Aluminium Assembly",
                enum_material_type.Timber => "Timber Assembly",
                _ => DynamicQuestionCatalogue.ItemLabel(QuestionSetKey)
            }
            : DynamicQuestionCatalogue.ItemLabel(QuestionSetKey);
    [Browsable(false)] public Guid? ParentItemGuid { get; set; }
    [Browsable(false)] public string ParentQuestionSetKey { get; set; } = "";
    [Browsable(false)] public string ParentMaterial { get; set; } = "";
    [Browsable(false)] public string ContractCauseOfDamage { get; set; } = "";
    [Browsable(false)] public bool RequiresSecuritySurvey { get; set; }
    [Browsable(false)] public bool ParentRepair { get; set; }
    [NotMapped, JsonIgnore, Browsable(false)] public virtual bool bRepair { get => ParentItemGuid.HasValue ? ParentRepair : AnswerBoolean("flex_repair") == true; set { if (ParentItemGuid.HasValue) ParentRepair = value; else SetAnswer("flex_repair", value ? "Yes" : "No"); } }
    [NotMapped, JsonIgnore, Browsable(false)] public virtual enum_material_type Material { get => QuestionSetKey.Equals(ItemAssemblyQuestions.Key, StringComparison.OrdinalIgnoreCase)
        ? Enum.TryParse<enum_material_type>(GetSurveyAnswerValue("material"), true, out var material) ? material : enum_material_type.None
        : enum_material_type.None; set { if (QuestionSetKey.Equals(ItemAssemblyQuestions.Key, StringComparison.OrdinalIgnoreCase)) SetAnswer("material", value.ToString()); } }
    [NotMapped, JsonIgnore, Browsable(false)] public virtual string opening_type { get => GetSurveyAnswerValue("opening_type"); set => SetAnswer("opening_type", value); }
    [NotMapped, JsonIgnore, Browsable(false)] public virtual enum_item_type base_item { get => ParentItemGuid.HasValue ? enum_item_type.custom : enum_item_type.none; set { } }
    [NotMapped, JsonIgnore, Browsable(false)] public virtual string ProductInto { get => ParentItemGuid.HasValue ? !string.IsNullOrWhiteSpace(ParentMaterial) ? ParentMaterial : DynamicQuestionCatalogue.ItemLabel(ParentQuestionSetKey) : GetSurveyAnswerValue("ProductInto"); set => SetAnswer("ProductInto", value); }
    [Browsable(false)] public bool bSashDrawn { get; set; }
    [Browsable(false)] public bool bSectionDrawn { get; set; }
    [Browsable(false)] public bool bMouldingDrawn { get; set; }
    [Browsable(false)] public bool bTimberColoursSigned { get; set; }
    [Browsable(false)] public bool bDoorComplete { get; set; }
    [Browsable(false)] public bool bWindowComplete { get; set; }
    [Browsable(false)] public bool bLockComplete { get; set; }
    [Browsable(false)] public bool bHandleDrawingComplete { get; set; }
    [Browsable(false)] public bool bSpecialGlassComplete { get; set; }
    [Browsable(false)] public enum_special_glass_type SpecialGlassType { get; set; }
    [Browsable(false)] public string sizeA { get; set; } = "";
    [Browsable(false)] public string sizeB { get; set; } = "";
    [Browsable(false)] public string sizeC { get; set; } = "";
    [Browsable(false)] public string sizeD { get; set; } = "";
    [Browsable(false)] public float sizeAf { get; set; }
    [Browsable(false)] public float sizeBf { get; set; }
    [Browsable(false)] public float sizeCf { get; set; }
    [Browsable(false)] public float sizeDf { get; set; }
    [Browsable(false)] public string lead_CWidths { get; set; } = "";
    [Browsable(false)] public string lead_CHeights { get; set; } = "";
    [Browsable(false)] public float lead_posX { get; set; }
    [Browsable(false)] public float lead_posY { get; set; }
    [Browsable(false)] public bool lead_bBackToBackComplete { get; set; }
    [Browsable(false)] public bool lead_bDiamondComplete { get; set; }
    [Browsable(false)] public bool lead_bGeorgianComplete { get; set; }
    [Browsable(false)] public bool lead_bBarComplete { get; set; }
    [Browsable(false)] public string l_size1 { get; set; } = "";
    [Browsable(false)] public string l_size2 { get; set; } = "";
    [Browsable(false)] public string l_sizeA { get; set; } = "";
    [Browsable(false)] public string l_sizeB { get; set; } = "";
    [Browsable(false)] public string l_sizeC { get; set; } = "";
    [Browsable(false)] public string l_sizeD { get; set; } = "";
    [Browsable(false)] public string l_sizeE { get; set; } = "";
    [Browsable(false)] public string l_sizeF { get; set; } = "";
    [Browsable(false)] public string l_sizeG { get; set; } = "";
    [Browsable(false)] public int left_bolt { get; set; }
    [Browsable(false)] public int right_bolt { get; set; }
    [Browsable(false)] public int l_num { get; set; }
    [Browsable(false)] public float lock_position { get; set; }
    [Browsable(false)] public float l_fpos1 { get; set; }
    [Browsable(false)] public float l_fpos2 { get; set; }
    [Browsable(false)] public float l_fpos3 { get; set; }
    [Browsable(false)] public float l_fpos4 { get; set; }
    [Browsable(false)] public float l_fpos5 { get; set; }
    [Browsable(false)] public float l_fpos6 { get; set; }
    [Browsable(false)] public float l_fpos7 { get; set; }
    [Browsable(false)] public string l_type1 { get; set; } = "";
    [Browsable(false)] public string l_type2 { get; set; } = "";
    [Browsable(false)] public string l_type3 { get; set; } = "";
    [Browsable(false)] public string l_type4 { get; set; } = "";
    [Browsable(false)] public string l_type5 { get; set; } = "";
    [Browsable(false)] public string l_type6 { get; set; } = "";
    [Browsable(false)] public string l_type7 { get; set; } = "";
    [Browsable(false)] public bool additional_drawn { get; set; }
    [Browsable(false)] public int bifold_signed { get; set; }
    [Browsable(false)] public bool cons_roof_under_drawn { get; set; }
}
