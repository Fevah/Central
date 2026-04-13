using System.Windows;
using System.Windows.Data;
using DevExpress.Xpf.Core;
using TIG.TotalLink.Client.Core.AppContext;
using TIG.TotalLink.Client.Core.ViewModel;

namespace TIG.TotalLink.Client.Editor.Control
{
    /// <summary>
    /// An extended DXDialogWindow which automatically manages the window state.
    /// </summary>
    public class DXDialogWindowEx : DXDialogWindow
    {
        #region Dependency Properties

        public static readonly DependencyProperty StoredWindowStateProperty =
            DependencyProperty.Register("StoredWindowState", typeof(WindowStateViewModel), typeof(DXDialogWindowEx));

        /// <summary>
        /// The window state that is stored in user settings.
        /// </summary>
        public WindowStateViewModel StoredWindowState
        {
            get { return (WindowStateViewModel)GetValue(StoredWindowStateProperty); }
            set { SetValue(StoredWindowStateProperty, value); }
        }

        #endregion


        #region Constructors

        public DXDialogWindowEx()
        {
        }

        public DXDialogWindowEx(string windowStateKey, double defaultWidth, double defaultHeight)
        {
            InitializeWindowState(windowStateKey, defaultWidth, defaultHeight);
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Initializes the window state from user settings.
        /// </summary>
        /// <param name="windowStateKey">The key to use when saving/restoring the window state.</param>
        /// <param name="defaultWidth">The default width for the window.</param>
        /// <param name="defaultHeight">The default height for the window.</param>
        private void InitializeWindowState(string windowStateKey, double defaultWidth, double defaultHeight)
        {
            // Get the window state
            StoredWindowState = AppContextViewModel.Instance.GetWindowState(windowStateKey, defaultWidth, defaultHeight);

            // Bind the window state
            SetWindowStateBinding(WindowStateProperty, "ActualState");
            SetWindowStateBinding(LeftProperty, "ActualLeft");
            SetWindowStateBinding(TopProperty, "ActualTop");
            SetWindowStateBinding(WidthProperty, "ActualWidth");
            SetWindowStateBinding(HeightProperty, "ActualHeight");
        }

        /// <summary>
        /// Creates a binding to StoredWindowState.
        /// </summary>
        /// <param name="property">The target property.</param>
        /// <param name="path">The source path within the StoredWindowState.</param>
        private void SetWindowStateBinding(DependencyProperty property, string path)
        {
            var binding = new Binding(string.Format("StoredWindowState.{0}", path))
            {
                RelativeSource = RelativeSource.Self,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            SetBinding(property, binding);
        }

        #endregion
    }
}
