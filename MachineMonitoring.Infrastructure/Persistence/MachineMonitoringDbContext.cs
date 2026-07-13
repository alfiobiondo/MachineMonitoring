using MachineMonitoring.Domain.Technology;
using Microsoft.EntityFrameworkCore;

namespace MachineMonitoring.Infrastructure.Persistence;

public sealed class MachineMonitoringDbContext : DbContext
{
    public DbSet<Material> Materials => Set<Material>();

    public DbSet<Nozzle> Nozzles => Set<Nozzle>();

    public MachineMonitoringDbContext(DbContextOptions<MachineMonitoringDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureMaterial(modelBuilder);
        ConfigureNozzle(modelBuilder);
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
}
