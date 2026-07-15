using System.Text.Json.Serialization;
using MachineMonitoring.Api.Errors;
using MachineMonitoring.Api.Operations;
using MachineMonitoring.Application.Production;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Application.Production.Results;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Domain.Technology;
using MachineMonitoring.Infrastructure.Persistence;
using MachineMonitoring.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

string connectionString =
    builder.Configuration.GetConnectionString("MachineMonitoring")
    ?? throw new InvalidOperationException("Connection string 'MachineMonitoring' was not found.");

builder.Services.AddDbContext<MachineMonitoringDbContext>(options =>
    options.UseNpgsql(connectionString)
);

builder.Services.AddScoped<IMaterialRepository, PostgresMaterialRepository>();

builder.Services.AddScoped<INozzleRepository, PostgresNozzleRepository>();

builder.Services.AddScoped<IDrawingFileRepository, PostgresDrawingFileRepository>();

builder.Services.AddScoped<IMachineCapabilitiesRepository, PostgresMachineCapabilitiesRepository>();

builder.Services.AddScoped<IMachineOperationRepository, PostgresMachineOperationRepository>();

builder.Services.AddSingleton<LaserCutConfigurationValidator>();

builder.Services.AddScoped<MachineOperationApplicationService>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

WebApplication app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet(
    "/health",
    () => Results.Ok(new { status = "Healthy", timestamp = DateTimeOffset.UtcNow })
);

app.MapGet(
        "/api/operations",
        async (
            string? machineId,
            string? status,
            IMachineOperationRepository repository,
            CancellationToken cancellationToken
        ) =>
        {
            MachineOperationStatus? parsedStatus = null;

            if (!string.IsNullOrWhiteSpace(status))
            {
                bool isValidStatus = Enum.TryParse(
                    status,
                    ignoreCase: true,
                    out MachineOperationStatus value
                );

                if (!isValidStatus)
                {
                    throw new ArgumentException($"Invalid operation status '{status}'.");
                }

                parsedStatus = value;
            }

            IReadOnlyCollection<MachineOperation> operations = await repository.GetAllAsync(
                machineId,
                parsedStatus,
                cancellationToken
            );

            MachineOperationResponse[] response = operations
                .Select(CreateOperationResponse)
                .ToArray();

            return Results.Ok(response);
        }
    )
    .WithName("GetMachineOperations")
    .WithTags("Operations");

app.MapGet(
        "/api/operations/{operationId:guid}",
        async (
            Guid operationId,
            IMachineOperationRepository repository,
            CancellationToken cancellationToken
        ) =>
        {
            MachineOperation? operation = await repository.GetByIdAsync(
                operationId,
                cancellationToken
            );

            if (operation is null)
            {
                return Results.NotFound(
                    new { message = $"Machine operation {operationId} was not found." }
                );
            }

            MachineOperationResponse response = CreateOperationResponse(operation);

            return Results.Ok(response);
        }
    )
    .WithName("GetMachineOperation")
    .WithTags("Operations");

app.MapPost(
        "/api/operations",
        async (
            CreateMachineOperationRequest request,
            MachineOperationApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            WorkpieceGeometryInput geometry = CreateGeometryInput(request.Geometry);

            CreateLaserCutOperationCommand command = new(
                WorkpieceId: request.WorkpieceId,
                MachineId: request.MachineId,
                MaterialId: request.MaterialId,
                NozzleId: request.NozzleId,
                DrawingFileId: request.DrawingFileId,
                Geometry: geometry,
                LaserPowerWatts: request.LaserPowerWatts,
                CuttingSpeedMillimetersPerMinute: request.CuttingSpeedMillimetersPerMinute,
                AssistGas: request.AssistGas,
                GasPressureBar: request.GasPressureBar,
                FocalOffsetMillimeters: request.FocalOffsetMillimeters,
                NumberOfPasses: request.NumberOfPasses
            );

            CreateLaserCutOperationResult result = await service.CreateLaserCutOperationAsync(
                command,
                cancellationToken
            );

            CreateMachineOperationResponse response = new(
                OperationId: result.OperationId,
                ConfigurationId: result.ConfigurationId,
                OperationStatus: result.OperationStatus.ToString(),
                GeometryType: result.GeometryType.ToString()
            );

            return Results.CreatedAtRoute(
                routeName: "GetMachineOperation",
                routeValues: new { operationId = result.OperationId },
                value: response
            );
        }
    )
    .WithName("CreateMachineOperation")
    .WithTags("Operations");

