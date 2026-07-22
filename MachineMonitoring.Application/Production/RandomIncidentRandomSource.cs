namespace MachineMonitoring.Application.Production;

public sealed class RandomIncidentRandomSource : IIncidentRandomSource
{
    private readonly Random _random = new();

    public double NextPercentage()
    {
        return _random.NextDouble() * 100;
    }
}
