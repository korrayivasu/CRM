namespace AcxiomCRM.Web.Common
{
    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string Manager = "Manager";
        public const string SalesExecutive = "SalesExecutive";

        public static readonly string[] All = { Admin, Manager, SalesExecutive };
    }

    public static class LeadStatuses
    {
        public const string New = "New";
        public const string Contacted = "Contacted";
        public const string Qualified = "Qualified";
        public const string Unqualified = "Unqualified";
        public const string Converted = "Converted";
        public const string Lost = "Lost";

        public static readonly string[] All = { New, Contacted, Qualified, Unqualified, Converted, Lost };
    }

    public static class OpportunityStages
    {
        public const string Qualification = "Qualification";
        public const string Proposal = "Proposal";
        public const string Negotiation = "Negotiation";
        public const string Won = "Won";
        public const string Lost = "Lost";

        public static readonly string[] All = { Qualification, Proposal, Negotiation, Won, Lost };
    }

    public static class FollowUpStatuses
    {
        public const string Planned = "Planned";
        public const string Completed = "Completed";
        public const string Missed = "Missed";
        public const string Cancelled = "Cancelled";

        public static readonly string[] All = { Planned, Completed, Missed, Cancelled };
    }

    public static class ActivityTypes
    {
        public const string Call = "Call";
        public const string Meeting = "Meeting";
        public const string Email = "Email";
        public const string Task = "Task";

        public static readonly string[] All = { Call, Meeting, Email, Task };
    }

    public static class CustomerStatuses
    {
        public const string Active = "Active";
        public const string Inactive = "Inactive";
        public const string Prospect = "Prospect";

        public static readonly string[] All = { Active, Inactive, Prospect };
    }

    public static class AuditActions
    {
        public const string Login = "Login";
        public const string FailedLogin = "Failed Login";
        public const string Logout = "Logout";
        public const string Lockout = "Lockout";
        public const string Create = "Create";
        public const string Update = "Update";
        public const string Delete = "Delete";
        public const string RoleChange = "Role Change";
        public const string Security = "Security";
        public const string LeadConverted = "Lead Converted";
    }
}
