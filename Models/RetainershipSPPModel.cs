using System;
using System.ComponentModel.DataAnnotations;

namespace TestingDemo.Models
{
    public class RetainershipSPPModel
    {
        public int Id { get; set; }
        public string? SSSCompanyRegNo { get; set; }
        public DateTime? SSSRegistrationDate { get; set; }
        public string? PHICCompanyRegNo { get; set; }
        public DateTime? PHICRegistrationDate { get; set; }
        public string? HDMFCompanyRegNo { get; set; }
        public DateTime? HDMFRegistrationDate { get; set; }
        public string? SPPComplianceActivities { get; set; } // Comma-separated
        public string? OtherSPPCompliance { get; set; }
        public DateTime? SPPRetainershipStartDate { get; set; }
    }
}