using System.ComponentModel.DataAnnotations;

namespace TestingDemo.Models
{
    public class OneTimeTransactionModel
    {
        public int Id { get; set; }
        public string? TypeOfRegistrant { get; set; }
        public string? AreaOfServices { get; set; }
        public string? OtherAreaOfServices { get; set; }
    }
}