using SmartOps.Web.Models;

public class SignedMandateViewModel
{
    public JobHeader JobHeader { get; set; } = null!;

    public string SignatureFilename { get; set; } = "";

    public string CompanyName { get; set; } = "";

    public string CompanyAddress1 { get; set; } = "";

    public string CompanyAddress2 { get; set; } = "";

    public string CompanyAddress3 { get; set; } = "";

    public string CompanyPostcode { get; set; } = "";

    public string CompanyTelephone { get; set; } = "";

    public string CompanyEmail { get; set; } = "";

    public string LogoUrl { get; set; } = "";
}