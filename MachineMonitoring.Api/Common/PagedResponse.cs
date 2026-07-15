namespace MachineMonitoring.Api.Common;

public sealed record PagedResponse<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages
);
