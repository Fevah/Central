using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Crm.ViewModel.Widget.Contact;

namespace TIG.TotalLink.Client.Module.Crm.View.Widget.Contact
{
    [Widget("Chain List", "Contact", "A list of Chain Contacts.  (Contains Contacts of type Chain only.)")]
    public partial class ChainListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ChainListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public ChainListView(ChainListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
