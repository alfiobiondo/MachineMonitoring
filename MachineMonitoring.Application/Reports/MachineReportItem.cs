using MachineMonitoring.Application.Diagnostics;
using MachineMonitoring.Domain;

namespace MachineMonitoring.Application.Reports;

public class MachineReportItem
{
    public Machine Machine { get; }

    public string Description { get; }

    public MachineDiagnostic? Diagnostic { get; }

    public string? DiagnosticError { get; }

    public bool HasDiagnostic => Diagnostic is not null;

    public MachineReportItem(
        Machine machine,
        string description,
        MachineDiagnostic? diagnostic,
        string? diagnosticError
    )
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        bool hasDiagnostic = diagnostic is not null;
        bool hasError = !string.IsNullOrWhiteSpace(diagnosticError);

        if (hasDiagnostic == hasError)
        {
            throw new ArgumentException(
                "The item must contain either a diagnostic or a diagnostic error."
            );
        }

        if (diagnostic is not null && machine.Id != diagnostic.MachineId)
        {
            throw new ArgumentException(
                "The diagnostic does not belong to the specified machine.",
                nameof(diagnostic)
            );
        }

        Machine = machine;
        Description = description;
        Diagnostic = diagnostic;
        DiagnosticError = diagnosticError;
    }
}
