namespace MachineMonitoring.Domain;

public class Machine
{
    public string Id { get; }
    public string Name { get; }
    public MachineStatus Status { get; }

    public string Location { get; }

    public string SerialNumber { get; }

    public Machine(
        string id,
        string name,
        MachineStatus status,
        string location,
        string serialNumber
    )
    {
        Id = id;
        Name = name;
        Status = status;
        Location = location;
        SerialNumber = serialNumber;
    }
}
