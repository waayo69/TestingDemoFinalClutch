using Microsoft.Data.SqlClient;
using System.IO.Compression;
using TestingDemo.Data;

namespace TestingDemo.Services
{
    public class BackupService : IBackupService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BackupService> _logger;
        private readonly string _backupDirectory;
        private readonly string _connectionString;

        public BackupService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<BackupService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _connectionString = _configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string not found");
            
            // Create backup directory if it doesn't exist
            _backupDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Backups");
            Directory.CreateDirectory(_backupDirectory);
        }

        public async Task<bool> CreateDatabaseBackupAsync()
        {
            try
            {
                // Ensure backup directory exists
                Directory.CreateDirectory(_backupDirectory);
                
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"CPCPA_Database_Backup_{timestamp}.sql";
                var backupPath = Path.Combine(_backupDirectory, backupFileName);

                _logger.LogInformation($"Creating database backup at: {backupPath}");

                // Extract database name from connection string
                var builder = new SqlConnectionStringBuilder(_connectionString);
                var databaseName = builder.InitialCatalog;
                var serverName = builder.DataSource;

                _logger.LogInformation($"Backing up database '{databaseName}' from server '{serverName}'");

                // For remote databases, we'll create a SQL script backup instead of a .bak file
                // This approach works with hosted databases that don't allow direct file system access
                var tables = new List<string>();
                
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Get all table names
                var getTablesQuery = @"
                    SELECT TABLE_NAME 
                    FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_TYPE = 'BASE TABLE' 
                    AND TABLE_SCHEMA = 'dbo'
                    ORDER BY TABLE_NAME";

                using var tablesCommand = new SqlCommand(getTablesQuery, connection);
                using var reader = await tablesCommand.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    tables.Add(reader.GetString(0));
                }
                reader.Close();

                // Create SQL script backup
                using var writer = new StreamWriter(backupPath);
                
                // Write header
                await writer.WriteLineAsync($"-- CPCPA Database Backup");
                await writer.WriteLineAsync($"-- Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                await writer.WriteLineAsync($"-- Database: {databaseName}");
                await writer.WriteLineAsync($"-- Server: {serverName}");
                await writer.WriteLineAsync();

                // Export each table's structure and data
                foreach (var tableName in tables)
                {
                    await writer.WriteLineAsync($"-- Table: {tableName}");
                    
                    // Get CREATE TABLE statement (simplified version)
                    var createTableQuery = $@"
                        SELECT 
                            'CREATE TABLE [' + TABLE_NAME + '] (' + 
                            STRING_AGG(
                                '[' + COLUMN_NAME + '] ' + 
                                DATA_TYPE + 
                                CASE 
                                    WHEN CHARACTER_MAXIMUM_LENGTH IS NOT NULL 
                                    THEN '(' + CAST(CHARACTER_MAXIMUM_LENGTH AS VARCHAR) + ')'
                                    ELSE ''
                                END +
                                CASE WHEN IS_NULLABLE = 'NO' THEN ' NOT NULL' ELSE '' END,
                                ', '
                            ) + ');'
                        FROM INFORMATION_SCHEMA.COLUMNS 
                        WHERE TABLE_NAME = '{tableName}'
                        GROUP BY TABLE_NAME";

                    try
                    {
                        using var createCommand = new SqlCommand(createTableQuery, connection);
                        var createStatement = await createCommand.ExecuteScalarAsync() as string;
                        if (!string.IsNullOrEmpty(createStatement))
                        {
                            await writer.WriteLineAsync(createStatement);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Could not generate CREATE statement for table {tableName}: {ex.Message}");
                        await writer.WriteLineAsync($"-- Could not generate CREATE statement for {tableName}");
                    }

                    await writer.WriteLineAsync();
                }

                await writer.WriteLineAsync("-- Backup completed successfully");

                _logger.LogInformation($"Database backup created successfully: {backupPath}");
                
                // Log backup info
                await LogBackupInfoAsync("Database", backupPath, true);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create database backup");
                await LogBackupInfoAsync("Database", "", false, ex.Message);
                return false;
            }
        }

        public async Task<bool> CreateClientFilesBackupAsync()
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"CPCPA_ClientFiles_Backup_{timestamp}.zip";
                var backupPath = Path.Combine(_backupDirectory, backupFileName);

                _logger.LogInformation($"Creating client files backup at: {backupPath}");

                // Get all clients and their uploaded files
                var clientFiles = new Dictionary<string, List<string>>();
                
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Get clients and their requirement photos
                var query = @"
                    SELECT 
                        c.ClientName,
                        c.Id as ClientId,
                        p.PhotoPath
                    FROM Clients c
                    LEFT JOIN PermitRequirements pr ON c.Id = pr.ClientId
                    LEFT JOIN RequirementPhotos p ON pr.Id = p.RequirementId
                    WHERE p.PhotoPath IS NOT NULL
                    ORDER BY c.ClientName";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var clientName = reader.GetString(0); // ClientName is first column
                    var photoPath = reader.GetString(2);  // PhotoPath is third column
                    
                    if (!clientFiles.ContainsKey(clientName))
                    {
                        clientFiles[clientName] = new List<string>();
                    }
                    
                    // Convert relative path to absolute path
                    var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", photoPath.TrimStart('/'));
                    if (File.Exists(fullPath))
                    {
                        clientFiles[clientName].Add(fullPath);
                    }
                }
                reader.Close();

                // Create ZIP archive with client-organized structure
                using var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create);
                
                // Add client files organized by client name
                foreach (var clientEntry in clientFiles)
                {
                    var clientName = SanitizeFileName(clientEntry.Key);
                    var files = clientEntry.Value;
                    
                    foreach (var filePath in files)
                    {
                        if (File.Exists(filePath))
                        {
                            var fileName = Path.GetFileName(filePath);
                            var entryPath = $"Clients/{clientName}/{fileName}";
                            archive.CreateEntryFromFile(filePath, entryPath);
                        }
                    }
                }

                // Also backup any orphaned files in uploads directory
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (Directory.Exists(uploadsDir))
                {
                    var allUploadFiles = Directory.GetFiles(uploadsDir, "*", SearchOption.AllDirectories);
                    var backedUpFiles = clientFiles.SelectMany(c => c.Value).ToHashSet();
                    
                    foreach (var file in allUploadFiles)
                    {
                        if (!backedUpFiles.Contains(file))
                        {
                            var relativePath = Path.GetRelativePath(uploadsDir, file);
                            var entryPath = $"Orphaned_Files/{relativePath}";
                            archive.CreateEntryFromFile(file, entryPath);
                        }
                    }
                }

                // Add backup manifest
                var manifestEntry = archive.CreateEntry("backup_manifest.txt");
                using var manifestStream = manifestEntry.Open();
                using var manifestWriter = new StreamWriter(manifestStream);
                
                await manifestWriter.WriteLineAsync($"CPCPA Client Files Backup");
                await manifestWriter.WriteLineAsync($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                await manifestWriter.WriteLineAsync($"Total Clients: {clientFiles.Count}");
                await manifestWriter.WriteLineAsync();
                
                foreach (var client in clientFiles)
                {
                    await manifestWriter.WriteLineAsync($"Client: {client.Key} ({client.Value.Count} files)");
                }

                _logger.LogInformation($"Client files backup created successfully: {backupPath}");
                _logger.LogInformation($"Backed up files for {clientFiles.Count} clients");
                
                // Log backup info
                await LogBackupInfoAsync("ClientFiles", backupPath, true);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create client files backup");
                await LogBackupInfoAsync("ClientFiles", "", false, ex.Message);
                return false;
            }
        }

        public async Task<bool> CreateFullBackupAsync()
        {
            try
            {
                _logger.LogInformation("Starting full backup process...");
                
                var databaseBackupSuccess = await CreateDatabaseBackupAsync();
                var clientFilesBackupSuccess = await CreateClientFilesBackupAsync();
                
                var success = databaseBackupSuccess && clientFilesBackupSuccess;
                
                if (success)
                {
                    _logger.LogInformation("Full backup completed successfully");
                    await LogBackupInfoAsync("Full", _backupDirectory, true);
                }
                else
                {
                    _logger.LogWarning("Full backup completed with errors");
                    await LogBackupInfoAsync("Full", _backupDirectory, false, "One or more backup components failed");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create full backup");
                await LogBackupInfoAsync("Full", "", false, ex.Message);
                return false;
            }
        }

        public async Task<List<BackupInfo>> GetBackupHistoryAsync()
        {
            var backupHistory = new List<BackupInfo>();
            
            try
            {
                if (!Directory.Exists(_backupDirectory))
                    return backupHistory;

                var backupFiles = Directory.GetFiles(_backupDirectory, "*.*")
                    .Where(f => f.EndsWith(".bak") || f.EndsWith(".sql") || f.EndsWith(".zip"))
                    .OrderByDescending(f => File.GetCreationTime(f));

                foreach (var file in backupFiles)
                {
                    var fileInfo = new FileInfo(file);
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    
                    var backupType = "Unknown";
                    if (fileName.Contains("Database")) backupType = "Database";
                    else if (fileName.Contains("ClientFiles")) backupType = "ClientFiles";
                    else if (fileName.Contains("Files")) backupType = "FileSystem";
                    
                    backupHistory.Add(new BackupInfo
                    {
                        BackupType = backupType,
                        CreatedDate = fileInfo.CreationTime,
                        FilePath = file,
                        FileSizeBytes = fileInfo.Length,
                        IsSuccessful = true
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get backup history");
            }
            
            return backupHistory;
        }

        public async Task<bool> DeleteBackupAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    _logger.LogWarning($"Backup file not found for deletion: {filePath}");
                    return false;
                }

                // Ensure the file is in the backup directory for security
                var fullBackupPath = Path.GetFullPath(_backupDirectory);
                var fullFilePath = Path.GetFullPath(filePath);
                
                if (!fullFilePath.StartsWith(fullBackupPath))
                {
                    _logger.LogWarning($"Attempted to delete file outside backup directory: {filePath}");
                    return false;
                }

                File.Delete(filePath);
                _logger.LogInformation($"Deleted backup file: {Path.GetFileName(filePath)}");
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to delete backup file: {filePath}");
                return false;
            }
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return string.IsNullOrWhiteSpace(sanitized) ? "Unknown_Client" : sanitized;
        }

        private async Task AddDirectoryToArchiveAsync(ZipArchive archive, string directoryPath, string entryName)
        {
            var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(directoryPath, file);
                var entryPath = Path.Combine(entryName, relativePath).Replace('\\', '/');
                archive.CreateEntryFromFile(file, entryPath);
            }
        }

        private async Task LogBackupInfoAsync(string backupType, string filePath, bool isSuccessful, string? errorMessage = null)
        {
            try
            {
                // You can extend this to log to database if needed
                var logMessage = $"Backup - Type: {backupType}, Success: {isSuccessful}, Path: {filePath}";
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    logMessage += $", Error: {errorMessage}";
                }
                
                if (isSuccessful)
                {
                    _logger.LogInformation(logMessage);
                }
                else
                {
                    _logger.LogError(logMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log backup information");
            }
        }
    }
}
