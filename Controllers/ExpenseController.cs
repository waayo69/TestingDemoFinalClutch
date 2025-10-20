using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TestingDemo.Data;
using TestingDemo.Models;
using System.Collections.Generic;
using System.Text.Json;

namespace TestingDemo.Controllers
{
    [Authorize(Roles = "Admin,Finance")]
    public class ExpenseController : Controller
    {
        private readonly ApplicationDbContext _context;
        public ExpenseController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Expense
        public async Task<IActionResult> Index(string month, string status)
        {
            var expenses = _context.Expenses.Where(e => !e.IsDeleted).AsQueryable();
            if (!string.IsNullOrEmpty(month))
            {
                if (DateTime.TryParse(month + "-01", out var monthDate))
                {
                    expenses = expenses.Where(e => e.DueDate.Month == monthDate.Month && e.DueDate.Year == monthDate.Year);
                }
            }
            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                expenses = expenses.Where(e => e.Status == status);
            }

            var list = await expenses.OrderBy(e => e.DueDate).ToListAsync();

            // Only show the original instance of recurring expenses (do not show future repeats)
            var uniqueList = list
                .Where(e => !e.Recurring || (e.Recurring && (e.RepeatMonths == null || e.RepeatMonths <= 1) || (e.Recurring && e.DueDate == list.Where(x => x.Id == e.Id).Min(x => x.DueDate))))
                .GroupBy(e => new
                {
                    Name = e.Name?.Trim().ToLower(),
                    e.Amount,
                    DueDate = e.DueDate.Date, // Only the date part
                    Category = e.Category?.Trim().ToLower(),
                    Location = e.Location?.Trim().ToLower()
                })
                .Select(g => g.First())
                .ToList();

            ViewBag.SelectedMonth = month;
            ViewBag.SelectedStatus = status;
            return View(uniqueList);
        }

        // GET: Expense/Calendar
        public async Task<IActionResult> Calendar(int? year, int? month)
        {
            var now = DateTime.Now;
            int y = year ?? now.Year;
            int m = month ?? now.Month;
            var expenses = await _context.Expenses.ToListAsync();

            var calendarExpenses = new List<ExpenseModel>();

            foreach (var expense in expenses)
            {
                if (expense.Recurring && expense.RepeatMonths.HasValue && expense.RepeatMonths.Value > 0)
                {
                    for (int i = 0; i < expense.RepeatMonths.Value; i++)
                    {
                        var repeatDate = expense.DueDate.AddMonths(i);
                        if (repeatDate.Year == y && repeatDate.Month == m)
                        {
                            // Determine status for this instance
                            string status = "Pending";
                            if (expense.PaidDate.HasValue && expense.PaidDate.Value.Year == repeatDate.Year && expense.PaidDate.Value.Month == repeatDate.Month)
                            {
                                status = "Paid";
                            }
                            else if (repeatDate < now.Date && expense.PaidDate.HasValue && expense.PaidDate.Value < repeatDate.AddMonths(1) && expense.PaidDate.Value >= repeatDate)
                            {
                                status = "Paid";
                            }
                            // else keep as Pending

                            calendarExpenses.Add(new ExpenseModel
                            {
                                Id = expense.Id,
                                Name = expense.Name,
                                Amount = expense.Amount,
                                DueDate = repeatDate,
                                Status = status,
                                Category = expense.Category,
                                Location = expense.Location,
                                PaidDate = (status == "Paid") ? expense.PaidDate : null,
                                Notes = expense.Notes,
                                CreatedDate = expense.CreatedDate,
                                Recurring = true,
                                RepeatMonths = expense.RepeatMonths
                            });
                        }
                    }
                }
                else if (expense.DueDate.Year == y && expense.DueDate.Month == m)
                {
                    calendarExpenses.Add(expense);
                }
            }

            ViewBag.Year = y;
            ViewBag.Month = m;
            return View(calendarExpenses);
        }

