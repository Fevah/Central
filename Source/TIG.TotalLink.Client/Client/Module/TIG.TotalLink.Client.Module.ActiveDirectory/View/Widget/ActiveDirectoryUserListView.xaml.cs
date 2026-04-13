using TIG.TotalLink.Client.Module.ActiveDirectory.ViewModel.Widget;
using TIG.TotalLink.Client.Module.Admin.Attribute;

namespace TIG.TotalLink.Client.Module.ActiveDirectory.View.Widget
{
    [Widget("Active Directory User List", "User", "A list of Active Directory users.")]
    public partial class ActiveDirectoryUserListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ActiveDirectoryUserListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="listViewModel">ViewModel to support UI part.</param>
        public ActiveDirectoryUserListView(ActiveDirectoryUserListViewModel listViewModel)
            : this()
        {
            DataContext = listViewModel;
        }

        #endregion
    }
}
