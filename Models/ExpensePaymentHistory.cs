using System;

namespace TestingDemo.Models
{
    public class ExpensePaymentHistory
    {
        public int Id { get; set; } // Primary key
        public int ExpenseModelId { get; set; } // Foreign key
        public ExpenseModel Expense { get; set; }
        public DateTime Date { get; set; }
        public string Action { get; set; } // "Paid" or "Undone"
        public string? Note { get; set; }
    }
}