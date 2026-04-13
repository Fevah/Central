using System;
using System.Windows;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Grid;

namespace TIG.TotalLink.Client.Editor.View
{
    /// <summary>
    /// Interaction logic for IncrementingTimeEdit.xaml.
    /// </summary>
    public partial class IncrementingTimeEdit
    {
        #region Dependency Properties

        /// <summary>
        /// The edit value of the control.
        /// </summary>
        public static readonly DependencyProperty EditValueProperty = DependencyProperty.Register("EditValue", typeof(int?), typeof(IncrementingTimeEdit));

        /// <summary>
        /// The total time spent.
        /// </summary>
        public int? EditValue
        {
            get { return (int?)GetValue(EditValueProperty); }
            set { SetValue(EditValueProperty, value); }
        }

        #endregion


        #region Private Fields

        private bool _canExecute;

        #endregion


        #region Constructors

        public IncrementingTimeEdit()
        {
            InitializeComponent();
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the button click of the add button in the user control.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">RoutedEventArgs.</param>
        private void ButtonInfo_OnClick(object sender, RoutedEventArgs e)
        {
            if (!_canExecute)
            {
                // If the parent of the editor is a DataControlBase...
                var dataControl = LayoutHelper.FindParentObject<DataControlBase>(this);
                if (dataControl != null)
                {
                    // Abort opening the dialog because it is triggered by the transfer of edit mode
                    _canExecute = true;
                    return;
                }
            }

            // Pop up the dialog and add the input time to the value
            var incrementedHours = IncrementingTimeDialogService.ShowDialog();
            if (incrementedHours == null)
                return;

            EditValue += (int)Math.Truncate(incrementedHours.Value * 60m);
        }

        #endregion
    }
}