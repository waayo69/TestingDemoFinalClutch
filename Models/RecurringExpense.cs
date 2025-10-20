using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace TestingDemo.Models
{
    public class RecurringExpense
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Expense Name")]
        public string Name { get; set; }

        [Required]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "Category")]
        public string Category { get; set; }

        [Required]
        [Display(Name = "Location")]
        public string Location { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "Due Day of Month")]
        [Range(1, 31)]
        public int DayOfMonthDue { get; set; } // e.g., 10 for the 10th of each month

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "Last Modified")]
        public DateTime LastModified { get; set; } = DateTime.Now;

        // Navigation property for payment history
        public virtual ICollection<ExpensePayment> PaymentHistory { get; set; } = new List<ExpensePayment>();

        // Computed properties
        public DateTime GetDueDateForMonth(int year, int month)
        {
            var day = Math.Min(DayOfMonthDue, DateTime.DaysInMonth(year, month));
            return new DateTime(year, month, day);
        }

        public string GetStatusForMonth(int year, int month)
        {
            var dueDate = GetDueDateForMonth(year, month);
            var payment = PaymentHistory?.FirstOrDefault(p => p.Year == year && p.Month == month);

            if (payment != null)
            {
                return payment.Status;
            }

            var today = DateTime.Today;
            if (dueDate < today)
            {
                return "Overdue";
            }
            else if (dueDate <= today.AddDays(7))
            {
                return "Pending";
            }
            else
            {
                return "Pending";
            }
        }

        public DateTime? GetPaidDateForMonth(int year, int month)
        {
            return PaymentHistory?.FirstOrDefault(p => p.Year == year && p.Month == month)?.PaidDate;
        }
    }
}