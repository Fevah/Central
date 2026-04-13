using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Location;

namespace TIG.TotalLink.Client.Module.Admin.View.Widget.Location
{
    [Widget("State List", "Location", "A list of States.")]
    public partial class StateListView
    {
        #region Constructors

        public StateListView()
        {
            InitializeComponent();
        }

        public StateListView(StateListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
