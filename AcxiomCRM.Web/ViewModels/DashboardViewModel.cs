using System;
using System.Collections.Generic;
using AcxiomCRM.Web.Models.Entities;

namespace AcxiomCRM.Web.ViewModels
{
    public class DashboardViewModel
    {
        // 8 Mandatory Dashboard Cards per Section 17.11
        public int TotalCustomers { get; set; }
        public int TotalLeads { get; set; }
        public int OpenLeads { get; set; }
        public int TotalOpportunities { get; set; }
        public int OpenOpportunities { get; set; }
        public int WonOpportunities { get; set; }
        public int LostOpportunities { get; set; }
        public decimal TotalPipelineValue { get; set; }
        public decimal WeightedPipelineValue { get; set; }

        // Date Filter Selection (Section 4.3)
        public string DateFilter { get; set; } = "ThisMonth"; // Today, ThisWeek, ThisMonth, All
        public DateTime? CustomFrom { get; set; }
        public DateTime? CustomTo { get; set; }

        // Role Scope & User Context
        public string UserRole { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;

        // Admin Specific Stats
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int RecentAuditEventsCount { get; set; }

        // Upcoming Follow-ups & Recent Activities (role-scoped)
        public List<FollowUp> UpcomingFollowUps { get; set; } = new List<FollowUp>();
        public List<Activity> RecentActivities { get; set; } = new List<Activity>();
        public List<Opportunity> RecentOpportunities { get; set; } = new List<Opportunity>();

        // Chart Data Models for Chart.js (Section 17.12)
        public ChartDataModel LeadStatusChart { get; set; } = new ChartDataModel();
        public ChartDataModel OpportunityPipelineChart { get; set; } = new ChartDataModel();
        public ChartDataModel MonthlySalesChart { get; set; } = new ChartDataModel();
    }

    public class ChartDataModel
    {
        public List<string> Labels { get; set; } = new List<string>();
        public List<decimal> Data { get; set; } = new List<decimal>();
        public List<string> BackgroundColors { get; set; } = new List<string>();
    }
}
