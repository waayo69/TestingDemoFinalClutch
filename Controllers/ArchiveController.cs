using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TestingDemo.Models;
using TestingDemo.Data;

namespace TestingDemo.Controllers
{
    [Authorize(Roles = "Admin,DocumentOfficer")]
    public class ArchiveController : BaseController
    {
        private readonly ApplicationDbContext _context;

        public ArchiveController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Archive/Index
        public async Task<IActionResult> Index(string sortOrder, string currentFilter, string searchString, int? pageNumber, string TypeOfProject, string CreatedDateFrom, string CreatedDateTo)
        {
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = String.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["DateSortParm"] = sortOrder == "Date" ? "date_desc" : "Date";
            ViewData["TypeSortParm"] = sortOrder == "Type" ? "type_desc" : "Type";
            ViewData["StatusSortParm"] = sortOrder == "Status" ? "status_desc" : "Status";

            if (searchString != null)
            {
                pageNumber = 1;
            }
            else
            {
                searchString = currentFilter;
            }

            ViewData["CurrentFilter"] = searchString;

            var clients = from c in _context.Clients
                          where c.Status == "Archived"
                          select c;

            if (!String.IsNullOrEmpty(searchString))
            {
                clients = clients.Where(s => s.ClientName.Contains(searchString));
            }
            if (!String.IsNullOrEmpty(TypeOfProject))
            {
                clients = clients.Where(s => s.TypeOfProject == TypeOfProject);
            }
            if (!String.IsNullOrEmpty(CreatedDateFrom) && DateTime.TryParse(CreatedDateFrom, out var createdDateFrom))
            {
                clients = clients.Where(s => s.CreatedDate.Date >= createdDateFrom.Date);
            }
            if (!String.IsNullOrEmpty(CreatedDateTo) && DateTime.TryParse(CreatedDateTo, out var createdDateTo))
            {
                clients = clients.Where(s => s.CreatedDate.Date <= createdDateTo.Date);
            }

            switch (sortOrder)
            {
                case "name_desc":
                    clients = clients.OrderByDescending(s => s.ClientName);
                    break;
                case "Date":
                    clients = clients.OrderBy(s => s.CreatedDate);
                    break;
                case "date_desc":
                    clients = clients.OrderByDescending(s => s.CreatedDate);
                    break;
                case "Type":
                    clients = clients.OrderBy(s => s.TypeOfProject);
                    break;
                case "type_desc":
                    clients = clients.OrderByDescending(s => s.TypeOfProject);
                    break;
                case "Status":
                    clients = clients.OrderBy(s => s.Status);
                    break;
                case "status_desc":
                    clients = clients.OrderByDescending(s => s.Status);
                    break;
                default:
                    clients = clients.OrderBy(s => s.CreatedDate);
                    break;
            }

            int pageSize = 10;
            return View(await PaginatedList<ClientModel>.CreateAsync(clients.AsNoTracking(), pageNumber ?? 1, pageSize));
        }

        // GET: Archive/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var client = await _context.Clients
                .Include(c => c.RetainershipSPP)
                .Include(c => c.RetainershipBIR)
                .Include(c => c.OneTimeTransaction)
                .Include(c => c.ExternalAudit)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (client == null)
            {
                return NotFound();
            }

            ViewBag.Requirements = await _context.PermitRequirements
                .Include(r => r.Photos)
                .Where(r => r.ClientId == id)
                .OrderBy(r => r.CreatedDate)
                .ToListAsync();

            return View(client);
        }

        // GET: Archive/ViewRequirementFile/5
        public async Task<IActionResult> ViewRequirementFile(int id)
        {
            var photo = await _context.RequirementPhotos.FindAsync(id);
            if (photo == null)
            {
                return NotFound();
            }

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", photo.PhotoPath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var contentType = GetContentType(filePath);
            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var fileName = Path.GetFileName(photo.PhotoPath);

            return File(fileBytes, contentType, fileName);
        }

