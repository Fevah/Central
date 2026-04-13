using System;
using System.Windows;
using System.Windows.Controls;
using DevExpress.Mvvm;
using DevExpress.Mvvm.UI;
using DevExpress.Mvvm.UI.Interactivity;
using DevExpress.Xpf.Core;
using TIG.TotalLink.Client.Editor.MvvmService.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Dialog;

namespace TIG.TotalLink.Client.Module.Admin.MvvmService
{
    [TargetType(typeof(Window))]
    [TargetType(typeof(UserControl))]
    public class RenameDialogService : DialogServiceBase, IRenameDialogService
    {
        #region IRenameDialogService

        /// <summary>
        /// Shows the dialog.
        /// </summary>
        /// <param name="title">The title to display on the dialog.</param>
        /// <param name="name">The original name to be edited.</param>
        /// <returns>The new name that was entered if the user pressed OK; otherwise null.</returns>
        public string ShowDialog(string title, string name)
        {

            // Create the view template if we haven't already
            if (ViewTemplate == null)
                ViewTemplate = XamlHelper.GetTemplate("<Grid xmlns:dialog=\"clr-namespace:TIG.TotalLink.Client.Module.Admin.View.Dialog;assembly=TIG.TotalLink.Client.Module.Admin\"><dialog:RenameDialogView/></Grid>");

            // Create a viewmodel and view to display in the dialog
            var viewModel = new RenameDialogViewModel(name);
            var view = CreateAndInitializeView(null, viewModel, null, null, this);

            // Create the dialog window
            var windowStateKey = "RenameDialog";
            var dialogWindow = CreateDialogWindow(view, windowStateKey, 400, 150);

            // Add the dialog window to the list of tracked windows
            Windows.Add(new WeakReference(dialogWindow));

            // Set the dialog title
            if (title != null)
                dialogWindow.Title = title;
            else
                DocumentUIServiceBase.SetTitleBinding(view, Window.TitleProperty, dialogWindow, true);

            // Attach event handlers
            dialogWindow.Closing += OnDialogWindowClosing;
            dialogWindow.Closed += OnDialogWindowClosed;

            // Set the dialog commands
            dialogWindow.CommandsSource = UICommand.GenerateFromMessageBoxButton(MessageBoxButton.OKCancel, GetLocalizer(this));

            // Show the dialog and return the result
            var result = dialogWindow.ShowDialogWindow();
            return (result != null && !result.IsCancel ? viewModel.Name : null);
        }

        #endregion
    }
}
