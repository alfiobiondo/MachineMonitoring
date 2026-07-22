using System.Text.Json.Serialization;
using MachineMonitoring.Api.Catalogs;
using MachineMonitoring.Api.Common;
using MachineMonitoring.Api.Errors;
using MachineMonitoring.Api.HealthChecks;
using MachineMonitoring.Api.Hubs;
using MachineMonitoring.Api.Machines;
using MachineMonitoring.Api.Operations;
using MachineMonitoring.Api.Production;
using MachineMonitoring.Api.Realtime;
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
using MachineMonitoring.Infrastructure.Configuration;
using MachineMonitoring.Infrastructure.Persistence.Outbox;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddMachineMonitoringApplication()
    .AddMachineMonitoringInfrastructure(builder.Configuration);

builder
    .Services.AddOptions<MachineDataOptions>()
    .Bind(builder.Configuration.GetSection(MachineDataOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddTransient<IMachineProvider, JsonMachineProvider>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

builder.Services.AddSignalR();

builder.Services.AddScoped<IOutboxMessageDispatcher, SignalROutboxMessageDispatcher>();

builder.Services.AddHostedService<OutboxProcessingBackgroundService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "MachineMonitoringWeb",
        policy =>
        {
            policy
                .WithOrigins("http://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    );
});

WebApplication app = builder.Build();

app.UseExceptionHandler();

app.UseCors("MachineMonitoringWeb");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = HealthCheckResponseWriter.WriteAsync,
    }
);

app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync,
    }
);

app.MapHub<MachineMonitoringHub>("/hubs/machine-monitoring");

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

app.MapGet(
        "/api/operations/{operationId:guid}/events",
        async (
            Guid operationId,
            MachineOperationEventApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            IReadOnlyCollection<MachineOperationEventResult> result =
                await service.GetByOperationIdAsync(operationId, cancellationToken);

            return Results.Ok(result.Select(CreateOperationEventResponse).ToArray());
        }
    )
    .WithName("GetOperationEvents")
    .WithTags("Operations");

app.MapPost(
        "/api/production-lots",
        async (
            CreateProductionLotRequest request,
            ProductionLotApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            CreateProductionLotResult result = await service.CreateAsync(
                new CreateProductionLotCommand(
                    Code: request.Code,
                    PlannedQuantity: request.PlannedQuantity
                ),
                cancellationToken
            );

            return Results.CreatedAtRoute(
                routeName: "GetProductionLot",
                routeValues: new { productionLotId = result.ProductionLotId },
                value: CreateProductionLotCreatedResponse(result)
            );
        }
    )
    .WithName("CreateProductionLot")
    .WithTags("Production");

app.MapPost(
        "/api/workpieces",
        async (
            CreateWorkpieceRequest request,
            WorkpieceApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            CreateWorkpieceResult result = await service.CreateAsync(
                new CreateWorkpieceCommand(
                    ProductionLotId: request.ProductionLotId,
                    SequenceNumber: request.SequenceNumber,
                    Code: request.Code,
                    MaterialCode: request.MaterialCode
                ),
                cancellationToken
            );

            return Results.CreatedAtRoute(
                routeName: "GetWorkpiece",
                routeValues: new { workpieceId = result.WorkpieceId },
                value: CreateWorkpieceCreatedResponse(result)
            );
        }
    )
    .WithName("CreateWorkpiece")
    .WithTags("Production");

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
                SequenceNumber: request.SequenceNumber,
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
                SequenceNumber: result.SequenceNumber,
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
        "/api/workpieces/{workpieceId:guid}/start",
        async (
            Guid workpieceId,
            StartWorkpieceRequest request,
            WorkpieceApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            StartWorkpieceCommand command = new(
                WorkpieceId: workpieceId,
                InitialPhase: request.InitialPhase,
                StartFromSequenceNumber: request.StartFromSequenceNumber
            );

            await service.StartAsync(command, cancellationToken);

            return Results.NoContent();
        }
    )
    .WithName("StartWorkpiece")
    .WithTags("Production");

app.MapGet(
        "/api/workpieces/{workpieceId:guid}/events",
        async (
            Guid workpieceId,
            MachineOperationEventApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            IReadOnlyCollection<MachineOperationEventResult> result =
                await service.GetByWorkpieceIdAsync(workpieceId, cancellationToken);

            return Results.Ok(result.Select(CreateOperationEventResponse).ToArray());
        }
    )
    .WithName("GetWorkpieceEvents")
    .WithTags("Production");

