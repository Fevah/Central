using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DevExpress.Mvvm;
using DevExpress.Mvvm.UI.Interactivity;
using DevExpress.Xpf.Core;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Core.Interface.MVVMService;
using TIG.TotalLink.Client.Editor.MvvmService.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Dialog;
using TIG.TotalLink.Client.Undo.Helper;

namespace TIG.TotalLink.Client.Module.Admin.MvvmService
{
    [TargetType(typeof(Window))]
    [TargetType(typeof(UserControl))]
    public class DetailDialogService : DialogServiceBase, IDetailDialogService
    {
        #region Private Methods

        /// <summary>
        /// Shows the dialog.
        /// </summary>
        /// <param name="editMode">The mode that the dialog is using to edit.</param>
        /// <param name="editObject">The object being edited by this dialog.</param>
        /// <param name="title">The title for the dialog.</param>
        /// <returns>True if the user pressed OK; otherwise false.</returns>
        private bool ShowDialogInternal(DetailEditMode editMode, INotifyPropertyChanged editObject, string title)
        {
            // Create the view template if we haven't already
            if (ViewTemplate == null)
                ViewTemplate = XamlHelper.GetTemplate("<Grid xmlns:dialog=\"clr-namespace:TIG.TotalLink.Client.Module.Admin.View.Dialog;assembly=TIG.TotalLink.Client.Module.Admin\"><dialog:DetailDialogView/></Grid>");

            // Create a viewmodel and view to display in the dialog
            var viewModel = new DetailDialogViewModel(editMode, editObject);
            var view = CreateAndInitializeView(null, viewModel, null, null, this);

            // Create the dialog window
            var windowStateKey = string.Format("DetailDialog_{0}", editObject.GetType().FullName);
            var defaultWindowSize = DataObjectHelper.GetDefaultDialogSize(editObject.GetType());
            var dialogWindow = CreateDialogWindow(view, windowStateKey, defaultWindowSize.Width, defaultWindowSize.Height);

            // Add the dialog window to the list of tracked windows
            Windows.Add(new WeakReference(dialogWindow));

            // Set the dialog title
            dialogWindow.Title = title;

            // Attach event handlers
            dialogWindow.Closing += OnDialogWindowClosing;
            dialogWindow.Closed += OnDialogWindowClosed;

            // Set the dialog commands
            var commands = UICommand.GenerateFromMessageBoxButton(MessageBoxButton.OKCancel, GetLocalizer(this));
            var okCommand = commands.First(c => Equals(c.Id, MessageBoxResult.OK));
            okCommand.Command = viewModel.OkCommand;
            dialogWindow.CommandsSource = commands;

            // Show the dialog
            var result = dialogWindow.ShowDialogWindow();

            // Return the result
            return (result != null && !result.IsCancel);
        }

        #endregion


        #region IDetailDialogService

        /// <summary>
        /// Shows the dialog.
        /// </summary>
        /// <param name="editMode">The mode that the dialog is using to edit.</param>
        /// <param name="editObject">The object being edited by this dialog.</param>
        /// <param name="objectTypeName">Specifies an alternative object type name to use, instead of using the type directly from the data object.</param>
        /// <returns>True if the user pressed OK; otherwise false.</returns>
        public bool ShowDialog(DetailEditMode editMode, INotifyPropertyChanged editObject, string objectTypeName = null)
        {
            return ShowDialogInternal(editMode, editObject, string.Format("{0} {1}", editMode, !string.IsNullOrWhiteSpace(objectTypeName) ? objectTypeName : editObject.GetType().Name.AddSpaces()));
        }

        /// <summary>
        /// Shows the dialog.
        /// </summary>
        /// <param name="editObject">The object being edited by this dialog.</param>
        /// <param name="title">The title for the dialog.</param>
        /// <returns>True if the user pressed OK; otherwise false.</returns>
        public bool ShowDialog(INotifyPropertyChanged editObject, string title)
        {
            return ShowDialogInternal(DetailEditMode.None, editObject, title);
        }

        #endregion
    }
}
