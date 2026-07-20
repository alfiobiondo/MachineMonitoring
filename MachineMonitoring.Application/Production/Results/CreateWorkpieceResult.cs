using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Results;

public sealed record CreateWorkpieceResult(
    Guid WorkpieceId,
    Guid ProductionLotId,
    int SequenceNumber,
    string Code,
    string MaterialCode,
    WorkpieceStatus Status
);