app.MapPost(
        "/api/production-lots/{productionLotId:guid}/start",
        async (
            Guid productionLotId,
            StartProductionLotRequest request,
            ProductionLotApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            StartProductionLotCommand command = new(
                ProductionLotId: productionLotId,
                InitialPhase: request.InitialPhase,
                StartFromWorkpieceSequenceNumber: request.StartFromWorkpieceSequenceNumber
            );

            await service.StartAsync(command, cancellationToken);

            return Results.NoContent();
        }
    )
    .WithName("StartProductionLot")
    .WithTags("Production");

app.MapGet(
        "/api/production-lots/{productionLotId:guid}/events",
        async (
            Guid productionLotId,
            MachineOperationEventApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            IReadOnlyCollection<MachineOperationEventResult> result =
                await service.GetByProductionLotIdAsync(productionLotId, cancellationToken);

            return Results.Ok(result.Select(CreateOperationEventResponse).ToArray());
        }
    )
    .WithName("GetProductionLotEvents")
    .WithTags("Production");

app.MapGet(
        "/api/workpieces/{workpieceId:guid}",
        async (
            Guid workpieceId,
            WorkpieceApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            WorkpieceDetailsResult result = await service.GetDetailsAsync(
                workpieceId,
                cancellationToken
            );

            return Results.Ok(CreateWorkpieceDetailsResponse(result));
        }
    )
    .WithName("GetWorkpiece")
    .WithTags("Production");

app.MapGet(
        "/api/production-lots/{productionLotId:guid}",
        async (
            Guid productionLotId,
            ProductionLotApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            ProductionLotDetailsResult result = await service.GetDetailsAsync(
                productionLotId,
                cancellationToken
            );

            return Results.Ok(CreateProductionLotDetailsResponse(result));
        }
    )
    .WithName("GetProductionLot")
    .WithTags("Production");

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

app.MapPost(
        "/api/operations/{operationId:guid}/fault",
        async (
            Guid operationId,
            FaultMachineOperationRequest request,
            MachineOperationApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            bool isValidSeverity = Enum.TryParse(
                request.Severity,
                ignoreCase: true,
                out MachineAlarmSeverity severity
            );

            if (!isValidSeverity)
            {
                throw new ArgumentException(
                    $"Invalid machine alarm severity '{request.Severity}'."
                );
            }

            FaultMachineOperationCommand command = new(
                OperationId: operationId,
                FailureReason: request.FailureReason,
                AlarmCode: request.AlarmCode,
                AlarmMessage: request.AlarmMessage,
                Severity: severity
            );

            await service.FaultAsync(command, cancellationToken);

            return Results.NoContent();
        }
    )
    .WithName("FaultMachineOperation")
    .WithTags("Operations");

app.MapGet(
        "/api/operations/{operationId:guid}/alarms",
        async (
            Guid operationId,
            MachineAlarmApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            IReadOnlyCollection<MachineAlarmResult> result = await service.GetByOperationIdAsync(
                operationId,
                cancellationToken
            );

            return Results.Ok(result.Select(CreateMachineAlarmResponse).ToArray());
        }
    )
    .WithName("GetOperationAlarms")
    .WithTags("Operations");

app.MapPost(
        "/api/alarms/{alarmId:guid}/acknowledge",
        async (
            Guid alarmId,
            MachineAlarmApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            await service.AcknowledgeAsync(
                new AcknowledgeMachineAlarmCommand(alarmId),
                cancellationToken
            );

            return Results.NoContent();
        }
    )
    .WithName("AcknowledgeMachineAlarm")
    .WithTags("Alarms");

app.MapPost(
        "/api/alarms/{alarmId:guid}/resolve",
        async (
            Guid alarmId,
            ResolveMachineAlarmRequest request,
            MachineAlarmApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            await service.ResolveAsync(
                new ResolveMachineAlarmCommand(alarmId, request.ResolutionNotes),
                cancellationToken
            );

            return Results.NoContent();
        }
    )
    .WithName("ResolveMachineAlarm")
    .WithTags("Alarms");

app.MapGet(
        "/api/machines/{machineId}/alarms",
        async (
            string machineId,
            bool? activeOnly,
            MachineAlarmApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            IReadOnlyCollection<MachineAlarmResult> result = await service.GetByMachineIdAsync(
                machineId,
                activeOnly ?? false,
                cancellationToken
            );

            return Results.Ok(result.Select(CreateMachineAlarmResponse).ToArray());
        }
    )
    .WithName("GetMachineAlarms")
    .WithTags("Alarms");

