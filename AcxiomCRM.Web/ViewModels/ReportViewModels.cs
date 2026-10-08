using System;
using System.Collections.Generic;

namespace AcxiomCRM.Web.ViewModels
{
    public class ReportHubViewModel
    {
        public int TotalCustomers { get; set; }
        public int TotalLeads { get; set; }
        public int TotalOpportunities { get; set; }
        public int TotalFollowUps { get; set; }
        public decimal PipelineSum { get; set; }
        public decimal WonSum { get; set; }
        public double LeadConversionRate { get; set; }
    }

    public class PipelineReportRow
    {
        public string Stage { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal WeightedAmount { get; set; }
        public double PercentageOfPipeline { get; set; }
    }

    public class ConversionReportModel
    {
        public int TotalLeads { get; set; }
        public int ConvertedLeads { get; set; }
        public double LeadConversionRate { get; set; }
        public int TotalClosedOpps { get; set; }
        public int WonOpps { get; set; }
        public int LostOpps { get; set; }
        public double OpportunityWinRate { get; set; }
        public decimal TotalWonRevenue { get; set; }
    }

    public class UserActivityReportRow
    {
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int CustomersCreated { get; set; }
        public int LeadsAssigned { get; set; }
        public int OpportunitiesCount { get; set; }
        public int ActivitiesLogged { get; set; }
        public int FollowUpsScheduled { get; set; }
    }
}
