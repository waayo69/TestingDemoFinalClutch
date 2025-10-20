using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TestingDemo.Models;
using TestingDemo.ViewModels;
using ClosedXML.Excel;
using System.Collections.Generic;
using TestingDemo.Data;

namespace TestingDemo.Controllers
{
    [Authorize(Roles = "Admin,Finance")] //Roles that can do these tasks
    public class AnalyticsController : Controller
    {
        private readonly ApplicationDbContext _context;
        public AnalyticsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, string status, string projectType, string urgency, string supplierName, string businessType)
        {
            var clientsQuery = _context.Clients.AsQueryable();

            // Filtering
            if (startDate.HasValue)
                clientsQuery = clientsQuery.Where(c => c.CreatedDate >= startDate.Value);
            if (endDate.HasValue)
                clientsQuery = clientsQuery.Where(c => c.CreatedDate <= endDate.Value);
            if (!string.IsNullOrEmpty(status))
                clientsQuery = clientsQuery.Where(c => c.Status == status);
            if (!string.IsNullOrEmpty(projectType))
                clientsQuery = clientsQuery.Where(c => c.TypeOfProject == projectType);
            if (!string.IsNullOrEmpty(urgency))
                clientsQuery = clientsQuery.Where(c => c.UrgencyLevel == urgency);
            if (!string.IsNullOrEmpty(supplierName))
                clientsQuery = clientsQuery.Where(c => c.RequestingParty.Contains(supplierName));
            if (!string.IsNullOrEmpty(businessType))
                clientsQuery = clientsQuery.Where(c => c.ClientType.Contains(businessType));

            var clients = await clientsQuery.ToListAsync();
            var requirements = await _context.PermitRequirements.ToListAsync();

            // For dropdowns
            var allClients = await _context.Clients.ToListAsync();
            var allProjectTypes = allClients.Select(c => c.TypeOfProject).Distinct().OrderBy(x => x).ToList();
            var allStatuses = allClients.Select(c => c.Status).Distinct().OrderBy(x => x).ToList();
            var allUrgencies = allClients.Select(c => c.UrgencyLevel).Distinct().OrderBy(x => x).ToList();
            var allSupplierNames = allClients.Select(c => c.RequestingParty).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList();
            var allBusinessTypes = allClients.Select(c => c.ClientType).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList();

            // Trend: Urgent requests by month
            var urgentTrend = allClients
                .Where(c => c.UrgencyLevel == "Urgent")
                .GroupBy(c => c.CreatedDate.ToString("yyyy-MM"))
                .ToDictionary(g => g.Key, g => g.Count());

            // Forecast: Simple linear forecast (last 3 months average)
            var last3Months = allClients
                .Where(c => c.CreatedDate > DateTime.Now.AddMonths(-3))
                .GroupBy(c => c.CreatedDate.ToString("yyyy-MM"))
                .Select(g => g.Count())
                .ToList();
            int forecast = last3Months.Count > 0 ? (int)Math.Round(last3Months.Average()) : 0;

            // Heatmap: Requests per day
            var heatmap = allClients
                .GroupBy(c => c.CreatedDate.Date)
                .Select(g => new AnalyticsViewModel.HeatmapDay { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToList();

            // AcquiringRequestModel analytics
            var vendors = await _context.AcquiringRequests.ToListAsync();
            var supplierCountByBusinessType = vendors.GroupBy(v => v.BusinessType).ToDictionary(g => g.Key, g => g.Count());
            var productsOffered = vendors.SelectMany(v => (v.ProductsOffered ?? "").Split(",", StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()))
                .GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count());
            int totalVendors = vendors.Count;
            int approvedVendors = vendors.Count(v => v.Status == "Approved");
            // ExternalAuditModel analytics
            var audits = await _context.ExternalAudits.ToListAsync();
            var auditPurposeCounts = audits.SelectMany(a => (a.ExternalAuditPurposes ?? "").Split(",", StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()))
                .GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count());
            var upcomingDeadlines = audits.Where(a => a.ExternalAuditReportDate.HasValue && a.ExternalAuditReportDate.Value >= DateTime.Now)
                .OrderBy(a => a.ExternalAuditReportDate)
                .Select(a => new AnalyticsViewModel.AuditDeadline { ClientName = "N/A", Deadline = a.ExternalAuditReportDate })
                .ToList();
            int totalAudits = audits.Count;
            int completedAudits = audits.Count(a => a.ExternalAuditStatus == "Completed");
            double auditCompletionRate = totalAudits == 0 ? 0 : completedAudits * 100.0 / totalAudits;
            // Stacked bar: Projects by ClientType and Status
            var clientTypes = allClients.Select(c => c.ClientType).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList();
            var statuses = allClients.Select(c => c.Status).Distinct().OrderBy(x => x).ToList();
            var stackedCounts = clientTypes.Select(ct => statuses.Select(st => allClients.Count(c => c.ClientType == ct && c.Status == st)).ToList()).ToList();

            // Approval Funnel
            int submitted = allClients.Count();
            int inProgress = allClients.Count(c => c.Status == "In Progress");
            int approved = allClients.Count(c => c.Status == "Approved");
            var approvalFunnelCounts = new Dictionary<string, int> {
                { "Submitted", submitted },
                { "In Progress", inProgress },
                { "Approved", approved }
            };
            var approvalFunnelRates = new Dictionary<string, double> {
                { "Submitted→In Progress", submitted == 0 ? 0 : inProgress * 100.0 / submitted },
                { "In Progress→Approved", inProgress == 0 ? 0 : approved * 100.0 / inProgress }
            };
            // Client Repeat Rate (last 6 months)
            var sixMonthsAgo = DateTime.Now.AddMonths(-6);
            var repeatClients = allClients.Where(c => c.CreatedDate >= sixMonthsAgo)
                .GroupBy(c => c.ClientName)
                .Count(g => g.Count() > 1);

            // Expense analytics
            var expenses = await _context.Expenses.ToListAsync();
            var totalExpenses = expenses.Sum(e => e.Amount);
            var expensesByMonth = expenses
                .GroupBy(e => e.DueDate.ToString("yyyy-MM"))
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));
            var expensesByCategory = expenses
                .GroupBy(e => string.IsNullOrEmpty(e.Category) ? "Uncategorized" : e.Category)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));
            var expensesByStatus = expenses
                .GroupBy(e => e.Status)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

            var viewModel = new AnalyticsViewModel
            {
                TotalClients = clients.Count,
                ClientsByStatus = clients.GroupBy(c => c.Status).ToDictionary(g => g.Key, g => g.Count()),
                ClientsByType = clients.GroupBy(c => c.TypeOfProject).ToDictionary(g => g.Key, g => g.Count()),
                ClientsByUrgency = clients.GroupBy(c => c.UrgencyLevel).ToDictionary(g => g.Key, g => g.Count()),
                ClientsByMonth = clients.GroupBy(c => c.CreatedDate.ToString("yyyy-MM")).ToDictionary(g => g.Key, g => g.Count()),
                PermitCompletionRate = requirements.Count == 0 ? 0 : requirements.Count(r => r.IsCompleted) * 100.0 / requirements.Count,
                AllProjectTypes = allProjectTypes,
                AllStatuses = allStatuses,
                AllUrgencies = allUrgencies,
                AllSupplierNames = allSupplierNames,
                AllBusinessTypes = allBusinessTypes,
                FilteredClients = clients,
                UrgentRequestsTrend = urgentTrend,
                ForecastNextMonthProjects = forecast,
                DailyRequestCounts = heatmap,
                SupplierCountByBusinessType = supplierCountByBusinessType,
                MostCommonProductsOffered = productsOffered,
                TotalVendors = totalVendors,
                ApprovedVendors = approvedVendors,
                ExternalAuditPurposeCounts = auditPurposeCounts,
                UpcomingAuditDeadlines = upcomingDeadlines,
                TotalExternalAudits = totalAudits,
                CompletedExternalAudits = completedAudits,
                ExternalAuditCompletionRate = auditCompletionRate,
                StackedClientTypes = clientTypes,
                StackedStatuses = statuses,
                StackedClientTypeStatusCounts = stackedCounts,
                ApprovalFunnelCounts = approvalFunnelCounts,
                ApprovalFunnelRates = approvalFunnelRates,
                ClientRepeatRate = repeatClients,
                TotalExpenses = totalExpenses,
                ExpensesByMonth = expensesByMonth,
                ExpensesByCategory = expensesByCategory,
                ExpensesByStatus = expensesByStatus,
                AllExpenses = expenses
            };
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(DateTime? startDate, DateTime? endDate, string status, string projectType, string urgency, string supplierName, string businessType)
        {
            var clientsQuery = _context.Clients.AsQueryable();
            if (startDate.HasValue)
                clientsQuery = clientsQuery.Where(c => c.CreatedDate >= startDate.Value);
            if (endDate.HasValue)
                clientsQuery = clientsQuery.Where(c => c.CreatedDate <= endDate.Value);
            if (!string.IsNullOrEmpty(status))
                clientsQuery = clientsQuery.Where(c => c.Status == status);
            if (!string.IsNullOrEmpty(projectType))
                clientsQuery = clientsQuery.Where(c => c.TypeOfProject == projectType);
            if (!string.IsNullOrEmpty(urgency))
                clientsQuery = clientsQuery.Where(c => c.UrgencyLevel == urgency);
            if (!string.IsNullOrEmpty(supplierName))
                clientsQuery = clientsQuery.Where(c => c.RequestingParty.Contains(supplierName));
            if (!string.IsNullOrEmpty(businessType))
                clientsQuery = clientsQuery.Where(c => c.ClientType.Contains(businessType));
            var clients = await clientsQuery.ToListAsync();

            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Clients");
                // Header
                ws.Cell(1, 1).Value = "Client";
                ws.Cell(1, 2).Value = "Type";
                ws.Cell(1, 3).Value = "Urgency";
                ws.Cell(1, 4).Value = "Days Pending";
                ws.Cell(1, 5).Value = "Status";
                ws.Cell(1, 6).Value = "Supplier Name";
                ws.Cell(1, 7).Value = "Business Type";
                ws.Cell(1, 8).Value = "Created";
                int row = 2;
                foreach (var c in clients)
                {
                    ws.Cell(row, 1).Value = c.ClientName;
                    ws.Cell(row, 2).Value = c.TypeOfProject;
                    ws.Cell(row, 3).Value = c.UrgencyLevel;
                    ws.Cell(row, 4).Value = (DateTime.Now - c.CreatedDate).Days;
                    ws.Cell(row, 5).Value = c.Status;
                    ws.Cell(row, 6).Value = c.RequestingParty;
                    ws.Cell(row, 7).Value = c.ClientType;
                    ws.Cell(row, 8).Value = c.CreatedDate.ToShortDateString();
                    row++;
                }
                using (var stream = new System.IO.MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Seek(0, System.IO.SeekOrigin.Begin);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Clients.xlsx");
                }
            }
        }

        [HttpGet]
        public IActionResult ExportPdf(DateTime? startDate, DateTime? endDate, string status, string projectType, string urgency, string supplierName, string businessType)
        {
            // TODO: Implement PDF export logic
            return Content("PDF export not yet implemented.");
        }

        [HttpGet]
        public async Task<IActionResult> MonthlyReport(string month)
        {
            if (string.IsNullOrEmpty(month))
                month = DateTime.Now.ToString("yyyy-MM");
            DateTime monthStart = DateTime.ParseExact(month + "-01", "yyyy-MM-dd", null);
            DateTime monthEnd = monthStart.AddMonths(1);

            var clients = await _context.Clients
                .Where(c => c.CreatedDate >= monthStart && c.CreatedDate < monthEnd)
                .ToListAsync();
            var requirements = await _context.PermitRequirements.ToListAsync();

            var viewModel = new AnalyticsViewModel
            {
                TotalClients = clients.Count,
                ClientsByStatus = clients.GroupBy(c => c.Status).ToDictionary(g => g.Key, g => g.Count()),
                ClientsByType = clients.GroupBy(c => c.TypeOfProject).ToDictionary(g => g.Key, g => g.Count()),
                ClientsByUrgency = clients.GroupBy(c => c.UrgencyLevel).ToDictionary(g => g.Key, g => g.Count()),
                ClientsByMonth = clients.GroupBy(c => c.CreatedDate.ToString("yyyy-MM")).ToDictionary(g => g.Key, g => g.Count()),
                PermitCompletionRate = requirements.Count == 0 ? 0 : requirements.Count(r => r.IsCompleted) * 100.0 / requirements.Count,
                FilteredClients = clients
            };
            ViewBag.Month = monthStart.ToString("MMMM yyyy");
            return View("MonthlyReport", viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetLatestData(DateTime? startDate, DateTime? endDate, string status, string projectType, string urgency, string supplierName, string businessType)
        {
            var clientsQuery = _context.Clients.AsQueryable();
            if (startDate.HasValue)
                clientsQuery = clientsQuery.Where(c => c.CreatedDate >= startDate.Value);
            if (endDate.HasValue)
                clientsQuery = clientsQuery.Where(c => c.CreatedDate <= endDate.Value);
            if (!string.IsNullOrEmpty(status))
                clientsQuery = clientsQuery.Where(c => c.Status == status);
            if (!string.IsNullOrEmpty(projectType))
                clientsQuery = clientsQuery.Where(c => c.TypeOfProject == projectType);
            if (!string.IsNullOrEmpty(urgency))
                clientsQuery = clientsQuery.Where(c => c.UrgencyLevel == urgency);
            if (!string.IsNullOrEmpty(supplierName))
                clientsQuery = clientsQuery.Where(c => c.RequestingParty.Contains(supplierName));
            if (!string.IsNullOrEmpty(businessType))
                clientsQuery = clientsQuery.Where(c => c.ClientType.Contains(businessType));
            var clients = await clientsQuery.ToListAsync();
            var requirements = await _context.PermitRequirements.ToListAsync();
            var allClients = await _context.Clients.ToListAsync();
            var allProjectTypes = allClients.Select(c => c.TypeOfProject).Distinct().OrderBy(x => x).ToList();
            var allStatuses = allClients.Select(c => c.Status).Distinct().OrderBy(x => x).ToList();
            var allUrgencies = allClients.Select(c => c.UrgencyLevel).Distinct().OrderBy(x => x).ToList();
            var allSupplierNames = allClients.Select(c => c.RequestingParty).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList();
            var allBusinessTypes = allClients.Select(c => c.ClientType).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList();
            var urgentTrend = allClients.Where(c => c.UrgencyLevel == "Urgent").GroupBy(c => c.CreatedDate.ToString("yyyy-MM")).ToDictionary(g => g.Key, g => g.Count());
            var last3Months = allClients.Where(c => c.CreatedDate > DateTime.Now.AddMonths(-3)).GroupBy(c => c.CreatedDate.ToString("yyyy-MM")).Select(g => g.Count()).ToList();
            int forecast = last3Months.Count > 0 ? (int)Math.Round(last3Months.Average()) : 0;
            var heatmap = allClients.GroupBy(c => c.CreatedDate.Date).Select(g => new TestingDemo.ViewModels.AnalyticsViewModel.HeatmapDay { Date = g.Key, Count = g.Count() }).OrderBy(x => x.Date).ToList();
            var vendors = await _context.AcquiringRequests.ToListAsync();
            var supplierCountByBusinessType = vendors.GroupBy(v => v.BusinessType).ToDictionary(g => g.Key, g => g.Count());
            var productsOffered = vendors.SelectMany(v => (v.ProductsOffered ?? "").Split(",", StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim())).GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count());
            int totalVendors = vendors.Count;
            int approvedVendors = vendors.Count(v => v.Status == "Approved");
            var audits = await _context.ExternalAudits.ToListAsync();
            var auditPurposeCounts = audits.SelectMany(a => (a.ExternalAuditPurposes ?? "").Split(",", StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim())).GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count());
            var upcomingDeadlines = audits.Where(a => a.ExternalAuditReportDate.HasValue && a.ExternalAuditReportDate.Value >= DateTime.Now).OrderBy(a => a.ExternalAuditReportDate).Select(a => new TestingDemo.ViewModels.AnalyticsViewModel.AuditDeadline { ClientName = "N/A", Deadline = a.ExternalAuditReportDate }).ToList();
            int totalAudits = audits.Count;
            int completedAudits = audits.Count(a => a.ExternalAuditStatus == "Completed");
            double auditCompletionRate = totalAudits == 0 ? 0 : completedAudits * 100.0 / totalAudits;
            var clientTypes = allClients.Select(c => c.ClientType).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList();
            var statuses = allClients.Select(c => c.Status).Distinct().OrderBy(x => x).ToList();
            var stackedCounts = clientTypes.Select(ct => statuses.Select(st => allClients.Count(c => c.ClientType == ct && c.Status == st)).ToList()).ToList();
            int submitted = allClients.Count();
            int inProgress = allClients.Count(c => c.Status == "In Progress");
            int approved = allClients.Count(c => c.Status == "Approved");
            var approvalFunnelCounts = new Dictionary<string, int> {
                { "Submitted", submitted },
                { "In Progress", inProgress },
                { "Approved", approved }
            };
            var approvalFunnelRates = new Dictionary<string, double> {
                { "Submitted→In Progress", submitted == 0 ? 0 : inProgress * 100.0 / submitted },
                { "In Progress→Approved", inProgress == 0 ? 0 : approved * 100.0 / inProgress }
            };
            var sixMonthsAgo = DateTime.Now.AddMonths(-6);
            var repeatClients = allClients.Where(c => c.CreatedDate >= sixMonthsAgo).GroupBy(c => c.ClientName).Count(g => g.Count() > 1);
            var viewModel = new TestingDemo.ViewModels.AnalyticsViewModel
            {
                TotalClients = clients.Count,
                ClientsByStatus = clients.GroupBy(c => c.Status).ToDictionary(g => g.Key, g => g.Count()),
                ClientsByType = clients.GroupBy(c => c.TypeOfProject).ToDictionary(g => g.Key, g => g.Count()),
                ClientsByUrgency = clients.GroupBy(c => c.UrgencyLevel).ToDictionary(g => g.Key, g => g.Count()),
                ClientsByMonth = clients.GroupBy(c => c.CreatedDate.ToString("yyyy-MM")).ToDictionary(g => g.Key, g => g.Count()),
                PermitCompletionRate = requirements.Count == 0 ? 0 : requirements.Count(r => r.IsCompleted) * 100.0 / requirements.Count,
                AllProjectTypes = allProjectTypes,
                AllStatuses = allStatuses,
                AllUrgencies = allUrgencies,
                AllSupplierNames = allSupplierNames,
                AllBusinessTypes = allBusinessTypes,
                FilteredClients = clients,
                UrgentRequestsTrend = urgentTrend,
                ForecastNextMonthProjects = forecast,
                DailyRequestCounts = heatmap,
                SupplierCountByBusinessType = supplierCountByBusinessType,
                MostCommonProductsOffered = productsOffered,
                TotalVendors = totalVendors,
                ApprovedVendors = approvedVendors,
                ExternalAuditPurposeCounts = auditPurposeCounts,
                UpcomingAuditDeadlines = upcomingDeadlines,
                TotalExternalAudits = totalAudits,
                CompletedExternalAudits = completedAudits,
                ExternalAuditCompletionRate = auditCompletionRate,
                StackedClientTypes = clientTypes,
                StackedStatuses = statuses,
                StackedClientTypeStatusCounts = stackedCounts,
                ApprovalFunnelCounts = approvalFunnelCounts,
                ApprovalFunnelRates = approvalFunnelRates,
                ClientRepeatRate = repeatClients
            };
            return Json(viewModel);
        }
    }
}