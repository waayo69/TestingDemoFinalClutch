using System;
using System.ComponentModel.DataAnnotations;

namespace TestingDemo.Models.ViewModels
{
    public class RecurringExpenseViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Expense Name")]
        public string Name { get; set; }

        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [Display(Name = "Category")]
        public string Category { get; set; }

        [Display(Name = "Location")]
        public string Location { get; set; }

        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Display(Name = "Due Day")]
        public int DayOfMonthDue { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; }

        [Display(Name = "Due Date")]
        public DateTime DueDate { get; set; }

        [Display(Name = "Paid Date")]
        public DateTime? PaidDate { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        // For payment actions
        public int CurrentYear { get; set; }
        public int CurrentMonth { get; set; }
        public bool CanMarkAsPaid { get; set; }
        public bool CanPostpone { get; set; }

        // Payment details
        public decimal? AmountPaid { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PaymentNotes { get; set; }
    }
}