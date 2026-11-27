using SmartOps.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartOps.Web.Controllers
{
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SmartOps.Web.Helpers.StaffRoles.ManageDiary)]
    public class AgentsController : Controller
    {
        private readonly Data.AppDBContext _context;

        public AgentsController(Data.AppDBContext context)
        {
            _context = context;
        }

        private void PopulateAgentTypeDropDownList(object? selectedValue = null)
        {
            ViewBag.AgentTypes = new SelectList(
                Enum.GetValues<agent_type>()
                    .Select(a => new { Value = (int)a, Text = a.ToString() }),
                "Value", "Text", selectedValue);
        }

        // GET: Agents
        public async Task<IActionResult> Index(int page = 1, string? branchCode = null)
        {
            if (_context.Agent == null)
            {
                return Problem("Entity set 'PropertyRepairsContext.Agent'  is null.");
            }

            branchCode = UserBranchDefaults.Filter(_context, HttpContext, branchCode);
            var query = _context.Agent.AsNoTracking().Include(a => a.Branch).AsQueryable();
            await BranchIndexFilters.PopulateAsync(_context, ViewData, branchCode,
                query.Select(a => a.Branch == null ? null : a.Branch.Code));
            if (branchCode != null)
                query = query.Where(a => a.Branch != null && a.Branch.Code != null && a.Branch.Code.Trim() == branchCode);

            var agents = await query
                            .OrderBy(a => a.Type)
                            .ThenBy(a => a.Id)
                            .ToRecordPageAsync(page, ViewData);

            return View(agents);
        }

        // GET: Agents/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Agent == null)
            {
                return NotFound();
            }

            var agent = await _context.Agent
                .Include(a => a.Branch)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (agent == null)
            {
                return NotFound();
            }

            return View(agent);
        }

        // GET: Agents/Create
        public IActionResult Create()
        {
            ViewData["BranchId"] = new SelectList(
                  _context.Branches
                      .OrderBy(b => b.Name),
                  "Id",
                  "Name", UserBranchDefaults.Id(_context, User));

            PopulateAgentTypeDropDownList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
                            [Bind("Id,Code,Name,SerialNumber,PrivilegedAccess,Type,BranchId")] Agent agent)
        {
            if (!UserBranchDefaults.AllowsSelection(_context, User, agent.BranchId, UserBranchDefaults.Id(_context, User)))
                return BadRequest("Your branch selection is locked.");
            if (ModelState.IsValid)
            {
                var branch = await _context.Branches
                    .FirstOrDefaultAsync(b => b.Id == agent.BranchId);

                if (branch == null)
                {
                    ModelState.AddModelError(
                        nameof(agent.BranchId),
                        "Please select a valid branch.");
                }
                else
                {
                    _context.Add(agent);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }
            }

            ViewData["BranchId"] = new SelectList(
                _context.Branches.OrderBy(b => b.Name),
                "Id",
                "Name",
                agent.BranchId);

            PopulateAgentTypeDropDownList(agent.Type);

            return View(agent);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var agent = await _context.Agent
                .FirstOrDefaultAsync(a => a.Id == id);

            if (agent == null)
                return NotFound();

            ViewData["BranchId"] = new SelectList(
                _context.Branches.OrderBy(b => b.Name),
                "Id",
                "Name",
                agent.BranchId);

            PopulateAgentTypeDropDownList(agent.Type);

            return View(agent);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
                                        int id,
                                        [Bind("Id,Code,Name,Password,SerialNumber,PrivilegedAccess,Type,BranchId")] Agent agent)
        {
            if (id != agent.Id)
                return NotFound();
            var originalBranch = await _context.Agent.Where(a => a.Id == id).Select(a => a.BranchId).FirstOrDefaultAsync();
            if (!UserBranchDefaults.AllowsSelection(_context, User, agent.BranchId, originalBranch))
                return BadRequest("Your branch selection is locked.");

            if (ModelState.IsValid)
            {
                var branch = await _context.Branches
                    .FirstOrDefaultAsync(b => b.Id == agent.BranchId);

                if (branch == null)
                {
                    ModelState.AddModelError(
                        nameof(agent.BranchId),
                        "Please select a valid branch.");
                }
                else
                {
                    try
                    {
                        var existingAgent = await _context.Agent
                            .FirstOrDefaultAsync(a => a.Id == id);

                        if (existingAgent == null)
                            return NotFound();

                        existingAgent.Code = agent.Code;
                        existingAgent.Name = agent.Name;
                        existingAgent.SerialNumber = string.IsNullOrWhiteSpace(agent.SerialNumber)
                            ? string.Empty
                            : agent.SerialNumber.Trim();
                        existingAgent.PrivilegedAccess = agent.PrivilegedAccess;
                        existingAgent.Type = agent.Type;

                        existingAgent.BranchId = branch.Id;

                        await _context.SaveChangesAsync();
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        if (!AgentExists(agent.Id))
                            return NotFound();

                        throw;
                    }

                    return RedirectToAction(nameof(Index));
                }
            }

            ViewData["BranchId"] = new SelectList(
                _context.Branches.OrderBy(b => b.Name),
                "Id",
                "Name",
                agent.BranchId);

            PopulateAgentTypeDropDownList(agent.Type);

            return View(agent);
        }

        // GET: Agents/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Agent == null)
            {
                return NotFound();
            }

            var agent = await _context.Agent
                .FirstOrDefaultAsync(m => m.Id == id);
            if (agent == null)
            {
                return NotFound();
            }

            return View(agent);
        }

        // POST: Agents/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Agent == null)
            {
                return Problem("Entity set 'PropertyRepairsContext.Agent'  is null.");
            }
            var agent = await _context.Agent.FindAsync(id);
            if (agent != null)
            {
                _context.Agent.Remove(agent);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool AgentExists(int id)
        {
          return (_context.Agent?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
