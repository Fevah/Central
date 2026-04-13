using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Location;

namespace TIG.TotalLink.Client.Module.Admin.View.Widget.Location
{
    [Widget("Postcode List", "Location", "A list of Postcodes.")]
    public partial class PostcodeListView
    {
        #region Constructors

        public PostcodeListView()
        {
            InitializeComponent();
        }

        public PostcodeListView(PostcodeListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
