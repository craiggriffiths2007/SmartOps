using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace SmartOps.Shared.Models
{
    public abstract class TenantEntity
    {
        [Browsable(false)]
        public int TenantId { get; set; }
    }
}
