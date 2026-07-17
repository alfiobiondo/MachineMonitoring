using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Application.Production.Repositories;

public interface IMachineAlarmRepository
{
    Task<MachineAlarm?> GetByIdAsync(Guid alarmId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MachineAlarm>> GetByMachineIdAsync(
        string machineId,
        bool activeOnly,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyCollection<MachineAlarm>> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken
    );

    Task AddAsync(MachineAlarm alarm, CancellationToken cancellationToken);

    Task UpdateAsync(MachineAlarm alarm, CancellationToken cancellationToken);
}
