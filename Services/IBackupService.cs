namespace TestingDemo.Services
{
    public interface IBackupService
    {
        Task<bool> CreateDatabaseBackupAsync();
        Task<bool> CreateClientFilesBackupAsync();
        Task<bool> CreateFullBackupAsync();
        Task<List<BackupInfo>> GetBackupHistoryAsync();
        Task<bool> DeleteBackupAsync(string filePath);
    }

    public class BackupInfo
    {
        public string BackupType { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public bool IsSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
