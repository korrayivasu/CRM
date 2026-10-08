using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Web.Models.Entities
{
    public class Activity
    {
        [Key]
        public int ActivityId { get; set; }

        [Required]
        [MaxLength(50)]
        public string ActivityType { get; set; } = "Call"; // Call, Meeting, Email, Task

        [Required]
        [MaxLength(200)]
        public string Subject { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        public DateTime ActivityDate { get; set; } = DateTime.UtcNow;

        public int? CustomerId { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public virtual Customer? Customer { get; set; }

        public int? LeadId { get; set; }

        [ForeignKey(nameof(LeadId))]
        public virtual Lead? Lead { get; set; }

        public int? OpportunityId { get; set; }

        [ForeignKey(nameof(OpportunityId))]
        public virtual Opportunity? Opportunity { get; set; }

        [Required]
        [MaxLength(450)]
        public string AssignedTo { get; set; } = string.Empty;

        [ForeignKey(nameof(AssignedTo))]
        public virtual ApplicationUser? AssignedToUser { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Completed";

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
