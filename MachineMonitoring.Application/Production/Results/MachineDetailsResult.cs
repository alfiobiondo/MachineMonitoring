using MachineMonitoring.Domain;

namespace MachineMonitoring.Application.Production.Results;

public sealed record MachineDetailsResult(
    string Id,
    string Name,
    string Location,
    string SerialNumber,
    MachineStatus CatalogStatus,
    MachineRuntimeStateResult Runtime
);
