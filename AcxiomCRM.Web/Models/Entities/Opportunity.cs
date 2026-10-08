using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Web.Models.Entities
{
    public class Opportunity
    {
        [Key]
        public int OpportunityId { get; set; }

        [Required]
        [MaxLength(150)]
        public string OpportunityName { get; set; } = string.Empty;

        [Required]
        public int CustomerId { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public virtual Customer? Customer { get; set; }

        public int? LeadId { get; set; }

        [ForeignKey(nameof(LeadId))]
        public virtual Lead? Lead { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 1000000000, ErrorMessage = "Opportunity Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(50)]
        public string Stage { get; set; } = "Qualification";

        [Required]
        [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
        public int Probability { get; set; } = 10;

        [Required]
        public DateTime ExpectedCloseDate { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Open";

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(450)]
        public string AssignedTo { get; set; } = string.Empty;

        [ForeignKey(nameof(AssignedTo))]
        public virtual ApplicationUser? AssignedToUser { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [NotMapped]
        public decimal WeightedAmount => (Amount * Probability) / 100m;

        public virtual ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
        public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
    }
}
