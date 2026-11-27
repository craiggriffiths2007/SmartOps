using SmartOps.Web.Helpers;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using SmartOps.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web.Controllers
{
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SmartOps.Web.Helpers.StaffRoles.ManageDiary)]
    public class AgentMessagesController : Controller
    {
        private readonly AppDBContext _context;

        public AgentMessagesController(AppDBContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            var messages = await _context.AgentMessages
                .Include(x => x.Recipients)
                    .ThenInclude(x => x.Agent)
                .OrderByDescending(x => x.Created)
                .ThenByDescending(x => x.Id)
                .ToRecordPageAsync(page, ViewData);

            return View(messages);
        }

        public async Task<IActionResult> Create()
        {
            return View(new AgentMessageCreateViewModel
            {
                Agents = await GetAgentsAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AgentMessageCreateViewModel model)
        {
            var validAgentIds = await ValidateMessageAsync(model);

            if (!ModelState.IsValid)
            {
                model.Agents = await GetAgentsAsync();
                return View(model);
            }

            var message = new AgentMessage
            {
                Subject = model.Subject.Trim(),
                BodyHtml = model.BodyHtml,
                Created = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "",
                Recipients = validAgentIds.Select(agentId => new AgentMessageRecipient
                {
                    AgentId = agentId,
                    IsRead = false
                }).ToList()
            };

            _context.AgentMessages.Add(message);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = message.Id });
        }

        public async Task<IActionResult> Details(int id)
        {
            var message = await GetMessageAsync(id);
            return message == null ? NotFound() : View(message);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var message = await GetMessageAsync(id);
            if (message == null)
                return NotFound();

            return View(new AgentMessageCreateViewModel
            {
                Subject = message.Subject,
                BodyHtml = message.BodyHtml,
                SelectedAgentIds = message.Recipients.Select(x => x.AgentId).ToList(),
                Agents = await GetAgentsAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AgentMessageCreateViewModel model)
        {
            var message = await _context.AgentMessages
                .Include(x => x.Recipients)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (message == null)
                return NotFound();

            var validAgentIds = await ValidateMessageAsync(model);

            if (!ModelState.IsValid)
            {
                model.Agents = await GetAgentsAsync();
                return View(model);
            }

            message.Subject = model.Subject.Trim();
            message.BodyHtml = model.BodyHtml;

            var selectedIds = validAgentIds.ToHashSet();

            // Preserve the read state for recipients who remain selected.
            var removedRecipients = message.Recipients
                .Where(x => !selectedIds.Contains(x.AgentId))
                .ToList();

            if (removedRecipients.Count > 0)
                _context.AgentMessageRecipients.RemoveRange(removedRecipients);

            var existingIds = message.Recipients
                .Select(x => x.AgentId)
                .ToHashSet();

            foreach (var agentId in selectedIds.Where(x => !existingIds.Contains(x)))
            {
                message.Recipients.Add(new AgentMessageRecipient
                {
                    AgentId = agentId,
                    IsRead = false,
                    ReadDateTime = null
                });
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = message.Id });
        }

        public async Task<IActionResult> Delete(int id)
        {
            var message = await GetMessageAsync(id);
            return message == null ? NotFound() : View(message);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var message = await _context.AgentMessages
                .FirstOrDefaultAsync(x => x.Id == id);

            if (message == null)
                return NotFound();

            _context.AgentMessages.Remove(message);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private async Task<List<int>> ValidateMessageAsync(AgentMessageCreateViewModel model)
        {
            model.SelectedAgentIds = model.SelectedAgentIds.Distinct().ToList();

            if (model.SelectedAgentIds.Count == 0)
                ModelState.AddModelError(nameof(model.SelectedAgentIds), "Select at least one agent.");

            if (string.IsNullOrWhiteSpace(model.BodyHtml) ||
                model.BodyHtml.Replace("<p><br></p>", "", StringComparison.OrdinalIgnoreCase).Trim().Length == 0)
                ModelState.AddModelError(nameof(model.BodyHtml), "Enter a message.");

            var validAgentIds = await _context.Agent
                .Where(x => model.SelectedAgentIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();

            if (validAgentIds.Count != model.SelectedAgentIds.Count)
                ModelState.AddModelError(nameof(model.SelectedAgentIds), "One or more selected agents are invalid.");

            return validAgentIds;
        }

        private Task<AgentMessage?> GetMessageAsync(int id) =>
            _context.AgentMessages
                .Include(x => x.Recipients)
                    .ThenInclude(x => x.Agent)
                .FirstOrDefaultAsync(x => x.Id == id);

        private Task<List<Agent>> GetAgentsAsync() =>
            _context.Agent.OrderBy(x => x.Name).ThenBy(x => x.Code).ToListAsync();
    }
}
