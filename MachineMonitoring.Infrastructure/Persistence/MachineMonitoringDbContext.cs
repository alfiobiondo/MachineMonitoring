using MachineMonitoring.Domain.Production;
using MachineMonitoring.Domain.Technology;
using MachineMonitoring.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace MachineMonitoring.Infrastructure.Persistence;

public sealed class MachineMonitoringDbContext : DbContext
{
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<Nozzle> Nozzles => Set<Nozzle>();
    public DbSet<DrawingFile> DrawingFiles => Set<DrawingFile>();
    public DbSet<MachineCapabilitiesRecord> MachineCapabilities => Set<MachineCapabilitiesRecord>();
    public DbSet<MachineCapabilityMaterialCategoryRecord> MachineCapabilityMaterialCategories =>
        Set<MachineCapabilityMaterialCategoryRecord>();
    public DbSet<MachineCapabilityNozzleRecord> MachineCapabilityNozzles =>
        Set<MachineCapabilityNozzleRecord>();
    public DbSet<MachineCapabilityGeometryTypeRecord> MachineCapabilityGeometryTypes =>
        Set<MachineCapabilityGeometryTypeRecord>();
    public DbSet<ProductionLotRecord> ProductionLots => Set<ProductionLotRecord>();
    public DbSet<WorkpieceRecord> Workpieces => Set<WorkpieceRecord>();
    public DbSet<MachineOperationRecord> MachineOperations => Set<MachineOperationRecord>();
    public DbSet<MachineOperationEventRecord> MachineOperationEvents =>
        Set<MachineOperationEventRecord>();
    public DbSet<MachineAlarmRecord> MachineAlarms => Set<MachineAlarmRecord>();
    public DbSet<MachineRuntimeStateRecord> MachineRuntimeStates => Set<MachineRuntimeStateRecord>();
    public DbSet<LaserCutConfigurationRecord> LaserCutConfigurations =>
        Set<LaserCutConfigurationRecord>();

    public MachineMonitoringDbContext(DbContextOptions<MachineMonitoringDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureMaterial(modelBuilder);
        ConfigureNozzle(modelBuilder);
        ConfigureDrawingFile(modelBuilder);
        ConfigureMachineCapabilities(modelBuilder);
        ConfigureProductionLot(modelBuilder);
        ConfigureWorkpiece(modelBuilder);
        ConfigureMachineOperation(modelBuilder);
        ConfigureMachineOperationEvent(modelBuilder);
        ConfigureMachineAlarm(modelBuilder);
        ConfigureMachineRuntimeState(modelBuilder);
        ConfigureLaserCutConfiguration(modelBuilder);
    }

    private static void ConfigureMaterial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Material>(entity =>
        {
            entity.ToTable("materials");

            entity.HasKey(material => material.Id);

            entity.Property(material => material.Id).HasColumnName("id");

            entity
                .Property(material => material.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(material => material.Code).IsUnique();

            entity
                .Property(material => material.Name)
                .HasColumnName("name")
                .HasMaxLength(200)
                .IsRequired();

            entity
                .Property(material => material.Category)
                .HasColumnName("category")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity
                .Property(material => material.Grade)
                .HasColumnName("grade")
                .HasMaxLength(100)
                .IsRequired();

            entity
                .Property(material => material.IsEnabled)
                .HasColumnName("is_enabled")
                .IsRequired();
        });
    }

