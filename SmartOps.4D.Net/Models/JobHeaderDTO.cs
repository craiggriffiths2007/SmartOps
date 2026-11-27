using SmartOps.Web.Models;
using System.Reflection;

public class JobSendDTO
{
    public string AgentCode { get; set; } = "";
    public string TenantCode { get; set; } = "";
    public string AuthenticationString { get; set; } = "";
    public List<JobHeaderDTO> JobHeaders { get; set; } = new();

}

public class JobHeaderDTO
{
    public JobHeader Head { get; set; } = new JobHeader();

        public List<CustomItem> CustomItems { get; set; } = new();

    public List<string> Images { get; set; } = new();
}
