using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Web.Models.Entities
{
    public class Customer
    {
        [Key]
        public int CustomerId { get; set; }

        [Required]
        [MaxLength(20)]
        public string CustomerCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string CustomerName { get; set; } = string.Empty;

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

        [MaxLength(300)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? State { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Active";

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ModifiedDate { get; set; }

        [Required]
        [MaxLength(450)]
        public string CreatedBy { get; set; } = string.Empty;

        [ForeignKey(nameof(CreatedBy))]
        public virtual ApplicationUser? CreatedByUser { get; set; }

        public virtual ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
        public virtual ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
        public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
        public virtual ICollection<Lead> Leads { get; set; } = new List<Lead>();
    }
}
