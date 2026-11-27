using SmartOps.Web.Controllers;
using SmartOps.Web.Models;


namespace SmartOps.Web.ViewModels
{
    public class VehicleCheckDetailsViewModel
    {
        public VehicleCheck? VehicleCheck { get; set; }

        public IEnumerable<PhotoImage>? insidePhotos { get; set; }

        public PhotoImage photoLeft { get; set; }
        public PhotoImage photoRight { get; set; }
        public PhotoImage photoFront { get; set; }
        public PhotoImage photoBack { get; set; }

        public PhotoImage diagramLeft { get; set; }
        public PhotoImage diagramRight { get; set; }
        public PhotoImage diagramFront { get; set; }
        public PhotoImage diagramBack { get; set; }
        public PhotoImage drv_signature { get; set; }
        public PhotoImage chk_signature { get; set; }

    }
}
