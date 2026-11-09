using SmartOps.Web.Helpers;
using SmartOps.Web.Data;
using SmartOps.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace SmartOps.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class StatisticsController : Controller
    {
        private readonly AppDBContext _context;

        public StatisticsController(AppDBContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            DateTime today = DateTime.UtcNow.Date;
            DateTime last7Days = today.AddDays(-6);
            DateTime last30Days = today.AddDays(-29);

            string? currentIpAddress =
                HttpContext.Connection.RemoteIpAddress?.ToString();

            var pageHits = await _context.PageHits
                    .AsNoTracking()
                    .Where(x => x.IpAddress == null ||
                                !x.IpAddress.StartsWith("92.40.181"))
                    .ToListAsync();

            int surveyAppChecksToday = await _context.SurveyAppRequests
                .CountAsync(x => x.RequestDate >= today);

            int surveyAppChecksLast30Days = await _context.SurveyAppRequests
                .CountAsync(x => x.RequestDate >= last30Days);

            var surveyAppRequests = await _context.SurveyAppRequests
                .AsNoTracking()
                .OrderByDescending(x => x.RequestDate)
                .Take(100)
                .ToListAsync();

            var model = new StatisticsViewModel
            {
                TotalHits = pageHits.Count,
                TodayHits = pageHits.Count(x => x.HitDate >= today),
                Last7DaysHits = pageHits.Count(x => x.HitDate >= last7Days),
                Last30DaysHits = pageHits.Count(x => x.HitDate >= last30Days),
                UniqueIpAddresses = pageHits
                    .Where(x => !string.IsNullOrWhiteSpace(x.IpAddress))
                    .Select(x => x.IpAddress)
                    .Distinct()
                    .Count(),
                CurrentIpAddress = currentIpAddress,
                SurveyAppChecksToday = surveyAppChecksToday,
                SurveyAppChecksLast30Days = surveyAppChecksLast30Days,
                SurveyAppLatestBuild =
                await _context.SystemSettings
                    .Where(x => x.Key == "SurveyAppLatestBuild")
                    .Select(x => x.Value)
                    .FirstOrDefaultAsync()
                ?? ""
            };

            model.Pages = pageHits
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Page) ? "Unknown" : x.Page)
                .Select(g => new PageStatisticsViewModel
                {
                    Page = g.Key,
                    Total = g.Count(),
                    Today = g.Count(x => x.HitDate >= today),
                    Last7Days = g.Count(x => x.HitDate >= last7Days),
                    Last30Days = g.Count(x => x.HitDate >= last30Days)
                })
                .OrderByDescending(x => x.Total)
                .ThenBy(x => x.Page)
                .ToList();

            model.DailyHits = Enumerable.Range(0, 30)
                .Select(offset => last30Days.AddDays(offset))
                .Select(date => new DailyHitViewModel
                {
                    Date = date,
                    Hits = pageHits.Count(x => x.HitDate.Date == date)
                })
                .ToList();

            model.RecentSurveyAppRequests = surveyAppRequests
                .Take(100)
                .Select(x => new RecentSurveyAppRequestViewModel
                {
                    RequestDate = x.RequestDate,
                    TenantCode = x.TenantCode,
                    AgentCode = x.AgentCode,
                    CurrentVersion = x.CurrentVersion,
                    CurrentBuild = x.CurrentBuild,
                    LatestBuild = x.LatestBuild,
                    UpdateAvailable = x.UpdateAvailable,
                    DeviceManufacturer = x.DeviceManufacturer,
                    DeviceModel = x.DeviceModel,
                    DeviceName = x.DeviceName,
                    Platform = x.Platform,
                    OsVersion = x.OsVersion,
                    IpAddress = x.IpAddress
                })
                .ToList();

            model.RecentHits = pageHits
                .OrderByDescending(x => x.HitDate)
                .Take(100)
                .Select(x => new RecentPageHitViewModel
                {
                    Id = x.Id,
                    Page = x.Page,
                    HitDate = x.HitDate,
                    IpAddress = x.IpAddress,
                    Referrer = x.Referrer,
                    UserAgent = x.UserAgent,
                    IsCurrentIp = !string.IsNullOrWhiteSpace(currentIpAddress) &&
                                  string.Equals(x.IpAddress, currentIpAddress, StringComparison.OrdinalIgnoreCase)
                })
                .ToList();

            VisitorAgentParser.PopulateStatistics(model, pageHits);

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSurveyAppBuild([Range(0, int.MaxValue)] int build)
        {
            if (!ModelState.IsValid)
            {
                TempData["StatisticsError"] = "Enter a whole build number between 0 and 2147483647.";
                return RedirectToAction("Index", new { tab = "app" });
            }
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(
                    x => x.Key == "SurveyAppLatestBuild");

            if (setting == null)
            {
                setting = new SystemSetting
                {
                    Key = "SurveyAppLatestBuild",
                    Value = build.ToString(CultureInfo.InvariantCulture)
                };

                _context.SystemSettings.Add(setting);
            }
            else
            {
                setting.Value = build.ToString(CultureInfo.InvariantCulture);
            }

            await _context.SaveChangesAsync();
            TempData["StatisticsSuccess"] = "Survey app release build updated.";

            return RedirectToAction("Index", new { tab = "app" });
        }

    }
}
