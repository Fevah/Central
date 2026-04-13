using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Crm.ViewModel.Widget.Contact;

namespace TIG.TotalLink.Client.Module.Crm.View.Widget.Contact
{
    [Widget("Branch List", "Contact", "A list of Branch Contacts.  (Contains Contacts of type Branch only.)")]
    public partial class BranchListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public BranchListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public BranchListView(BranchListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
