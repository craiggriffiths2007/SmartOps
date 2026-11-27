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
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SmartOps.Web.Helpers.StaffRoles.ManageBranch)]
    public class MileageSheetsController : Controller
    {
        private readonly AppDBContext _context;

        public MileageSheetsController(AppDBContext context)
        {
            _context = context;
        }

        // GET: MileageSheets
        public async Task<IActionResult> Index(int page = 1)
        {
            if (_context.MileageSheets == null)
            {
                return Problem("Entity set 'PropertyRepairsContext.MileageSheets' is null.");
            }

            var items = await _context.MileageSheets
                .OrderByDescending(m => m.DateTimeCompleted).ThenByDescending(m => m.Id)
                .ToRecordPageAsync(page, ViewData, pageSize: 20);

            return View(items);
        }

        // GET: MileageSheets/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var mileageSheet = await _context.MileageSheets
                .FirstOrDefaultAsync(m => m.Id == id);
            if (mileageSheet == null)
            {
                return NotFound();
            }

            ViewData["StartImage"] = _context.Images.Where(x => x.Filename == mileageSheet.photo_am).FirstOrDefault()?.Filename;
            ViewData["EndImage"] = _context.Images.Where(x => x.Filename == mileageSheet.photo_pm).FirstOrDefault()?.Filename;
            ViewData["SignatureImage"] = _context.Images.Where(x => x.Filename == mileageSheet.signature_filename).FirstOrDefault()?.Filename;

            return View(mileageSheet);
        }

        // GET: MileageSheets/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: MileageSheets/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,sheet_date,start_postcode,finish_postcode,start_mileage,end_mileage,start_time,end_time,no_of_other_places,time1,pcode1,time2,pcode2,time3,pcode3,registration,bSigned,signature_filename,bComplete,bSent,comments,OtehrPlaceNo,op_time1,op_postcode1,op_time2,op_postcode2,op_time3,op_postcode3,op_time4,op_postcode4,op_time5,op_postcode5,op_time6,op_postcode6,op_time7,op_postcode7,op_time8,op_postcode8,op_time9,op_postcode9,op_time10,op_postcode10,op_time11,op_postcode11,op_time12,op_postcode12,op_time13,op_postcode13,op_time14,op_postcode14,op_time15,op_postcode15,toll_charges,toll_charge_for,toll_charge_ammount,photo_am,photo_pm")] MileageSheet mileageSheet)
        {
            if (ModelState.IsValid)
            {
                _context.Add(mileageSheet);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(mileageSheet);
        }

        // GET: MileageSheets/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var mileageSheet = await _context.MileageSheets.FindAsync(id);
            if (mileageSheet == null)
            {
                return NotFound();
            }
            return View(mileageSheet);
        }

        // POST: MileageSheets/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,sheet_date,start_postcode,finish_postcode,start_mileage,end_mileage,start_time,end_time,no_of_other_places,time1,pcode1,time2,pcode2,time3,pcode3,registration,bSigned,signature_filename,bComplete,bSent,comments,OtehrPlaceNo,op_time1,op_postcode1,op_time2,op_postcode2,op_time3,op_postcode3,op_time4,op_postcode4,op_time5,op_postcode5,op_time6,op_postcode6,op_time7,op_postcode7,op_time8,op_postcode8,op_time9,op_postcode9,op_time10,op_postcode10,op_time11,op_postcode11,op_time12,op_postcode12,op_time13,op_postcode13,op_time14,op_postcode14,op_time15,op_postcode15,toll_charges,toll_charge_for,toll_charge_ammount,photo_am,photo_pm")] MileageSheet mileageSheet)
        {
            if (id != mileageSheet.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(mileageSheet);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MileageSheetExists(mileageSheet.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(mileageSheet);
        }

        // GET: MileageSheets/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var mileageSheet = await _context.MileageSheets
                .FirstOrDefaultAsync(m => m.Id == id);
            if (mileageSheet == null)
            {
                return NotFound();
            }

            return View(mileageSheet);
        }

        // POST: MileageSheets/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var mileageSheet = await _context.MileageSheets.FindAsync(id);
            if (mileageSheet != null)
            {
                _context.MileageSheets.Remove(mileageSheet);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MileageSheetExists(int id)
        {
            return _context.MileageSheets.Any(e => e.Id == id);
        }
    }
}
