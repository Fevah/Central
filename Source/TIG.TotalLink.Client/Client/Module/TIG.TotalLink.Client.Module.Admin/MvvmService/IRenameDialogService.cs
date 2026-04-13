namespace TIG.TotalLink.Client.Module.Admin.MvvmService
{
    public interface IRenameDialogService
    {
        /// <summary>
        /// Shows the dialog.
        /// </summary>
        /// <param name="title">The title to display on the dialog.</param>
        /// <param name="name">The original name to be edited.</param>
        /// <returns>The new name that was entered if the user pressed OK; otherwise null.</returns>
        string ShowDialog(string title, string name);
    }
}
