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
            var clients = await _context.Clients.ToListAsync();

            var model = new TestingDemo.ViewModels.DashboardViewModel
            {
                LiaisonClients = clients.Where(c => c.Status == "Liaison").ToList(),
                FinanceClients = clients.Where(c => c.Status == "Pending" || c.Status == "Finance" || c.Status == "Clearance").ToList(),
                PlanningClients = clients.Where(c => c.Status == "Planning").ToList(),
                ReceivedClients = clients.Where(c => c.Status == "CustomerCareReceived").ToList(),
                DocumentationClients = clients.Where(c => c.Status == "DocumentOfficer").ToList()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetLatestData()
        {
            var clients = await _context.Clients.ToListAsync();
            var model = new TestingDemo.ViewModels.DashboardViewModel
            {
                LiaisonClients = clients.Where(c => c.Status == "Liaison").ToList(),
                FinanceClients = clients.Where(c => c.Status == "Pending" || c.Status == "Finance" || c.Status == "Clearance").ToList(),
                PlanningClients = clients.Where(c => c.Status == "Planning").ToList(),
                ReceivedClients = clients.Where(c => c.Status == "CustomerCareReceived").ToList(),
                DocumentationClients = clients.Where(c => c.Status == "DocumentOfficer").ToList()
            };
            return Json(model);
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
