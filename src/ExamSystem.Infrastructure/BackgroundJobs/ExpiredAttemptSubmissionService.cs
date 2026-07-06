using ExamSystem.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExamSystem.Infrastructure.BackgroundJobs;

/// <summary>Periodically closes expired attempts (FR-2.7), supplementing 1b's lazy auto-submit.</summary>
public class ExpiredAttemptSubmissionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredAttemptSubmissionService> _logger;
    private readonly TimeSpan _interval;

    public ExpiredAttemptSubmissionService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ExpiredAttemptSubmissionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var seconds = configuration.GetValue<int?>("AutoSubmit:IntervalSeconds") ?? 60;
        _interval = TimeSpan.FromSeconds(Math.Max(5, seconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var closer = scope.ServiceProvider.GetRequiredService<IExpiredAttemptCloser>();
                var closed = await closer.CloseExpiredAsync(DateTime.UtcNow, stoppingToken);
                if (closed > 0)
                {
                    _logger.LogInformation("Auto-submitted {Count} expired attempt(s).", closed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Expired-attempt auto-submit tick failed; will retry next interval.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
