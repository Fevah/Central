using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Location;

namespace TIG.TotalLink.Client.Module.Admin.View.Widget.Location
{
    [Widget("Country List", "Location", "A list of Countries.")]
    public partial class CountryListView
    {
        #region Constructors

        public CountryListView()
        {
            InitializeComponent();
        }

        public CountryListView(CountryListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
