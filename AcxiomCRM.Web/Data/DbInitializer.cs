using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Web.Common;
using AcxiomCRM.Web.Models.Entities;

namespace AcxiomCRM.Web.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            await context.Database.MigrateAsync();

            // 1. Seed Roles
            foreach (var roleName in AppRoles.All)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Seed Admin User
            var adminEmail = "admin@acxiomcrm.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FullName = "System Administrator",
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(adminUser, "Admin@12345");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, AppRoles.Admin);
                }
            }

            // 3. Seed Manager User
            var managerEmail = "manager@acxiomcrm.com";
            var managerUser = await userManager.FindByEmailAsync(managerEmail);
            if (managerUser == null)
            {
                managerUser = new ApplicationUser
                {
                    UserName = managerEmail,
                    Email = managerEmail,
                    EmailConfirmed = true,
                    FullName = "Sales Manager (Vikram)",
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(managerUser, "Manager@12345");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(managerUser, AppRoles.Manager);
                }
            }

            // 4. Seed Sales Executive User
            var salesEmail = "sales@acxiomcrm.com";
            var salesUser = await userManager.FindByEmailAsync(salesEmail);
            if (salesUser == null)
            {
                salesUser = new ApplicationUser
                {
                    UserName = salesEmail,
                    Email = salesEmail,
                    EmailConfirmed = true,
                    FullName = "Sales Executive (Rajesh)",
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(salesUser, "Sales@12345");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(salesUser, AppRoles.SalesExecutive);
                }
            }

            // 5. Seed Customers if none exist
            if (!context.Customers.Any())
            {
                var customers = new List<Customer>
                {
                    new Customer
                    {
                        CustomerCode = "CUST-10001",
                        CustomerName = "Apex Global Technologies",
                        Email = "contact@apextechnologies.com",
                        Phone = "9876543210",
                        CompanyName = "Apex Global",
                        Address = "101 Cyber City, Phase II",
                        City = "Gurugram",
                        State = "Haryana",
                        Status = CustomerStatuses.Active,
                        CreatedDate = DateTime.UtcNow.AddDays(-30),
                        CreatedBy = salesUser!.Id
                    },
                    new Customer
                    {
                        CustomerCode = "CUST-10002",
                        CustomerName = "Nexus Infotech Solutions",
                        Email = "info@nexusinfotech.com",
                        Phone = "9876543211",
                        CompanyName = "Nexus Infotech",
                        Address = "404 Outer Ring Road, Bellandur",
                        City = "Bengaluru",
                        State = "Karnataka",
                        Status = CustomerStatuses.Active,
                        CreatedDate = DateTime.UtcNow.AddDays(-20),
                        CreatedBy = salesUser.Id
                    },
                    new Customer
                    {
                        CustomerCode = "CUST-10003",
                        CustomerName = "Zenith Healthcare Ltd",
                        Email = "procure@zenithhealth.org",
                        Phone = "9876543212",
                        CompanyName = "Zenith Healthcare",
                        Address = "88 Marine Lines",
                        City = "Mumbai",
                        State = "Maharashtra",
                        Status = CustomerStatuses.Active,
                        CreatedDate = DateTime.UtcNow.AddDays(-15),
                        CreatedBy = managerUser!.Id
                    }
                };

                context.Customers.AddRange(customers);
                await context.SaveChangesAsync();
            }

            // 6. Seed Leads if none exist
            if (!context.Leads.Any())
            {
                var leads = new List<Lead>
                {
                    new Lead
                    {
                        LeadCode = "LEAD-10001",
                        LeadName = "Priya Sharma",
                        Email = "priya.sharma@innovatecorp.in",
                        Phone = "9123456780",
                        CompanyName = "Innovate Corp",
                        Source = "Website",
                        Status = LeadStatuses.New,
                        Priority = "High",
                        ExpectedValue = 450000m,
                        CreatedDate = DateTime.UtcNow.AddDays(-5),
                        AssignedTo = salesUser!.Id
                    },
                    new Lead
                    {
                        LeadCode = "LEAD-10002",
                        LeadName = "Arun Verma",
                        Email = "arun.verma@sunriselogistics.com",
                        Phone = "9123456781",
                        CompanyName = "Sunrise Logistics",
                        Source = "Referral",
                        Status = LeadStatuses.Contacted,
                        Priority = "Medium",
                        ExpectedValue = 280000m,
                        CreatedDate = DateTime.UtcNow.AddDays(-8),
                        AssignedTo = salesUser.Id
                    },
                    new Lead
                    {
                        LeadCode = "LEAD-10003",
                        LeadName = "Sunita Rao",
                        Email = "sunita.rao@quantumretail.com",
                        Phone = "9123456782",
                        CompanyName = "Quantum Retail",
                        Source = "Cold Call",
                        Status = LeadStatuses.Qualified,
                        Priority = "High",
                        ExpectedValue = 750000m,
                        CreatedDate = DateTime.UtcNow.AddDays(-12),
                        AssignedTo = managerUser!.Id
                    }
                };

                context.Leads.AddRange(leads);
                await context.SaveChangesAsync();
            }

            // 7. Seed Opportunities if none exist
            if (!context.Opportunities.Any())
            {
                var cust1 = await context.Customers.FirstOrDefaultAsync(c => c.CustomerCode == "CUST-10001");
                var cust2 = await context.Customers.FirstOrDefaultAsync(c => c.CustomerCode == "CUST-10002");
                var cust3 = await context.Customers.FirstOrDefaultAsync(c => c.CustomerCode == "CUST-10003");

                if (cust1 != null && cust2 != null && cust3 != null)
                {
                    var opps = new List<Opportunity>
                    {
                        new Opportunity
                        {
                            OpportunityName = "Cloud Infrastructure Migration",
                            CustomerId = cust1.CustomerId,
                            Amount = 850000m,
                            Stage = OpportunityStages.Proposal,
                            Probability = 60,
                            ExpectedCloseDate = DateTime.UtcNow.AddDays(20),
                            Status = "Open",
                            CreatedDate = DateTime.UtcNow.AddDays(-10),
                            AssignedTo = salesUser!.Id,
                            Notes = "Proposal delivered; waiting for client security clearance."
                        },
                        new Opportunity
                        {
                            OpportunityName = "ERP Integration & Licensing",
                            CustomerId = cust2.CustomerId,
                            Amount = 1200000m,
                            Stage = OpportunityStages.Negotiation,
                            Probability = 80,
                            ExpectedCloseDate = DateTime.UtcNow.AddDays(14),
                            Status = "Open",
                            CreatedDate = DateTime.UtcNow.AddDays(-18),
                            AssignedTo = salesUser.Id,
                            Notes = "Contract commercials being finalized."
                        },
                        new Opportunity
                        {
                            OpportunityName = "Healthcare Telemetry System",
                            CustomerId = cust3.CustomerId,
                            Amount = 500000m,
                            Stage = OpportunityStages.Won,
                            Probability = 100,
                            ExpectedCloseDate = DateTime.UtcNow.AddDays(-2),
                            Status = "Won",
                            CreatedDate = DateTime.UtcNow.AddDays(-25),
                            AssignedTo = managerUser!.Id,
                            Notes = "Deal successfully closed and invoice raised."
                        }
                    };

                    context.Opportunities.AddRange(opps);
                    await context.SaveChangesAsync();
                }
            }

            // 8. Seed Follow-ups if none exist
            if (!context.FollowUps.Any())
            {
                var cust1 = await context.Customers.FirstOrDefaultAsync(c => c.CustomerCode == "CUST-10001");
                var lead1 = await context.Leads.FirstOrDefaultAsync(l => l.LeadCode == "LEAD-10001");

                var followUps = new List<FollowUp>
                {
                    new FollowUp
                    {
                        CustomerId = cust1?.CustomerId,
                        FollowUpDate = DateTime.UtcNow.AddDays(2),
                        FollowUpType = "Meeting",
                        Subject = "Proposal Review & Technical Q&A",
                        Remarks = "Discuss SLA agreements and architecture queries.",
                        Status = FollowUpStatuses.Planned,
                        AssignedTo = salesUser!.Id,
                        CreatedDate = DateTime.UtcNow.AddDays(-2)
                    },
                    new FollowUp
                    {
                        LeadId = lead1?.LeadId,
                        FollowUpDate = DateTime.UtcNow.AddDays(1),
                        FollowUpType = "Call",
                        Subject = "Introductory Product Demonstration",
                        Remarks = "Schedule 30 mins demo call with CTO.",
                        Status = FollowUpStatuses.Planned,
                        AssignedTo = salesUser.Id,
                        CreatedDate = DateTime.UtcNow.AddDays(-1)
                    }
                };

                context.FollowUps.AddRange(followUps);
                await context.SaveChangesAsync();
            }

            // 9. Seed Activities if none exist
            if (!context.Activities.Any())
            {
                var cust1 = await context.Customers.FirstOrDefaultAsync(c => c.CustomerCode == "CUST-10001");

                var activities = new List<Activity>
                {
                    new Activity
                    {
                        CustomerId = cust1?.CustomerId,
                        ActivityType = ActivityTypes.Call,
                        Subject = "Discovery call with VP of Engineering",
                        Description = "Understood key pain points and current cloud cost overheads.",
                        ActivityDate = DateTime.UtcNow.AddDays(-7),
                        Status = "Completed",
                        AssignedTo = salesUser!.Id,
                        CreatedDate = DateTime.UtcNow.AddDays(-7)
                    },
                    new Activity
                    {
                        CustomerId = cust1?.CustomerId,
                        ActivityType = ActivityTypes.Email,
                        Subject = "Dispatched Scope of Work (SOW)",
                        Description = "Shared detailed breakdown of architectural milestones.",
                        ActivityDate = DateTime.UtcNow.AddDays(-4),
                        Status = "Completed",
                        AssignedTo = salesUser.Id,
                        CreatedDate = DateTime.UtcNow.AddDays(-4)
                    }
                };

                context.Activities.AddRange(activities);
                await context.SaveChangesAsync();
            }

            // 10. Seed Initial Audit Logs
            if (!context.AuditLogs.Any())
            {
                var auditEntries = new List<AuditLog>
                {
                    new AuditLog
                    {
                        UserId = adminUser!.Id,
                        UserName = adminUser.UserName,
                        Action = AuditActions.Security,
                        EntityName = "System",
                        RecordId = "Init",
                        Details = "System initialized and security policies established.",
                        CreatedDate = DateTime.UtcNow.AddDays(-30),
                        IpAddress = "127.0.0.1",
                        Result = "Success"
                    },
                    new AuditLog
                    {
                        UserId = adminUser.Id,
                        UserName = adminUser.UserName,
                        Action = AuditActions.Create,
                        EntityName = "User",
                        RecordId = managerUser!.Id,
                        Details = "Created user manager@acxiomcrm.com with role Manager.",
                        CreatedDate = DateTime.UtcNow.AddDays(-29),
                        IpAddress = "127.0.0.1",
                        Result = "Success"
                    }
                };

                context.AuditLogs.AddRange(auditEntries);
                await context.SaveChangesAsync();
            }
        }
    }
}
