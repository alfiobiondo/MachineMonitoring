namespace MachineMonitoring.Application.Production.Commands;

public sealed record StartProductionLotCommand(
    Guid ProductionLotId,
    string InitialPhase,
    int? StartFromWorkpieceSequenceNumber
);
