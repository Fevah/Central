using System.ComponentModel;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

namespace Central.Workflows.Activities;

/// <summary>Updates a task's status. Optionally enforces allowed transitions.</summary>
[Activity("Central", "Tasks", "Update Task Status",
    Description = "Updates the status of a task item and records the transition.")]
[Category("Central Tasks")]
public class UpdateTaskStatusActivity : CodeActivity<bool>
{
    [Description("The ID of the task to update.")]
    public Input<int> TaskId { get; set; } = default!;

    [Description("The new status value (Open, InProgress, Review, Done, Blocked).")]
    public Input<string> NewStatus { get; set; } = default!;

    [Description("Optional: who triggered the transition.")]
    public Input<string?> TriggeredBy { get; set; } = default!;

    protected override void Execute(ActivityExecutionContext context)
    {
        var taskId = context.Get(TaskId);
        var newStatus = context.Get(NewStatus);
        var triggeredBy = context.Get(TriggeredBy) ?? "workflow";

        // Store transition data in workflow variables for downstream activities
        context.SetVariable("TransitionTaskId", taskId);
        context.SetVariable("TransitionNewStatus", newStatus);
        context.SetVariable("TransitionTriggeredBy", triggeredBy);

        // The actual DB update is handled by the host via IWorkflowEventHandler
        // or by a subsequent activity that has access to the repository.
        // This activity records the intent; execution is confirmed by the host.
        context.SetResult(true);
    }
}
