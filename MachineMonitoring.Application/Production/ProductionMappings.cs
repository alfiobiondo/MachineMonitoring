using MachineMonitoring.Application.Production.Results;
using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production;

internal static class ProductionMappings
{
    public static MachineOperationSummaryResult ToSummaryResult(MachineOperation operation)
    {
        return new MachineOperationSummaryResult(
            Id: operation.Id,
            WorkpieceId: operation.WorkpieceId,
            SequenceNumber: operation.SequenceNumber,
            MachineId: operation.MachineId,
            Type: operation.Type,
            Status: operation.Status,
            ProgressPercentage: operation.ProgressPercentage,
            CurrentPhase: operation.CurrentPhase,
            FailureReason: operation.FailureReason,
            CreatedAt: operation.CreatedAt,
            StartedAt: operation.StartedAt,
            CompletedAt: operation.CompletedAt
        );
    }
}
