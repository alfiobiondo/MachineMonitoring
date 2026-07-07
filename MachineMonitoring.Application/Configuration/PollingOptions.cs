using System.ComponentModel.DataAnnotations;

namespace MachineMonitoring.Application.Configuration;

public class PollingOptions
{
    public const string SectionName = "Polling";

    [Range(1, 300)]
    public int IntervalSeconds { get; set; }

    [Range(1, 60)]
    public int InitialDelaySeconds { get; set; }
}
