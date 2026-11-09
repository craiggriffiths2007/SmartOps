
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using SmartOps.Web.Helpers;

namespace SmartOps.Web.Controllers
{
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SmartOps.Web.Helpers.StaffRoles.ManageDiary)]
    public class CustomersController : Controller
    {
        private readonly AppDBContext _context;

        public CustomersController(AppDBContext context)
        {
            _context = context;
        }

        // GET: Customers
        public async Task<IActionResult> Index(
            string? search,
            string active = "active",
            int page = 1)
        {
            const int pageSize = 10;

            var query = _context.Customer.AsNoTracking().AsQueryable();

            if (active == "active")
            {
                query = query.Where(c => c.IsActive);
            }
            else if (active == "inactive")
            {
                query = query.Where(c => !c.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(c =>
                    c.Name.Contains(search) ||
                    c.Postcode.Contains(search) ||
                    c.Email.Contains(search) ||
                    c.Phone1.Contains(search) ||
                    (c.Phone2 != null &&
                     c.Phone2.Contains(search)) ||
                    (c.Phone3 != null &&
                     c.Phone3.Contains(search)));
            }

            var totalCount = await query.CountAsync();

            var totalPages = (int)Math.Ceiling(
                totalCount / (double)pageSize);

            page = Math.Clamp(page, 1, Math.Max(1, totalPages));

            var customers = await query
                .OrderBy(c => c.Name)
                .ThenBy(c => c.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var customerIds = customers.Select(c => c.Id).ToArray();
            ViewData["ActiveContracts"] = await ActiveContracts()
                .Where(c => customerIds.Contains(c.CustomerId))
                .GroupBy(c => c.CustomerId)
                .Select(group => group.OrderByDescending(c => c.ContractNumber)
                    .ThenByDescending(c => c.Id).First())
                .ToDictionaryAsync(c => c.CustomerId);

            ViewBag.PageNumber = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalCount;
            ViewBag.Search = search;
            ViewBag.Active = active;

            return View(customers);
        }

        // GET: Customers/Details/5
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Details(int? id, string? returnUrl = null, int? contractId = null)
        {
            if (id == null)
                return NotFound();

            var customer = await _context.Customer
                .FirstOrDefaultAsync(c => c.Id == id);

            if (customer == null)
                return NotFound();

            ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

            var activeContract = await ActiveContracts()
                .Where(c => c.CustomerId == customer.Id)
                .Where(c => contractId == null || c.Id == contractId)
                .OrderByDescending(c => c.ContractNumber)
                .ThenByDescending(c => c.Id)
                .FirstOrDefaultAsync();

            if (activeContract != null)
            {
                activeContract.Customer = customer;
                ViewData["ActiveContract"] = activeContract;
                ViewData["ReturnToCustomerDetails"] = true;
            }

            return View(customer);
        }

        // GET: Customers/Create
        public IActionResult Create(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
            return View(new Customer());
        }

        // POST: Customers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            string? returnUrl,
            [Bind("Name,Add1,Add2,Add3,Postcode,Phone1,Phone2,Phone3,Email,IsActive")]
            Customer customer)
        {
            // Login credentials are assigned when
            // the customer's contract is created.
            customer.IssuedPassword = "";
            customer.PasswordHash = "";
            customer.AuthenticationString = "";

            if (!string.IsNullOrWhiteSpace(customer.Email))
            {
                customer.Email =
                    customer.Email.Trim().ToLowerInvariant();
            }

            if (!ModelState.IsValid)
            {
                ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
                return View(customer);
            }

            _context.Customer.Add(customer);

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(Details),
                new {
                    id = customer.Id,
                    returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
                });
        }

        // GET: Customers/Edit/5
        public async Task<IActionResult> Edit(int? id, string? returnUrl = null)
        {
            if (id == null)
                return NotFound();

            var customer = await _context.Customer
                .FirstOrDefaultAsync(c => c.Id == id);

            if (customer == null)
                return NotFound();

            ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            string? returnUrl,
            [Bind("Id,Name,Add1,Add2,Add3,Postcode,Phone1,Phone2,Phone3,Email,IsActive")]
            Customer customer)
        {
            if (id != customer.Id)
                return NotFound();

            if (!ModelState.IsValid)
            {
            
                ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
                return View(customer);
            }

            var existingCustomer = await _context.Customer
                .FirstOrDefaultAsync(c => c.Id == id);

            if (existingCustomer == null)
                return NotFound();

            existingCustomer.Name = customer.Name;
            existingCustomer.Add1 = customer.Add1;
            existingCustomer.Add2 = customer.Add2;
            existingCustomer.Add3 = customer.Add3;
            existingCustomer.Postcode = customer.Postcode;

            existingCustomer.Phone1 = customer.Phone1;
            existingCustomer.Phone2 = customer.Phone2;
            existingCustomer.Phone3 = customer.Phone3;

            existingCustomer.Email =
                (customer.Email ?? "")
                .Trim()
                .ToLowerInvariant();

            existingCustomer.IsActive = customer.IsActive;

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(Details),
                new {
                    id = customer.Id,
                    returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
                });
        }

        // GET: Customers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var customer = await _context.Customer
                .FirstOrDefaultAsync(c => c.Id == id);

            if (customer == null)
                return NotFound();

            return View(customer);
        }

        // POST: Customers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer = await _context.Customer
                .FirstOrDefaultAsync(c => c.Id == id);

            if (customer != null)
            {
                _context.Customer.Remove(customer);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private IQueryable<Contract> ActiveContracts() =>
            _context.Contract.AsNoTracking().Where(c => c.IsActive && !c.IsCancelled);

        private bool CustomerExists(int id)
        {
            return _context.Customer.Any(c => c.Id == id);
        }
    }
}
