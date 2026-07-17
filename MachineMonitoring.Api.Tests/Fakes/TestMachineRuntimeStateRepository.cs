using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Api.Tests.Fakes;

public sealed class TestMachineRuntimeStateRepository : IMachineRuntimeStateRepository
{
    private readonly Dictionary<string, MachineRuntimeState> _items = new(
        StringComparer.OrdinalIgnoreCase
    );

    private readonly object _syncRoot = new();

    public Task<MachineRuntimeState?> GetByMachineIdAsync(
        string machineId,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _items.TryGetValue(machineId, out MachineRuntimeState? state);
            return Task.FromResult(state);
        }
    }

    public Task<IReadOnlyCollection<MachineRuntimeState>> GetAllAsync(
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            return Task.FromResult<IReadOnlyCollection<MachineRuntimeState>>(
                _items.Values.OrderBy(item => item.MachineId).ToArray()
            );
        }
    }

    public Task AddAsync(MachineRuntimeState state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _items[state.MachineId] = state;
            return Task.CompletedTask;
        }
    }

    public Task UpdateAsync(MachineRuntimeState state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _items[state.MachineId] = state;
            return Task.CompletedTask;
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _items.Clear();
        }
    }
}
