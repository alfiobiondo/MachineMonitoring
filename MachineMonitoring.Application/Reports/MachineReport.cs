namespace MachineMonitoring.Application.Reports;

public class MachineReport
{
    public DateTimeOffset GeneratedAt { get; }

    public IReadOnlyCollection<MachineReportItem> Items { get; }

    public MachineStatusSummary StatusSummary { get; }

    public int SuccessfulDiagnosticCount => Items.Count(item => item.HasDiagnostic);

    public int FailedDiagnosticCount => Items.Count(item => !item.HasDiagnostic);

    public MachineReport(
        DateTimeOffset generatedAt,
        IReadOnlyCollection<MachineReportItem> items,
        MachineStatusSummary statusSummary
    )
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(statusSummary);

        if (statusSummary.TotalCount != items.Count)
        {
            throw new ArgumentException(
                "The status summary does not match the machine collection.",
                nameof(statusSummary)
            );
        }

        GeneratedAt = generatedAt;
        Items = items;
        StatusSummary = statusSummary;
    }
}
