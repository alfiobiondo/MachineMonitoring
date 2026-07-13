namespace MachineMonitoring.Domain.Technology;

public sealed class LaserCutConfigurationValidator
{
    public void Validate(
        LaserCutConfiguration configuration,
        Material material,
        Nozzle nozzle,
        MachineCapabilities capabilities
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(nozzle);
        ArgumentNullException.ThrowIfNull(capabilities);

        if (configuration.MaterialId != material.Id)
        {
            throw new InvalidOperationException(
                "The supplied material does not match the configuration material ID."
            );
        }

        if (configuration.NozzleId != nozzle.Id)
        {
            throw new InvalidOperationException(
                "The supplied nozzle does not match the configuration nozzle ID."
            );
        }

        if (!capabilities.SupportsMaterial(material))
        {
            throw new InvalidOperationException(
                $"Machine {capabilities.MachineId} does not support material {material.Code}."
            );
        }

        if (!capabilities.SupportsNozzle(nozzle))
        {
            throw new InvalidOperationException(
                $"Machine {capabilities.MachineId} does not support nozzle {nozzle.Code}."
            );
        }

        if (!capabilities.SupportsThickness(configuration.Geometry.ThicknessMillimeters))
        {
            throw new InvalidOperationException(
                $"Machine {capabilities.MachineId} does not support thickness "
                    + $"{configuration.Geometry.ThicknessMillimeters} mm."
            );
        }

        if (!capabilities.SupportsGeometry(configuration.Geometry))
        {
            throw new InvalidOperationException(
                $"Machine {capabilities.MachineId} does not support the selected "
                    + $"{configuration.GeometryType} geometry or its dimensions."
            );
        }

        if (!capabilities.SupportsLaserPower(configuration.LaserPowerWatts))
        {
            throw new InvalidOperationException(
                $"Laser power {configuration.LaserPowerWatts} W exceeds the capabilities "
                    + $"of machine {capabilities.MachineId}."
            );
        }

        if (configuration.GasPressureBar > nozzle.MaximumPressureBar)
        {
            throw new InvalidOperationException(
                $"Gas pressure {configuration.GasPressureBar} bar exceeds the maximum "
                    + $"pressure of nozzle {nozzle.Code}."
            );
        }
    }
}
