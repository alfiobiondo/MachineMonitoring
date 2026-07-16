using System.Net;
using System.Net.Http.Json;
using MachineMonitoring.Api.Operations;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Domain.Technology;
using Microsoft.AspNetCore.Mvc;

namespace MachineMonitoring.Api.Tests;

[Collection(ApiTestCollection.Name)]
public sealed class OperationCreationEndpointTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly Guid MaterialId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private static readonly Guid NozzleId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    private static readonly Guid DrawingFileId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    private static readonly Guid ProductionLotId = Guid.Parse(
        "40000000-0000-0000-0000-000000000001"
    );

    private static readonly Guid WorkpieceId = Guid.Parse("50000000-0000-0000-0000-000000000001");

    public OperationCreationEndpointTests(CustomWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
        _client = factory.CreateClient();

        _factory.MachineOperationRepository.Clear();
        _factory.WorkpieceRepository.Clear();
        _factory.ProductionLotRepository.Clear();
        _factory.ProductionCatalog.Clear();
    }

    [Fact]
    public async Task CreateOperation_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        SeedValidCatalog();
        SeedProductionHierarchy();

        CreateMachineOperationRequest request = CreateValidRequest();

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/operations", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        CreateMachineOperationResponse? result =
            await response.Content.ReadFromJsonAsync<CreateMachineOperationResponse>();

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.OperationId);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains(result.OperationId.ToString(), response.Headers.Location.ToString());

        MachineMonitoring.Domain.Production.MachineOperation? storedOperation =
            await _factory.MachineOperationRepository.GetByIdAsync(
                result.OperationId,
                CancellationToken.None
            );

        Assert.NotNull(storedOperation);
        Assert.Equal(request.WorkpieceId, storedOperation.WorkpieceId);
        Assert.Equal("M-001", storedOperation.MachineId);
        Assert.Equal(
            MachineMonitoring.Domain.Production.MachineOperationStatus.Queued,
            storedOperation.Status
        );

        LaserCutConfiguration? storedConfiguration =
            await _factory.MachineOperationRepository.GetConfigurationByOperationIdAsync(
                result.OperationId,
                CancellationToken.None
            );

        Assert.NotNull(storedConfiguration);
        Assert.Equal(MaterialId, storedConfiguration.MaterialId);
        Assert.Equal(NozzleId, storedConfiguration.NozzleId);
        Assert.Equal(DrawingFileId, storedConfiguration.DrawingFileId);
        Assert.Equal(2500m, storedConfiguration.LaserPowerWatts);
    }

    [Fact]
    public async Task CreateOperation_WhenMaterialDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        SeedValidCatalog();
        SeedProductionHierarchy();

        CreateMachineOperationRequest validRequest = CreateValidRequest();

        CreateMachineOperationRequest request = validRequest with { MaterialId = Guid.NewGuid() };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/operations", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        ProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problemDetails);
        Assert.Equal("Resource not found", problemDetails.Title);
        Assert.Contains("Material", problemDetails.Detail);

        MachineMonitoring.Application.Common.PagedResult<MachineMonitoring.Domain.Production.MachineOperation> operations =
            await _factory.MachineOperationRepository.GetAllAsync(
                machineId: null,
                status: null,
                page: 1,
                pageSize: 20,
                cancellationToken: CancellationToken.None
            );

        Assert.Empty(operations.Items);
    }

    [Fact]
    public async Task CreateOperation_WhenLaserPowerIsUnsupported_ReturnsUnprocessableEntity()
    {
        // Arrange
        SeedValidCatalog();
        SeedProductionHierarchy();

        CreateMachineOperationRequest validRequest = CreateValidRequest();

        CreateMachineOperationRequest request = validRequest with { LaserPowerWatts = 5000m };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/operations", request);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        ProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problemDetails);
        Assert.Equal("Business rule violation", problemDetails.Title);
        Assert.Contains("Laser power", problemDetails.Detail);
    }

    [Fact]
    public async Task CreateOperation_WhenMachineCapabilitiesDoNotExist_ReturnsNotFound()
    {
        // Arrange
        SeedValidCatalog();
        SeedProductionHierarchy();

        CreateMachineOperationRequest validRequest = CreateValidRequest();

        CreateMachineOperationRequest request = validRequest with { MachineId = "M-999" };

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/operations", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        ProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problemDetails);
        Assert.Equal("Resource not found", problemDetails.Title);
    }

    private void SeedValidCatalog()
    {
        Material material = new(
            id: MaterialId,
            code: "INOX-304",
            name: "Stainless Steel 304",
            category: MaterialCategory.StainlessSteel,
            grade: "AISI 304"
        );

        Nozzle nozzle = new(
            id: NozzleId,
            code: "NZ-12",
            type: NozzleType.SingleLayer,
            diameterMillimeters: 1.2m,
            maximumPressureBar: 20m
        );
        nozzle.SetWearPercentage(10m);

        DrawingFile drawingFile = new(
            id: DrawingFileId,
            originalFileName: "tube-test.dwg",
            storedFileName: "tube-test-stored.dwg",
            contentType: "application/acad",
            sizeBytes: 1024,
            sha256Hash: new string('a', 64),
            uploadedAt: DateTimeOffset.UtcNow
        );

        MachineCapabilities capabilities = new(
            machineId: "M-001",
            maximumLaserPowerWatts: 3000m,
            minimumThicknessMillimeters: 1m,
            maximumThicknessMillimeters: 10m,
            supportedMaterialCategories: [MaterialCategory.StainlessSteel],
            supportedNozzleIds: [NozzleId],
            supportedGeometryTypes: [WorkpieceGeometryType.Tube],
            maximumTubeDiameterMillimeters: 200m,
            maximumTubeLengthMillimeters: 6500m,
            maximumSheetWidthMillimeters: null,
            maximumSheetHeightMillimeters: null
        );

        _factory.ProductionCatalog.SeedMaterial(material);
        _factory.ProductionCatalog.SeedNozzle(nozzle);
        _factory.ProductionCatalog.SeedDrawingFile(drawingFile);
        _factory.ProductionCatalog.SeedCapabilities(capabilities);
    }

    private void SeedProductionHierarchy()
    {
        ProductionLot productionLot = new(
            id: ProductionLotId,
            code: "LOT-001",
            plannedQuantity: 1,
            createdAt: DateTimeOffset.UtcNow
        );

        Workpiece workpiece = new(
            id: WorkpieceId,
            productionLotId: productionLot.Id,
            code: "WP-001",
            materialCode: "INOX-304",
            createdAt: DateTimeOffset.UtcNow
        );

        _factory.ProductionLotRepository.Seed(productionLot);
        _factory.WorkpieceRepository.Seed(workpiece);
    }

    private static CreateMachineOperationRequest CreateValidRequest()
    {
        return new CreateMachineOperationRequest(
            WorkpieceId: WorkpieceId,
            SequenceNumber: 1,
            MachineId: "M-001",
            MaterialId: MaterialId,
            NozzleId: NozzleId,
            DrawingFileId: DrawingFileId,
            Geometry: new WorkpieceGeometryRequest(
                Type: "Tube",
                ThicknessMillimeters: 3m,
                OuterDiameterMillimeters: 80m,
                LengthMillimeters: 6000m,
                WidthMillimeters: null,
                HeightMillimeters: null
            ),
            LaserPowerWatts: 2500m,
            CuttingSpeedMillimetersPerMinute: 1200m,
            AssistGas: AssistGasType.Nitrogen,
            GasPressureBar: 15m,
            FocalOffsetMillimeters: -0.5m,
            NumberOfPasses: 1
        );
    }
}
