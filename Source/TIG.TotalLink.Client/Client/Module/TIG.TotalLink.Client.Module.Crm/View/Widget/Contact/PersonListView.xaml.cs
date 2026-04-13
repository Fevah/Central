using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Crm.ViewModel.Widget.Contact;

namespace TIG.TotalLink.Client.Module.Crm.View.Widget.Contact
{
    [Widget("Person List", "Contact", "A list of Person Contacts.  (Contains Contacts of type Person only.)")]
    public partial class PersonListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public PersonListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public PersonListView(PersonListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
