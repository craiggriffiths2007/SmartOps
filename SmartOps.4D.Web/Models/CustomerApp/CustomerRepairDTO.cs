using SmartOps.Web.Models;
namespace SmartOps.Web.Models.CustomerApp;

public class CustomerRepairDTO
{
    public int ContractId { get; set; }
    public string Reference { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string AddressLine1 { get; set; } = "";
    public string Town { get; set; } = "";
    public string Postcode { get; set; } = "";
    public string DamageDescription { get; set; } = "";
    public string CauseOfDamage { get; set; } = "";
    public ContractStatus Status { get; set; }
    public string StatusTitle { get; set; } = "";
    public string StatusDescription { get; set; } = "";
    public string StatusEta { get; set; } = "";
    public CustomerAppointmentDTO? NextAppointment { get; set; }
    public List<CustomerRepairItemDTO> Items { get; set; } = [];
    public List<CustomerRepairUpdateDTO> Updates { get; set; } = [];
    public List<CustomerPhotoDTO> Photos { get; set; } = [];
}

public class CustomerAppointmentDTO
{
    public string Type { get; set; } = "";
    public DateTime DateTime { get; set; }
    public string EngineerName { get; set; } = "";
    public string Notes { get; set; } = "";
}

public class CustomerRepairItemDTO
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
}

public class CustomerRepairUpdateDTO
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime DateTime { get; set; }
    public bool IsCurrent { get; set; }
}

public class CustomerPhotoDTO
{
    public string Url { get; set; } = "";
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime DateTime { get; set; }
}

public class CustomerChangePasswordDTO
{
    public string AuthenticationString { get; set; } = "";
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

public class CustomerAccountDTO
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";

    public string AddressLine1 { get; set; } = "";
    public string AddressLine2 { get; set; } = "";
    public string Town { get; set; } = "";
    public string Postcode { get; set; } = "";

    public string Phone { get; set; } = "";
}