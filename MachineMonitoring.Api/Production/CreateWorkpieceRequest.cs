namespace MachineMonitoring.Api.Production;

public sealed record CreateWorkpieceRequest(
    Guid ProductionLotId,
    int SequenceNumber,
    string Code,
    string MaterialCode
);
