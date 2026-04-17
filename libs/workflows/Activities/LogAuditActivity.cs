using System.ComponentModel;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

namespace Central.Workflows.Activities;

/// <summary>Logs an audit trail entry for workflow actions.</summary>
[Activity("Central", "Audit", "Log Audit Entry",
    Description = "Records an audit log entry for the current workflow action.")]
[Category("Central Audit")]
public class LogAuditActivity : CodeActivity
{
    [Description("Action description (e.g., 'Status changed to Done').")]
    public Input<string> Action { get; set; } = default!;

    [Description("Entity type (e.g., 'task', 'device', 'sprint').")]
    public Input<string> EntityType { get; set; } = default!;

    [Description("Entity ID.")]
    public Input<int> EntityId { get; set; } = default!;

    [Description("User who performed the action.")]
    public Input<string?> PerformedBy { get; set; } = default!;

    [Description("Additional details (JSON or text).")]
    public Input<string?> Details { get; set; } = default!;

    protected override void Execute(ActivityExecutionContext context)
    {
        // Store for host-side persistence
        context.SetVariable("AuditAction", context.Get(Action));
        context.SetVariable("AuditEntityType", context.Get(EntityType));
        context.SetVariable("AuditEntityId", context.Get(EntityId));
        context.SetVariable("AuditPerformedBy", context.Get(PerformedBy));
        context.SetVariable("AuditDetails", context.Get(Details));
    }
}
