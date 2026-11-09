using SmartOps.Web.Helpers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using SmartOps.Web.ViewModels;

namespace SmartOps.Web.Controllers
{

    [Authorize(Roles = "SuperAdmin")]
    public class InsurersController : Controller
    {
        private readonly AppDBContext _context;
        private readonly UserManager<AppUser> _userManager;

        public InsurersController(
            AppDBContext context,
            UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: INSURERS
        public async Task<IActionResult> Index(int page = 1)
        {
            return View(await _context.Insurers.OrderBy(x => x.Name).ThenBy(x => x.Id).ToRecordPageAsync(page, ViewData));
        }

        // GET: INSURERS/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var insurer = await _context.Insurers
                .FirstOrDefaultAsync(i => i.Id == id);

            if (insurer == null)
                return NotFound();

            var model = new InsurerEditViewModel
            {
                Insurer = insurer,

                Users = await _userManager.Users
                    .Where(u => u.InsurerId == insurer.Id)
                    .OrderBy(u => u.Email)
                    .Select(u => new InsurerUserViewModel
                    {
                        Id = u.Id,
                        FirstName = u.FirstName ?? "",
                        LastName = u.LastName ?? "",
                        Email = u.Email ?? "",

                        IsDisabled = u.LockoutEnd.HasValue &&
                                     u.LockoutEnd.Value > DateTimeOffset.UtcNow
                    })
                    .ToListAsync()
            };

            return View(model);
        }

        // GET: INSURERS/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: INSURERS/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,ContactName,Email,Telephone,Address1,Address2,Address3,Postcode,IsActive")] Insurer insurer)
        {
            if (ModelState.IsValid)
            {
                _context.Add(insurer);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(insurer);
        }

        // GET: INSURERS/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var insurer = await _context.Insurers
                .FindAsync(id);

            if (insurer == null)
                return NotFound();

            var users = await _userManager.Users
                .Where(u => u.InsurerId == insurer.Id)
                .OrderBy(u => u.Email)
                .Select(u => new InsurerUserViewModel
                {
                    Id = u.Id,
                    FirstName = u.FirstName ?? "",
                    LastName = u.LastName ?? "",
                    Email = u.Email ?? ""
                })
                .ToListAsync();

            var model = new InsurerEditViewModel
            {
                Insurer = insurer,
                Users = users
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            InsurerEditViewModel model)
        {
            if (id != model.Insurer.Id)
                return NotFound();

            if (!ModelState.IsValid)
                return View(model);

            var insurer = await _context.Insurers.FindAsync(id);

            if (insurer == null)
                return NotFound();

            insurer.Name = model.Insurer.Name;
            insurer.ContactName = model.Insurer.ContactName;
            insurer.Email = model.Insurer.Email;
            insurer.Telephone = model.Insurer.Telephone;
            insurer.Address1 = model.Insurer.Address1;
            insurer.Address2 = model.Insurer.Address2;
            insurer.Address3 = model.Insurer.Address3;
            insurer.Postcode = model.Insurer.Postcode;
            insurer.IsActive = model.Insurer.IsActive;

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(Details),
                new { id = insurer.Id });
        }

        // GET: INSURERS/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var insurer = await _context.Insurers
                .FirstOrDefaultAsync(m => m.Id == id);
            if (insurer == null)
            {
                return NotFound();
            }

            return View(insurer);
        }

        // POST: INSURERS/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int? id)
        {
            var insurer = await _context.Insurers.FindAsync(id);
            if (insurer != null)
            {
                _context.Insurers.Remove(insurer);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool InsurerExists(int? id)
        {
            return _context.Insurers.Any(e => e.Id == id);
        }

        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null || user.InsurerId == null)
                return NotFound();

            var model = new InsurerUserViewModel
            {
                Id = user.Id,
                InsurerId = user.InsurerId,
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? "",
                Email = user.Email ?? "",
                IsDisabled = user.LockoutEnd.HasValue &&
                 user.LockoutEnd.Value > DateTimeOffset.UtcNow
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(
                                            string id,
                                            string newPassword)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null || user.InsurerId == null)
                return NotFound();

            var token =
                await _userManager.GeneratePasswordResetTokenAsync(user);

            var result =
                await _userManager.ResetPasswordAsync(
                    user,
                    token,
                    newPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        "",
                        error.Description);
                }

