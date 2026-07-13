namespace MachineMonitoring.Domain.Technology;

public sealed class DrawingFile
{
    public Guid Id { get; }

    public string OriginalFileName { get; }

    public string StoredFileName { get; }

    public string ContentType { get; }

    public long SizeBytes { get; }

    public string Sha256Hash { get; }

    public DateTimeOffset UploadedAt { get; }

    public DrawingFile(
        Guid id,
        string originalFileName,
        string storedFileName,
        string contentType,
        long sizeBytes,
        string sha256Hash,
        DateTimeOffset uploadedAt
    )
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The drawing file ID cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);

        ArgumentException.ThrowIfNullOrWhiteSpace(storedFileName);

        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        ArgumentException.ThrowIfNullOrWhiteSpace(sha256Hash);

        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sizeBytes),
                "The drawing file size must be greater than zero."
            );
        }

        if (!originalFileName.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The drawing file must have a .dwg extension.",
                nameof(originalFileName)
            );
        }

        Id = id;
        OriginalFileName = originalFileName;
        StoredFileName = storedFileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Sha256Hash = sha256Hash;
        UploadedAt = uploadedAt;
    }
}
