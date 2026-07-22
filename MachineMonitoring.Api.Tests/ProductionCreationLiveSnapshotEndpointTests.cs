using System.Net;
using System.Net.Http.Json;
using MachineMonitoring.Api.Machines;
using MachineMonitoring.Api.Operations;
using MachineMonitoring.Api.Production;
using MachineMonitoring.Api.Tests.Fakes;
using MachineMonitoring.Application;
using MachineMonitoring.Domain;
using MachineMonitoring.Infrastructure.Production.InMemory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MachineMonitoring.Api.Tests;

[Collection(PostgresApiTestCollection.Name)]
public sealed class ProductionCreationLiveSnapshotEndpointTests
{
    private readonly PostgresWebApplicationFactory _factory;

    public ProductionCreationLiveSnapshotEndpointTests(PostgresWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    [Fact]
    public async Task CreateHierarchyThenStartOperation_AllowsReadingLiveSnapshotForMachine()
    {
        using WebApplicationFactory<Program> factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IMachineProvider>();
                services.AddSingleton<IMachineProvider>(CreateMachineProvider("M-001"));
            })
        );
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage createLotResponse = await client.PostAsJsonAsync(
            "/api/production-lots",
            new CreateProductionLotRequest(
                Code: $"LOT-LIVE-{Guid.NewGuid():N}",
                PlannedQuantity: 1
            )
        );

        Assert.Equal(HttpStatusCode.Created, createLotResponse.StatusCode);

        CreateProductionLotResponse? lot =
            await createLotResponse.Content.ReadFromJsonAsync<CreateProductionLotResponse>();

        Assert.NotNull(lot);

        HttpResponseMessage createWorkpieceResponse = await client.PostAsJsonAsync(
            "/api/workpieces",
            new CreateWorkpieceRequest(
                ProductionLotId: lot.ProductionLotId,
                SequenceNumber: 1,
                Code: $"WP-LIVE-{Guid.NewGuid():N}",
                MaterialCode: "INOX-304"
            )
        );

        Assert.Equal(HttpStatusCode.Created, createWorkpieceResponse.StatusCode);

        CreateWorkpieceResponse? workpiece =
            await createWorkpieceResponse.Content.ReadFromJsonAsync<CreateWorkpieceResponse>();

        Assert.NotNull(workpiece);

        HttpResponseMessage createOperationResponse = await client.PostAsJsonAsync(
            "/api/operations",
            new CreateMachineOperationRequest(
                WorkpieceId: workpiece.WorkpieceId,
                SequenceNumber: 1,
                MachineId: "M-001",
                MaterialId: InMemoryProductionData.StainlessSteel304MaterialId,
                NozzleId: InMemoryProductionData.Nozzle12Id,
                DrawingFileId: InMemoryProductionData.TubeDrawingId,
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
                AssistGas: Domain.Technology.AssistGasType.Nitrogen,
                GasPressureBar: 15m,
                FocalOffsetMillimeters: -0.5m,
                NumberOfPasses: 1
            )
        );

        Assert.Equal(HttpStatusCode.Created, createOperationResponse.StatusCode);

        CreateMachineOperationResponse? operation =
            await createOperationResponse.Content.ReadFromJsonAsync<CreateMachineOperationResponse>();

        Assert.NotNull(operation);

        HttpResponseMessage startResponse = await client.PostAsJsonAsync(
            $"/api/operations/{operation.OperationId}/start",
            new StartMachineOperationRequest("Preparing laser")
        );

        Assert.Equal(HttpStatusCode.NoContent, startResponse.StatusCode);

        LiveSnapshotResponse? snapshot = await client.GetFromJsonAsync<LiveSnapshotResponse>(
            "/api/machines/M-001/live-snapshot"
        );

        Assert.NotNull(snapshot);
        Assert.NotNull(snapshot.ProductionLot);
        Assert.NotNull(snapshot.CurrentWorkpiece);
        Assert.NotNull(snapshot.CurrentOperation);
        Assert.Equal(lot.ProductionLotId, snapshot.ProductionLot.Id);
        Assert.Equal(workpiece.WorkpieceId, snapshot.CurrentWorkpiece.Id);
        Assert.Equal(operation.OperationId, snapshot.CurrentOperation.Id);
        Assert.Equal("M-001", snapshot.Machine.Id);
    }

    private static TestMachineProvider CreateMachineProvider(string machineId)
    {
        TestMachineProvider provider = new();
        provider.Seed(
            new Machine(
                id: machineId,
                name: $"Machine {machineId}",
                status: MachineStatus.Running,
                location: "Production Hall A",
                serialNumber: $"SN-{machineId}"
            )
        );
        return provider;
    }
}
