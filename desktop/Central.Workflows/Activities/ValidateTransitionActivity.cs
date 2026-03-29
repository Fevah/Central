using System.ComponentModel;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

namespace Central.Workflows.Activities;

/// <summary>Validates that a status transition is allowed based on workflow rules.</summary>
[Activity("Central", "Tasks", "Validate Transition",
    Description = "Checks if a task status transition is allowed. Outputs IsValid and Reason.")]
[Category("Central Tasks")]
public class ValidateTransitionActivity : CodeActivity<bool>
{
    [Description("Current status of the task.")]
    public Input<string> CurrentStatus { get; set; } = default!;

    [Description("Desired new status.")]
    public Input<string> NewStatus { get; set; } = default!;

    [Description("Required fields (comma-separated field names that must be non-empty).")]
    public Input<string?> RequiredFields { get; set; } = default!;

    protected override void Execute(ActivityExecutionContext context)
    {
        var current = context.Get(CurrentStatus) ?? "";
        var next = context.Get(NewStatus) ?? "";
        var required = context.Get(RequiredFields) ?? "";

        // Default allowed transitions
        var allowed = new Dictionary<string, string[]>
        {
            ["Open"] = ["InProgress", "Blocked", "Done"],
            ["InProgress"] = ["Review", "Blocked", "Done", "Open"],
            ["Review"] = ["Done", "InProgress", "Blocked"],
            ["Done"] = ["Open"],  // reopen
            ["Blocked"] = ["Open", "InProgress"],
        };

        var isValid = allowed.TryGetValue(current, out var targets) && targets.Contains(next);
        var reason = isValid ? "" : $"Transition from '{current}' to '{next}' is not allowed.";

        context.SetVariable("TransitionIsValid", isValid);
        context.SetVariable("TransitionReason", reason);
        context.SetResult(isValid);
    }
}
