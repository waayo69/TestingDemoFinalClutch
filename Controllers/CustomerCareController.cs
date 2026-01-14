using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using TestingDemo.Models;
using TestingDemo.ViewModels;
using Microsoft.AspNetCore.SignalR;
using TestingDemo.Data;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace TestingDemo.Controllers
{
    [Authorize(Roles = "CustomerCare,Admin")]
    public class CustomerCareController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public CustomerCareController(ApplicationDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // GET: CustomerCare/Index
        public async Task<IActionResult> Index(string sortOrder, string searchString, int? liaisonPageNumber, int? completedPageNumber)
        {
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = String.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["DateSortParm"] = sortOrder == "Date" ? "date_desc" : "Date";
            ViewData["CurrentFilter"] = searchString;

            int pageSize = 10;

            // Query for Liaison Clients
            var liaisonQuery = _context.Clients
                .Where(c => c.Status == "CustomerCare")
                .Include(c => c.RetainershipBIR)
                .Include(c => c.RetainershipSPP)
                .Include(c => c.OneTimeTransaction)
                .Include(c => c.ExternalAudit)
                .AsNoTracking();

            // Query for Sent/Completed Clients
            var completedQuery = _context.Clients
                .Where(c => c.Status == "DocumentOfficer" || c.Status == "Completed" || c.Status == "FinanceProgress")
                .Include(c => c.RetainershipBIR)
                .Include(c => c.RetainershipSPP)
                .Include(c => c.OneTimeTransaction)
                .Include(c => c.ExternalAudit)
                .AsNoTracking();

            if (!String.IsNullOrEmpty(searchString))
            {
                liaisonQuery = liaisonQuery.Where(s => s.ClientName.Contains(searchString) || s.TypeOfProject.Contains(searchString) || s.TrackingNumber.Contains(searchString));
                completedQuery = completedQuery.Where(s => s.ClientName.Contains(searchString) || s.TypeOfProject.Contains(searchString) || s.TrackingNumber.Contains(searchString));
            }

            switch (sortOrder)
            {
                case "name_desc":
                    liaisonQuery = liaisonQuery.OrderByDescending(s => s.ClientName);
                    completedQuery = completedQuery.OrderByDescending(s => s.ClientName);
                    break;
                case "Date":
                    liaisonQuery = liaisonQuery.OrderBy(s => s.CreatedDate);
                    completedQuery = completedQuery.OrderBy(s => s.CreatedDate);
                    break;
                case "date_desc":
                    liaisonQuery = liaisonQuery.OrderByDescending(s => s.CreatedDate);
                    completedQuery = completedQuery.OrderByDescending(s => s.CreatedDate);
                    break;
                default:
                    liaisonQuery = liaisonQuery.OrderByDescending(s => s.CreatedDate);
                    completedQuery = completedQuery.OrderByDescending(s => s.CreatedDate);
                    break;
            }

            var viewModel = new CustomerCareDashboardViewModel
            {
                LiaisonClients = await PaginatedList<ClientModel>.CreateAsync(liaisonQuery, liaisonPageNumber ?? 1, pageSize),
                CompletedClients = await PaginatedList<ClientModel>.CreateAsync(completedQuery, completedPageNumber ?? 1, pageSize),
                CurrentSort = sortOrder,
                NameSortParm = ViewData["NameSortParm"]?.ToString(),
                DateSortParm = ViewData["DateSortParm"]?.ToString()
            };

            return View(viewModel);
        }

        // GET: CustomerCare/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();
            var client = await _context.Clients
                .Include(c => c.RetainershipBIR)
                .Include(c => c.RetainershipSPP)
                .Include(c => c.OneTimeTransaction)
                .Include(c => c.ExternalAudit)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (client == null)
                return NotFound();
            var requirements = await _context.PermitRequirements
                .Where(r => r.ClientId == id)
                .Include(r => r.Photos)
                .ToListAsync();
            ViewBag.Requirements = requirements;
            if (Request.Query.ContainsKey("partial"))
            {
                ViewBag.IsPartial = true;
            }
            return View(client);
        }

        // POST: CustomerCare/ProceedToDocumentOfficer/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProceedToDocumentOfficer(int id)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client == null)
                return RedirectToAction("Index");

            var reqs = await _context.PermitRequirements
                .Include(r => r.Photos)
                .Where(r => r.ClientId == id)
                .ToListAsync();

            var hasMissing = reqs.Any(r => r.IsRequired && (r.Photos == null || r.Photos.Count == 0));
            if (hasMissing)
            {
                TempData["ErrorMessage"] = "All required requirements must have at least one file uploaded before sending to Documentation.";
                return RedirectToAction("Index");
            }

            client.Status = "DocumentOfficer";
            client.SubStatus = "New";
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "CustomerCare data changed");
            TempData["SuccessMessage"] = "Client moved to Document Officer.";
            return RedirectToAction("Index");
        }

        // POST: CustomerCare/ReturnToPlanning/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnToPlanning(int id)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client != null)
            {
                client.Status = "Planning";
                client.SubStatus = "For Review";
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Client returned to Planning Officer.";
            }
            return RedirectToAction("Index");
        }

        // POST: CustomerCare/SaveRequirements/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRequirements(int id)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client == null)
                return NotFound();

            var requirements = await _context.PermitRequirements.Where(r => r.ClientId == id).ToListAsync();

            foreach (var requirement in requirements)
            {
                var isPresentKey = $"present_{requirement.Id}";
                requirement.IsPresent = Request.Form.ContainsKey(isPresentKey);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Requirements inspection saved successfully.";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Ok(new { success = true, message = "Saved" });
            }
            return RedirectToAction("Index");
        }

        // GET: CustomerCare/GetLatestData
        [HttpGet]
        public async Task<IActionResult> GetLatestData(string sortOrder, string searchString, int? liaisonPageNumber, int? completedPageNumber)
        {
            int pageSize = 10;

            var liaisonQuery = _context.Clients
                .Where(c => c.Status == "CustomerCare")
                .AsNoTracking();

            var completedQuery = _context.Clients
                .Where(c => c.Status == "DocumentOfficer" || c.Status == "Completed" || c.Status == "FinanceProgress")
                .AsNoTracking();

            if (!string.IsNullOrEmpty(searchString))
            {
                liaisonQuery = liaisonQuery.Where(s => s.ClientName.Contains(searchString) || s.TypeOfProject.Contains(searchString) || s.TrackingNumber.Contains(searchString));
                completedQuery = completedQuery.Where(s => s.ClientName.Contains(searchString) || s.TypeOfProject.Contains(searchString) || s.TrackingNumber.Contains(searchString));
            }

            // Simplified sorting for AJAX
            liaisonQuery = liaisonQuery.OrderByDescending(c => c.CreatedDate);
            completedQuery = completedQuery.OrderByDescending(c => c.CreatedDate);

            var viewModel = new CustomerCareDashboardViewModel
            {
                LiaisonClients = await PaginatedList<ClientModel>.CreateAsync(liaisonQuery, liaisonPageNumber ?? 1, pageSize),
                CompletedClients = await PaginatedList<ClientModel>.CreateAsync(completedQuery, completedPageNumber ?? 1, pageSize)
            };

            return Json(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetRequirementSummary(int id)
        {
            var reqs = await _context.PermitRequirements
                .Include(r => r.Photos)
                .Where(r => r.ClientId == id)
                .Select(r => new
                {
                    r.Id,
                    r.RequirementName,
                    r.IsRequired,
                    Files = r.Photos.Count
                })
                .ToListAsync();
            return Json(new { items = reqs });
        }

        [Authorize(Roles = "Admin,CustomerCare")]
        public async Task<IActionResult> TrackingNumbers(string sortOrder)
        {
            ViewData["TrackingSortParm"] = String.IsNullOrEmpty(sortOrder) ? "tracking_desc" : "";
            ViewData["NameSortParm"] = sortOrder == "Name" ? "name_desc" : "Name";
            ViewData["DateSortParm"] = sortOrder == "Date" ? "date_desc" : "Date";

            var clients = _context.Clients.AsQueryable();

            switch (sortOrder)
            {
                case "tracking_desc":
                    clients = clients.OrderByDescending(c => c.TrackingNumber);
                    break;
                case "Name":
                    clients = clients.OrderBy(c => c.ClientName);
                    break;
                case "name_desc":
                    clients = clients.OrderByDescending(c => c.ClientName);
                    break;
                case "Date":
                    clients = clients.OrderBy(c => c.CreatedDate);
                    break;
                case "date_desc":
                    clients = clients.OrderByDescending(c => c.CreatedDate);
                    break;
                default:
                    clients = clients.OrderByDescending(c => c.CreatedDate);
                    break;
            }

            var result = await clients.Select(c => new
            {
                c.ClientName,
                c.TypeOfProject,
                c.Status,
                c.TrackingNumber,
                c.CreatedDate
            }).ToListAsync();

            return View(result);
        }

        // POST: CustomerCare/UploadRequirementFiles
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadRequirementFiles(int requirementId, List<IFormFile> files)
        {
            var requirement = await _context.PermitRequirements.FindAsync(requirementId);
            if (requirement == null)
                return NotFound();

            if (files != null && files.Count > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "proof-photos");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                foreach (var file in files)
                {
                    if (file != null && file.Length > 0)
                    {
                        var fileName = $"proof_{requirementId}_{System.DateTime.Now:yyyyMMddHHmmssfff}_{Path.GetFileName(file.FileName)}";
                        var filePath = Path.Combine(uploadsFolder, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }
                        var reqPhoto = new RequirementPhoto
                        {
                            RequirementId = requirementId,
                            PhotoPath = $"/uploads/proof-photos/{fileName}"
                        };
                        _context.RequirementPhotos.Add(reqPhoto);
                    }
                }
                // Mark requirement as completed once files are uploaded
                requirement.IsCompleted = true;
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "CustomerCare data changed");
                TempData["SuccessMessage"] = "Files uploaded successfully.";
            }
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var count = await _context.RequirementPhotos.CountAsync(p => p.RequirementId == requirementId);
                return Ok(new { success = true, requirementId, files = count });
            }
            return RedirectToAction(nameof(Details), new { id = requirement.ClientId });
        }

        // POST: CustomerCare/DeleteRequirementPhoto
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRequirementPhoto(int photoId)
        {
            var photo = await _context.RequirementPhotos.FindAsync(photoId);
            if (photo == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return NotFound();
                return RedirectToAction(nameof(Index));
            }

            var requirementId = photo.RequirementId;
            var requirement = await _context.PermitRequirements.FindAsync(requirementId);

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", photo.PhotoPath.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                try { System.IO.File.Delete(filePath); } catch { }
            }
            _context.RequirementPhotos.Remove(photo);
            // Determine remaining files for this requirement excluding the one marked for deletion
            var remaining = await _context.RequirementPhotos.CountAsync(p => p.RequirementId == requirementId && p.Id != photoId);
            // Mark requirement as Pending if no files remain
            if (requirement != null)
            {
                requirement.IsCompleted = remaining > 0;
            }
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "CustomerCare data changed");
            TempData["SuccessMessage"] = "Photo deleted successfully.";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var reqId = requirement?.Id ?? 0;
                var count = await _context.RequirementPhotos.CountAsync(p => p.RequirementId == reqId);
                return Ok(new { success = true, requirementId = reqId, files = count });
            }
            return RedirectToAction(nameof(Details), new { id = requirement?.ClientId });
        }
    }
}