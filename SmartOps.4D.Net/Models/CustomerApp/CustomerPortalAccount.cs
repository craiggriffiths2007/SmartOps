using SmartOps.Web.Models;
using System.ComponentModel.DataAnnotations;

namespace SmartOps.Web.Models.CustomerApp;



public class CustomerLoginRequestDTO
{
    public string Reference { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class CustomerLoginResultDTO
{
    public string AuthenticationString { get; set; } = "";
    public int CustomerId { get; set; }
    public int ContractId { get; set; }
    public string CustomerName { get; set; } = "";
}

public class CustomerRequestDTO
{
    public string AuthenticationString { get; set; } = "";
    public int ContractId { get; set; }
}

public class CreateCustomerPortalAccountDTO
{
    public int CustomerId { get; set; }
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}
