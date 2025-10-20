using System;
using System.ComponentModel.DataAnnotations;

namespace TestingDemo.Models
{
    public class RetainershipBIRModel
    {
        public int Id { get; set; }
        public string? TypeOfRegistrant { get; set; }
        public string? OCNNotes { get; set; }
        public DateTime? DateOCNGenerated { get; set; }
        public DateTime? DateBIRRegistration { get; set; }
        public string? BIRRdoNo { get; set; }
        public string? OtherBirRdoNo { get; set; }
        public string? BIRCertificateUploadPath { get; set; }
        public string? TaxFilingStatus { get; set; }
        public string? NeedCatchUpAccounting { get; set; }
        public string? CatchUpReasons { get; set; } // Comma-separated
        public string? OtherCatchUpReason { get; set; }
        public DateTime? CatchUpStartDate { get; set; }
        public string? BIRComplianceActivities { get; set; } // Comma-separated
        public string? OtherBIRCompliance { get; set; }
        public DateTime? BIRRetainershipStartDate { get; set; }
    }
}