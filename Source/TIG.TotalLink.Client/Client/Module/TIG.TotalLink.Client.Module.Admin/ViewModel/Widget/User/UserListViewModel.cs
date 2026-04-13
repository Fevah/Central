using System;
using System.Linq;
using System.Threading.Tasks;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;
using TIG.TotalLink.Shared.Facade.Admin;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.User
{
    public class UserListViewModel : ListViewModelBase<Shared.DataModel.Admin.User>
    {
        #region Private Fields

        private readonly IAdminFacade _adminFacade;

        #endregion


        #region Constructors

        public UserListViewModel()
        {
        }

        public UserListViewModel(IAdminFacade adminFacade)
            : this()
        {
            // Store services
            _adminFacade = adminFacade;
        }

        #endregion


        #region Overrides

        protected override async Task OnAddExecuteAsync()
        {
            await _adminFacade.ExecuteUnitOfWorkAsync(uow =>
            {
                uow.StartUiTracking(this);

                // Create a new user
                var user = new Shared.DataModel.Admin.User(uow)
                {
                    UserType = UserType.TotalLink
                };

                // If UseAddDialog = true, show a dialog to configure the new item
                if (UseAddDialog)
                {
                    return DetailDialogService.ShowDialog(DetailEditMode.Add, user);
                }

                // If UseAddDialog = false, save the item immediately
                return true;
            });
        }

        protected override bool OnDeleteCanExecute()
        {
            // Don't allow delete if any System users are selected
            if (SelectedItems.Any(u => u.UserType == UserType.System))
                return false;

            return base.OnDeleteCanExecute();
        }

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the AdminFacade
                ConnectToFacade(_adminFacade);

                // Initialize the data source
                ItemsSource = _adminFacade.CreateInstantFeedbackSource<Shared.DataModel.Admin.User>();
            });
        }

        #endregion
    }
}
