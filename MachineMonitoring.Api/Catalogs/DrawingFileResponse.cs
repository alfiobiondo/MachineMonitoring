namespace MachineMonitoring.Api.Catalogs;

public sealed record DrawingFileResponse(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hash,
    DateTimeOffset UploadedAt
);
