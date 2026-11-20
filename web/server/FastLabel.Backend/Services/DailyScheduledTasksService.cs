namespace FastLabel.Backend.Services;

public class DailyScheduledTasksService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IConfiguration _configuration = configuration;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRunUtc = GetNextDailyRunUtc(_configuration);
            var delay = nextRunUtc - DateTime.UtcNow;

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, stoppingToken);
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await ExecuteDailyTasksAsync(stoppingToken);

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private static DateTime GetNextDailyRunUtc(IConfiguration configuration)
    {
        if (!TimeSpan.TryParse("00:00", out var targetTimeOfDay))
        {
            targetTimeOfDay = new TimeSpan(3, 0, 0);
        }

        var nowUtc = DateTime.UtcNow;
        var candidateToday = nowUtc.Date.Add(targetTimeOfDay);

        return candidateToday > nowUtc
            ? candidateToday
            : candidateToday.AddDays(1);
    }

    private async Task ExecuteDailyTasksAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            //TODO : Add scheduled tasks

            await db.SaveChangesAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
        }
    }
}