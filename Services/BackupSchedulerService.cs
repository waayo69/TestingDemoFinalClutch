namespace TestingDemo.Services
{
    public class BackupSchedulerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BackupSchedulerService> _logger;
        private Timer? _timer;

        public BackupSchedulerService(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<BackupSchedulerService> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Backup Scheduler Service started");

            // Get configuration settings
            var isEnabled = _configuration.GetValue<bool>("BackupSettings:Enabled", true);
            if (!isEnabled)
            {
                _logger.LogInformation("Backup scheduling is disabled in configuration");
                return;
            }

            var intervalHours = _configuration.GetValue<int>("BackupSettings:IntervalHours", 24);
            var startTime = _configuration.GetValue<string>("BackupSettings:StartTime", "02:00");
            
            // Calculate initial delay to start at the specified time
            var initialDelay = CalculateInitialDelay(startTime);
            var interval = TimeSpan.FromHours(intervalHours);

            _logger.LogInformation($"Backup scheduled to run every {intervalHours} hours starting at {startTime}");
            _logger.LogInformation($"Next backup will run in {initialDelay.TotalHours:F1} hours");

            // Start the timer
            _timer = new Timer(ExecuteBackup, null, initialDelay, interval);

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async void ExecuteBackup(object? state)
        {
            try
            {
                _logger.LogInformation("Starting scheduled backup...");

                using var scope = _serviceProvider.CreateScope();
                var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();

                var backupType = _configuration.GetValue<string>("BackupSettings:BackupType", "Full");
                
                bool success = backupType.ToLower() switch
                {
                    "database" => await backupService.CreateDatabaseBackupAsync(),
                    "clientfiles" => await backupService.CreateClientFilesBackupAsync(),
                    "full" => await backupService.CreateFullBackupAsync(),
                    _ => await backupService.CreateFullBackupAsync()
                };

                if (success)
                {
                    _logger.LogInformation("Scheduled backup completed successfully");
                }
                else
                {
                    _logger.LogError("Scheduled backup failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled backup execution");
            }
        }

        private TimeSpan CalculateInitialDelay(string startTime)
        {
            try
            {
                if (TimeSpan.TryParse(startTime, out var targetTime))
                {
                    var now = DateTime.Now;
                    var today = now.Date;
                    var targetDateTime = today.Add(targetTime);

                    // If the target time has already passed today, schedule for tomorrow
                    if (targetDateTime <= now)
                    {
                        targetDateTime = targetDateTime.AddDays(1);
                    }

                    return targetDateTime - now;
                }
                else
                {
                    _logger.LogWarning($"Invalid start time format: {startTime}. Using default delay of 1 hour.");
                    return TimeSpan.FromHours(1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating initial delay for start time: {startTime}");
                return TimeSpan.FromHours(1);
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Backup Scheduler Service is stopping");
            
            _timer?.Change(Timeout.Infinite, 0);
            _timer?.Dispose();
            
            await base.StopAsync(stoppingToken);
        }

        public override void Dispose()
        {
            _timer?.Dispose();
            base.Dispose();
        }
    }
}