    private static void ConfigureNozzle(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Nozzle>(entity =>
        {
            entity.ToTable("nozzles");

            entity.HasKey(nozzle => nozzle.Id);

            entity.Property(nozzle => nozzle.Id).HasColumnName("id");

            entity
                .Property(nozzle => nozzle.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(nozzle => nozzle.Code).IsUnique();

            entity
                .Property(nozzle => nozzle.Type)
                .HasColumnName("type")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity
                .Property(nozzle => nozzle.DiameterMillimeters)
                .HasColumnName("diameter_millimeters")
                .HasPrecision(10, 3)
                .IsRequired();

            entity
                .Property(nozzle => nozzle.MaximumPressureBar)
                .HasColumnName("maximum_pressure_bar")
                .HasPrecision(10, 3)
                .IsRequired();

            entity
                .Property(nozzle => nozzle.IsAvailable)
                .HasColumnName("is_available")
                .IsRequired();

            entity
                .Property(nozzle => nozzle.WearPercentage)
                .HasColumnName("wear_percentage")
                .HasPrecision(5, 2)
                .IsRequired();
        });
    }

    private static void ConfigureDrawingFile(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DrawingFile>(entity =>
        {
            entity.ToTable("drawing_files");

            entity.HasKey(drawingFile => drawingFile.Id);

            entity.Property(drawingFile => drawingFile.Id).HasColumnName("id");

            entity
                .Property(drawingFile => drawingFile.OriginalFileName)
                .HasColumnName("original_file_name")
                .HasMaxLength(255)
                .IsRequired();

            entity
                .Property(drawingFile => drawingFile.StoredFileName)
                .HasColumnName("stored_file_name")
                .HasMaxLength(255)
                .IsRequired();

            entity.HasIndex(drawingFile => drawingFile.StoredFileName).IsUnique();

            entity
                .Property(drawingFile => drawingFile.ContentType)
                .HasColumnName("content_type")
                .HasMaxLength(100)
                .IsRequired();

            entity
                .Property(drawingFile => drawingFile.SizeBytes)
                .HasColumnName("size_bytes")
                .IsRequired();

            entity
                .Property(drawingFile => drawingFile.Sha256Hash)
                .HasColumnName("sha256_hash")
                .HasMaxLength(64)
                .IsRequired();

            entity
                .Property(drawingFile => drawingFile.UploadedAt)
                .HasColumnName("uploaded_at")
                .IsRequired();
        });
    }

    private static void ConfigureMachineCapabilities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MachineCapabilitiesRecord>(entity =>
        {
            entity.ToTable("machine_capabilities");

            entity.HasKey(capabilities => capabilities.MachineId);

            entity
                .Property(capabilities => capabilities.MachineId)
                .HasColumnName("machine_id")
                .HasMaxLength(50);

            entity
                .Property(capabilities => capabilities.MaximumLaserPowerWatts)
                .HasColumnName("maximum_laser_power_watts")
                .HasPrecision(12, 3)
                .IsRequired();

            entity
                .Property(capabilities => capabilities.MinimumThicknessMillimeters)
                .HasColumnName("minimum_thickness_millimeters")
                .HasPrecision(10, 3)
                .IsRequired();

            entity
                .Property(capabilities => capabilities.MaximumThicknessMillimeters)
                .HasColumnName("maximum_thickness_millimeters")
                .HasPrecision(10, 3)
                .IsRequired();

            entity
                .Property(capabilities => capabilities.MaximumTubeDiameterMillimeters)
                .HasColumnName("maximum_tube_diameter_millimeters")
                .HasPrecision(10, 3);

            entity
                .Property(capabilities => capabilities.MaximumTubeLengthMillimeters)
                .HasColumnName("maximum_tube_length_millimeters")
                .HasPrecision(12, 3);

            entity
                .Property(capabilities => capabilities.MaximumSheetWidthMillimeters)
                .HasColumnName("maximum_sheet_width_millimeters")
                .HasPrecision(12, 3);

            entity
                .Property(capabilities => capabilities.MaximumSheetHeightMillimeters)
                .HasColumnName("maximum_sheet_height_millimeters")
                .HasPrecision(12, 3);
        });

        modelBuilder.Entity<MachineCapabilityMaterialCategoryRecord>(entity =>
        {
            entity.ToTable("machine_capability_material_categories");

            entity.HasKey(item => new { item.MachineId, item.MaterialCategory });

            entity.Property(item => item.MachineId).HasColumnName("machine_id").HasMaxLength(50);

            entity
                .Property(item => item.MaterialCategory)
                .HasColumnName("material_category")
                .HasConversion<string>()
                .HasMaxLength(50);

            entity
                .HasOne(item => item.MachineCapabilities)
                .WithMany(capabilities => capabilities.SupportedMaterialCategories)
                .HasForeignKey(item => item.MachineId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MachineCapabilityNozzleRecord>(entity =>
        {
            entity.ToTable("machine_capability_nozzles");

            entity.HasKey(item => new { item.MachineId, item.NozzleId });

            entity.Property(item => item.MachineId).HasColumnName("machine_id").HasMaxLength(50);

            entity.Property(item => item.NozzleId).HasColumnName("nozzle_id");

            entity
                .HasOne(item => item.MachineCapabilities)
                .WithMany(capabilities => capabilities.SupportedNozzles)
                .HasForeignKey(item => item.MachineId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne<Nozzle>()
                .WithMany()
                .HasForeignKey(item => item.NozzleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MachineCapabilityGeometryTypeRecord>(entity =>
        {
            entity.ToTable("machine_capability_geometry_types");

            entity.HasKey(item => new { item.MachineId, item.GeometryType });

            entity.Property(item => item.MachineId).HasColumnName("machine_id").HasMaxLength(50);

            entity
                .Property(item => item.GeometryType)
                .HasColumnName("geometry_type")
                .HasConversion<string>()
                .HasMaxLength(50);

            entity
                .HasOne(item => item.MachineCapabilities)
                .WithMany(capabilities => capabilities.SupportedGeometryTypes)
                .HasForeignKey(item => item.MachineId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureMachineOperation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MachineOperationRecord>(entity =>
        {
            entity.ToTable("machine_operations");

            entity.HasKey(operation => operation.Id);

            entity.Property(operation => operation.Id).HasColumnName("id");

            entity
                .Property(operation => operation.WorkpieceId)
                .HasColumnName("workpiece_id")
                .IsRequired();

            entity
                .Property(operation => operation.SequenceNumber)
                .HasColumnName("sequence_number")
                .IsRequired();

            entity
                .Property(operation => operation.MachineId)
                .HasColumnName("machine_id")
                .HasMaxLength(50)
                .IsRequired();

            entity
                .Property(operation => operation.Type)
                .HasColumnName("type")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity
                .Property(operation => operation.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity
                .Property(operation => operation.ProgressPercentage)
                .HasColumnName("progress_percentage")
                .IsRequired();

            entity
                .Property(operation => operation.CurrentPhase)
                .HasColumnName("current_phase")
                .HasMaxLength(200);

            entity
                .Property(operation => operation.FailureReason)
                .HasColumnName("failure_reason")
                .HasMaxLength(500);

            entity
                .Property(operation => operation.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            entity.Property(operation => operation.StartedAt).HasColumnName("started_at");

            entity.Property(operation => operation.CompletedAt).HasColumnName("completed_at");

            entity.HasIndex(operation => operation.MachineId);

            entity.HasIndex(operation => operation.Status);

            entity.HasIndex(operation => new { operation.WorkpieceId, operation.SequenceNumber }).IsUnique();

            entity.HasIndex(operation => new
            {
                operation.WorkpieceId,
                operation.Status,
                operation.SequenceNumber,
            });

            entity
                .HasOne(operation => operation.Workpiece)
                .WithMany(workpiece => workpiece.Operations)
                .HasForeignKey(operation => operation.WorkpieceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureMachineOperationEvent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MachineOperationEventRecord>(entity =>
        {
            entity.ToTable("machine_operation_events");

            entity.HasKey(item => item.Id);

            entity.Property(item => item.Id).HasColumnName("id");

            entity
                .Property(item => item.MachineOperationId)
                .HasColumnName("machine_operation_id")
                .IsRequired();

            entity
                .Property(item => item.EventType)
                .HasColumnName("event_type")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(item => item.OccurredAt).HasColumnName("occurred_at").IsRequired();

            entity
                .Property(item => item.PreviousStatus)
                .HasColumnName("previous_status")
                .HasConversion<string>()
                .HasMaxLength(50);

            entity
                .Property(item => item.NewStatus)
                .HasColumnName("new_status")
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(item => item.ProgressPercentage).HasColumnName("progress_percentage");

            entity.Property(item => item.Phase).HasColumnName("phase").HasMaxLength(200);

            entity.Property(item => item.Reason).HasColumnName("reason").HasMaxLength(500);

            entity.Property(item => item.MachineAlarmId).HasColumnName("machine_alarm_id");

            entity.Property(item => item.Metadata).HasColumnName("metadata").HasColumnType("text");

            entity.HasIndex(item => new { item.MachineOperationId, item.OccurredAt });

            entity
                .HasOne(item => item.MachineOperation)
                .WithMany(operation => operation.Events)
                .HasForeignKey(item => item.MachineOperationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureMachineAlarm(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MachineAlarmRecord>(entity =>
        {
            entity.ToTable("machine_alarms");

            entity.HasKey(item => item.Id);

            entity.Property(item => item.Id).HasColumnName("id");

            entity.Property(item => item.MachineId).HasColumnName("machine_id").HasMaxLength(50);

            entity.Property(item => item.MachineOperationId).HasColumnName("machine_operation_id");

            entity.Property(item => item.Code).HasColumnName("code").HasMaxLength(100).IsRequired();

            entity
                .Property(item => item.Severity)
                .HasColumnName("severity")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity
                .Property(item => item.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity
                .Property(item => item.Message)
                .HasColumnName("message")
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(item => item.RaisedAt).HasColumnName("raised_at").IsRequired();
            entity.Property(item => item.AcknowledgedAt).HasColumnName("acknowledged_at");
            entity.Property(item => item.ResolvedAt).HasColumnName("resolved_at");

            entity
                .Property(item => item.ResolutionNotes)
                .HasColumnName("resolution_notes")
                .HasMaxLength(1000);

            entity.HasIndex(item => item.MachineId);
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => new { item.MachineId, item.Status });
            entity.HasIndex(item => item.MachineOperationId);

            entity
                .HasOne(item => item.MachineOperation)
                .WithMany(operation => operation.MachineAlarms)
                .HasForeignKey(item => item.MachineOperationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMachineRuntimeState(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MachineRuntimeStateRecord>(entity =>
        {
            entity.ToTable("machine_runtime_states");

            entity.HasKey(item => item.MachineId);

            entity.Property(item => item.MachineId).HasColumnName("machine_id").HasMaxLength(50);

            entity
                .Property(item => item.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(item => item.CurrentOperationId).HasColumnName("current_operation_id");
            entity.Property(item => item.LastChangedAt).HasColumnName("last_changed_at").IsRequired();
            entity.Property(item => item.FailureReason).HasColumnName("failure_reason").HasMaxLength(500);
            entity.Property(item => item.ActiveAlarmId).HasColumnName("active_alarm_id");
            entity.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken();

            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => item.CurrentOperationId).IsUnique();
            entity.HasIndex(item => item.ActiveAlarmId).IsUnique();

            entity
                .HasOne(item => item.CurrentOperation)
                .WithMany(operation => operation.RuntimeStates)
                .HasForeignKey(item => item.CurrentOperationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(item => item.ActiveAlarm)
                .WithMany(alarm => alarm.RuntimeStates)
                .HasForeignKey(item => item.ActiveAlarmId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLaserCutConfiguration(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LaserCutConfigurationRecord>(entity =>
        {
            entity.ToTable("laser_cut_configurations");

            entity.HasKey(configuration => configuration.Id);

            entity.Property(configuration => configuration.Id).HasColumnName("id");

            entity
                .Property(configuration => configuration.OperationId)
                .HasColumnName("operation_id")
                .IsRequired();

            entity.HasIndex(configuration => configuration.OperationId).IsUnique();

            entity
                .Property(configuration => configuration.MaterialId)
                .HasColumnName("material_id")
                .IsRequired();

            entity
                .Property(configuration => configuration.NozzleId)
                .HasColumnName("nozzle_id")
                .IsRequired();

            entity
                .Property(configuration => configuration.DrawingFileId)
                .HasColumnName("drawing_file_id")
                .IsRequired();

            entity
                .Property(configuration => configuration.GeometryType)
                .HasColumnName("geometry_type")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity
                .Property(configuration => configuration.ThicknessMillimeters)
                .HasColumnName("thickness_millimeters")
                .HasPrecision(10, 3)
                .IsRequired();

            entity
                .Property(configuration => configuration.TubeOuterDiameterMillimeters)
                .HasColumnName("tube_outer_diameter_millimeters")
                .HasPrecision(10, 3);

            entity
                .Property(configuration => configuration.TubeLengthMillimeters)
                .HasColumnName("tube_length_millimeters")
                .HasPrecision(12, 3);

            entity
                .Property(configuration => configuration.SheetWidthMillimeters)
                .HasColumnName("sheet_width_millimeters")
                .HasPrecision(12, 3);

            entity
                .Property(configuration => configuration.SheetHeightMillimeters)
                .HasColumnName("sheet_height_millimeters")
                .HasPrecision(12, 3);

            entity
                .Property(configuration => configuration.LaserPowerWatts)
                .HasColumnName("laser_power_watts")
                .HasPrecision(12, 3)
                .IsRequired();

            entity
                .Property(configuration => configuration.CuttingSpeedMillimetersPerMinute)
                .HasColumnName("cutting_speed_millimeters_per_minute")
                .HasPrecision(12, 3)
                .IsRequired();

            entity
                .Property(configuration => configuration.AssistGas)
                .HasColumnName("assist_gas")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity
                .Property(configuration => configuration.GasPressureBar)
                .HasColumnName("gas_pressure_bar")
                .HasPrecision(10, 3)
                .IsRequired();

            entity
                .Property(configuration => configuration.FocalOffsetMillimeters)
                .HasColumnName("focal_offset_millimeters")
                .HasPrecision(10, 3)
                .IsRequired();

            entity
                .Property(configuration => configuration.NumberOfPasses)
                .HasColumnName("number_of_passes")
                .IsRequired();

            entity
                .Property(configuration => configuration.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            entity
                .HasOne(configuration => configuration.Operation)
                .WithOne(operation => operation.LaserCutConfiguration)
                .HasForeignKey<LaserCutConfigurationRecord>(configuration =>
                    configuration.OperationId
                )
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne<Material>()
                .WithMany()
                .HasForeignKey(configuration => configuration.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne<Nozzle>()
                .WithMany()
                .HasForeignKey(configuration => configuration.NozzleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne<DrawingFile>()
                .WithMany()
                .HasForeignKey(configuration => configuration.DrawingFileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProductionLot(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductionLotRecord>(entity =>
        {
            entity.ToTable("production_lots");

            entity.HasKey(lot => lot.Id);

            entity.Property(lot => lot.Id).HasColumnName("id");

            entity.Property(lot => lot.Code).HasColumnName("code").HasMaxLength(100).IsRequired();

            entity.Property(lot => lot.PlannedQuantity).HasColumnName("planned_quantity").IsRequired();

            entity
                .Property(lot => lot.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(lot => lot.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(lot => lot.StartedAt).HasColumnName("started_at");
            entity.Property(lot => lot.CompletedAt).HasColumnName("completed_at");

            entity.HasIndex(lot => lot.Code).IsUnique();
            entity.HasIndex(lot => lot.Status);
        });
    }

    private static void ConfigureWorkpiece(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkpieceRecord>(entity =>
        {
            entity.ToTable("workpieces");

            entity.HasKey(workpiece => workpiece.Id);

            entity.Property(workpiece => workpiece.Id).HasColumnName("id");

            entity
                .Property(workpiece => workpiece.ProductionLotId)
                .HasColumnName("production_lot_id")
                .IsRequired();

            entity
                .Property(workpiece => workpiece.SequenceNumber)
                .HasColumnName("sequence_number")
                .IsRequired();

            entity.Property(workpiece => workpiece.Code).HasColumnName("code").HasMaxLength(100).IsRequired();

            entity
                .Property(workpiece => workpiece.MaterialCode)
                .HasColumnName("material_code")
                .HasMaxLength(100)
                .IsRequired();

            entity
                .Property(workpiece => workpiece.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity
                .Property(workpiece => workpiece.IsSequenceActive)
                .HasColumnName("is_sequence_active")
                .IsRequired();

            entity.Property(workpiece => workpiece.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(workpiece => workpiece.StartedAt).HasColumnName("started_at");
            entity.Property(workpiece => workpiece.CompletedAt).HasColumnName("completed_at");

            entity.HasIndex(workpiece => workpiece.ProductionLotId);
            entity.HasIndex(workpiece => workpiece.Status);
            entity.HasIndex(workpiece => new { workpiece.ProductionLotId, workpiece.SequenceNumber }).IsUnique();
            entity.HasIndex(workpiece => new
            {
                workpiece.ProductionLotId,
                workpiece.Status,
                workpiece.SequenceNumber,
            });

            entity
                .HasOne(workpiece => workpiece.ProductionLot)
                .WithMany(lot => lot.Workpieces)
                .HasForeignKey(workpiece => workpiece.ProductionLotId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
