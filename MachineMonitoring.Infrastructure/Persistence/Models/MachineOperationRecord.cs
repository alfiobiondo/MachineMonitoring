using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Infrastructure.Persistence.Models;

public sealed class MachineOperationRecord
{
    public Guid Id { get; set; }

    public Guid WorkpieceId { get; set; }

    public int SequenceNumber { get; set; }

    public string MachineId { get; set; } = string.Empty;

    public MachineOperationType Type { get; set; }

    public MachineOperationStatus Status { get; set; }

    public int ProgressPercentage { get; set; }

    public string? CurrentPhase { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public WorkpieceRecord Workpiece { get; set; } = null!;

    public LaserCutConfigurationRecord? LaserCutConfiguration { get; set; }

    public List<MachineOperationEventRecord> Events { get; set; } = [];

    public List<MachineAlarmRecord> MachineAlarms { get; set; } = [];
}
