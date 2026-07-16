using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Production;

namespace MachineMonitoring.Api.Tests.Fakes;

public sealed class TestWorkpieceRepository : IWorkpieceRepository
{
    private readonly Dictionary<Guid, Workpiece> _workpieces = [];
    private readonly object _syncRoot = new();

    public void Seed(Workpiece workpiece)
    {
        ArgumentNullException.ThrowIfNull(workpiece);

        lock (_syncRoot)
        {
            _workpieces[workpiece.Id] = workpiece;
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _workpieces.Clear();
        }
    }

    public Task<Workpiece?> GetByIdAsync(Guid workpieceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _workpieces.TryGetValue(workpieceId, out Workpiece? workpiece);
            return Task.FromResult(workpiece);
        }
    }

    public Task<IReadOnlyCollection<Workpiece>> GetByProductionLotIdAsync(
        Guid productionLotId,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            IReadOnlyCollection<Workpiece> workpieces = _workpieces
                .Values.Where(item => item.ProductionLotId == productionLotId)
                .OrderBy(item => item.Code)
                .ThenBy(item => item.Id)
                .ToArray();

            return Task.FromResult(workpieces);
        }
    }

    public Task AddAsync(Workpiece workpiece, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(workpiece);

        lock (_syncRoot)
        {
            _workpieces.Add(workpiece.Id, workpiece);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Workpiece workpiece, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(workpiece);

        lock (_syncRoot)
        {
            _workpieces[workpiece.Id] = workpiece;
        }

        return Task.CompletedTask;
    }
}
