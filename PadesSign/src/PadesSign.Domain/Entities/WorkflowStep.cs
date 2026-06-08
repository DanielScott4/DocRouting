using PadesSign.Domain.ValueObjects;

namespace PadesSign.Domain.Entities;

public class WorkflowStep
{
    public Guid                    Id           { get; init; } = Guid.NewGuid();
    public Guid                    TemplateId   { get; init; }
    public int                     Order        { get; init; }
    /// <summary>Specific user assigned, or null to route by role.</summary>
    public Guid?                   AssigneeId   { get; init; }
    /// <summary>Route to any user with this role when AssigneeId is null.</summary>
    public string?                 RoleName     { get; init; }
    /// <summary>Steps with the same Order value run in parallel.</summary>
    public bool                    IsParallel   { get; init; }
    public TimeSpan?               Deadline     { get; init; }
    public SignatureFieldDefinition Field        { get; init; } = null!;
}