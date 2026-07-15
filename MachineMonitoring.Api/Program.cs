using System.Text.Json.Serialization;
using MachineMonitoring.Api.Catalogs;
using MachineMonitoring.Api.Common;
using MachineMonitoring.Api.Errors;
using MachineMonitoring.Api.Operations;
using MachineMonitoring.Application;
using MachineMonitoring.Application.Common;
using MachineMonitoring.Application.Exceptions;
using MachineMonitoring.Application.Production;
using MachineMonitoring.Application.Production.Commands;
using MachineMonitoring.Application.Production.Repositories;
using MachineMonitoring.Application.Production.Results;
using MachineMonitoring.Domain.Production;
using MachineMonitoring.Domain.Technology;
using MachineMonitoring.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddMachineMonitoringApplication()
    .AddMachineMonitoringInfrastructure(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

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
            int? page,
            int? pageSize,
            IMachineOperationRepository repository,
            CancellationToken cancellationToken
        ) =>
        {
            int resolvedPage = page ?? 1;
            int resolvedPageSize = pageSize ?? 20;

            if (resolvedPage <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(page),
                    "Page must be greater than zero."
                );
            }

            if (resolvedPageSize is <= 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pageSize),
                    "Page size must be between 1 and 100."
                );
            }

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

            PagedResult<MachineOperation> result = await repository.GetAllAsync(
                machineId,
                parsedStatus,
                resolvedPage,
                resolvedPageSize,
                cancellationToken
            );

            MachineOperationResponse[] items = result
                .Items.Select(CreateOperationResponse)
                .ToArray();

            PagedResponse<MachineOperationResponse> response = new(
                Items: items,
                Page: result.Page,
                PageSize: result.PageSize,
                TotalItems: result.TotalItems,
                TotalPages: result.TotalPages
            );

            return Results.Ok(response);
        }
    )
    .WithName("GetMachineOperations")
    .WithTags("Operations");

