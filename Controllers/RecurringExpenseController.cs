using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TestingDemo.Data;
using TestingDemo.Models;
using TestingDemo.Models.ViewModels;
using System.Text.Json;
using System.Collections.Generic;

namespace TestingDemo.Controllers
{
    [Authorize(Roles = "Admin,Finance")]
    public class RecurringExpenseController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RecurringExpenseController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: RecurringExpense
        public async Task<IActionResult> Index(string month, string status)
        {
            var currentDate = DateTime.Now;
            var selectedYear = currentDate.Year;
            var selectedMonth = currentDate.Month;

            // Parse month filter if provided
            if (!string.IsNullOrEmpty(month))
            {
                if (DateTime.TryParse(month + "-01", out var monthDate))
                {
                    selectedYear = monthDate.Year;
                    selectedMonth = monthDate.Month;
                }
            }

            // Get all active recurring expenses
            var recurringExpenses = await _context.RecurringExpenses
                .Where(re => re.IsActive)
                .Include(re => re.PaymentHistory)
                .ToListAsync();

            // Convert to ViewModels with current month status
            var viewModels = recurringExpenses.Select(re => new RecurringExpenseViewModel
            {
                Id = re.Id,
                Name = re.Name,
                Amount = re.Amount,
                Category = re.Category,
                Location = re.Location,
                StartDate = re.StartDate,
                DayOfMonthDue = re.DayOfMonthDue,
                Status = re.GetStatusForMonth(selectedYear, selectedMonth),
                DueDate = re.GetDueDateForMonth(selectedYear, selectedMonth),
                PaidDate = re.GetPaidDateForMonth(selectedYear, selectedMonth),
                IsActive = re.IsActive,
                Notes = re.Notes,
                CurrentYear = selectedYear,
                CurrentMonth = selectedMonth,
                CanMarkAsPaid = re.GetStatusForMonth(selectedYear, selectedMonth) != "Paid",
                CanPostpone = re.GetStatusForMonth(selectedYear, selectedMonth) == "Pending"
            }).ToList();

            // Apply status filter
            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                viewModels = viewModels.Where(vm => vm.Status == status).ToList();
            }

            ViewBag.SelectedMonth = month ?? $"{selectedYear:yyyy}-{selectedMonth:MM}";
            ViewBag.SelectedStatus = status ?? "All";
            ViewBag.CurrentYear = selectedYear;
            ViewBag.CurrentMonth = selectedMonth;

