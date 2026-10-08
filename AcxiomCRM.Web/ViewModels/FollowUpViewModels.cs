using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Web.Models.Entities;

namespace AcxiomCRM.Web.ViewModels
{
    public class FollowUpListViewModel
    {
        public int FollowUpId { get; set; }
        public DateTime FollowUpDate { get; set; }
        public string FollowUpType { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Remarks { get; set; }
        public string RelatedTo { get; set; } = string.Empty;
        public string AssignedToName { get; set; } = string.Empty;
        public bool IsOverdue { get; set; }
    }

    public class CreateFollowUpViewModel
    {
        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
        [Display(Name = "Follow-Up Subject")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Follow-up date and time is required.")]
        [Display(Name = "Follow-Up Date & Time")]
        public DateTime FollowUpDate { get; set; } = DateTime.UtcNow.AddDays(1);

        [Required(ErrorMessage = "Type is required.")]
        [Display(Name = "Follow-Up Type")]
        public string FollowUpType { get; set; } = "Call";

        [Display(Name = "Link to Customer")]
        public int? CustomerId { get; set; }

        [Display(Name = "Link to Lead")]
        public int? LeadId { get; set; }

        [Display(Name = "Link to Opportunity")]
        public int? OpportunityId { get; set; }

        [StringLength(1000)]
        [Display(Name = "Remarks / Discussion Agenda")]
        public string? Remarks { get; set; }

        [Display(Name = "Assign To")]
        public string? AssignedTo { get; set; }
    }

    public class ActivityListViewModel
    {
        public int ActivityId { get; set; }
        public string ActivityType { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime ActivityDate { get; set; }
        public string RelatedTo { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string AssignedToName { get; set; } = string.Empty;
    }

    public class CreateActivityViewModel
    {
        [Required(ErrorMessage = "Activity Type is required.")]
        [Display(Name = "Activity Type")]
        public string ActivityType { get; set; } = "Call";

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(200)]
        [Display(Name = "Subject / Headline")]
        public string Subject { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "Activity Summary & Outcome")]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Date & Time")]
        public DateTime ActivityDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Link to Customer")]
        public int? CustomerId { get; set; }

        [Display(Name = "Link to Lead")]
        public int? LeadId { get; set; }

        [Display(Name = "Link to Opportunity")]
        public int? OpportunityId { get; set; }

        [Required]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Completed";

        [Display(Name = "Sales Executive")]
        public string? AssignedTo { get; set; }
    }
}
