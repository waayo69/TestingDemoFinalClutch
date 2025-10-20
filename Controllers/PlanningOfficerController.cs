using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TestingDemo.Models;
using Microsoft.AspNetCore.SignalR;
using TestingDemo.Data;
using System.Collections.Generic;
using System.IO;

namespace TestingDemo.Controllers
{
    [Authorize(Roles = "PlanningOfficer,Admin")]
    public class PlanningOfficerController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public PlanningOfficerController(ApplicationDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // GET: PlanningOfficer/Index
        public IActionResult Index(int? pageNumber)
        {
            return RedirectToAction(nameof(PendingClients), new { pageNumber });
        }

        // GET: PlanningOfficer/PlanningClients (deprecated) -> Redirect to Index (Planning)
        public IActionResult PlanningClients(int? pageNumber)
        {
            return RedirectToAction(nameof(Index), new { pageNumber });
        }

        // GET: PlanningOfficer/PendingClients
        public async Task<IActionResult> PendingClients(int? pageNumber)
        {
            ViewData["Title"] = "Pending Clients";
            ViewData["ListTitle"] = "Pending Clients";
            ViewData["CurrentAction"] = "PendingClients";

            int pageSize = 10;
            var clientsQuery = _context.Clients
                .Where(c => c.Status == "Planning")
                .Include(c => c.RetainershipBIR)
                .Include(c => c.RetainershipSPP)
                .Include(c => c.OneTimeTransaction)
                .Include(c => c.ExternalAudit)
                .OrderBy(c => c.CreatedDate)
                .AsNoTracking();

            var paginatedClients = await PaginatedList<ClientModel>.CreateAsync(clientsQuery, pageNumber ?? 1, pageSize);

            var clientIds = paginatedClients.Select(c => c.Id).ToList();
            var requirements = await _context.PermitRequirements
                .Where(r => clientIds.Contains(r.ClientId))
                .Include(r => r.Photos)
                .ToListAsync();

            ViewBag.Requirements = requirements
                .GroupBy(r => r.ClientId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return View("PlanningClients", paginatedClients);
        }

        // GET: PlanningOfficer/CompletedClients
        public async Task<IActionResult> CompletedClients(int? pageNumber)
        {
            ViewData["Title"] = "Completed Clients";
            ViewData["ListTitle"] = "Completed Clients";
            ViewData["CurrentAction"] = "CompletedClients";

            int pageSize = 10;
            var clientsQuery = _context.Clients
                .Where(c => c.Status == "Liaison" || c.Status == "CustomerCareReceived" || c.Status == "DocumentOfficer")
                .Include(c => c.RetainershipBIR)
                .Include(c => c.RetainershipSPP)
                .Include(c => c.OneTimeTransaction)
                .Include(c => c.ExternalAudit)
                .OrderBy(c => c.CreatedDate)
                .AsNoTracking();

            var paginatedClients = await PaginatedList<ClientModel>.CreateAsync(clientsQuery, pageNumber ?? 1, pageSize);

            var clientIds = paginatedClients.Select(c => c.Id).ToList();
            var requirements = await _context.PermitRequirements
                .Where(r => clientIds.Contains(r.ClientId))
                .ToListAsync();

            ViewBag.Requirements = requirements
                .GroupBy(r => r.ClientId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return View("PlanningClients", paginatedClients);
        }

        // Details action removed - details now handled via modal in the Planning Officer views

        // GET: PlanningOfficer/PlanRequirements/5
        public async Task<IActionResult> PlanRequirements(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var client = await _context.Clients.FindAsync(id);
            if (client == null)
            {
                return NotFound();
            }

            // Ensure status is Planning
            if (client.Status != "Planning")
            {
                client.Status = "Planning";
                await _context.SaveChangesAsync();
            }

            // Redirect to PlanningClients modal flow
            TempData["OpenClientId"] = id.ToString();
            return RedirectToAction("Index");
        }

        // Simple action for direct adding (no validation)
        // GET: PlanningOfficer/QuickAddRequirement/5?name=Test&description=Description
        public async Task<IActionResult> QuickAddRequirement(int id, string name, string description)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(description))
            {
                return BadRequest("Name and description are required");
            }

            var client = await _context.Clients.FindAsync(id);
            if (client == null)
            {
                return NotFound("Client not found");
            }

            var requirement = new PermitRequirementModel
            {
                ClientId = id,
                RequirementName = name,
                Description = description,
                IsRequired = true,
                CreatedDate = DateTime.Now
            };

            _context.PermitRequirements.Add(requirement);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Requirement added successfully!";
            TempData["OpenClientId"] = id.ToString();
            return RedirectToAction("Index");
        }

        // POST: PlanningOfficer/AddRequirement
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRequirement([Bind("ClientId,RequirementName,Description,IsRequired")] PermitRequirementModel requirement, List<IFormFile> proofOfCompletionPhotos)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Make sure the ClientId is valid
                    var client = await _context.Clients.FindAsync(requirement.ClientId);
                    if (client == null)
                    {
                        ModelState.AddModelError("ClientId", "Invalid client ID");
                    }

                    if (ModelState.IsValid)
                    {
                        requirement.CreatedDate = DateTime.Now;
                        requirement.IsCompleted = false;
                        _context.PermitRequirements.Add(requirement);
                        await _context.SaveChangesAsync();

                        // Handle multiple photo uploads
                        if (proofOfCompletionPhotos != null && proofOfCompletionPhotos.Count > 0)
                        {
                            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "proof-photos");
                            if (!Directory.Exists(uploadsFolder))
                                Directory.CreateDirectory(uploadsFolder);
                            foreach (var photo in proofOfCompletionPhotos)
                            {
                                if (photo != null && photo.Length > 0)
                                {
                                    var fileName = $"proof_{requirement.Id}_{DateTime.Now:yyyyMMddHHmmssfff}_{Path.GetFileName(photo.FileName)}";
                                    var filePath = Path.Combine(uploadsFolder, fileName);
                                    using (var stream = new FileStream(filePath, FileMode.Create))
                                    {
                                        await photo.CopyToAsync(stream);
                                    }
                                    var reqPhoto = new RequirementPhoto
                                    {
                                        RequirementId = requirement.Id,
                                        PhotoPath = $"/uploads/proof-photos/{fileName}"
                                    };
                                    _context.RequirementPhotos.Add(reqPhoto);
                                }
                            }
                            await _context.SaveChangesAsync();
                        }
                        await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "PlanningOfficer data changed");
                        TempData["SuccessMessage"] = "Requirement added successfully!";
                        TempData["OpenClientId"] = requirement.ClientId.ToString();
                        var returnUrl = Request?.Form["returnUrl"].ToString();
                        if (!string.IsNullOrWhiteSpace(returnUrl))
                            return Redirect(returnUrl);
                        return RedirectToAction("Index");
                    }
                }
                else
                {
                    foreach (var kvp in ModelState)
                        foreach (var error in kvp.Value.Errors)
                            System.Diagnostics.Debug.WriteLine($"ModelState Error on {kvp.Key}: {error.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception: {ex.Message}");
                ModelState.AddModelError("", "An error occurred while saving the requirement.");
            }
            TempData["RequirementName"] = requirement.RequirementName ?? string.Empty;
            TempData["RequirementDescription"] = requirement.Description ?? string.Empty;
            TempData["RequirementIsRequired"] = requirement.IsRequired ? "true" : "false";
            TempData["ErrorMessage"] = "Failed to add requirement. Please check your input.";
            TempData["OpenClientId"] = requirement.ClientId.ToString();
            var errors = ModelState.Where(kvp => kvp.Value.Errors.Any()).Select(kvp => $"{kvp.Key}: {string.Join("; ", kvp.Value.Errors.Select(e => e.ErrorMessage))}").ToList();
            if (errors.Any())
                TempData["ValidationErrors"] = string.Join(" | ", errors);
            var backUrl = Request?.Form["returnUrl"].ToString();
            if (!string.IsNullOrWhiteSpace(backUrl))
                return Redirect(backUrl);
            return RedirectToAction("Index");
        }

        // GET: PlanningOfficer/EditRequirement/5
        public async Task<IActionResult> EditRequirement(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Load requirement with photos
            var requirement = await _context.PermitRequirements
                .Include(r => r.Photos)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (requirement == null)
            {
                return NotFound();
            }

            // Get client information
            ViewBag.Client = await _context.Clients.FindAsync(requirement.ClientId);

            return View(requirement);
        }

        // POST: PlanningOfficer/EditRequirement/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRequirement(int id, PermitRequirementModel requirement, List<IFormFile> proofOfCompletionPhotos, int[] deletePhotoIds)
        {
            try
            {
                if (id != requirement.Id)
                {
                    return NotFound();
                }

                if (!ModelState.IsValid)
                {
                    // Rehydrate existing requirement with photos so the view can display everything
                    var invalidExisting = await _context.PermitRequirements
                        .Include(r => r.Photos)
                        .FirstOrDefaultAsync(r => r.Id == id);
                    ViewBag.Client = await _context.Clients.FindAsync(requirement.ClientId);
                    return View(invalidExisting ?? requirement);
                }

                var existing = await _context.PermitRequirements.Include(r => r.Photos).FirstOrDefaultAsync(r => r.Id == id);
                if (existing == null)
                    return NotFound();

                existing.RequirementName = requirement.RequirementName;
                existing.Description = requirement.Description;
                existing.IsRequired = requirement.IsRequired;
                // Do not update IsCompleted from Planning Officer anymore; Customer Care marks completion on upload

                // Delete selected photos
                if (deletePhotoIds != null && deletePhotoIds.Length > 0)
                {
                    foreach (var photoId in deletePhotoIds)
                    {
                        var photo = await _context.RequirementPhotos.FindAsync(photoId);
                        if (photo != null)
                        {
                            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", photo.PhotoPath.TrimStart('/'));
                            if (System.IO.File.Exists(filePath))
                            {
                                try { System.IO.File.Delete(filePath); } catch { }
                            }
                            _context.RequirementPhotos.Remove(photo);
                        }
                    }
                }

                // Add new photos
                if (proofOfCompletionPhotos != null && proofOfCompletionPhotos.Count > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "proof-photos");

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    foreach (var photo in proofOfCompletionPhotos)
                    {
                        if (photo != null && photo.Length > 0)
                        {
                            var fileName = $"proof_{id}_{DateTime.Now:yyyyMMddHHmmssfff}_{Path.GetFileName(photo.FileName)}";
                            var filePath = Path.Combine(uploadsFolder, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await photo.CopyToAsync(stream);
                            }

                            var reqPhoto = new RequirementPhoto
                            {
                                RequirementId = id,
                                PhotoPath = $"/uploads/proof-photos/{fileName}"
                            };
                            _context.RequirementPhotos.Add(reqPhoto);
                        }
                    }
                }

                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "PlanningOfficer data changed");
                TempData["SuccessMessage"] = "Requirement updated successfully!";
                TempData["OpenClientId"] = existing.ClientId.ToString();
                TempData["OpenRequirementId"] = existing.Id.ToString();

                // Redirect back to the edit view so the user immediately sees newly uploaded files
                return RedirectToAction(nameof(EditRequirement), new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred while updating the requirement: {ex.Message}";
                ViewBag.Client = await _context.Clients.FindAsync(requirement.ClientId);
                return View(requirement);
            }
        }

        // POST: PlanningOfficer/DeleteRequirement/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRequirement(int id)
        {
            var requirement = await _context.PermitRequirements.FindAsync(id);

            if (requirement != null)
            {
                int clientId = requirement.ClientId;
                _context.PermitRequirements.Remove(requirement);
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "PlanningOfficer data changed");

                TempData["SuccessMessage"] = "Requirement deleted successfully!";
            }

            return RedirectToAction("Index");
        }

        // Test endpoint for photo upload
        [HttpPost]
        public async Task<IActionResult> TestPhotoUpload(List<IFormFile> proofOfCompletionPhotos)
        {
            try
            {
                if (proofOfCompletionPhotos != null && proofOfCompletionPhotos.Count > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "test-photos");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    foreach (var file in proofOfCompletionPhotos)
                    {
                        if (file.Length > 0)
                        {
                            var fileName = $"test_{DateTime.Now:yyyyMMddHHmmssfff}_{Path.GetFileName(file.FileName)}";
                            var filePath = Path.Combine(uploadsFolder, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }
                        }
                    }
                }

                return Json(new { success = true, message = "Test upload successful", fileCount = proofOfCompletionPhotos?.Count ?? 0 });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: PlanningOfficer/DeleteRequirementPhoto/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRequirementPhoto(int photoId)
        {
            var photo = await _context.RequirementPhotos.FindAsync(photoId);
            if (photo == null)
                return Json(new { success = false, message = "Photo not found" });
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", photo.PhotoPath.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                try { System.IO.File.Delete(filePath); } catch { }
            }
            _context.RequirementPhotos.Remove(photo);
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "PlanningOfficer data changed");
            return Json(new { success = true, message = "Photo deleted successfully" });
        }

        // POST: PlanningOfficer/CompleteRequirements/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteRequirements(int id)
        {
            var client = await _context.Clients.FindAsync(id);

            if (client != null)
            {
                client.Status = "Completed";
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "PlanningOfficer data changed");

                TempData["SuccessMessage"] = "Requirements planning completed successfully!";
            }

            return RedirectToAction("Index");
        }

        // POST: PlanningOfficer/ToggleRequirementStatus/5
        [HttpPost]
        public async Task<IActionResult> ToggleRequirementStatus(int id, bool isCompleted)
        {
            try
            {
                var requirement = await _context.PermitRequirements.FindAsync(id);
                if (requirement == null)
                {
                    return Json(new { success = false, message = "Requirement not found" });
                }

                // Update completion status
                requirement.IsCompleted = isCompleted;
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "PlanningOfficer data changed");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: PlanningOfficer/ProceedToLiaison/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProceedToLiaison(int id)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client == null)
            {
                return NotFound();
            }

            client.Status = "Liaison"; // Status updated to Liaison
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "PlanningOfficer data changed");

            TempData["SuccessMessage"] = $"Client {client.ClientName} has been proceeded to Liaison.";
            return RedirectToAction("Index");
        }

        // POST: PlanningOfficer/BackToFinance/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BackToFinance(int id, string note)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client != null)
            {
                client.Status = "Pending"; // Changed from "Finance" to "Pending"
                client.PlanningReturnNote = note; // Save the note for Finance to see
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "PlanningOfficer data changed");
                TempData["SuccessMessage"] = $"Client returned to Finance's pending list. Note: {note}";
            }
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> GetLatestData(int? pageNumber)
        {
            int pageSize = 10;
            var clientsQuery = _context.Clients
                .Where(c => c.Status == "Planning")
                .Include(c => c.RetainershipBIR)
                .Include(c => c.RetainershipSPP)
                .Include(c => c.OneTimeTransaction)
                .Include(c => c.ExternalAudit)
                .OrderBy(c => c.CreatedDate)
                .AsNoTracking();
            var paginatedClients = await PaginatedList<ClientModel>.CreateAsync(clientsQuery, pageNumber ?? 1, pageSize);
            var clientIds = paginatedClients.Select(c => c.Id).ToList();
            var requirements = await _context.PermitRequirements
                .Where(r => clientIds.Contains(r.ClientId))
                .ToListAsync();
            var requirementsByClient = requirements
                .GroupBy(r => r.ClientId)
                .ToDictionary(g => g.Key, g => g.ToList());
            return Json(new { Clients = paginatedClients, RequirementsByClient = requirementsByClient });
        }

        // GET: PlanningOfficer/Requirements/{id}
        [HttpGet]
        public async Task<IActionResult> Requirements(int id, bool readOnly = false)
        {
            var requirements = await _context.PermitRequirements
                .Where(r => r.ClientId == id)
                .Include(r => r.Photos) // ensure photos are loaded for display
                .OrderBy(r => r.Id)
                .AsNoTracking()
                .ToListAsync();

            ViewData["ReadOnly"] = readOnly;
            return PartialView("_RequirementsList", requirements);
        }

        private bool RequirementExists(int id)
        {
            return _context.PermitRequirements.Any(e => e.Id == id);
        }
    }
}