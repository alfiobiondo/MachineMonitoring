namespace MachineMonitoring.Infrastructure.Persistence.Models;

public sealed class OutboxMessageRecord
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