app.MapGet(
        "/api/machines",
        async (MachineRuntimeApplicationService service, CancellationToken cancellationToken) =>
        {
            IReadOnlyCollection<MachineDetailsResult> result = await service.GetAllAsync(
                cancellationToken
            );

            return Results.Ok(result.Select(CreateMachineDetailsResponse).ToArray());
        }
    )
    .WithName("GetMachines")
    .WithTags("Machines");

app.MapGet(
        "/api/machines/{machineId}",
        async (
            string machineId,
            MachineRuntimeApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            MachineDetailsResult result = await service.GetByIdAsync(machineId, cancellationToken);
            return Results.Ok(CreateMachineDetailsResponse(result));
        }
    )
    .WithName("GetMachineById")
    .WithTags("Machines");

app.MapGet(
        "/api/machines/{machineId}/state",
        async (
            string machineId,
            MachineRuntimeApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            MachineRuntimeStateResult result = await service.GetStateAsync(
                machineId,
                cancellationToken
            );

            return Results.Ok(CreateMachineRuntimeStateResponse(result));
        }
    )
    .WithName("GetMachineRuntimeState")
    .WithTags("Machines");

app.MapGet(
        "/api/machines/{machineId}/live-snapshot",
        async (string machineId, ILiveSnapshotQuery query, CancellationToken cancellationToken) =>
        {
            LiveSnapshotResult result = await query.GetByMachineIdAsync(
                machineId,
                cancellationToken
            );

            return Results.Ok(CreateLiveSnapshotResponse(result));
        }
    )
    .WithName("GetMachineLiveSnapshot")
    .WithTags("Machines");

app.MapPost(
        "/api/machines/{machineId}/fault",
        async (
            string machineId,
            FaultMachineRequest request,
            MachineRuntimeApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            if (
                !Enum.TryParse(
                    request.Severity,
                    ignoreCase: true,
                    out MachineAlarmSeverity severity
                )
            )
            {
                throw new ArgumentException($"Invalid alarm severity '{request.Severity}'.");
            }

            await service.FaultAsync(
                new FaultMachineCommand(
                    MachineId: machineId,
                    Code: request.Code,
                    Severity: severity,
                    Message: request.Message,
                    OperationId: request.OperationId
                ),
                cancellationToken
            );

            return Results.NoContent();
        }
    )
    .WithName("FaultMachine")
    .WithTags("Machines");

app.MapPost(
        "/api/machines/{machineId}/maintenance/start",
        async (
            string machineId,
            MachineReasonRequest? request,
            MachineRuntimeApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            await service.StartMaintenanceAsync(
                new StartMachineMaintenanceCommand(machineId, request?.Reason),
                cancellationToken
            );

            return Results.NoContent();
        }
    )
    .WithName("StartMachineMaintenance")
    .WithTags("Machines");

app.MapPost(
        "/api/machines/{machineId}/maintenance/complete",
        async (
            string machineId,
            MachineRuntimeApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            await service.CompleteMaintenanceAsync(
                new CompleteMachineMaintenanceCommand(machineId),
                cancellationToken
            );

            return Results.NoContent();
        }
    )
    .WithName("CompleteMachineMaintenance")
    .WithTags("Machines");

app.MapPost(
        "/api/machines/{machineId}/offline",
        async (
            string machineId,
            MachineReasonRequest? request,
            MachineRuntimeApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            await service.SetOfflineAsync(
                new SetMachineOfflineCommand(machineId, request?.Reason),
                cancellationToken
            );

            return Results.NoContent();
        }
    )
    .WithName("SetMachineOffline")
    .WithTags("Machines");

app.MapPost(
        "/api/machines/{machineId}/online",
        async (
            string machineId,
            MachineRuntimeApplicationService service,
            CancellationToken cancellationToken
        ) =>
        {
            await service.SetOnlineAsync(new SetMachineOnlineCommand(machineId), cancellationToken);

            return Results.NoContent();
        }
    )
    .WithName("SetMachineOnline")
    .WithTags("Machines");

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
        SequenceNumber: result.SequenceNumber,
        MachineId: result.MachineId,
        Type: result.Type.ToString(),
        Status: result.Status.ToString(),
        ProgressPercentage: result.ProgressPercentage,
        CurrentPhase: result.CurrentPhase,
        FailureReason: result.FailureReason,
        MachineRuntimeStatus: result.MachineRuntimeStatus.ToString(),
        ActiveBlockingAlarm: result.ActiveBlockingAlarm is null
            ? null
            : CreateMachineAlarmResponse(result.ActiveBlockingAlarm),
        CanResume: result.CanResume,
        CanPause: result.CanPause,
        CanFault: result.CanFault,
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
        SequenceNumber: operation.SequenceNumber,
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

