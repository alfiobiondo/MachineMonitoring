using MachineMonitoring.Domain.Technology;

namespace MachineMonitoring.Infrastructure.Production.InMemory;

public static class InMemoryProductionData
{
    public static readonly Guid StainlessSteel304MaterialId = Guid.Parse(
        "10000000-0000-0000-0000-000000000001"
    );

    public static readonly Guid CarbonSteelS355MaterialId = Guid.Parse(
        "10000000-0000-0000-0000-000000000002"
    );

    public static readonly Guid Nozzle12Id = Guid.Parse("20000000-0000-0000-0000-000000000001");

    public static readonly Guid Nozzle20Id = Guid.Parse("20000000-0000-0000-0000-000000000002");

    public static readonly Guid TubeDrawingId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    public static readonly Guid SheetDrawingId = Guid.Parse("30000000-0000-0000-0000-000000000002");

    public static IReadOnlyCollection<Material> CreateMaterials()
    {
        return
        [
            new Material(
                id: StainlessSteel304MaterialId,
                code: "INOX-304",
                name: "Stainless Steel 304",
                category: MaterialCategory.StainlessSteel,
                grade: "AISI 304"
            ),
            new Material(
                id: CarbonSteelS355MaterialId,
                code: "STEEL-S355",
                name: "Carbon Steel S355",
                category: MaterialCategory.CarbonSteel,
                grade: "S355"
            ),
        ];
    }

    public static IReadOnlyCollection<Nozzle> CreateNozzles()
    {
        return
        [
            new Nozzle(
                id: Nozzle12Id,
                code: "NZ-12",
                type: NozzleType.DoubleLayer,
                diameterMillimeters: 1.2m,
                maximumPressureBar: 20m
            ),
            new Nozzle(
                id: Nozzle20Id,
                code: "NZ-20",
                type: NozzleType.SingleLayer,
                diameterMillimeters: 2m,
                maximumPressureBar: 12m
            ),
        ];
    }

    public static IReadOnlyCollection<DrawingFile> CreateDrawingFiles()
    {
        return
        [
            new DrawingFile(
                id: TubeDrawingId,
                originalFileName: "tubo_80x3.dwg",
                storedFileName: "tube-80x3.dwg",
                contentType: "application/acad",
                sizeBytes: 1_850_000,
                sha256Hash: "TUBE-DRAWING-HASH",
                uploadedAt: DateTimeOffset.UtcNow
            ),
            new DrawingFile(
                id: SheetDrawingId,
                originalFileName: "lamiera_2000x1000x3.dwg",
                storedFileName: "sheet-2000x1000x3.dwg",
                contentType: "application/acad",
                sizeBytes: 2_100_000,
                sha256Hash: "SHEET-DRAWING-HASH",
                uploadedAt: DateTimeOffset.UtcNow
            ),
        ];
    }

    public static IReadOnlyCollection<MachineCapabilities> CreateMachineCapabilities()
    {
        return
        [
            new MachineCapabilities(
                machineId: "M-001",
                maximumLaserPowerWatts: 3_000m,
                minimumThicknessMillimeters: 0.5m,
                maximumThicknessMillimeters: 8m,
                supportedMaterialCategories:
                [
                    MaterialCategory.StainlessSteel,
                    MaterialCategory.CarbonSteel,
                ],
                supportedNozzleIds: [Nozzle12Id, Nozzle20Id],
                supportedGeometryTypes: [WorkpieceGeometryType.Tube],
                maximumTubeDiameterMillimeters: 200m,
                maximumTubeLengthMillimeters: 6_500m
            ),
            new MachineCapabilities(
                machineId: "M-002",
                maximumLaserPowerWatts: 4_000m,
                minimumThicknessMillimeters: 0.5m,
                maximumThicknessMillimeters: 12m,
                supportedMaterialCategories:
                [
                    MaterialCategory.StainlessSteel,
                    MaterialCategory.CarbonSteel,
                    MaterialCategory.Aluminium,
                ],
                supportedNozzleIds: [Nozzle12Id, Nozzle20Id],
                supportedGeometryTypes: [WorkpieceGeometryType.Sheet],
                maximumSheetWidthMillimeters: 3_000m,
                maximumSheetHeightMillimeters: 1_500m
            ),
        ];
    }
}
