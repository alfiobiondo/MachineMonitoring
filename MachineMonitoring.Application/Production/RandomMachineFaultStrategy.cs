using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Domain.Production;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Application.Production;

public sealed class RandomMachineFaultStrategy : IMachineFaultStrategy
{
    private readonly OperationSimulatorOptions _options;
    private readonly Random _random;

    public RandomMachineFaultStrategy(IOptions<OperationSimulatorOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _random = Random.Shared;
    }

    public MachineFaultDecision Evaluate(string machineId, Guid? currentOperationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);

        if (
            !_options.MachineFaultSimulationEnabled
            || _options.MachineFaultProbabilityPercentage <= 0
        )
        {
            return MachineFaultDecision.None;
        }

        if (_random.Next(0, 100) >= _options.MachineFaultProbabilityPercentage)
        {
            return MachineFaultDecision.None;
        }

        return new MachineFaultDecision(
            ShouldFault: true,
            AlarmCode: "SIM_MACHINE_FAULT",
            Severity: MachineAlarmSeverity.Error,
            Message: "Simulated machine fault.",
            Reason: "Simulated machine fault."
        );
    }
}
