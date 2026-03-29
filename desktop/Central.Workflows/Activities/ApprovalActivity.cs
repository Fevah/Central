using System.ComponentModel;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

namespace Central.Workflows.Activities;

/// <summary>
/// Creates an approval request and suspends the workflow until approved/rejected.
/// Uses Elsa bookmarks to pause execution.
/// </summary>
[Activity("Central", "Approvals", "Request Approval",
    Description = "Suspends the workflow and waits for a user to approve or reject.")]
[Category("Central Approvals")]
public class ApprovalActivity : Activity
{
    [Description("User ID of the approver.")]
    public Input<int> ApproverId { get; set; } = default!;

    [Description("Description of what needs approval.")]
    public Input<string> ApprovalDescription { get; set; } = default!;

    [Description("Reference entity type (e.g., 'task', 'device').")]
    public Input<string?> EntityType { get; set; } = default!;

    [Description("Reference entity ID.")]
    public Input<int?> EntityId { get; set; } = default!;

    /// <summary>Output: true if approved, false if rejected.</summary>
    public Output<bool> IsApproved { get; set; } = default!;

    /// <summary>Output: comment from the approver.</summary>
    public Output<string?> ApproverComment { get; set; } = default!;

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var approverId = context.Get(ApproverId);
        var description = context.Get(ApprovalDescription);

        // Store approval request data
        context.SetVariable("ApprovalApproverId", approverId);
        context.SetVariable("ApprovalDescription", description);
        context.SetVariable("ApprovalEntityType", context.Get(EntityType));
        context.SetVariable("ApprovalEntityId", context.Get(EntityId));

        // Create a bookmark — workflow suspends here until resumed
        context.CreateBookmark(new ApprovalBookmarkPayload(approverId, description ?? ""), OnResumed);
    }

    private async ValueTask OnResumed(ActivityExecutionContext context)
    {
        // When resumed, the host passes approval result as input
        var input = context.WorkflowInput;
        var approved = input.TryGetValue("Approved", out var val) && val is true;
        var comment = input.TryGetValue("Comment", out var c) ? c?.ToString() : null;

        context.Set(IsApproved, approved);
        context.Set(ApproverComment, comment);

        await context.CompleteActivityAsync();
    }
}

public record ApprovalBookmarkPayload(int ApproverId, string Description);
