using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Web.Models.Entities;

namespace AcxiomCRM.Web.ViewModels
{
    public class OpportunityListViewModel
    {
        public int OpportunityId { get; set; }
        public string OpportunityName { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Stage { get; set; } = string.Empty;
        public int Probability { get; set; }
        public decimal WeightedAmount { get; set; }
        public DateTime ExpectedCloseDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string AssignedToName { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    public class CreateOpportunityViewModel
    {
        [Required(ErrorMessage = "Opportunity Name is required.")]
        [StringLength(150, ErrorMessage = "Opportunity Name cannot exceed 150 characters.")]
        [Display(Name = "Opportunity Name / Deal Title")]
        public string OpportunityName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Customer selection is required.")]
        [Display(Name = "Select Customer")]
        public int CustomerId { get; set; }

        public int? LeadId { get; set; }

        [Required(ErrorMessage = "Opportunity Amount is required.")]
        [Range(0.01, 1000000000, ErrorMessage = "Opportunity Amount must be greater than 0.")]
        [Display(Name = "Deal Amount (₹)")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Pipeline Stage is required.")]
        [Display(Name = "Pipeline Stage")]
        public string Stage { get; set; } = "Qualification";

        [Required(ErrorMessage = "Probability is required.")]
        [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
        [Display(Name = "Win Probability (%)")]
        public int Probability { get; set; } = 20;

        [Required(ErrorMessage = "Expected Close Date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Expected Close Date")]
        public DateTime ExpectedCloseDate { get; set; } = DateTime.UtcNow.AddDays(30);

        [StringLength(500)]
        [Display(Name = "Notes & Deal Strategy")]
        public string? Notes { get; set; }

        [Display(Name = "Sales Executive")]
        public string? AssignedTo { get; set; }
    }

    public class EditOpportunityViewModel
    {
        public int OpportunityId { get; set; }

        [Required(ErrorMessage = "Opportunity Name is required.")]
        [StringLength(150)]
        [Display(Name = "Opportunity Name")]
        public string OpportunityName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Customer selection is required.")]
        [Display(Name = "Select Customer")]
        public int CustomerId { get; set; }

        public int? LeadId { get; set; }

        [Required(ErrorMessage = "Opportunity Amount is required.")]
        [Range(0.01, 1000000000, ErrorMessage = "Opportunity Amount must be greater than 0.")]
        [Display(Name = "Deal Amount (₹)")]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "Pipeline Stage")]
        public string Stage { get; set; } = string.Empty;

        [Required]
        [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
        [Display(Name = "Win Probability (%)")]
        public int Probability { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Expected Close Date")]
        public DateTime ExpectedCloseDate { get; set; }

        [Required]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Open";

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Sales Executive")]
        public string? AssignedTo { get; set; }
    }

    public class PipelineKanbanViewModel
    {
        public List<Opportunity> Qualification { get; set; } = new List<Opportunity>();
        public List<Opportunity> Proposal { get; set; } = new List<Opportunity>();
        public List<Opportunity> Negotiation { get; set; } = new List<Opportunity>();
        public List<Opportunity> Won { get; set; } = new List<Opportunity>();
        public List<Opportunity> Lost { get; set; } = new List<Opportunity>();
        public decimal TotalPipelineValue { get; set; }
        public decimal TotalWeightedValue { get; set; }
    }
}
