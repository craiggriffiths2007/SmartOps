using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SmartOps.Web.Models
{
    public class JobHeader : JobHeaderBase
    {
        [Key]
        [Browsable(false)]
        [DisplayName("Record ID")]
        public int Id { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool EnRouteNotificationSent { get; set; }

        [Browsable(false)]
        public GarageDoorMotor? GarageDoorMotor { get; set; }

        [Browsable(false)]
        public SecuritySurvey? SecuritySurvey { get; set; }

        public JobHeaderIndex AsJobHeaderIndex()
        {
            return new JobHeaderIndex(
                Id,
                JobType,
                AgentCode,
                DiaryDate,
                DateTimeCompleted);
        }
    }

    public class GarageDoorMotor : GarageDoorMotorBase
    {
        public int Id { get; set; }
    }
    public class SecuritySurvey : SecuritySurveyBase
    {
        public int Id { get; set; }
    }

    public class JobHeaderIndex
    {
        public int Id { get; set; }
        public enum_job_type JobType { get; set; }
        public string AgentCode { get; set; } = "";
        public DateTime DiaryDate { get; set; } = DateTime.Today;
        public DateTime DateTimeCompleted { get; set; } = DateTime.Today;

        public JobHeaderIndex(
            int id,
            enum_job_type _JobType,
            string _AgentCode,
            DateTime _DiaryDate,
            DateTime _DateTimeCompleted)
        {
            Id = id;
            JobType = _JobType;
            AgentCode = _AgentCode;
            DiaryDate = _DiaryDate;
            DateTimeCompleted = _DateTimeCompleted;
        }
    }
}