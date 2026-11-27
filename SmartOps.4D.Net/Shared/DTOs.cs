using System;
using System.Collections.Generic;
using System.Text;

namespace SmartOps.Shared.Models
{
    public class OKRecordDTO
    {
        public string? comments { get; set; }
        public int DBId { get; set; }
    }
    public class CustomerImageRequestDTO
    {
        public string AuthenticationString { get; set; } = "";
        public int ContractId { get; set; }
        public string Filename { get; set; } = "";
    }

}