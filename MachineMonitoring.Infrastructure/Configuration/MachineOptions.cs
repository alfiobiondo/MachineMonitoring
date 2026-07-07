using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MachineMonitoring.Domain;

namespace MachineMonitoring.Infrastructure.Configuration;

public class MachineOptions
{
    public const string SectionName = "Machine";

    [Required]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public MachineStatus? Status { get; set; }

    [Required]
    public string Location { get; set; } = string.Empty;
}
