using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Threading.Tasks;
using TestingDemo.Models;
using TestingDemo.Data;

namespace TestingDemo.Controllers
{
    [Authorize]
    public class HomeController : BaseController
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context; // ✅ Add database context

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var model = await GetDashboardData();
            return View(model);
        }

        public async Task<IActionResult> Monitor()
        {
            var model = await GetDashboardData();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetLatestData()
        {
            var model = await GetDashboardData();
            return Json(model);
        }

        private async Task<TestingDemo.ViewModels.DashboardViewModel> GetDashboardData()
        {
            var clients = await _context.Clients.ToListAsync();
            var users = await _context.Users.ToListAsync();

            string GetUserName(string? userId) => users.FirstOrDefault(u => u.Id == userId)?.FullName ?? (users.FirstOrDefault(u => u.Id == userId)?.UserName ?? "Unassigned");

            var model = new TestingDemo.ViewModels.DashboardViewModel();

            foreach (var client in clients)
            {
                var item = new TestingDemo.ViewModels.ClientQueueItem
                {
                    Client = client,
                };

                if (client.Status == "Liaison" || client.Status == "CustomerCare" || client.Status == "CustomerCareReceived")
                {
                    item.AssignedUserName = GetUserName(client.AssignedCustomerCareId);
                    if (client.Status == "Liaison") model.LiaisonClients.Add(item);
                    else model.ReceivedClients.Add(item);
                }
                else if (client.Status == "Pending" || client.Status == "Finance")
                {
                    item.AssignedUserName = GetUserName(client.AssignedFinanceId);
                    model.FinanceClients.Add(item);
                }
                else if (client.Status == "Clearance" || (client.Status == "Archived" && client.SubStatus == "Ready for Claiming"))
                {
                    item.AssignedUserName = GetUserName(client.AssignedFinanceId);
                    model.ClearanceClients.Add(item);
                }
                else if (client.Status == "Planning")
                {
                    item.AssignedUserName = GetUserName(client.AssignedPlanningOfficerId);
                    model.PlanningClients.Add(item);
                }
                else if (client.Status == "DocumentOfficer")
                {
                    item.AssignedUserName = GetUserName(client.AssignedDocumentOfficerId);
                    model.DocumentationClients.Add(item);
                }
            }

            return model;
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult TrackRequest()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> TrackRequest(string trackingNumber)
        {
            if (string.IsNullOrWhiteSpace(trackingNumber))
            {
                ViewBag.Error = "Please enter your tracking number.";
                return View();
            }
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.TrackingNumber == trackingNumber);
            if (client == null)
            {
                ViewBag.Error = "Tracking number not found. Please check and try again.";
                return View();
            }
            return View("TrackResult", client);
        }
    }
}