static WorkpieceDetailsResponse CreateWorkpieceDetailsResponse(WorkpieceDetailsResult result)
{
    return new WorkpieceDetailsResponse(
        Id: result.Id,
        ProductionLotId: result.ProductionLotId,
        SequenceNumber: result.SequenceNumber,
        Code: result.Code,
        MaterialCode: result.MaterialCode,
        Status: result.Status.ToString(),
        IsSequenceActive: result.IsSequenceActive,
        CreatedAt: result.CreatedAt,
        StartedAt: result.StartedAt,
        CompletedAt: result.CompletedAt,
        Operations: result
            .Operations.Select(operation => new MachineOperationResponse(
                Id: operation.Id,
                WorkpieceId: operation.WorkpieceId,
                SequenceNumber: operation.SequenceNumber,
                MachineId: operation.MachineId,
                Type: operation.Type.ToString(),
                Status: operation.Status.ToString(),
                ProgressPercentage: operation.ProgressPercentage,
                CurrentPhase: operation.CurrentPhase,
                FailureReason: operation.FailureReason,
                CreatedAt: operation.CreatedAt,
                StartedAt: operation.StartedAt,
                CompletedAt: operation.CompletedAt
            ))
            .ToArray()
    );
}

static CreateProductionLotResponse CreateProductionLotCreatedResponse(
    CreateProductionLotResult result
)
{
    return new CreateProductionLotResponse(
        ProductionLotId: result.ProductionLotId,
        Code: result.Code,
        PlannedQuantity: result.PlannedQuantity,
        Status: result.Status.ToString()
    );
}

static CreateWorkpieceResponse CreateWorkpieceCreatedResponse(CreateWorkpieceResult result)
{
    return new CreateWorkpieceResponse(
        WorkpieceId: result.WorkpieceId,
        ProductionLotId: result.ProductionLotId,
        SequenceNumber: result.SequenceNumber,
        Code: result.Code,
        MaterialCode: result.MaterialCode,
        Status: result.Status.ToString()
    );
}

static ProductionLotDetailsResponse CreateProductionLotDetailsResponse(
    ProductionLotDetailsResult result
)
{
    return new ProductionLotDetailsResponse(
        Id: result.Id,
        Code: result.Code,
        PlannedQuantity: result.PlannedQuantity,
        Status: result.Status.ToString(),
        CreatedAt: result.CreatedAt,
        StartedAt: result.StartedAt,
        CompletedAt: result.CompletedAt,
        Workpieces: result.Workpieces.Select(CreateWorkpieceDetailsResponse).ToArray()
    );
}

static MachineOperationEventResponse CreateOperationEventResponse(
    MachineOperationEventResult result
)
{
    return new MachineOperationEventResponse(
        Id: result.Id,
        MachineOperationId: result.MachineOperationId,
        WorkpieceId: result.WorkpieceId,
        ProductionLotId: result.ProductionLotId,
        OperationSequenceNumber: result.OperationSequenceNumber,
        WorkpieceSequenceNumber: result.WorkpieceSequenceNumber,
        EventType: result.EventType.ToString(),
        OccurredAt: result.OccurredAt,
        PreviousStatus: result.PreviousStatus?.ToString(),
        NewStatus: result.NewStatus?.ToString(),
        ProgressPercentage: result.ProgressPercentage,
        Phase: result.Phase,
        Reason: result.Reason,
        MachineAlarmId: result.MachineAlarmId,
        Metadata: result.Metadata
    );
}

static MachineAlarmResponse CreateMachineAlarmResponse(MachineAlarmResult result)
{
    return new MachineAlarmResponse(
        Id: result.Id,
        MachineId: result.MachineId,
        MachineOperationId: result.MachineOperationId,
        Code: result.Code,
        Severity: result.Severity.ToString(),
        Status: result.Status.ToString(),
        Message: result.Message,
        RaisedAt: result.RaisedAt,
        AcknowledgedAt: result.AcknowledgedAt,
        ResolvedAt: result.ResolvedAt,
        ResolutionNotes: result.ResolutionNotes
    );
}

