using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DevExpress.Spreadsheet;
using DevExpress.Xpf.Core;
using DevExpress.XtraSpreadsheet;

namespace TIG.TotalLink.Client.Module.Admin.View.Core
{
    public partial class ImporterView : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty ActiveWorksheetProperty = DependencyProperty.RegisterAttached(
            "ActiveWorksheet", typeof(object), typeof(ImporterView), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true, DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        /// <summary>
        /// The worksheet that is currently active in the spreadsheet control.
        /// </summary>
        public object ActiveWorksheet
        {
            get { return GetValue(ActiveWorksheetProperty); }
            set { SetValue(ActiveWorksheetProperty, value); }
        }

        #endregion


        #region Constructors

        public ImporterView()
        {
            InitializeComponent();
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the SpreadsheetControl.ActiveSheetChanged event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void SpreadsheetControl_ActiveSheetChanged(object sender, ActiveSheetChangedEventArgs e)
        {
            ActiveWorksheet = SpreadsheetControl.ActiveWorksheet;
        }

        /// <summary>
        /// Handles the SpreadsheetControl.DocumentLoaded event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void SpreadsheetControl_DocumentLoaded(object sender, System.EventArgs e)
        {
            ActiveWorksheet = SpreadsheetControl.ActiveWorksheet;
        }

        /// <summary>
        /// Handles the SpreadsheetControl.EmptyDocumentCreated event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void SpreadsheetControl_EmptyDocumentCreated(object sender, System.EventArgs e)
        {
            ActiveWorksheet = SpreadsheetControl.ActiveWorksheet;
        }

        /// <summary>
        /// Handles the SpreadsheetControl.Loaded event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void SpreadsheetControl_Loaded(object sender, RoutedEventArgs e)
        {
            ActiveWorksheet = SpreadsheetControl.ActiveWorksheet;
        }

        /// <summary>
        /// Handles the SpreadsheetControl.UnhandledException event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void SpreadsheetControl_UnhandledException(object sender, SpreadsheetUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            DXMessageBox.Show(e.Exception.Message, "Spreadsheet Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Handles the SpreadsheetControl.IsEnabledChanged event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void SpreadsheetControl_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // When the spreadsheet is re-enabled (which will happen after importing) we will create and destroy a worksheet, but make sure the Modified flag is unchanged
            // This will force the worksheet to refresh and update the display of any error messages
            if (SpreadsheetControl.IsEnabled)
            {
                // Get the active worksheet and modified state
                var activeWorksheet = ActiveWorksheet as Worksheet;
                var wasModified = SpreadsheetControl.Document.Modified;

                // Add and remove a dummy worksheet
                var newWorksheet = SpreadsheetControl.Document.Worksheets.Add();
                SpreadsheetControl.Document.Worksheets.Remove(newWorksheet);

                // Reset the active worksheet and modified state
                SpreadsheetControl.Document.Worksheets.ActiveWorksheet = activeWorksheet;
                SpreadsheetControl.Document.Modified = wasModified;
            }
        }

        #endregion
    }
}
