using System.Windows.Controls;

namespace TIG.TotalLink.Client.Module.Admin.View.Core
{
    public partial class UploaderView : UserControl
    {
        #region Constructors

        public UploaderView()
        {
            InitializeComponent();
        }

        #endregion


        #region Event Handlers

        private void UploaderViewControl_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            //// Attempt to get the DataContext as an ISupportLayoutData
            //var supportLayout = DataContext as ISupportLayoutData;
            //if (supportLayout == null)
            //    return;

            //// Initialize viewmodel delegates
            //supportLayout.GetLayout = GridControl.GetLayout;
            //supportLayout.SetLayout = GridControl.SetLayout;
        }

        #endregion
    }
}