static MachineRuntimeStateResponse CreateMachineRuntimeStateResponse(
    MachineRuntimeStateResult result
)
{
    return new MachineRuntimeStateResponse(
        MachineId: result.MachineId,
        Status: result.Status.ToString(),
        CurrentOperationId: result.CurrentOperationId,
        LastChangedAt: result.LastChangedAt,
        FailureReason: result.FailureReason,
        ActiveAlarmId: result.ActiveAlarmId,
        ActiveAlarmsCount: result.ActiveAlarmsCount
    );
}

static MachineDetailsResponse CreateMachineDetailsResponse(MachineDetailsResult result)
{
    return new MachineDetailsResponse(
        Id: result.Id,
        Name: result.Name,
        Location: result.Location,
        SerialNumber: result.SerialNumber,
        CatalogStatus: result.CatalogStatus.ToString(),
        Runtime: CreateMachineRuntimeStateResponse(result.Runtime)
    );
}

static LiveSnapshotResponse CreateLiveSnapshotResponse(LiveSnapshotResult result)
{
    return new LiveSnapshotResponse(
        Machine: new LiveSnapshotMachineResponse(
            Id: result.Machine.Id,
            Name: result.Machine.Name,
            Status: result.Machine.Status?.ToString(),
            LastChangedAt: result.Machine.LastChangedAt
        ),
        RuntimeVersion: result.RuntimeVersion,
        ProductionLot: result.ProductionLot is null
            ? null
            : new LiveSnapshotProductionLotResponse(
                Id: result.ProductionLot.Id,
                Code: result.ProductionLot.Code,
                Status: result.ProductionLot.Status.ToString(),
                ProgressPercentage: result.ProductionLot.ProgressPercentage,
                CompletedOperations: result.ProductionLot.CompletedOperations,
                TotalOperations: result.ProductionLot.TotalOperations
            ),
        CurrentWorkpiece: result.CurrentWorkpiece is null
            ? null
            : new LiveSnapshotWorkpieceResponse(
                Id: result.CurrentWorkpiece.Id,
                Code: result.CurrentWorkpiece.Code,
                Status: result.CurrentWorkpiece.Status.ToString(),
                SequenceNumber: result.CurrentWorkpiece.SequenceNumber,
                Position: result.CurrentWorkpiece.Position,
                TotalWorkpieces: result.CurrentWorkpiece.TotalWorkpieces,
                ProgressPercentage: result.CurrentWorkpiece.ProgressPercentage,
                CompletedOperations: result.CurrentWorkpiece.CompletedOperations,
                TotalOperations: result.CurrentWorkpiece.TotalOperations
            ),
        CurrentOperation: result.CurrentOperation is null
            ? null
            : new LiveSnapshotOperationResponse(
                Id: result.CurrentOperation.Id,
                Type: result.CurrentOperation.Type.ToString(),
                Status: result.CurrentOperation.Status.ToString(),
                SequenceNumber: result.CurrentOperation.SequenceNumber,
                Position: result.CurrentOperation.Position,
                TotalOperations: result.CurrentOperation.TotalOperations,
                ProgressPercentage: result.CurrentOperation.ProgressPercentage,
                CurrentPhase: result.CurrentOperation.CurrentPhase,
                StartedAt: result.CurrentOperation.StartedAt
            ),
        ActiveAlarms: result
            .ActiveAlarms.Select(alarm => new LiveSnapshotAlarmResponse(
                Id: alarm.Id,
                Code: alarm.Code,
                Severity: alarm.Severity.ToString(),
                Status: alarm.Status.ToString(),
                Message: alarm.Message,
                IsBlocking: alarm.IsBlocking,
                RaisedAt: alarm.RaisedAt
            ))
            .ToArray(),
        Warnings: result
            .Warnings.Select(warning => new LiveSnapshotWarningResponse(
                Id: warning.Id,
                MachineId: warning.MachineId,
                Code: warning.Code,
                Severity: warning.Severity,
                Title: warning.Title,
                Message: warning.Message,
                DetectedAt: warning.DetectedAt,
                ResolvedAt: warning.ResolvedAt,
                IsActive: warning.IsActive,
                SourceId: warning.SourceId
            ))
            .ToArray(),
        SnapshotAt: result.SnapshotAt
    );
}

public partial class Program;
