namespace SessionService.Infrastructure.BackgroundServices;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SessionService.Application.Sessions;

public class ClueAutoReleaseWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ClueAutoReleaseWorker> _logger;
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    public ClueAutoReleaseWorker(IServiceProvider services, ILogger<ClueAutoReleaseWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ClueAutoReleaseWorker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<ClueAutoReleaseService>();
                await svc.ProcessAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unexpected error in ClueAutoReleaseWorker");
            }
            await Task.Delay(_interval, stoppingToken);
        }
    }
}
