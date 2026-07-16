namespace MachineMonitoring.Api.Operations;

public sealed record CreateMachineOperationResponse(
    Guid OperationId,
    Guid ConfigurationId,
    int SequenceNumber,
    string OperationStatus,
    string GeometryType
);
