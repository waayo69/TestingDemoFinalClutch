using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestingDemo.Services;
using TestingDemo.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace TestingDemo.Controllers
{
    [Authorize(Roles = "Admin")]
    public class BackupController : Controller
    {
        private readonly IBackupService _backupService;
        private readonly ILogger<BackupController> _logger;
        private readonly IConfiguration _configuration;

        public BackupController(IBackupService backupService, ILogger<BackupController> logger, IConfiguration configuration)
        {
            _backupService = backupService;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var backupHistory = await _backupService.GetBackupHistoryAsync();
                return View(backupHistory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading backup history");
                TempData["ErrorMessage"] = "Failed to load backup history.";
                return View(new List<BackupInfo>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateDatabaseBackup()
        {
            try
            {
                var success = await _backupService.CreateDatabaseBackupAsync();
                
                if (success)
                {
                    TempData["SuccessMessage"] = "Database backup created successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to create database backup. Check logs for details.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating database backup");
                TempData["ErrorMessage"] = "An error occurred while creating the database backup.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CreateClientFilesBackup()
        {
            try
            {
                var success = await _backupService.CreateClientFilesBackupAsync();
                
                if (success)
                {
                    TempData["SuccessMessage"] = "Client files backup created successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to create client files backup. Check logs for details.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating client files backup");
                TempData["ErrorMessage"] = "An error occurred while creating the client files backup.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CreateFullBackup()
        {
            try
            {
                var success = await _backupService.CreateFullBackupAsync();
                
                if (success)
                {
                    TempData["SuccessMessage"] = "Full backup created successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Full backup completed with errors. Check logs for details.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating full backup");
                TempData["ErrorMessage"] = "An error occurred while creating the full backup.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteBackup(string filePath)
        {
            try
            {
                var success = await _backupService.DeleteBackupAsync(filePath);
                
                if (success)
                {
                    TempData["SuccessMessage"] = "Backup deleted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to delete backup. File may not exist.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting backup");
                TempData["ErrorMessage"] = "An error occurred while deleting the backup.";
            }

            return RedirectToAction(nameof(Index));
        }

        public IActionResult DownloadBackup(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                {
                    TempData["ErrorMessage"] = "Backup file not found.";
                    return RedirectToAction(nameof(Index));
                }

                var fileName = Path.GetFileName(filePath);
                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                
                return File(fileBytes, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading backup file: {FilePath}", filePath);
                TempData["ErrorMessage"] = "Failed to download backup file.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public IActionResult GetBackupSettings()
        {
            try
            {
                var settings = new BackupSettingsModel
                {
                    Enabled = _configuration.GetValue<bool>("BackupSettings:Enabled", true),
                    BackupType = _configuration.GetValue<string>("BackupSettings:BackupType", "Full") ?? "Full",
                    IntervalHours = _configuration.GetValue<int>("BackupSettings:IntervalHours", 24),
                    StartTime = _configuration.GetValue<string>("BackupSettings:StartTime", "02:00") ?? "02:00",
                    RetentionDays = _configuration.GetValue<int>("BackupSettings:RetentionDays", 30),
                    BackupDirectory = _configuration.GetValue<string>("BackupSettings:BackupDirectory", "Backups") ?? "Backups"
                };

                return Json(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting backup settings");
                return Json(new { error = "Failed to load backup settings" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateBackupSettings([FromBody] BackupSettingsModel settings)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Json(new { success = false, errors = errors });
                }

                // Update appsettings.json
                var appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                
                if (!System.IO.File.Exists(appSettingsPath))
                {
                    return Json(new { success = false, error = "Configuration file not found" });
                }

                var json = await System.IO.File.ReadAllTextAsync(appSettingsPath);
                var jsonDocument = JsonDocument.Parse(json);
                var root = jsonDocument.RootElement;

                // Create new JSON with updated backup settings
                var updatedJson = UpdateBackupSettingsInJson(root, settings);
                
                // Write back to file
                await System.IO.File.WriteAllTextAsync(appSettingsPath, updatedJson);

                _logger.LogInformation("Backup settings updated successfully");
                
                return Json(new { success = true, message = "Backup settings updated successfully. Restart the application for changes to take effect." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating backup settings");
                return Json(new { success = false, error = "Failed to update backup settings" });
            }
        }

        private string UpdateBackupSettingsInJson(JsonElement root, BackupSettingsModel settings)
        {
            var options = new JsonWriterOptions { Indented = true };
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, options);

            writer.WriteStartObject();

            foreach (var property in root.EnumerateObject())
            {
                if (property.Name == "BackupSettings")
                {
                    writer.WritePropertyName("BackupSettings");
                    writer.WriteStartObject();
                    writer.WriteBoolean("Enabled", settings.Enabled);
                    writer.WriteString("BackupType", settings.BackupType);
                    writer.WriteNumber("IntervalHours", settings.IntervalHours);
                    writer.WriteString("StartTime", settings.StartTime);
                    writer.WriteNumber("RetentionDays", settings.RetentionDays);
                    writer.WriteString("BackupDirectory", settings.BackupDirectory);
                    writer.WriteEndObject();
                }
                else
                {
                    property.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
            writer.Flush();

            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
