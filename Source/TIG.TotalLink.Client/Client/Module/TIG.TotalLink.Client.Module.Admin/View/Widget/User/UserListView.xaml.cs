using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.User;

namespace TIG.TotalLink.Client.Module.Admin.View.Widget.User
{
    [Widget("User List", "User", "A list of Users.")]
    public partial class UserListView : UserControl
    {
        #region Constructors

        public UserListView()
        {
            InitializeComponent();
        }

        public UserListView(UserListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
