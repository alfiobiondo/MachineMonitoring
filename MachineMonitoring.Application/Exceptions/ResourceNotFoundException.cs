namespace MachineMonitoring.Application.Exceptions;

public sealed class ResourceNotFoundException : Exception
{
    public string ResourceType { get; }

    public string ResourceId { get; }

    public ResourceNotFoundException(string resourceType, string resourceId)
        : base($"{resourceType} '{resourceId}' was not found.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);

        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

        ResourceType = resourceType;
        ResourceId = resourceId;
    }
}
