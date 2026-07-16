using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Infrastructure.Persistence.Models;

public sealed class WorkpieceRecord
{
    public Guid Id { get; set; }

    public Guid ProductionLotId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string MaterialCode { get; set; } = string.Empty;

    public WorkpieceStatus Status { get; set; }

    public bool IsSequenceActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public ProductionLotRecord ProductionLot { get; set; } = null!;

    public List<MachineOperationRecord> Operations { get; set; } = [];
}
