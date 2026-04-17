using System.ComponentModel;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

namespace Central.Workflows.Activities;

/// <summary>Sets a field value on an entity. Used in auto-actions on workflow transitions.</summary>
[Activity("Central", "Data", "Set Field Value",
    Description = "Sets a field on the target entity (e.g., completed_at = NOW on Done status).")]
[Category("Central Data")]
public class SetFieldActivity : CodeActivity
{
    [Description("Entity type (e.g., 'task').")]
    public Input<string> EntityType { get; set; } = default!;

    [Description("Entity ID.")]
    public Input<int> EntityId { get; set; } = default!;

    [Description("Field name to set.")]
    public Input<string> FieldName { get; set; } = default!;

    [Description("Value to set (use 'NOW' for current timestamp, 'NULL' to clear).")]
    public Input<string> FieldValue { get; set; } = default!;

    protected override void Execute(ActivityExecutionContext context)
    {
        context.SetVariable("SetFieldEntityType", context.Get(EntityType));
        context.SetVariable("SetFieldEntityId", context.Get(EntityId));
        context.SetVariable("SetFieldName", context.Get(FieldName));
        context.SetVariable("SetFieldValue", context.Get(FieldValue));
    }
}
