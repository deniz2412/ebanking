using EBanking.AuditService.Services.Interfaces;

namespace EBanking.AuditService.Services;

public class IntegrityVerificationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IntegrityVerificationService> _logger;
    private readonly TimeSpan _verificationInterval;

    public IntegrityVerificationService(IServiceProvider serviceProvider, IConfiguration configuration, 
        ILogger<IntegrityVerificationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Default to checking every hour, configurable
        var intervalMinutes = configuration.GetValue("IntegrityCheck:IntervalMinutes", 60);
        _verificationInterval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Integrity verification service started with interval: {Interval}", _verificationInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformIntegrityCheckAsync();
                await Task.Delay(_verificationInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic integrity verification");
                
                // Wait a shorter time before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Integrity verification service stopped");
    }

    private async Task PerformIntegrityCheckAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

        try
        {
            // Verify the last 1000 records by default
            var verificationResult = await auditService.VerifyIntegrityAsync();

            if (verificationResult.IsValid)
            {
                _logger.LogInformation("Periodic integrity check passed - Range: {FromSequence}-{ToSequence}, Records: {RecordsChecked}",
                    verificationResult.FromSequence, verificationResult.ToSequence, verificationResult.RecordsChecked);
            }
            else
            {
                _logger.LogWarning("Periodic integrity check FAILED - Range: {FromSequence}-{ToSequence}, Records: {RecordsChecked}, Issues: {IssueCount}",
                    verificationResult.FromSequence, verificationResult.ToSequence, verificationResult.RecordsChecked, 
                    verificationResult.Issues?.Length ?? 0);

                // Log each issue
                if (verificationResult.Issues != null)
                {
                    foreach (var issue in verificationResult.Issues)
                    {
                        _logger.LogWarning("Integrity issue detected: {Issue}", issue);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing periodic integrity check");
        }
    }
}
