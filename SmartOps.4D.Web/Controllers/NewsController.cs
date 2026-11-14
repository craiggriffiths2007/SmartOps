using SmartOps.Web.Helpers;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web.Controllers
{
    [AllowAnonymous]
    public class NewsController : Controller
    {
        private readonly AppDBContext _context;
        private readonly ILogger<NewsController> _logger;

        public NewsController(
            AppDBContext context,
            ILogger<NewsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private async Task RecordPageHit(string page)
        {
            try
            {
                _context.PageHits.Add(new PageHit
                {
                    Page = page,
                    HitDate = DateTime.UtcNow,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Referrer = Request.Headers.Referer.ToString(),
                    UserAgent = Request.Headers.UserAgent.ToString()
                });

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record page hit for {Page}", page);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1)
        {
            await RecordPageHit("News");

            var articles = await _context.NewsArticles
                .AsNoTracking()
                .Where(x => x.IsPublished && x.PublishedDate <= DateTime.UtcNow)
                .OrderByDescending(x => x.PublishedDate)
                .ThenByDescending(x => x.Id)
                .ToRecordPageAsync(page, ViewData);

            return View(articles);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var article = await _context.NewsArticles
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.IsPublished &&
                    x.PublishedDate <= DateTime.UtcNow);

            if (article == null)
                return NotFound();

            await RecordPageHit($"News: {article.Title}");

            return View(article);
        }
    }
}
