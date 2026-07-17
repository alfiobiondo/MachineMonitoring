namespace MachineMonitoring.Application.Production.Commands;

public sealed record StartWorkpieceCommand(
    Guid WorkpieceId,
    string InitialPhase,
    int? StartFromSequenceNumber
);
