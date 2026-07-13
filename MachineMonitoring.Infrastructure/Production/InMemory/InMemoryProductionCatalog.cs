using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Infrastructure.Production.InMemory;

public sealed class InMemoryProductionCatalog
{
    public IReadOnlyCollection<Material> Materials { get; }

    public IReadOnlyCollection<Nozzle> Nozzles { get; }

    public IReadOnlyCollection<DrawingFile> DrawingFiles { get; }

    public IReadOnlyCollection<MachineCapabilities> MachineCapabilities { get; }

    public InMemoryProductionCatalog()
    {
        Materials = InMemoryProductionData.CreateMaterials();

        Nozzles = InMemoryProductionData.CreateNozzles();

        DrawingFiles = InMemoryProductionData.CreateDrawingFiles();

        MachineCapabilities = InMemoryProductionData.CreateMachineCapabilities();
    }
}
