using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Web.Models.Entities
{
    public class FollowUp
    {
        [Key]
        public int FollowUpId { get; set; }

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
        public DateTime FollowUpDate { get; set; }

        [Required]
        [MaxLength(50)]
        public string FollowUpType { get; set; } = "Call";

        [Required]
        [MaxLength(200)]
        public string Subject { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Remarks { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Planned";

        [Required]
        [MaxLength(450)]
        public string AssignedTo { get; set; } = string.Empty;

        [ForeignKey(nameof(AssignedTo))]
        public virtual ApplicationUser? AssignedToUser { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