        // GET: Expense/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Expense/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ExpenseModel expense)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Create POST called. ModelState.IsValid: {ModelState.IsValid}");
            if (!ModelState.IsValid)
            {
                foreach (var key in ModelState.Keys)
                {
                    var errors = ModelState[key].Errors;
                    foreach (var error in errors)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] ModelState error for {key}: {error.ErrorMessage}");
                    }
                }
            }
            if (expense.Recurring)
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] Redirecting to RecurringExpenseController.Create");
                TempData["RecurringExpenseData"] = JsonSerializer.Serialize(expense);
                return RedirectToAction("Create", "RecurringExpense");
            }

            if (ModelState.IsValid)
            {
                // Check for soft-deleted expense with same Name, DueDate, Category, Location
                var existing = await _context.Expenses
                    .Where(e => e.IsDeleted
                        && e.Name == expense.Name
                        && e.DueDate.Date == expense.DueDate.Date
                        && e.Category == expense.Category
                        && e.Location == expense.Location)
                    .FirstOrDefaultAsync();
                if (existing != null)
                {
                    System.Diagnostics.Debug.WriteLine("[DEBUG] Restoring soft-deleted expense");
                    // Restore and update the old record
                    existing.IsDeleted = false;
                    existing.Status = expense.Status;
                    existing.Amount = expense.Amount;
                    existing.PaidDate = expense.PaidDate;
                    existing.Notes = expense.Notes;
                    existing.CreatedDate = DateTime.Now;
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Expense restored and updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                System.Diagnostics.Debug.WriteLine("[DEBUG] Creating new expense");
                // Otherwise, create new
                expense.CreatedDate = DateTime.Now;
                _context.Expenses.Add(expense);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Expense created successfully!";
                return RedirectToAction(nameof(Index));
            }
            System.Diagnostics.Debug.WriteLine("[DEBUG] ModelState invalid, returning view");
            return View(expense);
        }

        // GET: Expense/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var expense = await _context.Expenses.FindAsync(id);
            if (expense == null) return NotFound();
            return View(expense);
        }

        // POST: Expense/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ExpenseModel expense)
        {
            if (id != expense.Id) return NotFound();
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(expense);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Expense updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ExpenseExists(expense.Id))
                        return NotFound();
                    else
                        throw;
                }
            }
            return View(expense);
        }

        // GET: Expense/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var expense = await _context.Expenses.FindAsync(id);
            if (expense == null) return NotFound();
            return View(expense);
        }

        // POST: Expense/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var expense = await _context.Expenses.FindAsync(id);
            if (expense == null) return NotFound();
            _context.Expenses.Remove(expense);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Expense deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        // POST: Expense/MarkAsPaid
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            var expense = await _context.Expenses
                .Include(e => e.PaymentHistory)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (expense == null) return NotFound();
            if (expense.PaymentHistory == null)
                expense.PaymentHistory = new List<ExpensePaymentHistory>();
            expense.Status = "Paid";
            expense.PaidDate = DateTime.Now;
            expense.PaymentHistory.Add(new ExpensePaymentHistory { Date = DateTime.Now, Action = "Paid" });
            expense.IsDeleted = true; // Soft delete from main table
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Expense marked as paid.";
            TempData["UndoMarkAsPaidId"] = id;
            return RedirectToAction("Index");
        }

        // POST: Expense/UndoMarkAsPaid
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UndoMarkAsPaid(int id)
        {
            var expense = await _context.Expenses
                .Include(e => e.PaymentHistory)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (expense == null) return NotFound();
            if (expense.PaymentHistory == null)
                expense.PaymentHistory = new List<ExpensePaymentHistory>();
            expense.Status = "Pending";
            expense.PaidDate = null;
            expense.PaymentHistory.Add(new ExpensePaymentHistory { Date = DateTime.Now, Action = "Undone" });
            expense.IsDeleted = false; // Restore to main table
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Payment has been undone.";
            return RedirectToAction("Index");
        }

        // GET: Expense/AllPaymentHistory
        public async Task<IActionResult> AllPaymentHistory(string month)
        {
            DateTime? filterMonth = null;
            if (!string.IsNullOrEmpty(month) && DateTime.TryParse(month + "-01", out var parsedMonth))
                filterMonth = parsedMonth;

            // Normal paid expenses
            var normalPaid = await _context.Expenses
                .Where(e => e.Status == "Paid" && (!filterMonth.HasValue || (e.PaidDate.HasValue && e.PaidDate.Value.Year == filterMonth.Value.Year && e.PaidDate.Value.Month == filterMonth.Value.Month)))
                .Select(e => new GlobalPaymentHistoryVM
                {
                    Name = e.Name,
                    Amount = e.Amount,
                    PaidDate = e.PaidDate,
                    Category = e.Category,
                    Type = "Normal",
                    Notes = e.Notes
                })
                .ToListAsync();

            // Recurring paid payments
            var recurringPaid = await _context.ExpensePayments
                .Where(ep => ep.Status == "Paid" && (!filterMonth.HasValue || (ep.PaidDate.HasValue && ep.PaidDate.Value.Year == filterMonth.Value.Year && ep.PaidDate.Value.Month == filterMonth.Value.Month)))
                .Include(ep => ep.RecurringExpense)
                .Select(ep => new GlobalPaymentHistoryVM
                {
                    Name = ep.RecurringExpense.Name,
                    Amount = ep.AmountPaid ?? ep.RecurringExpense.Amount,
                    PaidDate = ep.PaidDate,
                    Category = ep.RecurringExpense.Category,
                    Type = "Recurring",
                    Notes = ep.Notes
                })
                .ToListAsync();

            var allPaid = normalPaid.Concat(recurringPaid).OrderByDescending(x => x.PaidDate).ToList();
            ViewBag.SelectedMonth = month;
            return View(allPaid);
        }

        public class GlobalPaymentHistoryVM
        {
            public string Name { get; set; }
            public decimal Amount { get; set; }
            public DateTime? PaidDate { get; set; }
            public string Category { get; set; }
            public string Type { get; set; } // Normal or Recurring
            public string Notes { get; set; }
        }

        private bool ExpenseExists(int id)
        {
            return _context.Expenses.Any(e => e.Id == id);
        }
    }
}