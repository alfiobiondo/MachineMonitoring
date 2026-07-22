using MachineMonitoring.Application.Configuration;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Application.Production;

public sealed class MachineIncidentCooldownTracker
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _minimumInterval;
    private readonly Dictionary<string, DateTimeOffset> _lastIncidentByMachineId = [];
    private readonly object _syncRoot = new();

    public MachineIncidentCooldownTracker(
        IOptions<MachineIncidentSimulatorOptions> options,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
        _minimumInterval = TimeSpan.FromSeconds(options.Value.MinimumSecondsBetweenIncidents);
    }

    public bool IsInCooldown(string machineId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);

        if (_minimumInterval == TimeSpan.Zero)
        {
            return false;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();

        lock (_syncRoot)
        {
            return _lastIncidentByMachineId.TryGetValue(machineId, out DateTimeOffset lastIncidentAt)
                && now - lastIncidentAt < _minimumInterval;
        }
    }

    public void RecordIncident(string machineId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);

        lock (_syncRoot)
        {
            _lastIncidentByMachineId[machineId] = _timeProvider.GetUtcNow();
        }
    }
}
