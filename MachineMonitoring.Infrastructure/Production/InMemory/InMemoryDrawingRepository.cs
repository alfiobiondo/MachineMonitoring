using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Infrastructure.Production.InMemory;

public sealed class InMemoryDrawingFileRepository : IDrawingFileRepository
{
    private readonly Dictionary<Guid, DrawingFile> _drawingFiles;

    public InMemoryDrawingFileRepository(InMemoryProductionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _drawingFiles = catalog.DrawingFiles.ToDictionary(drawingFile => drawingFile.Id);
    }

    public Task<DrawingFile?> GetByIdAsync(Guid drawingFileId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _drawingFiles.TryGetValue(drawingFileId, out DrawingFile? drawingFile);

        return Task.FromResult(drawingFile);
    }

    public Task<IReadOnlyCollection<DrawingFile>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyCollection<DrawingFile> drawingFiles = _drawingFiles
            .Values.OrderByDescending(drawingFile => drawingFile.UploadedAt)
            .ToArray();

        return Task.FromResult(drawingFiles);
    }
}
