using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Infrastructure.Persistence.Outbox;

public sealed class OutboxProcessingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly int _batchSize;
    private readonly TimeSpan _pollingInterval;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OutboxProcessingBackgroundService> _logger;

    public OutboxProcessingBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxProcessingOptions> options,
        TimeProvider timeProvider,
        ILogger<OutboxProcessingBackgroundService> logger
    )
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        OutboxProcessingOptions configuredOptions = options.Value;

        _scopeFactory = scopeFactory;
        _batchSize = configuredOptions.BatchSize;
        _pollingInterval = TimeSpan.FromSeconds(configuredOptions.PollingIntervalSeconds);
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processing worker started.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                bool shouldDelay = true;

                try
                {
                    OutboxProcessingResult result = await ProcessOnceAsync(stoppingToken);

                    if (result.AttemptedCount == 0)
                    {
                        _logger.LogDebug(
                            "Outbox batch is empty. AttemptedCount={AttemptedCount}, SucceededCount={SucceededCount}, FailedCount={FailedCount}.",
                            result.AttemptedCount,
                            result.SucceededCount,
                            result.FailedCount
                        );
                    }
                    else if (result.FailedCount > 0)
                    {
                        _logger.LogWarning(
                            "Outbox batch completed with failures. AttemptedCount={AttemptedCount}, SucceededCount={SucceededCount}, FailedCount={FailedCount}.",
                            result.AttemptedCount,
                            result.SucceededCount,
                            result.FailedCount
                        );
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Outbox batch completed successfully. AttemptedCount={AttemptedCount}, SucceededCount={SucceededCount}, FailedCount={FailedCount}.",
                            result.AttemptedCount,
                            result.SucceededCount,
                            result.FailedCount
                        );
                    }

                    shouldDelay =
                        result.AttemptedCount != _batchSize || result.FailedCount > 0;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Unexpected error during outbox processing."
                    );
                }

                if (!shouldDelay)
                {
                    continue;
                }

                try
                {
                    _logger.LogDebug(
                        "Waiting {PollingIntervalSeconds} seconds before the next outbox polling cycle.",
                        _pollingInterval.TotalSeconds
                    );

                    await Task.Delay(_pollingInterval, _timeProvider, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        finally
        {
            _logger.LogInformation("Outbox processing worker stopped.");
        }
    }

    private async Task<OutboxProcessingResult> ProcessOnceAsync(CancellationToken stoppingToken)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

        IOutboxProcessor processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();

        return await processor.ProcessPendingAsync(_batchSize, stoppingToken);
    }
}
