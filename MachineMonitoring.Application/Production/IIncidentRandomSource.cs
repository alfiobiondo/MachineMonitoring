namespace MachineMonitoring.Application.Production;

public interface IIncidentRandomSource
{
    double NextPercentage();
}
