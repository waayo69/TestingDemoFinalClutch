using System;
using System.ComponentModel.DataAnnotations;

namespace TestingDemo.Models
{
    public class ExternalAuditModel
    {
        public int Id { get; set; }
        public string? ExternalAuditStatus { get; set; }
        public string? ExternalAuditPurposes { get; set; } // Comma-separated
        public string? ExternalAuditOtherPurpose { get; set; }
        public DateTime? ExternalAuditReportDate { get; set; }
    }
}