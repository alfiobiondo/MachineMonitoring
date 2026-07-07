using MachineMonitoring.Application.Reports;
using MachineMonitoring.Domain;
using Microsoft.Extensions.Logging;

namespace MachineMonitoring.Application;

public class MachineManager
{
    private readonly IMachineProvider _machineProvider;
    private readonly MachineFormatter _machineFormatter;
    private readonly ILogger<MachineManager> _logger;

    public MachineManager(
        IMachineProvider machineProvider,
        MachineFormatter machineFormatter,
        ILogger<MachineManager> logger
    )
    {
        ArgumentNullException.ThrowIfNull(machineProvider);
        ArgumentNullException.ThrowIfNull(machineFormatter);
        ArgumentNullException.ThrowIfNull(logger);

        _machineProvider = machineProvider;
        _machineFormatter = machineFormatter;
        _logger = logger;
    }

    public Task<IReadOnlyCollection<Machine>> GetMachinesAsync(CancellationToken cancellationToken)
    {
        return _machineProvider.GetMachinesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> GetMachineDescriptionsAsync(
        CancellationToken cancellationToken
    )
    {
        IReadOnlyCollection<Machine> machines = await _machineProvider.GetMachinesAsync(
            cancellationToken
        );

        List<string> descriptions = new();

        foreach (Machine machine in machines)
        {
            if (machine.Status == MachineStatus.Alarm)
            {
                _logger.LogWarning("Machine {MachineId} is in alarm state.", machine.Id);
            }
            else if (machine.Status == MachineStatus.Offline)
            {
                _logger.LogWarning("Machine {MachineId} is offline.", machine.Id);
            }

            string description = _machineFormatter.Format(machine);

            descriptions.Add(description);
        }

        return descriptions;
    }

    public string GetDetailedMachineDescription(Machine machine)
    {
        ArgumentNullException.ThrowIfNull(machine);

        return _machineFormatter.FormatDetailed(machine);
    }

    public async Task<IReadOnlyCollection<string>> GetDetailedMachineDescriptionsAsync(
        CancellationToken cancellationToken
    )
    {
        _logger.LogDebug("Retrieving machines asynchronously for detailed descriptions.");

        IReadOnlyCollection<Machine> machines = await _machineProvider.GetMachinesAsync(
            cancellationToken
        );

        List<string> descriptions = new();

        foreach (Machine machine in machines)
        {
            if (machine.Status == MachineStatus.Alarm)
            {
                _logger.LogWarning("Machine {MachineId} is in alarm state.", machine.Id);
            }
            else if (machine.Status == MachineStatus.Offline)
            {
                _logger.LogWarning("Machine {MachineId} is offline.", machine.Id);
            }

            string description = _machineFormatter.FormatDetailed(machine);

            descriptions.Add(description);
        }

        return descriptions;
    }

    public async Task<MachineReport> CreateReportAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Creating machine report.");

        IReadOnlyCollection<Machine> machines = await _machineProvider.GetMachinesAsync(
            cancellationToken
        );

        List<Machine> orderedMachines = machines
            .OrderBy(machine => GetStatusPriority(machine.Status))
            .ThenBy(machine => machine.Id)
            .ToList();

        List<string> descriptions = new();

        foreach (Machine machine in orderedMachines)
        {
            LogAbnormalStatus(machine);

            string description = _machineFormatter.FormatDetailed(machine);

            descriptions.Add(description);
        }

        MachineStatusSummary statusSummary = CreateStatusSummary(machines);

        MachineReport report = new(
            generatedAt: DateTimeOffset.Now,
            machines: orderedMachines,
            descriptions: descriptions,
            statusSummary: statusSummary
        );

        _logger.LogInformation(
            "Machine report created for {MachineCount} machines.",
            machines.Count
        );

        return report;
    }

    private void LogAbnormalStatus(Machine machine)
    {
        if (machine.Status == MachineStatus.Alarm)
        {
            _logger.LogWarning("Machine {MachineId} is in alarm state.", machine.Id);
        }
        else if (machine.Status == MachineStatus.Offline)
        {
            _logger.LogWarning("Machine {MachineId} is offline.", machine.Id);
        }
    }

    private static MachineStatusSummary CreateStatusSummary(IReadOnlyCollection<Machine> machines)
    {
        Dictionary<MachineStatus, int> counts = machines
            .GroupBy(machine => machine.Status)
            .ToDictionary(group => group.Key, group => group.Count());

        return new MachineStatusSummary(counts);
    }

    private static int GetStatusPriority(MachineStatus status)
    {
        return status switch
        {
            MachineStatus.Alarm => 1,
            MachineStatus.Offline => 2,
            MachineStatus.Idle => 3,
            MachineStatus.Running => 4,
            _ => 5,
        };
    }
}
