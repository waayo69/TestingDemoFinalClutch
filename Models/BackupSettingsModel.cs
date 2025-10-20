using System.ComponentModel.DataAnnotations;

namespace TestingDemo.Models
{
    public class BackupSettingsModel
    {
        [Display(Name = "Enable Automatic Backups")]
        public bool Enabled { get; set; } = true;

        [Display(Name = "Backup Type")]
        [Required]
        public string BackupType { get; set; } = "Full";

        [Display(Name = "Backup Interval (Hours)")]
        [Range(1, 168, ErrorMessage = "Interval must be between 1 and 168 hours (1 week)")]
        public int IntervalHours { get; set; } = 24;

        [Display(Name = "Start Time (24-hour format)")]
        [Required]
        [RegularExpression(@"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "Please enter time in HH:MM format (e.g., 02:00)")]
        public string StartTime { get; set; } = "02:00";

        [Display(Name = "Retention Days")]
        [Range(1, 365, ErrorMessage = "Retention must be between 1 and 365 days")]
        public int RetentionDays { get; set; } = 30;

        [Display(Name = "Backup Directory")]
        public string BackupDirectory { get; set; } = "Backups";
    }
}
