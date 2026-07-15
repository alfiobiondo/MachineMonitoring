namespace MachineMonitoring.Api.Catalogs;

public sealed record MaterialResponse(
    Guid Id,
    string Code,
    string Name,
    string Category,
    string Grade,
    bool IsEnabled
);
