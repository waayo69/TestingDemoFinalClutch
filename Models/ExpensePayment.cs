using System;
using System.ComponentModel.DataAnnotations;

namespace TestingDemo.Models
{
    public class ExpensePayment
    {
        public int Id { get; set; }

        [Required]
        public int RecurringExpenseId { get; set; }

        [Required]
        [Display(Name = "Year")]
        public int Year { get; set; }

        [Required]
        [Display(Name = "Month")]
        [Range(1, 12)]
        public int Month { get; set; }

        [Display(Name = "Paid Date")]
        [DataType(DataType.Date)]
        public DateTime? PaidDate { get; set; }

        [Required]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Pending"; // Paid, Pending, Overdue, Postponed

        [Display(Name = "Amount Paid")]
        [DataType(DataType.Currency)]
        public decimal? AmountPaid { get; set; }

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Payment Method")]
        public string? PaymentMethod { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "Last Modified")]
        public DateTime LastModified { get; set; } = DateTime.Now;

        // Navigation property
        public virtual RecurringExpense RecurringExpense { get; set; }

        // Computed properties
        public DateTime DueDate => RecurringExpense?.GetDueDateForMonth(Year, Month) ?? DateTime.MinValue;

        public bool IsPaid => Status == "Paid" && PaidDate.HasValue;

        public bool IsOverdue => !IsPaid && DueDate < DateTime.Today;
    }
}