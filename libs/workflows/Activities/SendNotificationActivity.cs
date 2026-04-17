using System.ComponentModel;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;

namespace Central.Workflows.Activities;

/// <summary>Sends a notification to one or more users.</summary>
[Activity("Central", "Notifications", "Send Notification",
    Description = "Sends a notification to specified users via the Central notification system.")]
[Category("Central Notifications")]
public class SendNotificationActivity : CodeActivity
{
    [Description("Comma-separated user IDs or 'all' for broadcast.")]
    public Input<string> Recipients { get; set; } = default!;

    [Description("Notification title.")]
    public Input<string> Title { get; set; } = default!;

    [Description("Notification message body.")]
    public Input<string> Message { get; set; } = default!;

    [Description("Notification level: Info, Success, Warning, Error.")]
    public Input<string> Level { get; set; } = new("Info");

    protected override void Execute(ActivityExecutionContext context)
    {
        var recipients = context.Get(Recipients);
        var title = context.Get(Title);
        var message = context.Get(Message);
        var level = context.Get(Level) ?? "Info";

        // Store for host-side dispatch
        context.SetVariable("NotificationRecipients", recipients);
        context.SetVariable("NotificationTitle", title);
        context.SetVariable("NotificationMessage", message);
        context.SetVariable("NotificationLevel", level);
    }
}