            return View(viewModels);
        }

        // GET: RecurringExpense/Create
        public IActionResult Create()
        {
            if (TempData["RecurringExpenseData"] is string json && !string.IsNullOrEmpty(json))
            {
                var expense = JsonSerializer.Deserialize<TestingDemo.Models.ExpenseModel>(json);
                if (expense != null)
                {
                    // Map ExpenseModel to RecurringExpense
                    var recurring = new RecurringExpense
                    {
                        Name = expense.Name,
                        Amount = expense.Amount,
                        Category = expense.Category,
                        Location = expense.Location,
                        StartDate = expense.DueDate,
                        DayOfMonthDue = expense.DueDate.Day,
                        Notes = expense.Notes
                    };
                    return View(recurring);
                }
            }
            return View();
        }

        // POST: RecurringExpense/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RecurringExpense recurringExpense)
        {
            if (ModelState.IsValid)
            {
                recurringExpense.CreatedDate = DateTime.Now;
                recurringExpense.LastModified = DateTime.Now;
                recurringExpense.IsActive = true;

                _context.RecurringExpenses.Add(recurringExpense);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Recurring expense created successfully!";
                return RedirectToAction(nameof(Index));
            }

            return View(recurringExpense);
        }

        // GET: RecurringExpense/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var recurringExpense = await _context.RecurringExpenses.FindAsync(id);
            if (recurringExpense == null) return NotFound();

            return View(recurringExpense);
        }

        // POST: RecurringExpense/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, RecurringExpense recurringExpense)
        {
            if (id != recurringExpense.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    recurringExpense.LastModified = DateTime.Now;
                    _context.Update(recurringExpense);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Recurring expense updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RecurringExpenseExists(recurringExpense.Id))
                        return NotFound();
                    else
                        throw;
                }
            }

            return View(recurringExpense);
        }

        // GET: RecurringExpense/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var recurringExpense = await _context.RecurringExpenses
                .Include(re => re.PaymentHistory)
                .FirstOrDefaultAsync(re => re.Id == id);

            if (recurringExpense == null) return NotFound();

            return View(recurringExpense);
        }

        // POST: RecurringExpense/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var recurringExpense = await _context.RecurringExpenses
                .Include(re => re.PaymentHistory)
                .FirstOrDefaultAsync(re => re.Id == id);

            if (recurringExpense == null) return NotFound();

            _context.RecurringExpenses.Remove(recurringExpense);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Recurring expense deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        // POST: RecurringExpense/MarkAsPaid
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(int id, int year, int month, decimal? amountPaid, string? paymentMethod, string? notes)
        {
            var recurringExpense = await _context.RecurringExpenses.FindAsync(id);
            if (recurringExpense == null) return NotFound();

            // Check if payment already exists
            var existingPayment = await _context.ExpensePayments
                .FirstOrDefaultAsync(ep => ep.RecurringExpenseId == id && ep.Year == year && ep.Month == month);

            if (existingPayment != null)
            {
                // Update existing payment
                existingPayment.Status = "Paid";
                existingPayment.PaidDate = DateTime.Now;
                existingPayment.AmountPaid = amountPaid ?? recurringExpense.Amount;
                existingPayment.PaymentMethod = paymentMethod;
                existingPayment.Notes = notes;
                existingPayment.LastModified = DateTime.Now;
            }
            else
            {
                // Create new payment record
                var payment = new ExpensePayment
                {
                    RecurringExpenseId = id,
                    Year = year,
                    Month = month,
                    Status = "Paid",
                    PaidDate = DateTime.Now,
                    AmountPaid = amountPaid ?? recurringExpense.Amount,
                    PaymentMethod = paymentMethod,
                    Notes = notes,
                    CreatedDate = DateTime.Now,
                    LastModified = DateTime.Now
                };

                _context.ExpensePayments.Add(payment);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Payment marked as paid for {recurringExpense.Name} ({year}-{month:00})";
            return RedirectToAction(nameof(Index));
        }

        // POST: RecurringExpense/Postpone
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Postpone(int id, int year, int month, string? notes)
        {
            var recurringExpense = await _context.RecurringExpenses.FindAsync(id);
            if (recurringExpense == null) return NotFound();

            // Check if payment already exists
            var existingPayment = await _context.ExpensePayments
                .FirstOrDefaultAsync(ep => ep.RecurringExpenseId == id && ep.Year == year && ep.Month == month);

            if (existingPayment != null)
            {
                // Update existing payment
                existingPayment.Status = "Postponed";
                existingPayment.Notes = notes;
                existingPayment.LastModified = DateTime.Now;
            }
            else
            {
                // Create new payment record
                var payment = new ExpensePayment
                {
                    RecurringExpenseId = id,
                    Year = year,
                    Month = month,
                    Status = "Postponed",
                    Notes = notes,
                    CreatedDate = DateTime.Now,
                    LastModified = DateTime.Now
                };

                _context.ExpensePayments.Add(payment);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Expense postponed for {recurringExpense.Name} ({year}-{month:00})";
            return RedirectToAction(nameof(Index));
        }

        // GET: RecurringExpense/PaymentHistory/5
        public async Task<IActionResult> PaymentHistory(int? id)
        {
            if (id == null) return NotFound();

            var recurringExpense = await _context.RecurringExpenses
                .Include(re => re.PaymentHistory.OrderByDescending(p => p.Year).ThenByDescending(p => p.Month))
                .FirstOrDefaultAsync(re => re.Id == id);

            if (recurringExpense == null) return NotFound();

            return View(recurringExpense);
        }

        // GET: RecurringExpense/Calendar
        public async Task<IActionResult> Calendar(int? year, int? month)
        {
            var now = DateTime.Now;
            int y = year ?? now.Year;
            int m = month ?? now.Month;

            // Normalize month and year
            if (m > 12)
            {
                m = 1;
                y++;
            }
            else if (m < 1)
            {
                m = 12;
                y--;
            }

            var recurringExpenses = await _context.RecurringExpenses.Where(re => re.IsActive).ToListAsync();
            var calendarInstances = new List<RecurringExpenseViewModel>();

            foreach (var re in recurringExpenses)
            {
                // For each recurring expense, generate an instance for this month if within active range
                var dueDate = re.GetDueDateForMonth(y, m);
                if (dueDate >= re.StartDate && dueDate <= now.AddYears(5)) // up to 5 years in future
                {
                    calendarInstances.Add(new RecurringExpenseViewModel
                    {
                        Id = re.Id,
                        Name = re.Name,
                        Amount = re.Amount,
                        Category = re.Category,
                        Location = re.Location,
                        StartDate = re.StartDate,
                        DayOfMonthDue = re.DayOfMonthDue,
                        Status = re.GetStatusForMonth(y, m),
                        DueDate = dueDate,
                        PaidDate = re.GetPaidDateForMonth(y, m),
                        IsActive = re.IsActive,
                        Notes = re.Notes,
                        CurrentYear = y,
                        CurrentMonth = m,
                        CanMarkAsPaid = re.GetStatusForMonth(y, m) != "Paid",
                        CanPostpone = re.GetStatusForMonth(y, m) == "Pending"
                    });
                }
            }

            ViewBag.Year = y;
            ViewBag.Month = m;
            return View(calendarInstances);
        }

        // POST: RecurringExpense/UndoMarkAsPaid
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UndoMarkAsPaid(int id, int year, int month)
        {
            var payment = await _context.ExpensePayments
                .FirstOrDefaultAsync(ep => ep.RecurringExpenseId == id && ep.Year == year && ep.Month == month && ep.Status == "Paid");
            if (payment != null)
            {
                _context.ExpensePayments.Remove(payment);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Payment record has been removed and status is now pending.";
            }
            else
            {
                TempData["ErrorMessage"] = "No paid record found to undo.";
            }
            return RedirectToAction("PaymentHistory", new { id });
        }

        // POST: RecurringExpense/UndoAllPayments
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UndoAllPayments(int id)
        {
            var payments = _context.ExpensePayments.Where(ep => ep.RecurringExpenseId == id).ToList();
            if (payments.Any())
            {
                _context.ExpensePayments.RemoveRange(payments);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "All payment history has been undone. All statuses are now pending.";
            }
            else
            {
                TempData["ErrorMessage"] = "No payment records found to undo.";
            }
            return RedirectToAction("PaymentHistory", new { id });
        }

        private bool RecurringExpenseExists(int id)
        {
            return _context.RecurringExpenses.Any(e => e.Id == id);
        }
    }
}