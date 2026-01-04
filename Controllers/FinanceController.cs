using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using TestingDemo.Data;
using TestingDemo.Models;
using TestingDemo.ViewModels;

namespace TestingDemo.Controllers
{
    [Authorize(Roles = "Finance,Admin")]
    public class FinanceController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public FinanceController(ApplicationDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // GET: Finance/Index
        public async Task<IActionResult> Index(string sortOrder, string searchString, int? pendingPageNumber, int? clearancePageNumber, int? planningPageNumber)
        {
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["DateSortParm"] = sortOrder == "Date" ? "date_desc" : "Date";
            ViewData["CurrentFilter"] = searchString;

            int pageSize = 5;

            var pendingQuery = _context.Clients
                .Where(c => c.Status == "Pending" || c.Status == "Finance" || c.Status == "For Review")
                .Include(c => c.RetainershipBIR)
                .Include(c => c.RetainershipSPP)
                .Include(c => c.OneTimeTransaction)
                .Include(c => c.ExternalAudit)
                .AsNoTracking();
            var clearanceQuery = _context.Clients
                .Where(c => c.Status == "Clearance")
                .Include(c => c.RetainershipBIR)
                .Include(c => c.RetainershipSPP)
                .Include(c => c.OneTimeTransaction)
                .Include(c => c.ExternalAudit)
                .AsNoTracking();
            var planningQuery = _context.Clients
                .Where(c => c.Status == "Planning" || c.Status == "CustomerCare")
                .Include(c => c.RetainershipBIR)
                .Include(c => c.RetainershipSPP)
                .Include(c => c.OneTimeTransaction)
                .Include(c => c.ExternalAudit)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(searchString))
            {
                pendingQuery = pendingQuery.Where(s => s.ClientName.Contains(searchString) || s.TypeOfProject.Contains(searchString));
                clearanceQuery = clearanceQuery.Where(s => s.ClientName.Contains(searchString) || s.TypeOfProject.Contains(searchString));
                planningQuery = planningQuery.Where(s => s.ClientName.Contains(searchString) || s.TypeOfProject.Contains(searchString));
            }

            switch (sortOrder)
            {
                case "name_desc":
                    pendingQuery = pendingQuery.OrderByDescending(s => s.ClientName);
                    clearanceQuery = clearanceQuery.OrderByDescending(s => s.ClientName);
                    planningQuery = planningQuery.OrderByDescending(s => s.ClientName);
                    break;
                case "Date":
                    pendingQuery = pendingQuery.OrderBy(s => s.CreatedDate);
                    clearanceQuery = clearanceQuery.OrderBy(s => s.CreatedDate);
                    planningQuery = planningQuery.OrderBy(s => s.CreatedDate);
                    break;
                case "date_desc":
                    pendingQuery = pendingQuery.OrderByDescending(s => s.CreatedDate);
                    clearanceQuery = clearanceQuery.OrderByDescending(s => s.CreatedDate);
                    planningQuery = planningQuery.OrderByDescending(s => s.CreatedDate);
                    break;
                default:
                    pendingQuery = pendingQuery.OrderBy(s => s.CreatedDate);
                    clearanceQuery = clearanceQuery.OrderBy(s => s.CreatedDate);
                    planningQuery = planningQuery.OrderBy(s => s.CreatedDate);
                    break;
            }

            var viewModel = new FinanceDashboardViewModel
            {
                PendingClients = await PaginatedList<ClientModel>.CreateAsync(pendingQuery, pendingPageNumber ?? 1, pageSize),
                ClearanceClients = await PaginatedList<ClientModel>.CreateAsync(clearanceQuery, clearancePageNumber ?? 1, pageSize),
                PlanningClients = await PaginatedList<ClientModel>.CreateAsync(planningQuery, planningPageNumber ?? 1, pageSize)
            };

            return View(viewModel);
        }

        // Details view removed - details now handled via modal in the Finance table

