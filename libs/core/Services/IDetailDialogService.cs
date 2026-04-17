namespace Central.Core.Services;

/// <summary>
/// Generic detail dialog service — shows add/edit dialogs for any entity type.
/// Based on TotalLink's DetailDialogService pattern.
/// </summary>
public interface IDetailDialogService
{
    /// <summary>Show a dialog for adding or editing an entity.</summary>
    /// <typeparam name="T">Entity type.</typeparam>
    /// <param name="mode">Add, Edit, or View.</param>
    /// <param name="entity">The entity to edit (new instance for Add).</param>
    /// <param name="title">Optional title override. Default: "{Mode} {TypeName}".</param>
    /// <returns>True if user confirmed (OK), false if cancelled.</returns>
    bool ShowDialog<T>(DetailEditMode mode, T entity, string? title = null) where T : class;

    /// <summary>Show a confirmation dialog.</summary>
    /// <param name="message">The question to ask.</param>
    /// <param name="title">Dialog title.</param>
    /// <returns>True if user confirmed.</returns>
    bool Confirm(string message, string title = "Confirm");

    /// <summary>Show an info dialog.</summary>
    void Info(string message, string title = "Info");

    /// <summary>Show an error dialog.</summary>
    void Error(string message, string title = "Error");
}

public enum DetailEditMode
{
    Add,
    Edit,
    View
}

/// <summary>
/// Attribute to set default dialog size for an entity type.
/// Based on TotalLink's DialogSizeAttribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class DialogSizeAttribute : Attribute
{
    public double Width { get; }
    public double Height { get; }

    public DialogSizeAttribute(double width, double height)
    {
        Width = width;
        Height = height;
    }
}
