using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Web.Models.Entities
{
    public class Lead
    {
        [Key]
        public int LeadId { get; set; }

        [Required]
        [MaxLength(20)]
        public string LeadCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string LeadName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Phone]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? CompanyName { get; set; }

        [Required]
        [MaxLength(50)]
        public string Source { get; set; } = "Website";

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "New";

        [MaxLength(20)]
        public string Priority { get; set; } = "Medium";

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 1000000000)]
        public decimal ExpectedValue { get; set; } = 0;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(450)]
        public string AssignedTo { get; set; } = string.Empty;

        [ForeignKey(nameof(AssignedTo))]
        public virtual ApplicationUser? AssignedToUser { get; set; }

        public int? ConvertedCustomerId { get; set; }
        [ForeignKey(nameof(ConvertedCustomerId))]
        public virtual Customer? ConvertedCustomer { get; set; }

        public int? ConvertedOpportunityId { get; set; }
        [ForeignKey(nameof(ConvertedOpportunityId))]
        public virtual Opportunity? ConvertedOpportunity { get; set; }

        public virtual ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
        public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
    }
}
