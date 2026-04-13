using System.Windows.Controls;
using System.Windows.Input;
using DevExpress.Xpf.Core.Native;

namespace TIG.TotalLink.Client.Core.View
{
    public partial class SplashScreenView : UserControl
    {
        #region Private Fields

        private System.Windows.Window _window;

        #endregion


        #region Constructors

        public SplashScreenView()
        {
            InitializeComponent();
        }

        #endregion


        #region Private Properties

        /// <summary>
        /// Finds and returns the parent Window.
        /// </summary>
        private System.Windows.Window Window
        {
            get
            {
                if (_window == null)
                    _window = LayoutHelper.FindParentObject<System.Windows.Window>(this);

                return _window;
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the MouseDown event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                Window.DragMove();
        }

        #endregion
    }
}
