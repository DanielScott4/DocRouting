using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PadesSign.Application.Interfaces;
using PadesSign.Domain.Entities;
using PadesSign.Domain.ValueObjects;

namespace PadesSign.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/workflow-templates")]
public class WorkflowTemplatesController : ControllerBase
{
    private readonly ITemplateRepository _templates;
    public WorkflowTemplatesController(ITemplateRepository templates)
        => _templates = templates;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _templates.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var t = await _templates.GetAsync(id, ct);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TemplateDto dto, CancellationToken ct)
    {
        var template = WorkflowTemplate.Create(dto.Name, dto.Description,
            dto.Steps.Select(MapStep));
        await _templates.AddAsync(template, ct);
        return CreatedAtAction(nameof(Get), new { id = template.Id }, template);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] TemplateDto dto, CancellationToken ct)
    {
        var template = await _templates.GetAsync(id, ct);
        if (template is null) return NotFound();
        template.Update(dto.Name, dto.Description, dto.Steps.Select(MapStep));
        await _templates.UpdateAsync(template, ct);
        return Ok(template);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var template = await _templates.GetAsync(id, ct);
        if (template is null) return NotFound();
        template.Archive();
        await _templates.UpdateAsync(template, ct);
        return NoContent();
    }

    private static WorkflowStep MapStep(StepDto s) => new()
    {
        TemplateId = Guid.Empty, // set by EF relationship
        Order      = s.Order,
        AssigneeId = s.AssigneeId,
        RoleName   = s.RoleName,
        IsParallel = s.IsParallel,
        Deadline   = s.DeadlineHours.HasValue ? TimeSpan.FromHours(s.DeadlineHours.Value) : null,
        Field      = new SignatureFieldDefinition(
            s.PageNumber, s.X, s.Y, s.Width, s.Height, s.Reason, s.Location)
    };
}

public record TemplateDto(string Name, string Description, List<StepDto> Steps);
public record StepDto(int Order, Guid? AssigneeId, string? RoleName, bool IsParallel,
    int? DeadlineHours, int PageNumber, float X, float Y, float Width, float Height,
    string Reason, string Location);