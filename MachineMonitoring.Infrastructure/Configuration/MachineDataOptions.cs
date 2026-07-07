using System.ComponentModel.DataAnnotations;

namespace MachineMonitoring.Infrastructure.Configuration;

public class MachineDataOptions
{
    public const string SectionName = "MachineData";

    [Required]
    public string FilePath { get; set; } = string.Empty;
}
