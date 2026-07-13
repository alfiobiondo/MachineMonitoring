namespace MachineMonitoring.Domain.Technology;

public sealed class Material
{
    public Guid Id { get; }

    public string Code { get; }

    public string Name { get; }

    public MaterialCategory Category { get; }

    public string Grade { get; }

    public bool IsEnabled { get; private set; }

    private Material()
    {
        Code = null!;
        Name = null!;
        Grade = null!;
    }

    public Material(Guid id, string code, string name, MaterialCategory category, string grade)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The material ID cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(grade);

        Id = id;
        Code = code;
        Name = name;
        Category = category;
        Grade = grade;
        IsEnabled = true;
    }

    public void Disable()
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException($"Material {Code} is already disabled.");
        }

        IsEnabled = false;
    }

    public void Enable()
    {
        if (IsEnabled)
        {
            throw new InvalidOperationException($"Material {Code} is already enabled.");
        }

        IsEnabled = true;
    }
}
