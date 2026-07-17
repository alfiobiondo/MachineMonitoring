namespace MachineMonitoring.Api.Machines;

public sealed record MachineDetailsResponse(
    string Id,
    string Name,
    string Location,
    string SerialNumber,
    string CatalogStatus,
    MachineRuntimeStateResponse Runtime
);