        // GET: Finance/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Finance/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClientModel client)
        {
            // Remove TrackingNumber from validation since it will be set in code
            ModelState.Remove("TrackingNumber");

            ViewBag.PostedForm = Request.Form.Keys.ToDictionary(k => k, k => Request.Form[k]);

            if (string.IsNullOrEmpty(client.TaxId))
                ModelState.Remove("TaxId");

            // Bind project-type-specific data
            switch (client.TypeOfProject)
            {
                case "Retainership - BIR":
                    var bir = new RetainershipBIRModel
                    {
                        TypeOfRegistrant = Request.Form["RetainershipBIR.TypeOfRegistrant"],
                        OCNNotes = Request.Form["RetainershipBIR.OCNNotes"],
                        DateOCNGenerated = DateTime.TryParse(Request.Form["RetainershipBIR.DateOCNGenerated"], out var docn) ? docn : (DateTime?)null,
                        DateBIRRegistration = DateTime.TryParse(Request.Form["RetainershipBIR.DateBIRRegistration"], out var dreg) ? dreg : (DateTime?)null,
                        BIRRdoNo = Request.Form["RetainershipBIR.BIRRdoNo"],
                        OtherBirRdoNo = Request.Form["RetainershipBIR.OtherBirRdoNo"],
                        TaxFilingStatus = Request.Form["RetainershipBIR.TaxFilingStatus"],
                        NeedCatchUpAccounting = Request.Form["RetainershipBIR.NeedCatchUpAccounting"],
                        CatchUpReasons = Request.Form["RetainershipBIR.CatchUpReasons"],
                        OtherCatchUpReason = Request.Form["RetainershipBIR.OtherCatchUpReason"],
                        CatchUpStartDate = DateTime.TryParse(Request.Form["RetainershipBIR.CatchUpStartDate"], out var dcu) ? dcu : (DateTime?)null,
                        BIRComplianceActivities = Request.Form["RetainershipBIR.BIRComplianceActivities"],
                        OtherBIRCompliance = Request.Form["RetainershipBIR.OtherBIRCompliance"],
                        BIRRetainershipStartDate = DateTime.TryParse(Request.Form["RetainershipBIR.BIRRetainershipStartDate"], out var drs) ? drs : (DateTime?)null
                    };
                    if (string.IsNullOrWhiteSpace(bir.BIRRdoNo))
                    {
                        ModelState.AddModelError("RetainershipBIR.BIRRdoNo", "BIR RDO No. is required.");
                    }
                    client.RetainershipBIR = bir;
                    client.RetainershipSPP = null;
                    client.OneTimeTransaction = null;
                    client.ExternalAudit = null;
                    break;
                case "Retainership - SPP":
                    var spp = new RetainershipSPPModel
                    {
                        SSSCompanyRegNo = Request.Form["RetainershipSPP.SSSCompanyRegNo"],
                        SSSRegistrationDate = DateTime.TryParse(Request.Form["RetainershipSPP.SSSRegistrationDate"], out var dsss) ? dsss : (DateTime?)null,
                        PHICCompanyRegNo = Request.Form["RetainershipSPP.PHICCompanyRegNo"],
                        PHICRegistrationDate = DateTime.TryParse(Request.Form["RetainershipSPP.PHICRegistrationDate"], out var dphic) ? dphic : (DateTime?)null,
                        HDMFCompanyRegNo = Request.Form["RetainershipSPP.HDMFCompanyRegNo"],
                        HDMFRegistrationDate = DateTime.TryParse(Request.Form["RetainershipSPP.HDMFRegistrationDate"], out var dhdmf) ? dhdmf : (DateTime?)null,
                        SPPComplianceActivities = Request.Form["RetainershipSPP.SPPComplianceActivities"],
                        OtherSPPCompliance = Request.Form["RetainershipSPP.OtherSPPCompliance"],
                        SPPRetainershipStartDate = DateTime.TryParse(Request.Form["RetainershipSPP.SPPRetainershipStartDate"], out var dspp) ? dspp : (DateTime?)null
                    };
                    client.RetainershipSPP = spp;
                    client.RetainershipBIR = null;
                    client.OneTimeTransaction = null;
                    client.ExternalAudit = null;
                    break;
                case "One Time Transaction":
                    var oneTime = new OneTimeTransactionModel
                    {
                        TypeOfRegistrant = Request.Form["OneTimeTransaction.TypeOfRegistrant"],
                        AreaOfServices = Request.Form["OneTimeTransaction.AreaOfServices"],
                        OtherAreaOfServices = Request.Form["OneTimeTransaction.OtherAreaOfServices"]
                    };
                    client.OneTimeTransaction = oneTime;
                    client.RetainershipBIR = null;
                    client.RetainershipSPP = null;
                    client.ExternalAudit = null;
                    break;
                case "External Audit":
                    var audit = new ExternalAuditModel
                    {
                        ExternalAuditStatus = Request.Form["ExternalAudit.ExternalAuditStatus"],
                        ExternalAuditPurposes = Request.Form["ExternalAudit.ExternalAuditPurposes"],
                        ExternalAuditOtherPurpose = Request.Form["ExternalAudit.ExternalAuditOtherPurpose"],
                        ExternalAuditReportDate = DateTime.TryParse(Request.Form["ExternalAudit.ExternalAuditReportDate"], out var daudit) ? daudit : (DateTime?)null
                    };
                    client.ExternalAudit = audit;
                    client.RetainershipBIR = null;
                    client.RetainershipSPP = null;
                    client.OneTimeTransaction = null;
                    break;
            }

            client.OtherTypeOfProject = Request.Form["OtherTypeOfProject"];
            client.OtherRequestingParty = Request.Form["OtherRequestingParty"];

            if (ModelState.IsValid)
            {
                client.CreatedDate = DateTime.Now;
                client.Status = "Pending";
                // Generate unique tracking number
                client.TrackingNumber = await GenerateUniqueTrackingNumber();
                _context.Add(client);
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "Finance data changed");
                TempData["SuccessMessage"] = "Client created successfully!";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.DebugErrors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return View(client);
        }

