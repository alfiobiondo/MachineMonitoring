using MachineMonitoring.Domain;

namespace MachineMonitoring.Application.Reports;

public class MachineReport
{
    public DateTimeOffset GeneratedAt { get; }

    public IReadOnlyCollection<Machine> Machines { get; }

    public IReadOnlyCollection<string> Descriptions { get; }

    public MachineStatusSummary StatusSummary { get; }

    public MachineReport(
        DateTimeOffset generatedAt,
        IReadOnlyCollection<Machine> machines,
        IReadOnlyCollection<string> descriptions,
        MachineStatusSummary statusSummary
    )
    {
        ArgumentNullException.ThrowIfNull(machines);
        ArgumentNullException.ThrowIfNull(descriptions);
        ArgumentNullException.ThrowIfNull(statusSummary);

        if (machines.Count != descriptions.Count)
        {
            throw new ArgumentException(
                "Each machine must have a corresponding description.",
                nameof(descriptions)
            );
        }

        if (statusSummary.TotalCount != machines.Count)
        {
            throw new ArgumentException(
                "The status summary does not match the machine collection.",
                nameof(statusSummary)
            );
        }

        GeneratedAt = generatedAt;
        Machines = machines;
        Descriptions = descriptions;
        StatusSummary = statusSummary;
    }
}
