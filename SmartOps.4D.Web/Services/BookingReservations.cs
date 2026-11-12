using SmartOps.Web.Data;
using SmartOps.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web.Services;

public static class BookingReservations
{
    public static readonly TimeSpan HoldTime = TimeSpan.FromMinutes(10);

    public static Task<BookingReservation?> FindOwnedAsync(AppDBContext db, Guid? id, string? userId) =>
        db.BookingReservations.SingleOrDefaultAsync(r => r.Id == id && r.UserId == userId);

    public static async Task<string?> CheckForSaveAsync(AppDBContext db, Guid? id, string? userId, Job job)
    {
        if (!id.HasValue) return null; // Manually entered bookings still get the full conflict check.
        var reservation = await FindOwnedAsync(db, id, userId);
        if (reservation == null || reservation.ExpiresUtc <= DateTime.UtcNow)
            return "Your reservation has expired or been released. Please select an available time again.";
        var start = job.DiaryDate.Date.Add(job.Time.TimeOfDay);
        if (reservation.AgentId != job.AgentId || reservation.Start != start || reservation.End != start.Add(job.JobTime))
            return "The booking details have changed. Please reserve an available time for these details.";
        return null;
    }

    public static object Describe(BookingReservation reservation) => new {
        id = reservation.Id, agentId = reservation.AgentId,
        date = reservation.Start.ToString("yyyy-MM-dd"), time = reservation.Start.ToString("HH:mm"),
        end = reservation.End.ToString("HH:mm"),
        duration = (reservation.End - reservation.Start).ToString(@"hh\:mm"),
        expiresUtc = DateTime.SpecifyKind(reservation.ExpiresUtc, DateTimeKind.Utc)
    };
}
