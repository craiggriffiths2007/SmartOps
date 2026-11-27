using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
namespace SmartOps.Web.Models;
public class CustomItem : CustomItemBase
{
    [Key, Browsable(false)]
    public int Id { get; set; }
    public SurveyItem AsSurveyItem() => new(Id, item_type, ParentItemGuid.HasValue ? enum_item_type.custom : enum_item_type.none,
        item_number, ContractCode, ItemTitle);
}
