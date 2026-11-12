using SmartOps.Web.Models;

namespace SmartOps.Web.Helpers
{
    public static class ContractStatusHelper
    {
        public static string GetTitle(ContractStatus status)
        {
            return status switch
            {
                ContractStatus.Reported => "Claim Reported",
                ContractStatus.SurveyBooked => "Survey Booked",
                ContractStatus.SurveyCompleted => "Survey Completed",
                ContractStatus.EstimateCompleted => "Estimate Completed",
                ContractStatus.Authorised => "Authorised",
                ContractStatus.FittingBooked => "Fitting Booked",
                ContractStatus.FittingCompleted => "Fitting Completed",
                ContractStatus.Invoiced => "Invoiced",




                _ => status.ToString()
            };
        }

        public static string GetDescription(ContractStatus status)
        {
            return status switch
            {
                ContractStatus.Reported =>
                    "The claim has been received and added to the system.",

                ContractStatus.SurveyBooked =>
                    "A survey appointment has been booked.",

                ContractStatus.SurveyCompleted =>
                    "The survey has been completed.",

                ContractStatus.EstimateCompleted =>
                    "The estimate has been completed.",

                ContractStatus.Authorised =>
                    "The repair has been authorised.",

                ContractStatus.FittingBooked =>
                    "A fitting appointment has been booked.",

                ContractStatus.FittingCompleted =>
                    "The fitting has been completed.",

                ContractStatus.Invoiced =>
                    "An invoice has been issued for the completed work.",


                _ =>
                    "The contract status has been updated."
            };
        }

        public static bool IsCustomerVisible(ContractStatus status)
        {
            return status switch
            {
                ContractStatus.Reported => true,
                ContractStatus.SurveyBooked => true,
                ContractStatus.SurveyCompleted => true,
                ContractStatus.FittingBooked => true,
                ContractStatus.FittingCompleted => true,
                ContractStatus.Invoiced => true,


                _ => false
            };
        }
    }

    public static class ContractUpdateHelper
    {
        public static ContractUpdate Create(
            Contract contract,
            ContractStatus status)
        {
            // Keep the current Contract status in sync
            contract.Status = status;

            return new ContractUpdate
            {
                ContractId = contract.Id,
                TenantId = contract.TenantId,
                DateTime = DateTime.Now,
                Status = status,
                Title = ContractStatusHelper.GetTitle(status),
                Description = ContractStatusHelper.GetDescription(status),
                CustomerVisible = ContractStatusHelper.IsCustomerVisible(status)
            };
        }
    }
}