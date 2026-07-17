using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Domain.Production;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Application.Production;

public sealed class RandomOperationFaultStrategy : IOperationFaultStrategy
{
    private readonly OperationSimulatorOptions _options;
    private readonly Random _random;

    public RandomOperationFaultStrategy(IOptions<OperationSimulatorOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _random = Random.Shared;
    }

    public OperationFaultDecision Evaluate(MachineOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (
            !_options.OperationFaultSimulationEnabled
            || _options.OperationFaultProbabilityPercentage <= 0
        )
        {
            return OperationFaultDecision.None;
        }

        if (_random.Next(0, 100) >= _options.OperationFaultProbabilityPercentage)
        {
            return OperationFaultDecision.None;
        }

        return new OperationFaultDecision(
            ShouldFault: true,
            AlarmCode: "SIM_OP_FAULT",
            Severity: MachineAlarmSeverity.Warning,
            Message: "Simulated operation fault.",
            Reason: "Simulated operation fault."
        );
    }
}
