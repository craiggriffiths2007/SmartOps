namespace SmartOps.Web.Models
{
    public class AccidentsVehicleDTO
    {
        public VAccident Accident { get; set; } = new();
    }

    public class VAccident : TenantEntity
    {
        public int Id { get; set; }
        public Guid Guid { get; set; } = Guid.NewGuid();
        public string CheckID { get; set; } = "";
        public DateTime DateTimeCompleted { get; set; } = DateTime.Today;
        public string date_time { get; set; } = "";
        public bool bSent { get; set; }
        public bool bComplete { get; set; }
        public int no_of_photos { get; set; }
        public bool y_signed { get; set; }
        public bool c_drawings { get; set; }
        public string AccidentAnswersJson { get; set; } = "";
        public string AccidentQuestionsSnapshotJson { get; set; } = "";
        public int? AccidentQuestionSetVersion { get; set; }
    }
}
