using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Crm.ViewModel.Widget.Contact;

namespace TIG.TotalLink.Client.Module.Crm.View.Widget.Contact
{
    [Widget("Business List", "Contact", "A list of Business Contacts.  (Contains Contacts of type Chain, Company and Branch.)")]
    public partial class BusinessListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public BusinessListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public BusinessListView(BusinessListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
