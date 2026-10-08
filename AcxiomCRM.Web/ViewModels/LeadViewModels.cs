using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Web.Models.Entities;

namespace AcxiomCRM.Web.ViewModels
{
    public class LeadListViewModel
    {
        public int LeadId { get; set; }
        public string LeadCode { get; set; } = string.Empty;
        public string LeadName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public decimal ExpectedValue { get; set; }
        public DateTime CreatedDate { get; set; }
        public string AssignedToName { get; set; } = string.Empty;
        public int? ConvertedCustomerId { get; set; }
    }

    public class CreateLeadViewModel
    {
        [Required(ErrorMessage = "Lead name is mandatory.")]
        [StringLength(150, ErrorMessage = "Lead Name cannot exceed 150 characters.")]
        [Display(Name = "Contact Name")]
        public string LeadName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(150)]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid 10-digit mobile number.")]
        [Display(Name = "Phone Number (10 digits)")]
        public string Phone { get; set; } = string.Empty;

        [StringLength(150)]
        [Display(Name = "Company Name")]
        public string? CompanyName { get; set; }

        [Required(ErrorMessage = "Lead source is required.")]
        [Display(Name = "Lead Source")]
        public string Source { get; set; } = "Website";

        [Required(ErrorMessage = "Lead status is mandatory.")]
        [Display(Name = "Lead Status")]
        public string Status { get; set; } = "New";

        [Display(Name = "Priority")]
        public string Priority { get; set; } = "Medium";

        [Range(0, 1000000000, ErrorMessage = "Expected value must be a positive number.")]
        [Display(Name = "Expected Deal Value (₹)")]
        public decimal ExpectedValue { get; set; } = 0;

        [Display(Name = "Assigned Sales Executive")]
        public string? AssignedTo { get; set; }
    }

    public class EditLeadViewModel
    {
        public int LeadId { get; set; }

        public string LeadCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Lead name is mandatory.")]
        [StringLength(150)]
        public string LeadName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone is required.")]
        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid 10-digit mobile number.")]
        public string Phone { get; set; } = string.Empty;

        [StringLength(150)]
        public string? CompanyName { get; set; }

        [Required]
        public string Source { get; set; } = string.Empty;

        [Required]
        public string Status { get; set; } = string.Empty;

        public string Priority { get; set; } = "Medium";

        [Range(0, 1000000000)]
        public decimal ExpectedValue { get; set; }

        public string? AssignedTo { get; set; }
    }

    public class ConvertLeadViewModel
    {
        public int LeadId { get; set; }
        public string LeadCode { get; set; } = string.Empty;
        public string LeadName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public decimal ExpectedValue { get; set; }

        [Display(Name = "Create an active Opportunity immediately?")]
        public bool CreateOpportunity { get; set; } = true;

        [Display(Name = "Opportunity Name")]
        public string OpportunityName { get; set; } = string.Empty;

        [Range(0.01, 1000000000, ErrorMessage = "Opportunity amount must be greater than 0.")]
        [Display(Name = "Opportunity Amount (₹)")]
        public decimal OpportunityAmount { get; set; }

        [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
        [Display(Name = "Probability (%)")]
        public int Probability { get; set; } = 25;

        [Display(Name = "Expected Close Date")]
        public DateTime ExpectedCloseDate { get; set; } = DateTime.UtcNow.AddDays(30);
    }
}
