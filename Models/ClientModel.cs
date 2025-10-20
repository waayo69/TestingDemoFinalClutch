using System.ComponentModel.DataAnnotations;
using TestingDemo.Models;

namespace TestingDemo.Models
{
    public class ClientModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string Email { get; set; }

        public string RequestingParty { get; set; }
        public string RequestorName { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public string ClientType { get; set; }

        [Required(ErrorMessage = "Client Name is required")]
        [Display(Name = "Client Name")]
        public string ClientName { get; set; }

        [Display(Name = "Tax ID")]
        public string? TaxId { get; set; }

        [Required(ErrorMessage = "Contact Number is required")]
        [Display(Name = "Contact Number")]
        [Phone]
        public string ContactPersonNumber { get; set; }

        public string ContactPersonEmailAddress { get; set; }

        public string RegisteredCompanyName { get; set; }
        [Required(ErrorMessage = "Company Address is required")]
        [Display(Name = "CompanyAddress")]
        public string RegisteredCompanyAddress { get; set; }
        [Required(ErrorMessage = "Project Type is required")]
        [Display(Name = "Project Type")]
        public string TypeOfProject { get; set; }

        [Required(ErrorMessage = "Urgency Level is required")]
        [Display(Name = "Urgency Level")]
        public string UrgencyLevel { get; set; } = "Normal";

        public string Status { get; set; } = "Pending";

        [Display(Name = "Planning Return Note")]
        public string? PlanningReturnNote { get; set; }

        // Navigation properties for project type-specific data
        public RetainershipBIRModel? RetainershipBIR { get; set; }
        public RetainershipSPPModel? RetainershipSPP { get; set; }
        public OneTimeTransactionModel? OneTimeTransaction { get; set; }
        public ExternalAuditModel? ExternalAudit { get; set; }

        public string? OtherTypeOfProject { get; set; }
        public string? OtherRequestingParty { get; set; }

        // Unique tracking number for client-side tracking
        [Required]
        [RegularExpression(@"^[A-Z]{4}-\d{6}$", ErrorMessage = "Tracking number must be in the format AWYZ-078923")]
        public string TrackingNumber { get; set; }
    }
}