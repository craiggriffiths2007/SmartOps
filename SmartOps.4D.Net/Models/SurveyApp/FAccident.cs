namespace SmartOps.Web.Models;

public class FAccident : TenantEntity
{
        public int Id { get; set; }
        public Guid Guid { get; set; } = Guid.NewGuid();
        public string CheckID { get; set; } = "";
        public DateTime DateTimeCompleted { get; set; } = DateTime.Today;
        public string Type { get; set; } = "";
        public string sig_fname1 { get; set; } = "";
        public string sig_fname2 { get; set; } = "";
        public int no_of_photos { get; set; }
        public DateTime date_time { get; set; }
        public bool bSent { get; set; }
        public bool bComplete { get; set; }
        public string AccidentAnswersJson { get; set; } = "";
        public string AccidentQuestionsSnapshotJson { get; set; } = "";
        public int? AccidentQuestionSetVersion { get; set; }
}
