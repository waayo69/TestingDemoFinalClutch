using System;
using System.Collections.Generic;

namespace TestingDemo.ViewModels
{
    public class AnalyticsViewModel
    {
        public int TotalClients { get; set; }
        public Dictionary<string, int> ClientsByStatus { get; set; }
        public Dictionary<string, int> ClientsByType { get; set; }
        public Dictionary<string, int> ClientsByUrgency { get; set; }
        public Dictionary<string, int> ClientsByMonth { get; set; }
        public double PermitCompletionRate { get; set; }
        public List<string> AllProjectTypes { get; set; }
        public List<string> AllStatuses { get; set; }
        public List<string> AllUrgencies { get; set; }
        public List<string> AllSupplierNames { get; set; }
        public List<string> AllBusinessTypes { get; set; }
        public List<TestingDemo.Models.ClientModel> FilteredClients { get; set; }
        public Dictionary<string, int> UrgentRequestsTrend { get; set; }
        public int ForecastNextMonthProjects { get; set; }
        public List<HeatmapDay> DailyRequestCounts { get; set; }
        public class HeatmapDay { public DateTime Date { get; set; } public int Count { get; set; } }
        // AcquiringRequestModel analytics
        public Dictionary<string, int> SupplierCountByBusinessType { get; set; }
        public Dictionary<string, int> MostCommonProductsOffered { get; set; }
        public int TotalVendors { get; set; }
        public int ApprovedVendors { get; set; }
        // ExternalAuditModel analytics
        public Dictionary<string, int> ExternalAuditPurposeCounts { get; set; }
        public List<AuditDeadline> UpcomingAuditDeadlines { get; set; }
        public int TotalExternalAudits { get; set; }
        public int CompletedExternalAudits { get; set; }
        public double ExternalAuditCompletionRate { get; set; }
        public class AuditDeadline { public string ClientName { get; set; } public DateTime? Deadline { get; set; } }
        // Stacked bar: Projects by ClientType and Status
        public List<string> StackedClientTypes { get; set; }
        public List<string> StackedStatuses { get; set; }
        public List<List<int>> StackedClientTypeStatusCounts { get; set; }
        // Approval funnel analytics
        public Dictionary<string, int> ApprovalFunnelCounts { get; set; }
        public Dictionary<string, double> ApprovalFunnelRates { get; set; }
        // Client repeat rate
        public int ClientRepeatRate { get; set; }
        // Expense analytics
        public decimal TotalExpenses { get; set; }
        public Dictionary<string, decimal> ExpensesByMonth { get; set; }
        public Dictionary<string, decimal> ExpensesByCategory { get; set; }
        public Dictionary<string, decimal> ExpensesByStatus { get; set; }
        public List<TestingDemo.Models.ExpenseModel> AllExpenses { get; set; }
    }
}