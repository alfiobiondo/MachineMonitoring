using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Api.Tests.Fakes;

public sealed class TestProductionCatalog
    : IMaterialRepository,
        INozzleRepository,
        IDrawingFileRepository,
        IMachineCapabilitiesRepository
{
    private readonly Dictionary<Guid, Material> _materials = [];
    private readonly Dictionary<Guid, Nozzle> _nozzles = [];
    private readonly Dictionary<Guid, DrawingFile> _drawingFiles = [];
    private readonly Dictionary<string, MachineCapabilities> _capabilities = new(
        StringComparer.OrdinalIgnoreCase
    );

    private readonly object _syncRoot = new();

    public void SeedMaterial(Material material)
    {
        ArgumentNullException.ThrowIfNull(material);

        lock (_syncRoot)
        {
            _materials[material.Id] = material;
        }
    }

    public void SeedNozzle(Nozzle nozzle)
    {
        ArgumentNullException.ThrowIfNull(nozzle);

        lock (_syncRoot)
        {
            _nozzles[nozzle.Id] = nozzle;
        }
    }

    public void SeedDrawingFile(DrawingFile drawingFile)
    {
        ArgumentNullException.ThrowIfNull(drawingFile);

        lock (_syncRoot)
        {
            _drawingFiles[drawingFile.Id] = drawingFile;
        }
    }

    public void SeedCapabilities(MachineCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        lock (_syncRoot)
        {
            _capabilities[capabilities.MachineId] = capabilities;
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _materials.Clear();
            _nozzles.Clear();
            _drawingFiles.Clear();
            _capabilities.Clear();
        }
    }

    Task<Material?> IMaterialRepository.GetByIdAsync(
        Guid materialId,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _materials.TryGetValue(materialId, out Material? material);

            return Task.FromResult(material);
        }
    }

    Task<IReadOnlyCollection<Material>> IMaterialRepository.GetAllAsync(
        bool enabledOnly,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            IEnumerable<Material> query = _materials.Values;

            if (enabledOnly)
            {
                query = query.Where(material => material.IsEnabled);
            }

            IReadOnlyCollection<Material> result = query
                .OrderBy(material => material.Code)
                .ToArray();

            return Task.FromResult(result);
        }
    }

    Task<Nozzle?> INozzleRepository.GetByIdAsync(Guid nozzleId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _nozzles.TryGetValue(nozzleId, out Nozzle? nozzle);

            return Task.FromResult(nozzle);
        }
    }

    Task<IReadOnlyCollection<Nozzle>> INozzleRepository.GetAllAsync(
        bool availableOnly,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            IEnumerable<Nozzle> query = _nozzles.Values;

            if (availableOnly)
            {
                query = query.Where(nozzle => nozzle.IsAvailable);
            }

            IReadOnlyCollection<Nozzle> result = query.OrderBy(nozzle => nozzle.Code).ToArray();

            return Task.FromResult(result);
        }
    }

    Task<DrawingFile?> IDrawingFileRepository.GetByIdAsync(
        Guid drawingFileId,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _drawingFiles.TryGetValue(drawingFileId, out DrawingFile? drawingFile);

            return Task.FromResult(drawingFile);
        }
    }

    Task<IReadOnlyCollection<DrawingFile>> IDrawingFileRepository.GetAllAsync(
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            IReadOnlyCollection<DrawingFile> result = _drawingFiles
                .Values.OrderByDescending(drawingFile => drawingFile.UploadedAt)
                .ToArray();

            return Task.FromResult(result);
        }
    }

    public Task<MachineCapabilities?> GetByMachineIdAsync(
        string machineId,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);

        lock (_syncRoot)
        {
            _capabilities.TryGetValue(machineId, out MachineCapabilities? capabilities);

            return Task.FromResult(capabilities);
        }
    }
}