        // GET: Finance/Edit/5
        public async Task<IActionResult> Edit(int? id)
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

            return View(client);
        }

        // POST: Finance/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ClientModel client)
        {
            if (id != client.Id)
            {
                return NotFound();
            }

            if (string.IsNullOrEmpty(client.TaxId))
                ModelState.Remove("TaxId");

            // Load the existing client and related data
            var existingClient = await _context.Clients
                .Include(c => c.RetainershipBIR)
                .Include(c => c.RetainershipSPP)
                .Include(c => c.OneTimeTransaction)
                .Include(c => c.ExternalAudit)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (existingClient == null)
                return NotFound();

            // Update base fields
            _context.Entry(existingClient).CurrentValues.SetValues(client);

            // Update project-type-specific data
            switch (client.TypeOfProject)
            {
                case "Retainership - BIR":
                    if (existingClient.RetainershipBIR == null)
                        existingClient.RetainershipBIR = new RetainershipBIRModel();
                    existingClient.RetainershipBIR.TypeOfRegistrant = Request.Form["RetainershipBIR.TypeOfRegistrant"];
                    existingClient.RetainershipBIR.OCNNotes = Request.Form["RetainershipBIR.OCNNotes"];
                    existingClient.RetainershipBIR.DateOCNGenerated = DateTime.TryParse(Request.Form["RetainershipBIR.DateOCNGenerated"], out var docn) ? docn : (DateTime?)null;
                    existingClient.RetainershipBIR.DateBIRRegistration = DateTime.TryParse(Request.Form["RetainershipBIR.DateBIRRegistration"], out var dreg) ? dreg : (DateTime?)null;
                    existingClient.RetainershipBIR.BIRRdoNo = Request.Form["RetainershipBIR.BIRRdoNo"];
                    existingClient.RetainershipBIR.OtherBirRdoNo = Request.Form["RetainershipBIR.OtherBirRdoNo"];
                    existingClient.RetainershipBIR.TaxFilingStatus = Request.Form["RetainershipBIR.TaxFilingStatus"];
                    existingClient.RetainershipBIR.NeedCatchUpAccounting = Request.Form["RetainershipBIR.NeedCatchUpAccounting"];
                    existingClient.RetainershipBIR.CatchUpReasons = Request.Form["RetainershipBIR.CatchUpReasons"];
                    existingClient.RetainershipBIR.OtherCatchUpReason = Request.Form["RetainershipBIR.OtherCatchUpReason"];
                    existingClient.RetainershipBIR.CatchUpStartDate = DateTime.TryParse(Request.Form["RetainershipBIR.CatchUpStartDate"], out var dcu) ? dcu : (DateTime?)null;
                    existingClient.RetainershipBIR.BIRComplianceActivities = Request.Form["RetainershipBIR.BIRComplianceActivities"];
                    existingClient.RetainershipBIR.OtherBIRCompliance = Request.Form["RetainershipBIR.OtherBIRCompliance"];
                    existingClient.RetainershipBIR.BIRRetainershipStartDate = DateTime.TryParse(Request.Form["RetainershipBIR.BIRRetainershipStartDate"], out var drs) ? drs : (DateTime?)null;
                    if (string.IsNullOrWhiteSpace(existingClient.RetainershipBIR.BIRRdoNo))
                    {
                        ModelState.AddModelError("RetainershipBIR.BIRRdoNo", "BIR RDO No. is required.");
                    }
                    existingClient.RetainershipSPP = null;
                    existingClient.OneTimeTransaction = null;
                    existingClient.ExternalAudit = null;
                    break;
                case "Retainership - SPP":
                    if (existingClient.RetainershipSPP == null)
                        existingClient.RetainershipSPP = new RetainershipSPPModel();
                    existingClient.RetainershipSPP.SSSCompanyRegNo = Request.Form["RetainershipSPP.SSSCompanyRegNo"];
                    existingClient.RetainershipSPP.SSSRegistrationDate = DateTime.TryParse(Request.Form["RetainershipSPP.SSSRegistrationDate"], out var dsss) ? dsss : (DateTime?)null;
                    existingClient.RetainershipSPP.PHICCompanyRegNo = Request.Form["RetainershipSPP.PHICCompanyRegNo"];
                    existingClient.RetainershipSPP.PHICRegistrationDate = DateTime.TryParse(Request.Form["RetainershipSPP.PHICRegistrationDate"], out var dphic) ? dphic : (DateTime?)null;
                    existingClient.RetainershipSPP.HDMFCompanyRegNo = Request.Form["RetainershipSPP.HDMFCompanyRegNo"];
                    existingClient.RetainershipSPP.HDMFRegistrationDate = DateTime.TryParse(Request.Form["RetainershipSPP.HDMFRegistrationDate"], out var dhdmf) ? dhdmf : (DateTime?)null;
                    existingClient.RetainershipSPP.SPPComplianceActivities = Request.Form["RetainershipSPP.SPPComplianceActivities"];
                    existingClient.RetainershipSPP.OtherSPPCompliance = Request.Form["RetainershipSPP.OtherSPPCompliance"];
                    existingClient.RetainershipSPP.SPPRetainershipStartDate = DateTime.TryParse(Request.Form["RetainershipSPP.SPPRetainershipStartDate"], out var dspp) ? dspp : (DateTime?)null;
                    existingClient.RetainershipBIR = null;
                    existingClient.OneTimeTransaction = null;
                    existingClient.ExternalAudit = null;
                    break;
                case "One Time Transaction":
                    if (existingClient.OneTimeTransaction == null)
                        existingClient.OneTimeTransaction = new OneTimeTransactionModel();
                    existingClient.OneTimeTransaction.TypeOfRegistrant = Request.Form["OneTimeTransaction.TypeOfRegistrant"];
                    existingClient.OneTimeTransaction.AreaOfServices = Request.Form["OneTimeTransaction.AreaOfServices"];
                    existingClient.OneTimeTransaction.OtherAreaOfServices = Request.Form["OneTimeTransaction.OtherAreaOfServices"];
                    existingClient.RetainershipBIR = null;
                    existingClient.RetainershipSPP = null;
                    existingClient.ExternalAudit = null;
                    break;
                case "External Audit":
                    if (existingClient.ExternalAudit == null)
                        existingClient.ExternalAudit = new ExternalAuditModel();
                    existingClient.ExternalAudit.ExternalAuditStatus = Request.Form["ExternalAudit.ExternalAuditStatus"];
                    existingClient.ExternalAudit.ExternalAuditPurposes = Request.Form["ExternalAudit.ExternalAuditPurposes"];
                    existingClient.ExternalAudit.ExternalAuditOtherPurpose = Request.Form["ExternalAudit.ExternalAuditOtherPurpose"];
                    existingClient.ExternalAudit.ExternalAuditReportDate = DateTime.TryParse(Request.Form["ExternalAudit.ExternalAuditReportDate"], out var daudit) ? daudit : (DateTime?)null;
                    existingClient.RetainershipBIR = null;
                    existingClient.RetainershipSPP = null;
                    existingClient.OneTimeTransaction = null;
                    break;
            }

            // In Edit POST action, after SetValues(client):
            existingClient.OtherTypeOfProject = Request.Form["OtherTypeOfProject"];
            existingClient.OtherRequestingParty = Request.Form["OtherRequestingParty"];

            if (ModelState.IsValid)
            {
                try
                {
                    await _context.SaveChangesAsync();
                    await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "Finance data changed");
                    TempData["SuccessMessage"] = "Client updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ClientExists(client.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(client);
        }

        // GET: Finance/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var client = await _context.Clients
                .Include(c => c.RetainershipBIR)
                .Include(c => c.RetainershipSPP)
                .Include(c => c.OneTimeTransaction)
                .Include(c => c.ExternalAudit)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (client == null)
            {
                return NotFound();
            }

            return View(client);
        }

        // POST: Finance/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client != null)
            {
                _context.Clients.Remove(client);
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "Finance data changed");
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Finance/SendToPlanningOfficer/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendToPlanningOfficer(int id)
        {
            var client = await _context.Clients.FindAsync(id);

            if (client != null)
            {
                // Update status to indicate it's ready for Planning
                client.Status = "Planning";
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "Finance data changed");

                TempData["SuccessMessage"] = "Client sent to Planning Officer successfully!";

                // Redirect to Planning Officer controller if user has permission
                if (User.IsInRole("PlanningOfficer") || User.IsInRole("Admin"))
                {
                    return RedirectToAction("Index", "PlanningOfficer");
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Finance/ArchiveClient/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveClient(int id)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client != null)
            {
                client.Status = "Archived";
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "Finance data changed");
                TempData["SuccessMessage"] = $"Client {client.ClientName} has been archived successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Finance/ReturnToDocumentOfficer/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnToDocumentOfficer(int id)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client != null)
            {
                client.Status = "DocumentOfficer";
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveUpdate", "Finance data changed");
                TempData["SuccessMessage"] = "Client returned to Document Officer.";
            }
            return RedirectToAction("Index");
        }

        // GET: Finance/GetLatestData
        [HttpGet]
        public async Task<IActionResult> GetLatestData(string sortOrder, string searchString, int? pendingPageNumber, int? clearancePageNumber, int? planningPageNumber)
        {
            int pageSize = 5;
            var pendingQuery = _context.Clients
                .Where(c => c.Status == "Pending" || c.Status == "Finance" || c.Status == "For Review")
                .Include(c => c.RetainershipBIR)
                .Include(c => c.RetainershipSPP)
                .Include(c => c.OneTimeTransaction)
                .Include(c => c.ExternalAudit)
                .AsNoTracking();
            var clearanceQuery = _context.Clients
                .Where(c => c.Status == "Clearance")
                .Include(c => c.RetainershipBIR)
                .Include(c => c.RetainershipSPP)
                .Include(c => c.OneTimeTransaction)
                .Include(c => c.ExternalAudit)
                .AsNoTracking();
            var planningQuery = _context.Clients
                .Where(c => c.Status == "Planning" || c.Status == "CustomerCare")
                .Include(c => c.RetainershipBIR)
                .Include(c => c.RetainershipSPP)
                .Include(c => c.OneTimeTransaction)
                .Include(c => c.ExternalAudit)
                .AsNoTracking();
            if (!string.IsNullOrEmpty(searchString))
            {
                pendingQuery = pendingQuery.Where(s => s.ClientName.Contains(searchString) || s.TypeOfProject.Contains(searchString));
                clearanceQuery = clearanceQuery.Where(s => s.ClientName.Contains(searchString) || s.TypeOfProject.Contains(searchString));
                planningQuery = planningQuery.Where(s => s.ClientName.Contains(searchString) || s.TypeOfProject.Contains(searchString));
            }
            switch (sortOrder)
            {
                case "name_desc":
                    pendingQuery = pendingQuery.OrderByDescending(s => s.ClientName);
                    clearanceQuery = clearanceQuery.OrderByDescending(s => s.ClientName);
                    planningQuery = planningQuery.OrderByDescending(s => s.ClientName);
                    break;
                case "Date":
                    pendingQuery = pendingQuery.OrderBy(s => s.CreatedDate);
                    clearanceQuery = clearanceQuery.OrderBy(s => s.CreatedDate);
                    planningQuery = planningQuery.OrderBy(s => s.CreatedDate);
                    break;
                case "date_desc":
                    pendingQuery = pendingQuery.OrderByDescending(s => s.CreatedDate);
                    clearanceQuery = clearanceQuery.OrderByDescending(s => s.CreatedDate);
                    planningQuery = planningQuery.OrderByDescending(s => s.CreatedDate);
                    break;
                default:
                    pendingQuery = pendingQuery.OrderBy(s => s.CreatedDate);
                    clearanceQuery = clearanceQuery.OrderBy(s => s.CreatedDate);
                    planningQuery = planningQuery.OrderBy(s => s.CreatedDate);
                    break;
            }
            var viewModel = new TestingDemo.ViewModels.FinanceDashboardViewModel
            {
                PendingClients = await TestingDemo.Models.PaginatedList<TestingDemo.Models.ClientModel>.CreateAsync(pendingQuery, pendingPageNumber ?? 1, pageSize),
                ClearanceClients = await TestingDemo.Models.PaginatedList<TestingDemo.Models.ClientModel>.CreateAsync(clearanceQuery, clearancePageNumber ?? 1, pageSize),
                PlanningClients = await TestingDemo.Models.PaginatedList<TestingDemo.Models.ClientModel>.CreateAsync(planningQuery, planningPageNumber ?? 1, pageSize)
            };
            return Json(viewModel);
        }

        private bool ClientExists(int id)
        {
            return _context.Clients.Any(e => e.Id == id);
        }

        private async Task<string> GenerateUniqueTrackingNumber()
        {
            var rand = new Random();
            string letters() => new string(Enumerable.Range(0, 4).Select(_ => (char)rand.Next('A', 'Z' + 1)).ToArray());
            string digits() => rand.Next(0, 1000000).ToString("D6");
            string tracking;
            do
            {
                tracking = $"{letters()}-{digits()}";
            } while (await _context.Clients.AnyAsync(c => c.TrackingNumber == tracking));
            return tracking;
        }
    }
}