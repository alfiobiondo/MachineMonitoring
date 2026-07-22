namespace MachineMonitoring.Api.Production;

public sealed record CreateWorkpieceResponse(
    Guid WorkpieceId,
    Guid ProductionLotId,
    int SequenceNumber,
    string Code,
    string MaterialCode,
    string Status
);
