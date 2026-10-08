using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Web.Models.Entities;

namespace AcxiomCRM.Web.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Lead> Leads => Set<Lead>();
        public DbSet<Opportunity> Opportunities => Set<Opportunity>();
        public DbSet<FollowUp> FollowUps => Set<FollowUp>();
        public DbSet<Activity> Activities => Set<Activity>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Customer indexes & uniqueness
            builder.Entity<Customer>(entity =>
            {
                entity.HasIndex(c => c.CustomerCode).IsUnique();
                entity.HasIndex(c => c.Email).IsUnique();
                entity.HasIndex(c => c.Phone).IsUnique();

                entity.HasOne(c => c.CreatedByUser)
                    .WithMany(u => u.CreatedCustomers)
                    .HasForeignKey(c => c.CreatedBy)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Lead indexes & uniqueness
            builder.Entity<Lead>(entity =>
            {
                entity.HasIndex(l => l.LeadCode).IsUnique();

                entity.HasOne(l => l.AssignedToUser)
                    .WithMany(u => u.AssignedLeads)
                    .HasForeignKey(l => l.AssignedTo)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.ConvertedCustomer)
                    .WithMany(c => c.Leads)
                    .HasForeignKey(l => l.ConvertedCustomerId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.Property(l => l.ExpectedValue)
                    .HasColumnType("decimal(18,2)");
            });

            // Opportunity configurations
            builder.Entity<Opportunity>(entity =>
            {
                entity.HasOne(o => o.Customer)
                    .WithMany(c => c.Opportunities)
                    .HasForeignKey(o => o.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(o => o.Lead)
                    .WithMany()
                    .HasForeignKey(o => o.LeadId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(o => o.AssignedToUser)
                    .WithMany(u => u.AssignedOpportunities)
                    .HasForeignKey(o => o.AssignedTo)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(o => o.Amount)
                    .HasColumnType("decimal(18,2)");
            });

            // FollowUp configurations
            builder.Entity<FollowUp>(entity =>
            {
                entity.HasOne(f => f.Customer)
                    .WithMany(c => c.FollowUps)
                    .HasForeignKey(f => f.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(f => f.Lead)
                    .WithMany(l => l.FollowUps)
                    .HasForeignKey(f => f.LeadId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(f => f.Opportunity)
                    .WithMany(o => o.FollowUps)
                    .HasForeignKey(f => f.OpportunityId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(f => f.AssignedToUser)
                    .WithMany(u => u.AssignedFollowUps)
                    .HasForeignKey(f => f.AssignedTo)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Activity configurations
            builder.Entity<Activity>(entity =>
            {
                entity.HasOne(a => a.Customer)
                    .WithMany(c => c.Activities)
                    .HasForeignKey(a => a.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Lead)
                    .WithMany(l => l.Activities)
                    .HasForeignKey(a => a.LeadId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Opportunity)
                    .WithMany(o => o.Activities)
                    .HasForeignKey(a => a.OpportunityId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.AssignedToUser)
                    .WithMany(u => u.AssignedActivities)
                    .HasForeignKey(a => a.AssignedTo)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // AuditLog indexing
            builder.Entity<AuditLog>(entity =>
            {
                entity.HasIndex(a => a.EntityName);
                entity.HasIndex(a => a.Action);
                entity.HasIndex(a => a.CreatedDate);
                entity.HasIndex(a => a.UserId);
            });
        }
    }
}