                return RedirectToAction(
                    nameof(EditUser),
                    new { id });
            }

            return RedirectToAction(
                nameof(Details),
                new { id = user.InsurerId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null || user.InsurerId == null)
                return NotFound();

            await _userManager.SetLockoutEnabledAsync(
                user,
                true);

            await _userManager.SetLockoutEndDateAsync(
                user,
                DateTimeOffset.MaxValue);

            return RedirectToAction(
                nameof(Details),
                new { id = user.InsurerId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null || user.InsurerId == null)
                return NotFound();

            await _userManager.SetLockoutEndDateAsync(
                user,
                null);

            return RedirectToAction(
                nameof(Details),
                new { id = user.InsurerId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(
      InsurerUserViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);

            if (user == null || user.InsurerId == null)
                return NotFound();

            if (!ModelState.IsValid)
                return View(model);

            // Update normal user details
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.Email = model.Email;
            user.UserName = model.Email;

            // Only change password when one was entered
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                var token =
                    await _userManager.GeneratePasswordResetTokenAsync(user);

                var passwordResult =
                    await _userManager.ResetPasswordAsync(
                        user,
                        token,
                        model.NewPassword);

                if (!passwordResult.Succeeded)
                {
                    foreach (var error in passwordResult.Errors)
                    {
                        ModelState.AddModelError(
                            nameof(model.NewPassword),
                            error.Description);
                    }

                    return View(model);
                }
            }

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        "",
                        error.Description);
                }

                return View(model);
            }

            return RedirectToAction(
                nameof(Details),
                new { id = user.InsurerId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLogin(
            int insurerId,
            string newFirstName,
            string newLastName,
            string newEmail,
            string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newFirstName) ||
                string.IsNullOrWhiteSpace(newLastName) ||
                string.IsNullOrWhiteSpace(newEmail) ||
                string.IsNullOrWhiteSpace(newPassword))
            {
                return RedirectToAction(
                    nameof(Edit),
                    new { id = insurerId });
            }

            var insurer = await _context.Insurers
                .FirstOrDefaultAsync(x => x.Id == insurerId);

            if (insurer == null)
                return NotFound();

            var user = new AppUser
            {
                UserName = newEmail.Trim(),
                Email = newEmail.Trim(),
                EmailConfirmed = true,

                FirstName = newFirstName.Trim(),
                LastName = newLastName.Trim(),

                TenantId = null,
                InsurerId = insurer.Id,

                IsApproved = true,
                ApprovedAt = DateTime.UtcNow
            };

            // 1. Create the user first
            var result = await _userManager.CreateAsync(
                user,
                newPassword);

            if (!result.Succeeded)
            {
                TempData["CreateUserError"] = string.Join(
                    "\n",
                    result.Errors.Select(e => e.Description));

                TempData["NewFirstName"] = newFirstName;
                TempData["NewLastName"] = newLastName;
                TempData["NewEmail"] = newEmail;

                return RedirectToAction(
                    nameof(Details),
                    new { id = insurerId });
            }

            // 2. Assign the Insurer role
            var roleResult = await _userManager.AddToRoleAsync(
                user,
                Roles.Insurer.ToString());

            if (!roleResult.Succeeded)
            {
                // Roll back the new user to avoid leaving an account
                // without its required role.
                var deleteResult = await _userManager.DeleteAsync(user);

                TempData["CreateUserError"] = string.Join(
                    "\n",
                    roleResult.Errors.Select(e => e.Description))
                    + (deleteResult.Succeeded
                        ? ""
                        : "\nThe user could not be rolled back. Please review the account.");

                return RedirectToAction(
                    nameof(Details),
                    new { id = insurerId });
            }

            return RedirectToAction(
                nameof(Details),
                new { id = insurerId });
        }

    }
}
