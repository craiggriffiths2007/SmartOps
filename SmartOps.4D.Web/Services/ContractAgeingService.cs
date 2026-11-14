using SmartOps.Web.Data;
using SmartOps.Web.Models;
using SmartOps.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web.Services;

/// <summary>
/// Read-only contract-health calculator. Uses dates recorded by the authoritative
/// 4D ContractUpdates table; never writes calculated colours to 4D.
/// </summary>
public static class ContractAgeingService
{
    public sealed record Limits(int Amber, int Red);
    public sealed record Health(enum_tlight? Light, int? Days, DateTime? StatusSince, DateTime? CreatedAt, string Reason)
    {
        public string DisplayColor => Light switch
        {
            enum_tlight.Red => "#dc3545",
            enum_tlight.Orange => "#fd7e14",
            enum_tlight.Green => "#198754",
            _ => "#929292"
        };
    }

    // Defaults matching GlassOps. Admin-editable 4D settings require a 4D
    // schema/API change and are deliberately not persisted to the transient bridge.
    private static readonly Dictionary<ContractStatus, Limits> Defaults = new()
    {
        [ContractStatus.Reported] = new(2, 4),
        [ContractStatus.SurveyBooked] = new(2, 5),
        [ContractStatus.SurveyCompleted] = new(1, 3),
        [ContractStatus.EstimateCompleted] = new(2, 5),
        [ContractStatus.Authorised] = new(1, 3),
        [ContractStatus.FittingBooked] = new(3, 7),
        [ContractStatus.FittingCompleted] = new(2, 5)
    };

    public static int WorkingDays(DateTime from, DateTime to)
    {
        var start = from.Date;
        var end = to.Date;
        if (end <= start) return 0;
        var days = 0;
        for (var date = start.AddDays(1); date <= end; date = date.AddDays(1))
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                days++;
        return days;
    }

    public static async Task<Dictionary<int, Health>> CalculateAsync(
        AppDBContext db, IReadOnlyCollection<Contract> contracts)
    {
        var result = new Dictionary<int, Health>();
        if (contracts.Count == 0) return result;
        var ids = contracts.Select(c => c.Id).ToArray();
        var history = await db.ContractUpdates.AsNoTracking()
            .Where(u => ids.Contains(u.ContractId) && u.Title != "Branch allocation changed")
            .Select(u => new { u.ContractId, u.Status, u.DateTime })
            .ToListAsync();
        var byContract = history.GroupBy(u => u.ContractId)
            .ToDictionary(g => g.Key, g => g.ToArray());
        var visits = await db.Job.AsNoTracking()
            .Where(j => ids.Contains(j.ContractId))
            .Select(j => new { j.ContractId, j.DiaryDate, j.JobType })
            .ToListAsync();
        var byVisits = visits.GroupBy(j => j.ContractId)
            .ToDictionary(g => g.Key, g => g.ToArray());
        var now = DateTime.Now;

        foreach (var contract in contracts)
        {
            var events = byContract.TryGetValue(contract.Id, out var saved) ? saved : [];
            var created = events.Length == 0 ? (DateTime?)null : events.Min(e => e.DateTime);
            var changed = events.Where(e => e.Status == contract.Status)
                .Select(e => (DateTime?)e.DateTime).Max();

            if (contract.IsCancelled || contract.Status == ContractStatus.Invoiced)
            {
                result[contract.Id] = new(null, null, changed, created,
                    contract.IsCancelled ? "Cancelled — no action required" : "Invoiced — no action required");
                continue;
            }

            if (!changed.HasValue || !Defaults.TryGetValue(contract.Status, out var limits))
            {
                result[contract.Id] = new(null, null, changed, created,
                    "Age unavailable — no dated update for the current workflow status");
                continue;
            }

            var days = WorkingDays(changed.Value, now);
            var action = contract.Status switch
            {
                ContractStatus.Reported => "Book survey",
                ContractStatus.SurveyBooked => "Complete survey",
                ContractStatus.SurveyCompleted => "Prepare estimate",
                ContractStatus.EstimateCompleted => "Obtain authorisation",
                ContractStatus.Authorised => "Book fitting",
                ContractStatus.FittingBooked => "Complete fitting",
                ContractStatus.FittingCompleted => "Create invoice",
                _ => "Review contract"
            };

            if (contract.Status is ContractStatus.SurveyBooked or ContractStatus.FittingBooked &&
                byVisits.TryGetValue(contract.Id, out var jobs))
            {
                var expectedType = contract.Status == ContractStatus.SurveyBooked
                    ? enum_job_type.Survey : enum_job_type.Fitting;
                var relevant = jobs.Where(j => j.JobType == expectedType &&
                    j.DiaryDate >= changed.Value.Date).OrderBy(j => j.DiaryDate).ToArray();
                if (relevant.Length > 0)
                {
                    var upcoming = relevant.FirstOrDefault(j => j.DiaryDate.Date >= now.Date);
                    var date = (upcoming ?? relevant[^1]).DiaryDate.Date;
                    action = "Appointment " + date.ToString("dd/MM/yyyy");
                    days = date >= now.Date ? 0 : WorkingDays(date, now);
                }
            }

            var colour = days >= limits.Red ? enum_tlight.Red
                : days >= limits.Amber ? enum_tlight.Orange
                : enum_tlight.Green;
            result[contract.Id] = new(colour, days, changed, created,
                $"{action} — {days} working day(s): " +
                (colour == enum_tlight.Red ? "Overdue" :
                 colour == enum_tlight.Orange ? "Attention required" : "Within target"));
        }
        return result;
    }
}
