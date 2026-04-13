using System.Windows.Controls;
using DevExpress.Xpf.Editors;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Global;

namespace TIG.TotalLink.Client.Module.Admin.View.Widget.Global
{
    [Widget("Message Log", "Global", "Displays informational messages from other widgets.")]
    public partial class MessageLogView
    {
        #region Private Fields

        private TextBox _textBox;
        private bool _wasScrolledToEnd;

        #endregion


        #region Constructors

        /// <summary>
        /// Parameterless constructor.
        /// </summary>
        public MessageLogView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">The view model.</param>
        public MessageLogView(MessageLogViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion


        #region Private Properties

        /// <summary>
        /// Indicates if the LogTextEdit is scrolled all the way to the end.
        /// </summary>
        public bool IsLogTextScrolledToEnd
        {
            get { return _textBox.VerticalOffset + _textBox.ViewportHeight >= _textBox.ExtentHeight; }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the UserControl.Loaded event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _textBox = LogTextEdit.EditCore as TextBox;
        }

        /// <summary>
        /// Handles the LogTextEdit.EditValueChanging event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void LogTextEdit_EditValueChanging(object sender, EditValueChangingEventArgs e)
        {
            _wasScrolledToEnd = IsLogTextScrolledToEnd;
        }

        /// <summary>
        /// Handles the LogTextEdit.EditValueChanged event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void LogTextEdit_EditValueChanged(object sender, EditValueChangedEventArgs e)
        {
            if (_wasScrolledToEnd)
                _textBox.ScrollToEnd();
        }

        #endregion
    }
}
