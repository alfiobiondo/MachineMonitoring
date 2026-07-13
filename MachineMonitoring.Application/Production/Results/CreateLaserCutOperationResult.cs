using MachineMonitoring.Domain.Production;
using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Application.Production.Results;

public sealed record CreateLaserCutOperationResult(
    Guid OperationId,
    Guid ConfigurationId,
    MachineOperationStatus OperationStatus,
    WorkpieceGeometryType GeometryType
);
