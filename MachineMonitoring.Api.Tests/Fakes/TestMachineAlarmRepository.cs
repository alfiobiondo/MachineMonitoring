using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Api.Tests.Fakes;

public sealed class TestMachineAlarmRepository : IMachineAlarmRepository
{
    private readonly Dictionary<Guid, MachineAlarm> _items = [];

    public Task<MachineAlarm?> GetByIdAsync(Guid alarmId, CancellationToken cancellationToken)
    {
        _items.TryGetValue(alarmId, out MachineAlarm? alarm);
        return Task.FromResult(alarm);
    }

    public Task<IReadOnlyCollection<MachineAlarm>> GetByMachineIdAsync(
        string machineId,
        bool activeOnly,
        CancellationToken cancellationToken
    )
    {
        IEnumerable<MachineAlarm> query = _items.Values.Where(item => item.MachineId == machineId);

        if (activeOnly)
        {
            query = query.Where(item => item.Status != MachineAlarmStatus.Resolved);
        }

        return Task.FromResult<IReadOnlyCollection<MachineAlarm>>(
            query.OrderByDescending(item => item.RaisedAt).ThenByDescending(item => item.Id).ToArray()
        );
    }

    public Task<IReadOnlyCollection<MachineAlarm>> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult<IReadOnlyCollection<MachineAlarm>>(
            _items.Values
                .Where(item => item.MachineOperationId == operationId)
                .OrderByDescending(item => item.RaisedAt)
                .ThenByDescending(item => item.Id)
                .ToArray()
        );
    }

    public Task AddAsync(MachineAlarm alarm, CancellationToken cancellationToken)
    {
        _items.Add(alarm.Id, alarm);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MachineAlarm alarm, CancellationToken cancellationToken)
    {
        _items[alarm.Id] = alarm;
        return Task.CompletedTask;
    }

    public void Clear() => _items.Clear();
}
