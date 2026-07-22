using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Infrastructure.Persistence.Models;

public sealed class ProductionLotRecord
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public int PlannedQuantity { get; set; }

    public ProductionLotStatus Status { get; set; }

    public ProductionLotExecutionMode ExecutionMode { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public List<WorkpieceRecord> Workpieces { get; set; } = [];
}
