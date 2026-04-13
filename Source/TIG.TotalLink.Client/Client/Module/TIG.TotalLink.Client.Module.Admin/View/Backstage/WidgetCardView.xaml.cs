using System.Windows.Controls;
using DevExpress.Xpf.Grid;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Backstage;

namespace TIG.TotalLink.Client.Module.Admin.View.Backstage
{
    /// <summary>
    /// Interaction logic for WidgetCardView.xaml
    /// </summary>
    public partial class WidgetCardView : UserControl
    {
        public WidgetCardView()
        {
            InitializeComponent();
        }

        public WidgetCardView(WidgetCardViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }


        /// <summary>
        /// Handles the GridControl.CustomColumnSort event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void GridControl_CustomColumnSort(object sender, CustomColumnSortEventArgs e)
        {
            // Force the Global category to the top
            if (e.Column.FieldName == "Category" && (string)e.Value1 == "Global" || (string)e.Value2 == "Global")
            {
                if ((string)e.Value1 == "Global")
                    e.Result = -1;
                else
                    e.Result = 1;
                e.Handled = true;
            }
        }
    }
}