app.MapPost(
        "/api/operations/{operationId:guid}/start",
        async (
            Guid operationId,
            StartMachineOperationRequest request,
            MachineOperationApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            StartMachineOperationCommand command = new(
                OperationId: operationId,
                InitialPhase: request.InitialPhase
            );

            await service.StartAsync(command, cancellationToken);

            return Results.NoContent();
        }
    )
    .WithName("StartMachineOperation")
    .WithTags("Operations");

app.MapPost(
        "/api/operations/{operationId:guid}/pause",
        async (
            Guid operationId,
            MachineOperationApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            PauseMachineOperationCommand command = new(OperationId: operationId);

            await service.PauseAsync(command, cancellationToken);

            return Results.NoContent();
        }
    )
    .WithName("PauseMachineOperation")
    .WithTags("Operations");

app.MapPost(
        "/api/operations/{operationId:guid}/resume",
        async (
            Guid operationId,
            MachineOperationApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            ResumeMachineOperationCommand command = new(OperationId: operationId);

            await service.ResumeAsync(command, cancellationToken);

            return Results.NoContent();
        }
    )
    .WithName("ResumeMachineOperation")
    .WithTags("Operations");

app.MapPost(
        "/api/operations/{operationId:guid}/cancel",
        async (
            Guid operationId,
            MachineOperationApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            CancelMachineOperationCommand command = new(OperationId: operationId);

            await service.CancelAsync(command, cancellationToken);

            return Results.NoContent();
        }
    )
    .WithName("CancelMachineOperation")
    .WithTags("Operations");

app.MapPost(
        "/api/operations/{operationId:guid}/progress",
        async (
            Guid operationId,
            UpdateMachineOperationProgressRequest request,
            MachineOperationApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            UpdateMachineOperationProgressCommand command = new(
                OperationId: operationId,
                ProgressPercentage: request.ProgressPercentage,
                CurrentPhase: request.CurrentPhase
            );

            await service.UpdateProgressAsync(command, cancellationToken);

            return Results.NoContent();
        }
    )
    .WithName("UpdateMachineOperationProgress")
    .WithTags("Operations");

app.MapPost(
        "/api/operations/{operationId:guid}/complete",
        async (
            Guid operationId,
            MachineOperationApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            CompleteMachineOperationCommand command = new(OperationId: operationId);

            await service.CompleteAsync(command, cancellationToken);

            return Results.NoContent();
        }
    )
    .WithName("CompleteMachineOperation")
    .WithTags("Operations");

app.MapPost(
        "/api/operations/{operationId:guid}/fail",
        async (
            Guid operationId,
            FailMachineOperationRequest request,
            MachineOperationApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            FailMachineOperationCommand command = new(
                OperationId: operationId,
                FailureReason: request.FailureReason
            );

            await service.FailAsync(command, cancellationToken);

            return Results.NoContent();
        }
    )
    .WithName("FailMachineOperation")
    .WithTags("Operations");

app.Run();

static WorkpieceGeometryInput CreateGeometryInput(WorkpieceGeometryRequest request)
{
    ArgumentNullException.ThrowIfNull(request);

    return request.Type.Trim().ToLowerInvariant() switch
    {
        "tube" => new TubeGeometryInput(
            OuterDiameterMillimeters: request.OuterDiameterMillimeters
                ?? throw new ArgumentException(
                    "OuterDiameterMillimeters is required for Tube geometry."
                ),
            ThicknessMillimeters: request.ThicknessMillimeters,
            LengthMillimeters: request.LengthMillimeters
                ?? throw new ArgumentException("LengthMillimeters is required for Tube geometry.")
        ),

        "sheet" => new SheetGeometryInput(
            WidthMillimeters: request.WidthMillimeters
                ?? throw new ArgumentException("WidthMillimeters is required for Sheet geometry."),
            HeightMillimeters: request.HeightMillimeters
                ?? throw new ArgumentException("HeightMillimeters is required for Sheet geometry."),
            ThicknessMillimeters: request.ThicknessMillimeters
        ),

        _ => throw new ArgumentException(
            $"Unsupported geometry type '{request.Type}'. "
                + "Supported values are 'Tube' and 'Sheet'."
        ),
    };
}

static MachineOperationResponse CreateOperationResponse(MachineOperation operation)
{
    return new MachineOperationResponse(
        Id: operation.Id,
        WorkpieceId: operation.WorkpieceId,
        MachineId: operation.MachineId,
        Type: operation.Type.ToString(),
        Status: operation.Status.ToString(),
        ProgressPercentage: operation.ProgressPercentage,
        CurrentPhase: operation.CurrentPhase,
        FailureReason: operation.FailureReason,
        CreatedAt: operation.CreatedAt,
        StartedAt: operation.StartedAt,
        CompletedAt: operation.CompletedAt
    );
}
