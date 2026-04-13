using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Crm.ViewModel.Widget.Contact;

namespace TIG.TotalLink.Client.Module.Crm.View.Widget.Contact
{
    [Widget("Company List", "Contact", "A list of Company Contacts.  (Contains Contacts of type Company only.)")]
    public partial class CompanyListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public CompanyListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public CompanyListView(CompanyListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
