using Central.Workflows;
using Central.Workflows.Activities;

namespace Central.Tests.Tasks;

/// <summary>
/// Tests for Elsa custom activity metadata and configuration.
/// Activities execute within Elsa runtime — these tests verify structure and attributes.
/// </summary>
public class WorkflowActivityTests
{
    [Fact]
    public void ValidateTransitionActivity_HasCorrectCategory()
    {
        var attrs = typeof(ValidateTransitionActivity).GetCustomAttributes(false);
        var category = attrs.OfType<Elsa.Workflows.Attributes.ActivityAttribute>().FirstOrDefault();
        Assert.NotNull(category);
        Assert.Equal("Central", category.Namespace);
        Assert.Equal("Tasks", category.Category);
    }

    [Fact]
    public void UpdateTaskStatusActivity_HasCorrectCategory()
    {
        var attrs = typeof(UpdateTaskStatusActivity).GetCustomAttributes(false);
        var activity = attrs.OfType<Elsa.Workflows.Attributes.ActivityAttribute>().FirstOrDefault();
        Assert.NotNull(activity);
        Assert.Equal("Central", activity.Namespace);
        Assert.Equal("Tasks", activity.Category);
    }

    [Fact]
    public void SendNotificationActivity_HasCorrectCategory()
    {
        var attrs = typeof(SendNotificationActivity).GetCustomAttributes(false);
        var activity = attrs.OfType<Elsa.Workflows.Attributes.ActivityAttribute>().FirstOrDefault();
        Assert.NotNull(activity);
        Assert.Equal("Central", activity.Namespace);
        Assert.Equal("Notifications", activity.Category);
    }

    [Fact]
    public void ApprovalActivity_HasCorrectCategory()
    {
        var attrs = typeof(ApprovalActivity).GetCustomAttributes(false);
        var activity = attrs.OfType<Elsa.Workflows.Attributes.ActivityAttribute>().FirstOrDefault();
        Assert.NotNull(activity);
        Assert.Equal("Central", activity.Namespace);
        Assert.Equal("Approvals", activity.Category);
    }

    [Fact]
    public void LogAuditActivity_HasCorrectCategory()
    {
        var attrs = typeof(LogAuditActivity).GetCustomAttributes(false);
        var activity = attrs.OfType<Elsa.Workflows.Attributes.ActivityAttribute>().FirstOrDefault();
        Assert.NotNull(activity);
        Assert.Equal("Central", activity.Namespace);
        Assert.Equal("Audit", activity.Category);
    }

    [Fact]
    public void SetFieldActivity_HasCorrectCategory()
    {
        var attrs = typeof(SetFieldActivity).GetCustomAttributes(false);
        var activity = attrs.OfType<Elsa.Workflows.Attributes.ActivityAttribute>().FirstOrDefault();
        Assert.NotNull(activity);
        Assert.Equal("Central", activity.Namespace);
        Assert.Equal("Data", activity.Category);
    }

    [Fact]
    public void AllActivities_DiscoverableInAssembly()
    {
        var assembly = typeof(WorkflowsAssemblyMarker).Assembly;
        var activityTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttributes(typeof(Elsa.Workflows.Attributes.ActivityAttribute), false).Length > 0)
            .ToList();

        Assert.Equal(6, activityTypes.Count);
    }

    [Fact]
    public void ApprovalBookmarkPayload_StoresData()
    {
        var payload = new ApprovalBookmarkPayload(42, "Please approve deployment");
        Assert.Equal(42, payload.ApproverId);
        Assert.Equal("Please approve deployment", payload.Description);
    }

    [Fact]
    public void TaskStatusTransitionWorkflow_Exists()
    {
        var assembly = typeof(WorkflowsAssemblyMarker).Assembly;
        var workflowTypes = assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Elsa.Workflows.WorkflowBase)))
            .ToList();

        Assert.Single(workflowTypes);
        Assert.Equal("TaskStatusTransitionWorkflow", workflowTypes[0].Name);
    }
}
