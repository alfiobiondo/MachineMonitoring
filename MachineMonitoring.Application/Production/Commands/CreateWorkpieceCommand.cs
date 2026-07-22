namespace MachineMonitoring.Application.Production.Commands;

public sealed record CreateWorkpieceCommand(
    Guid ProductionLotId,
    int SequenceNumber,
    string Code,
    string MaterialCode
);
