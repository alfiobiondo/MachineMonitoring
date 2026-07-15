namespace MachineMonitoring.Api.Operations;

public sealed record CreateMachineOperationResponse(
    Guid OperationId,
    Guid ConfigurationId,
    string OperationStatus,
    string GeometryType
);
