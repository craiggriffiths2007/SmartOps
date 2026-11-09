using SmartOps.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web.Services;

public static class BookingAvailability
{
    public static readonly TimeSpan TravelTime = TimeSpan.FromHours(1);
    public static readonly TimeSpan DayStart = TimeSpan.FromHours(7);
    public static readonly TimeSpan DayEnd = TimeSpan.FromHours(19);
    public static readonly TimeSpan SlotStep = TimeSpan.FromMinutes(30);

    private sealed record Occupied(DateTime Start, DateTime End);
    public sealed record Slot(string Time, string End);
    public sealed record AvailableDay(string Date, string Label, List<Slot> Slots);

    private static async Task<List<Occupied>> OccupiedAsync(AppDBContext db, int agentId,
        DateTime from, DateTime through, int? excludeJobId, Guid? reservationId, string? userId)
    {
        // SQL time columns cannot hold durations of 24 hours or more. Include
        // adjacent diary dates for overnight work and travel across midnight.
        var lower = from.Date.AddDays(-1);
        var upper = through.Date.AddDays(2);
        var bookings = await db.Job.AsNoTracking()
            .Where(j => j.AgentId == agentId && (!excludeJobId.HasValue || j.Id != excludeJobId.Value)
                && j.DiaryDate >= lower && j.DiaryDate < upper)
            .Select(j => new { j.DiaryDate, j.Time, j.JobTime }).ToListAsync();
        var occupied = bookings.Select(j => {
            var start = j.DiaryDate.Date.Add(j.Time.TimeOfDay);
            return new Occupied(start, start.Add(j.JobTime));
        }).ToList();
        var now = DateTime.UtcNow;
        var reservations = await db.BookingReservations.AsNoTracking()
            .Where(r => r.AgentId == agentId && r.ExpiresUtc > now && r.Start < upper && r.End >= lower
                && (!reservationId.HasValue || r.Id != reservationId.Value || r.UserId != userId))
            .Select(r => new { r.Start, r.End }).ToListAsync();
        occupied.AddRange(reservations.Select(r => new Occupied(r.Start, r.End)));
        return occupied;
    }

    private static bool Fits(DateTime start, DateTime end, IEnumerable<Occupied> bookings) =>
        !bookings.Any(j => start < j.End.Add(TravelTime) && end.Add(TravelTime) > j.Start);

    public static async Task<string?> ValidateAsync(AppDBContext db, int agentId, DateTime date,
        DateTime time, TimeSpan duration, int? excludeJobId = null, Guid? reservationId = null, string? userId = null)
    {
        if (duration <= TimeSpan.Zero || duration >= TimeSpan.FromDays(1))
            return "Enter a booking duration greater than zero and less than 24 hours.";
        if (date.Year < 2 || date.Year > 9997)
            return "Choose a valid booking date.";
        if (!await db.Agent.AnyAsync(a => a.Id == agentId))
            return "Choose an agent from your company.";
        var start = date.Date.Add(time.TimeOfDay);
        if (start < DateTime.Now)
            return "Choose a booking date and time from now onwards.";
        var end = start.Add(duration);
        var bookings = await OccupiedAsync(db, agentId, start, end, excludeJobId, reservationId, userId);
        return Fits(start, end, bookings) ? null
            : "This agent already has a booking at that time or needs an hour for travel. Choose another available slot.";
    }

    public static async Task<List<AvailableDay>> GetDaysAsync(AppDBContext db, int agentId,
        DateTime date, TimeSpan duration, int? excludeJobId = null, Guid? reservationId = null, string? userId = null)
    {
        var first = date.Date < DateTime.Today ? DateTime.Today : date.Date;
        var bookings = await OccupiedAsync(db, agentId, first, first.AddDays(6), excludeJobId, reservationId, userId);
        var now = DateTime.Now;
        var days = new List<AvailableDay>();
        for (int offset = 0; offset < 7; offset++)
        {
            var day = first.AddDays(offset);
            var slots = new List<Slot>();
            for (var start = day.Add(DayStart); start.Add(duration) <= day.Add(DayEnd); start = start.Add(SlotStep))
            {
                var end = start.Add(duration);
                if (start >= now && Fits(start, end, bookings))
                    slots.Add(new Slot(start.ToString("HH:mm"), end.ToString("HH:mm")));
            }
            days.Add(new AvailableDay(day.ToString("yyyy-MM-dd"),
                day.ToString("dddd d MMMM", System.Globalization.CultureInfo.GetCultureInfo("en-GB")), slots));
        }
        return days;
    }
}
