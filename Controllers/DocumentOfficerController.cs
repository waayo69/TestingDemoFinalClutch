using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using TestingDemo.Models;
using Microsoft.AspNetCore.SignalR;
using TestingDemo.Data;

namespace TestingDemo.Controllers
{
    [Authorize(Roles = "DocumentOfficer,Admin")]
    public class DocumentOfficerController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public DocumentOfficerController(ApplicationDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // GET: DocumentOfficer/Index
        public async Task<IActionResult> Index()
        {
            var clients = await _context.Clients
                .Where(c => c.Status == "DocumentOfficer")
                .Include(c => c.RetainershipBIR)
                .Include(c => c.RetainershipSPP)
                .Include(c => c.OneTimeTransaction)
                .Include(c => c.ExternalAudit)
                .AsNoTracking()
                .ToListAsync();
            return View(clients);
        }

        // GET: DocumentOfficer/Details/5
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

            // Load permit requirements with their photos for this client
            ViewBag.PermitRequirements = await _context.PermitRequirements
                .Include(pr => pr.Photos)
                .Where(pr => pr.ClientId == id)
                .ToListAsync();

            return View(client);
        }

        // POST: DocumentOfficer/ProceedToFinance/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProceedToFinance(int id)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client != null)
            {
                client.Status = "Clearance";
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "DocumentOfficer data changed");
                TempData["SuccessMessage"] = "Client has been sent to Finance for clearance.";
            }
            return RedirectToAction("Index");
        }

        // POST: DocumentOfficer/ReturnToCustomerCare/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnToCustomerCare(int id)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client != null)
            {
                client.Status = "Liaison";
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "DocumentOfficer data changed");
                TempData["SuccessMessage"] = "Client returned to Customer Care (Liaison).";
            }
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> GetLatestData()
        {
            var clients = await _context.Clients
                .Where(c => c.Status == "DocumentOfficer")
                .Include(c => c.RetainershipBIR)
                .Include(c => c.RetainershipSPP)
                .Include(c => c.OneTimeTransaction)
                .Include(c => c.ExternalAudit)
                .AsNoTracking()
                .ToListAsync();
            return Json(clients);
        }

        // GET: DocumentOfficer/ViewRequirementFile/5
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
                return NotFound("File not found on server.");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var fileName = Path.GetFileName(photo.PhotoPath);
            var contentType = GetContentType(fileName);

            return File(fileBytes, contentType, fileName);
        }

        // GET: DocumentOfficer/GetRequirementFiles/5
        [HttpGet]
        public async Task<IActionResult> GetRequirementFiles(int clientId)
        {
            var requirements = await _context.PermitRequirements
                .Include(pr => pr.Photos)
                .Where(pr => pr.ClientId == clientId)
                .ToListAsync();

            return PartialView("_RequirementFilesPartial", requirements);
        }

        // POST: DocumentOfficer/UploadOptionalFiles
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadOptionalFiles(int requirementId, List<IFormFile> files)
        {
            var requirement = await _context.PermitRequirements.FindAsync(requirementId);
            if (requirement == null)
            {
                return NotFound();
            }

            if (files != null && files.Count > 0)
            {
                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "proof-photos");
                Directory.CreateDirectory(uploadsPath);

                foreach (var file in files)
                {
                    if (file.Length > 0)
                    {
                        var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                        var filePath = Path.Combine(uploadsPath, fileName);

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

                await _context.SaveChangesAsync();
                
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Ok(new { success = true, message = "Files uploaded successfully." });
                }
                
                TempData["SuccessMessage"] = "Optional files uploaded successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: DocumentOfficer/DeleteRequirementFile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRequirementFile(int photoId)
        {
            var photo = await _context.RequirementPhotos.FindAsync(photoId);
            if (photo == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return NotFound(new { success = false, message = "File not found." });
                return NotFound();
            }

            // Delete physical file
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", photo.PhotoPath.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                try 
                { 
                    System.IO.File.Delete(filePath); 
                } 
                catch 
                { 
                    // Continue even if file deletion fails
                }
            }

            // Remove from database
            _context.RequirementPhotos.Remove(photo);
            await _context.SaveChangesAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Ok(new { success = true, message = "File deleted successfully." });
            }

            TempData["SuccessMessage"] = "File deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream"
            };
        }
    }
}