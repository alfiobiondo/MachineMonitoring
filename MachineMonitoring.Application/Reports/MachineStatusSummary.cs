using MachineMonitoring.Domain;

namespace MachineMonitoring.Application.Reports;

public class MachineStatusSummary
{
    public IReadOnlyDictionary<MachineStatus, int> Counts { get; }

    public int TotalCount { get; }

    public MachineStatusSummary(IReadOnlyDictionary<MachineStatus, int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);

        if (counts.Values.Any(count => count < 0))
        {
            throw new ArgumentException(
                "Machine status counts cannot be negative.",
                nameof(counts)
            );
        }

        Counts = new Dictionary<MachineStatus, int>(counts);
        TotalCount = counts.Values.Sum();
    }

    public int GetCount(MachineStatus status)
    {
        return Counts.TryGetValue(status, out int count) ? count : 0;
    }
}
