using SmartOps.Web.Helpers;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class AdminNewsController : Controller
    {
        private readonly AppDBContext _context;

        public AdminNewsController(AppDBContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1)
        {
            var articles = await _context.NewsArticles
                .AsNoTracking()
                .OrderByDescending(x => x.PublishedDate)
                .ThenByDescending(x => x.Id)
                .ToRecordPageAsync(page, ViewData);

            return View(articles);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new NewsArticle
            {
                PublishedDate = DateTime.UtcNow,
                IsPublished = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(NewsArticle article)
        {
            article.Title = article.Title?.Trim() ?? "";
            article.Summary = article.Summary?.Trim() ?? "";
            article.BodyHtml = article.BodyHtml?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(article.BodyHtml) ||
                article.BodyHtml == "<p><br></p>")
            {
                ModelState.AddModelError(nameof(article.BodyHtml), "Article body is required.");
            }

            if (!ModelState.IsValid)
                return View(article);

            article.Id = 0;
            article.CreatedAt = DateTime.UtcNow;
            article.UpdatedAt = DateTime.UtcNow;

            _context.NewsArticles.Add(article);
            await _context.SaveChangesAsync();

            TempData["NewsMessage"] = "News article created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var article = await _context.NewsArticles.FindAsync(id);
            if (article == null)
                return NotFound();

            return View(article);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, NewsArticle article)
        {
            if (id != article.Id)
                return NotFound();

            article.Title = article.Title?.Trim() ?? "";
            article.Summary = article.Summary?.Trim() ?? "";
            article.BodyHtml = article.BodyHtml?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(article.BodyHtml) ||
                article.BodyHtml == "<p><br></p>")
            {
                ModelState.AddModelError(nameof(article.BodyHtml), "Article body is required.");
            }

            if (!ModelState.IsValid)
                return View(article);

            var existing = await _context.NewsArticles.FindAsync(id);
            if (existing == null)
                return NotFound();

            existing.Title = article.Title;
            existing.Summary = article.Summary;
            existing.BodyHtml = article.BodyHtml;
            existing.PublishedDate = article.PublishedDate;
            existing.IsPublished = article.IsPublished;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["NewsMessage"] = "News article updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var article = await _context.NewsArticles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (article == null)
                return NotFound();

            return View(article);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var article = await _context.NewsArticles.FindAsync(id);
            if (article == null)
                return NotFound();

            _context.NewsArticles.Remove(article);
            await _context.SaveChangesAsync();

            TempData["NewsMessage"] = "News article deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
