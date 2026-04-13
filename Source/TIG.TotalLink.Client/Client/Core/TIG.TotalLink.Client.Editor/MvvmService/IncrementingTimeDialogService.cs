using System;
using System.Windows;
using System.Windows.Controls;
using DevExpress.Mvvm;
using DevExpress.Mvvm.UI.Interactivity;
using DevExpress.Xpf.Core;
using TIG.TotalLink.Client.Editor.MvvmService.Core;
using TIG.TotalLink.Client.Editor.MvvmService.Interface;
using TIG.TotalLink.Client.Editor.ViewModel;

namespace TIG.TotalLink.Client.Editor.MvvmService
{
    [TargetType(typeof(Window))]
    [TargetType(typeof(UserControl))]
    public class IncrementingTimeDialogService : DialogServiceBase, IIncrementingTimeDialogService
    {
        #region IIncrementingTimeDialogService

        /// <summary>
        /// Shows the dialog.
        /// </summary>
        /// <returns>The added hours.</returns>
        public decimal? ShowDialog()
        {
            // Create the view template if we haven't already
            if (ViewTemplate == null)
                ViewTemplate = XamlHelper.GetTemplate("<Grid xmlns:dialog=\"clr-namespace:TIG.TotalLink.Client.Editor.View.Dialog;assembly=TIG.TotalLink.Client.Editor\"><dialog:IncrementingTimeDialogView/></Grid>");

            // Create a viewmodel and view to display in the dialog
            var viewModel = new IncrementingTimeDialogViewModel(0);
            var view = CreateAndInitializeView(null, viewModel, null, null, this);

            // Create the dialog window
            const string windowStateKey = "IncrementingTimeDialog";
            var dialogWindow = CreateDialogWindow(view, windowStateKey, 400, 150);

            // Add the dialog window to the list of tracked windows
            Windows.Add(new WeakReference(dialogWindow));

            // Set the dialog title
            dialogWindow.Title = "Add Time";

            // Attach event handlers
            dialogWindow.Closing += OnDialogWindowClosing;
            dialogWindow.Closed += OnDialogWindowClosed;

            // Set the dialog commands
            dialogWindow.CommandsSource = UICommand.GenerateFromMessageBoxButton(MessageBoxButton.OKCancel, GetLocalizer(this));

            // Show the dialog and return the result
            var result = dialogWindow.ShowDialogWindow();
            return (result != null && !result.IsCancel ? (decimal?)viewModel.Hours : null);
        }

        #endregion
    }
}
