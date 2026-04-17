using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Central.Workflows.Activities;

namespace Central.Workflows.Workflows;

/// <summary>
/// Default task status transition workflow.
/// Validates transition → updates status → logs audit → sends notification.
/// Triggered via HTTP POST /workflows/task-transition.
/// </summary>
public class TaskStatusTransitionWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        // Input variables
        var taskId = builder.WithVariable<int>("TaskId", 0);
        var currentStatus = builder.WithVariable<string>("CurrentStatus", "");
        var newStatus = builder.WithVariable<string>("NewStatus", "");
        var userId = builder.WithVariable<string>("UserId", "");

        builder.Name = "Task Status Transition";
        builder.Description = "Validates and executes a task status change with audit logging and notifications.";

        builder.Root = new Sequence
        {
            Activities =
            {
                // Step 1: Validate the transition
                new ValidateTransitionActivity
                {
                    CurrentStatus = new(ctx => currentStatus.Get(ctx)),
                    NewStatus = new(ctx => newStatus.Get(ctx)),
                },

                // Step 2: Update the task status (if valid)
                new If
                {
                    Condition = new(ctx => ctx.GetVariable<bool>("TransitionIsValid")),
                    Then = new Sequence
                    {
                        Activities =
                        {
                            new UpdateTaskStatusActivity
                            {
                                TaskId = new(ctx => taskId.Get(ctx)),
                                NewStatus = new(ctx => newStatus.Get(ctx)),
                                TriggeredBy = new(ctx => userId.Get(ctx)),
                            },
                            new LogAuditActivity
                            {
                                Action = new(ctx => $"Status changed to {newStatus.Get(ctx)}"),
                                EntityType = new("task"),
                                EntityId = new(ctx => taskId.Get(ctx)),
                                PerformedBy = new(ctx => userId.Get(ctx)),
                            },
                            new SendNotificationActivity
                            {
                                Recipients = new("all"),
                                Title = new("Task Status Updated"),
                                Message = new(ctx => $"Task #{taskId.Get(ctx)} moved to {newStatus.Get(ctx)}"),
                                Level = new("Info"),
                            },
                        }
                    },
                    Else = new SendNotificationActivity
                    {
                        Recipients = new(ctx => userId.Get(ctx)),
                        Title = new("Transition Denied"),
                        Message = new(ctx => ctx.GetVariable<string>("TransitionReason") ?? "Invalid transition"),
                        Level = new("Warning"),
                    }
                }
            }
        };
    }
}