        private string GetContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream"
            };
        }

        [HttpGet]
        public async Task<IActionResult> GetClientDetails(int id)
        {
            var client = await _context.Clients
                .Include(c => c.RetainershipBIR)
                .Include(c => c.RetainershipSPP)
                .Include(c => c.OneTimeTransaction)
                .Include(c => c.ExternalAudit)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
                return NotFound();

            // Get requirements for this client with photos
            var requirements = await _context.PermitRequirements
                .Include(r => r.Photos)
                .Where(r => r.ClientId == id)
                .OrderBy(r => r.CreatedDate)
                .ToListAsync();

            return Json(new
            {
                id = client.Id,
                trackingNumber = client.TrackingNumber,
                clientName = client.ClientName,
                requestingParty = client.RequestingParty,
                requestorName = client.RequestorName,
                clientType = client.ClientType,
                taxId = client.TaxId,
                contactPersonNumber = client.ContactPersonNumber,
                contactPersonEmailAddress = client.ContactPersonEmailAddress,
                registeredCompanyName = client.RegisteredCompanyName,
                registeredCompanyAddress = client.RegisteredCompanyAddress,
                typeOfProject = client.TypeOfProject,
                urgencyLevel = client.UrgencyLevel,
                status = client.Status,
                planningReturnNote = client.PlanningReturnNote,
                otherTypeOfProject = client.OtherTypeOfProject,
                otherRequestingParty = client.OtherRequestingParty,
                createdDate = client.CreatedDate.ToString("yyyy-MM-dd HH:mm"),
                retainershipBIR = client.RetainershipBIR,
                retainershipSPP = client.RetainershipSPP,
                oneTimeTransaction = client.OneTimeTransaction,
                externalAudit = client.ExternalAudit,
                requirements = requirements.Select(r => new
                {
                    id = r.Id,
                    requirementName = r.RequirementName,
                    description = r.Description,
                    isRequired = r.IsRequired,
                    isCompleted = r.IsCompleted,
                    isPresent = r.IsPresent,
                    createdDate = r.CreatedDate.ToString("yyyy-MM-dd HH:mm"),
                    photos = r.Photos?.Select(p => new
                    {
                        id = p.Id,
                        photoPath = p.PhotoPath
                    })
                })
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetLatestData(string sortOrder, string currentFilter, string searchString, int? pageNumber, string TypeOfProject, string CreatedDateFrom, string CreatedDateTo)
        {
            var clients = from c in _context.Clients
                          where c.Status == "Archived"
                          select c;
            if (!string.IsNullOrEmpty(searchString))
                clients = clients.Where(s => s.ClientName.Contains(searchString));
            if (!string.IsNullOrEmpty(TypeOfProject))
                clients = clients.Where(s => s.TypeOfProject == TypeOfProject);
            if (!string.IsNullOrEmpty(CreatedDateFrom) && DateTime.TryParse(CreatedDateFrom, out var createdDateFrom))
                clients = clients.Where(s => s.CreatedDate.Date >= createdDateFrom.Date);
            if (!string.IsNullOrEmpty(CreatedDateTo) && DateTime.TryParse(CreatedDateTo, out var createdDateTo))
                clients = clients.Where(s => s.CreatedDate.Date <= createdDateTo.Date);
            switch (sortOrder)
            {
                case "name_desc":
                    clients = clients.OrderByDescending(s => s.ClientName);
                    break;
                case "Date":
                    clients = clients.OrderBy(s => s.CreatedDate);
                    break;
                case "date_desc":
                    clients = clients.OrderByDescending(s => s.CreatedDate);
                    break;
                case "Type":
                    clients = clients.OrderBy(s => s.TypeOfProject);
                    break;
                case "type_desc":
                    clients = clients.OrderByDescending(s => s.TypeOfProject);
                    break;
                case "Status":
                    clients = clients.OrderBy(s => s.Status);
                    break;
                case "status_desc":
                    clients = clients.OrderByDescending(s => s.Status);
                    break;
                default:
                    clients = clients.OrderBy(s => s.CreatedDate);
                    break;
            }
            int pageSize = 10;
            var paginated = await TestingDemo.Models.PaginatedList<TestingDemo.Models.ClientModel>.CreateAsync(clients.AsNoTracking(), pageNumber ?? 1, pageSize);
            return Json(paginated);
        }
    }
}