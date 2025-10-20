using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace TestingDemo.Models
{
    public class ExpenseModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Expense Name")]
        public string Name { get; set; }

        [Required]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Due Date")]
        public DateTime DueDate { get; set; }

        [Required]
        [Display(Name = "Status")]
        public string Status { get; set; } // Paid, Pending, Overdue, Postponed

        [Required]
        [Display(Name = "Category")]
        public string Category { get; set; }

        [Required]
        [Display(Name = "Location")]
        public string Location { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Paid Date")]
        public DateTime? PaidDate { get; set; }

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public bool Recurring { get; set; }
        public int? RepeatMonths { get; set; }

        // Payment history for normal expenses
        public virtual ICollection<ExpensePaymentHistory> PaymentHistory { get; set; } = new List<ExpensePaymentHistory>();

        // Soft deletion
        public bool IsDeleted { get; set; } = false;
    }
}