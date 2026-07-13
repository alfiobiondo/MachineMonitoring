using System.ComponentModel.DataAnnotations;

namespace MachineMonitoring.Application.Configuration;

public class DiagnosticOptions
{
    public const string SectionName = "Diagnostics";

    [Range(1, 100)]
    public int MaxConcurrency { get; init; } = 3;
}
