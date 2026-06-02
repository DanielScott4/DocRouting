namespace PadesSign.Domain.Entities;

public class WorkflowTemplate
{
    public Guid              Id          { get; private set; } = Guid.NewGuid();
    public string            Name        { get; private set; } = string.Empty;
    public string            Description { get; private set; } = string.Empty;
    public bool              IsArchived  { get; private set; }
    public DateTime          CreatedAt   { get; private set; } = DateTime.UtcNow;
    public DateTime          UpdatedAt   { get; private set; } = DateTime.UtcNow;

    private readonly List<WorkflowStep> _steps = new();
    public IReadOnlyList<WorkflowStep>  Steps  => _steps.AsReadOnly();

    private WorkflowTemplate() { }

    public static WorkflowTemplate Create(string name, string description,
        IEnumerable<WorkflowStep> steps)
    {
        var t = new WorkflowTemplate { Name = name, Description = description };
        t._steps.AddRange(steps);
        return t;
    }

    public void Update(string name, string description, IEnumerable<WorkflowStep> steps)
    {
        Name = name; Description = description; UpdatedAt = DateTime.UtcNow;
        _steps.Clear(); _steps.AddRange(steps);
    }

    public void Archive() { IsArchived = true; UpdatedAt = DateTime.UtcNow; }
}