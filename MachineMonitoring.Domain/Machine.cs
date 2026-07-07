namespace MachineMonitoring.Domain;

public class Machine
{
    public string Id { get; }
    public string Name { get; }
    public MachineStatus Status { get; }

    public string Location { get; }

    public Machine(string id, string name, MachineStatus status, string location)
    {
        Id = id;
        Name = name;
        Status = status;
        Location = location;
    }
}
