using MachineMonitoring.Application.Configuration;
using MachineMonitoring.Application.Diagnostics;
using MachineMonitoring.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MachineMonitoring.Application;

public sealed class CachedMachineDiagnosticService : IMachineDiagnosticService
{
    private readonly IRetryingMachineDiagnosticService _innerService;
    private readonly IMemoryCache _cache;
    private readonly DiagnosticCacheOptions _options;
    private readonly ILogger<CachedMachineDiagnosticService> _logger;

    public CachedMachineDiagnosticService(
        IRetryingMachineDiagnosticService innerService,
        IMemoryCache cache,
        IOptions<DiagnosticCacheOptions> options,
        ILogger<CachedMachineDiagnosticService> logger
    )
    {
        ArgumentNullException.ThrowIfNull(innerService);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _innerService = innerService;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MachineDiagnostic> GetDiagnosticAsync(
        Machine machine,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(machine);

        if (!_options.Enabled)
        {
            _logger.LogDebug("Diagnostic cache is disabled for machine {MachineId}.", machine.Id);

            return await _innerService.GetDiagnosticAsync(machine, cancellationToken);
        }

        string cacheKey = CreateCacheKey(machine.Id);

        if (
            _cache.TryGetValue(cacheKey, out MachineDiagnostic? cachedDiagnostic)
            && cachedDiagnostic is not null
        )
        {
            _logger.LogDebug("Diagnostic cache hit for machine {MachineId}.", machine.Id);

            return cachedDiagnostic;
        }

        _logger.LogDebug("Diagnostic cache miss for machine {MachineId}.", machine.Id);

        MachineDiagnostic diagnostic = await _innerService.GetDiagnosticAsync(
            machine,
            cancellationToken
        );

        MemoryCacheEntryOptions cacheEntryOptions = new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.DurationSeconds),
        };

        _cache.Set(cacheKey, diagnostic, cacheEntryOptions);

        _logger.LogDebug(
            "Diagnostic for machine {MachineId} cached for {CacheDurationSeconds} seconds.",
            machine.Id,
            _options.DurationSeconds
        );

        return diagnostic;
    }

    public void Remove(string machineId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);

        string cacheKey = CreateCacheKey(machineId);

        _cache.Remove(cacheKey);

        _logger.LogDebug("Cached diagnostic removed for machine {MachineId}.", machineId);
    }

    private static string CreateCacheKey(string machineId)
    {
        return $"machine-diagnostic:{machineId}";
    }
}
