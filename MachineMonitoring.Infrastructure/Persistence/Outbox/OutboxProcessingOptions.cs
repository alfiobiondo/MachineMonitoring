using System.ComponentModel.DataAnnotations;

namespace MachineMonitoring.Infrastructure.Persistence.Outbox;

public sealed class OutboxProcessingOptions
{
    public const string SectionName = "OutboxProcessing";

    [Range(1, 1000)]
    public int BatchSize { get; init; } = 100;

    [Range(1, 3600)]
    public int PollingIntervalSeconds { get; init; } = 5;
}
