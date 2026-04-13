using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Crm.ViewModel.Widget.Contact;

namespace TIG.TotalLink.Client.Module.Crm.View.Widget.Contact
{
    [Widget("Contact List", "Contact", "A list of Contacts.  (Contains Contacts of all types.)")]
    public partial class ContactListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ContactListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public ContactListView(ContactListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
