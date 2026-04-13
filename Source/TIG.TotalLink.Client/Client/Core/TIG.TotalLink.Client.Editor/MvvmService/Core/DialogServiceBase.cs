using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DevExpress.Mvvm;
using DevExpress.Mvvm.Native;
using DevExpress.Mvvm.UI;
using DevExpress.Mvvm.UI.Interactivity;
using DevExpress.Xpf.Core;
using TIG.TotalLink.Client.Editor.Control;

namespace TIG.TotalLink.Client.Editor.MvvmService.Core
{
    [TargetType(typeof(Window))]
    [TargetType(typeof(UserControl))]
    public class DialogServiceBase : ViewServiceBase, IMessageBoxButtonLocalizer, IDocumentOwner
    {
        #region Protected Properties

        protected readonly List<WeakReference> Windows = new List<WeakReference>();

        #endregion


        #region Dependency Properties

        public static readonly DependencyProperty DialogWindowStartupLocationProperty =
            DependencyProperty.Register("DialogWindowStartupLocation", typeof(WindowStartupLocation), typeof(DialogServiceBase), new PropertyMetadata(WindowStartupLocation.CenterOwner));

        public static readonly DependencyProperty SetWindowOwnerProperty =
            DependencyProperty.Register("SetWindowOwner", typeof(bool), typeof(DialogServiceBase), new PropertyMetadata(true));

        public static readonly DependencyProperty DialogStyleProperty =
            DependencyProperty.Register("DialogStyle", typeof(Style), typeof(DialogServiceBase), new PropertyMetadata(null));

        /// <summary>
        /// The startup location for the dialog.
        /// </summary>
        public WindowStartupLocation DialogWindowStartupLocation
        {
            get { return (WindowStartupLocation)GetValue(DialogWindowStartupLocationProperty); }
            set { SetValue(DialogWindowStartupLocationProperty, value); }
        }

        /// <summary>
        /// The owner of the dialog.
        /// </summary>
        public bool SetWindowOwner
        {
            get { return (bool)GetValue(SetWindowOwnerProperty); }
            set { SetValue(SetWindowOwnerProperty, value); }
        }

        /// <summary>
        /// The style to apply to the dialog.
        /// </summary>
        public Style DialogStyle
        {
            get { return (Style)GetValue(DialogStyleProperty); }
            set { SetValue(DialogStyleProperty, value); }
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Creates a DXDialogWindow containing the specified view.
        /// </summary>
        /// <param name="view">The view to display in the dialog window.</param>
        /// <returns>The new DXDialogWindow.</returns>
        protected virtual DXDialogWindowEx CreateDialogWindow(object view)
        {
            // Create and configure the new window
            var dialogWindow = new DXDialogWindowEx();
            ConfigureDialogWindow(dialogWindow, view);

            // Return the new window
            return dialogWindow;
        }

        /// <summary>
        /// Creates a DXDialogWindow containing the specified view, and automatically manages the window state.
        /// </summary>
        /// <param name="view">The view to display in the dialog window.</param>
        /// <param name="windowStateKey">The key to use when saving/restoring the window state.</param>
        /// <param name="defaultWidth">The default width for the window.</param>
        /// <param name="defaultHeight">The default height for the window.</param>
        /// <returns>The new DXDialogWindow.</returns>
        protected virtual DXDialogWindowEx CreateDialogWindow(object view, string windowStateKey, double defaultWidth, double defaultHeight)
        {
            // Create and configure the new window
            var dialogWindow = new DXDialogWindowEx(windowStateKey, defaultWidth, defaultHeight);
            ConfigureDialogWindow(dialogWindow, view);

            // Return the new window
            return dialogWindow;
        }

        /// <summary>
        /// Configures a dialog window.
        /// </summary>
        /// <param name="dialogWindow">The DXDialogWindow to configure.</param>
        /// <param name="view">The view to display in the dialog window.</param>
        protected virtual void ConfigureDialogWindow(DXDialogWindow dialogWindow, object view)
        {
            // Configure the window
            dialogWindow.Content = view;
            dialogWindow.WindowStartupLocation = DialogWindowStartupLocation;

            // Set the window owner
            if (SetWindowOwner)
                dialogWindow.Owner = Window.GetWindow(AssociatedObject);

            // Set the window style
            if (DialogStyle != null)
                dialogWindow.Style = DialogStyle;

            // Apply the current theme
            UpdateThemeName(dialogWindow);
        }

        /// <summary>
        /// Gets the viewmodel contained in the specified window.
        /// </summary>
        /// <param name="window">The window to find the viewmodel for.</param>
        /// <returns>The viewmodel.</returns>
        protected object GetViewModel(DXDialogWindow window)
        {
            return ViewHelper.GetViewModelFromView(window.Content);
        }

        /// <summary>
        /// Returns all windows being tracked by this service.
        /// </summary>
        /// <returns>All windows being tracked by this service.</returns>
        protected IEnumerable<DXDialogWindow> GetWindows()
        {
            for (int windowIndex = Windows.Count; --windowIndex >= 0; )
            {
                var window = (DXDialogWindow)Windows[windowIndex].Target;
                if (window == null)
                    Windows.RemoveAt(windowIndex);
                else
                    yield return window;
            }
        }

        #endregion


        #region Static Methods

        /// <summary>
        /// Gets a message box button localizer for the specified service.
        /// </summary>
        /// <param name="service">The service to find the localizer for.</param>
        /// <returns>The message box button localizer.</returns>
        protected static IMessageBoxButtonLocalizer GetLocalizer(DialogServiceBase service)
        {
            return service as IMessageBoxButtonLocalizer ?? new DefaultMessageBoxButtonLocalizer();
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the Closing event for the dialog.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event parameters.</param>
        protected void OnDialogWindowClosing(object sender, CancelEventArgs e)
        {
            var window = (DXDialogWindow)sender;
            DocumentViewModelHelper.OnClose(GetViewModel(window), e);
        }

        /// <summary>
        /// Handles the Closes event for the dialog.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event parameters.</param>
        protected virtual void OnDialogWindowClosed(object sender, System.EventArgs e)
        {
            var window = (DXDialogWindow)sender;
            DocumentViewModelHelper.OnDestroy(GetViewModel(window));
        }

        #endregion


        #region IMessageBoxButtonLocalizer

        string IMessageBoxButtonLocalizer.Localize(MessageBoxResult button)
        {
            return new DXDialogWindowMessageBoxButtonLocalizer().Localize(button);
        }

        #endregion


        #region IDocumentOwner

        void IDocumentOwner.Close(IDocumentContent documentContent, bool force)
        {
            var documentWindow = GetWindows().FirstOrDefault(w => ViewHelper.GetViewModelFromView(w.Content) == documentContent);
            if (documentWindow == null)
                return;

            if (force)
                documentWindow.Closing -= OnDialogWindowClosing;

            documentWindow.Close();
        }

        #endregion
    }
}
