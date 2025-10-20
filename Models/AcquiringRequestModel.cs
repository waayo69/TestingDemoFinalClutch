using System.ComponentModel.DataAnnotations;

public class AcquiringRequestModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Supplier Name is required")]
    [Display(Name = "Supplier Name")]
    public string SupplierName { get; set; }

    [Required(ErrorMessage = "Contact Person is required")]
    [Display(Name = "Contact Person")]
    public required string ContactPerson { get; set; }

    [Required(ErrorMessage = "Contact Number is required")]
    [Display(Name = "Contact Number")]
    [Phone]
    public string ContactNumber { get; set; }

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress]
    public string Email { get; set; }

    [Required(ErrorMessage = "Business Address is required")]
    [Display(Name = "Business Address")]
    public string BusinessAddress { get; set; }

    [Required(ErrorMessage = "Business Type is required")]
    [Display(Name = "Business Type")]
    public string BusinessType { get; set; }

    [Display(Name = "Products/Services Offered")]
    public string ProductsOffered { get; set; }

    [Display(Name = "Additional Notes")]
    public string AdditionalNotes { get; set; }

    public string Status { get; set; } = "Pending";
    public DateTime SubmissionDate { get; set; } = DateTime.Now;
}
