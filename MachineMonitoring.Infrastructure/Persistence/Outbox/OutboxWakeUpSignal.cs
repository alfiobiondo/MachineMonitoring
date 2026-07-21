namespace MachineMonitoring.Infrastructure.Persistence.Outbox;

public sealed class OutboxWakeUpSignal : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(initialCount: 0, maxCount: 1);

    public void Notify()
    {
        try
        {
            _semaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // Un segnale è già pendente.
            // Non serve accodarne altri perché il worker processerà tutti i batch disponibili.
        }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        return _semaphore.WaitAsync(timeout, cancellationToken);
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