app.MapGet(
        "/api/operations/{operationId:guid}",
        async (
            Guid operationId,
            MachineOperationApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            MachineOperationDetailsResult result = await service.GetDetailsAsync(
                operationId,
                cancellationToken
            );

            MachineOperationDetailsResponse response = CreateOperationDetailsResponse(result);

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

app.MapGet(
        "/api/materials",
        async (
            bool? enabledOnly,
            IMaterialRepository repository,
            CancellationToken cancellationToken
        ) =>
        {
            bool resolvedEnabledOnly = enabledOnly ?? true;

            IReadOnlyCollection<Material> materials = await repository.GetAllAsync(
                resolvedEnabledOnly,
                cancellationToken
            );

            MaterialResponse[] response = materials
                .Select(material => new MaterialResponse(
                    Id: material.Id,
                    Code: material.Code,
                    Name: material.Name,
                    Category: material.Category.ToString(),
                    Grade: material.Grade,
                    IsEnabled: material.IsEnabled
                ))
                .ToArray();

            return Results.Ok(response);
        }
    )
    .WithName("GetMaterials")
    .WithTags("Catalogs");

app.MapGet(
        "/api/nozzles",
        async (
            bool? availableOnly,
            INozzleRepository repository,
            CancellationToken cancellationToken
        ) =>
        {
            bool resolvedAvailableOnly = availableOnly ?? true;

            IReadOnlyCollection<Nozzle> nozzles = await repository.GetAllAsync(
                resolvedAvailableOnly,
                cancellationToken
            );

            NozzleResponse[] response = nozzles
                .Select(nozzle => new NozzleResponse(
                    Id: nozzle.Id,
                    Code: nozzle.Code,
                    Type: nozzle.Type.ToString(),
                    DiameterMillimeters: nozzle.DiameterMillimeters,
                    MaximumPressureBar: nozzle.MaximumPressureBar,
                    WearPercentage: nozzle.WearPercentage,
                    IsAvailable: nozzle.IsAvailable
                ))
                .ToArray();

            return Results.Ok(response);
        }
    )
    .WithName("GetNozzles")
    .WithTags("Catalogs");

app.MapGet(
        "/api/drawing-files",
        async (IDrawingFileRepository repository, CancellationToken cancellationToken) =>
        {
            IReadOnlyCollection<DrawingFile> drawingFiles = await repository.GetAllAsync(
                cancellationToken
            );

            DrawingFileResponse[] response = drawingFiles
                .Select(drawingFile => new DrawingFileResponse(
                    Id: drawingFile.Id,
                    OriginalFileName: drawingFile.OriginalFileName,
                    ContentType: drawingFile.ContentType,
                    SizeBytes: drawingFile.SizeBytes,
                    Sha256Hash: drawingFile.Sha256Hash,
                    UploadedAt: drawingFile.UploadedAt
                ))
                .ToArray();

            return Results.Ok(response);
        }
    )
    .WithName("GetDrawingFiles")
    .WithTags("Catalogs");

app.MapGet(
        "/api/machines/{machineId}/capabilities",
        async (
            string machineId,
            IMachineCapabilitiesRepository repository,
            CancellationToken cancellationToken
        ) =>
        {
            MachineCapabilities? capabilities = await repository.GetByMachineIdAsync(
                machineId,
                cancellationToken
            );

            if (capabilities is null)
            {
                throw new ResourceNotFoundException(
                    resourceType: "Machine capabilities",
                    resourceId: machineId
                );
            }

            MachineCapabilitiesResponse response = new(
                MachineId: capabilities.MachineId,
                MaximumLaserPowerWatts: capabilities.MaximumLaserPowerWatts,
                MinimumThicknessMillimeters: capabilities.MinimumThicknessMillimeters,
                MaximumThicknessMillimeters: capabilities.MaximumThicknessMillimeters,
                SupportedMaterialCategories: capabilities
                    .SupportedMaterialCategories.Select(category => category.ToString())
                    .OrderBy(category => category)
                    .ToArray(),
                SupportedNozzleIds: capabilities.SupportedNozzleIds.OrderBy(id => id).ToArray(),
                SupportedGeometryTypes: capabilities
                    .SupportedGeometryTypes.Select(type => type.ToString())
                    .OrderBy(type => type)
                    .ToArray(),
                MaximumTubeDiameterMillimeters: capabilities.MaximumTubeDiameterMillimeters,
                MaximumTubeLengthMillimeters: capabilities.MaximumTubeLengthMillimeters,
                MaximumSheetWidthMillimeters: capabilities.MaximumSheetWidthMillimeters,
                MaximumSheetHeightMillimeters: capabilities.MaximumSheetHeightMillimeters
            );

            return Results.Ok(response);
        }
    )
    .WithName("GetMachineCapabilities")
    .WithTags("Catalogs");

app.Run();

static MachineOperationDetailsResponse CreateOperationDetailsResponse(
    MachineOperationDetailsResult result
)
{
    WorkpieceGeometryResponse geometry = result.Configuration.Geometry switch
    {
        TubeGeometryDetailsResult tube => new WorkpieceGeometryResponse(
            Type: tube.Type.ToString(),
            ThicknessMillimeters: tube.ThicknessMillimeters,
            OuterDiameterMillimeters: tube.OuterDiameterMillimeters,
            InnerDiameterMillimeters: tube.InnerDiameterMillimeters,
            LengthMillimeters: tube.LengthMillimeters,
            WidthMillimeters: null,
            HeightMillimeters: null
        ),

        SheetGeometryDetailsResult sheet => new WorkpieceGeometryResponse(
            Type: sheet.Type.ToString(),
            ThicknessMillimeters: sheet.ThicknessMillimeters,
            OuterDiameterMillimeters: null,
            InnerDiameterMillimeters: null,
            LengthMillimeters: null,
            WidthMillimeters: sheet.WidthMillimeters,
            HeightMillimeters: sheet.HeightMillimeters
        ),

        _ => throw new InvalidOperationException("Unsupported geometry result."),
    };

    LaserCutConfigurationResponse configuration = new(
        Id: result.Configuration.Id,
        Material: new CatalogItemResponse(
            Id: result.Configuration.MaterialId,
            Code: result.Configuration.MaterialCode,
            Name: result.Configuration.MaterialName
        ),
        Nozzle: new CatalogItemResponse(
            Id: result.Configuration.NozzleId,
            Code: result.Configuration.NozzleCode,
            Name: result.Configuration.NozzleCode
        ),
        DrawingFile: new DrawingFileSummaryResponse(
            Id: result.Configuration.DrawingFileId,
            OriginalFileName: result.Configuration.DrawingFileName
        ),
        Geometry: geometry,
        LaserPowerWatts: result.Configuration.LaserPowerWatts,
        CuttingSpeedMillimetersPerMinute: result.Configuration.CuttingSpeedMillimetersPerMinute,
        AssistGas: result.Configuration.AssistGas.ToString(),
        GasPressureBar: result.Configuration.GasPressureBar,
        FocalOffsetMillimeters: result.Configuration.FocalOffsetMillimeters,
        NumberOfPasses: result.Configuration.NumberOfPasses,
        CreatedAt: result.Configuration.CreatedAt
    );

    return new MachineOperationDetailsResponse(
        Id: result.Id,
        WorkpieceId: result.WorkpieceId,
        MachineId: result.MachineId,
        Type: result.Type.ToString(),
        Status: result.Status.ToString(),
        ProgressPercentage: result.ProgressPercentage,
        CurrentPhase: result.CurrentPhase,
        FailureReason: result.FailureReason,
        CreatedAt: result.CreatedAt,
        StartedAt: result.StartedAt,
        CompletedAt: result.CompletedAt,
        Configuration: configuration
    );
}

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

public partial class Program;
