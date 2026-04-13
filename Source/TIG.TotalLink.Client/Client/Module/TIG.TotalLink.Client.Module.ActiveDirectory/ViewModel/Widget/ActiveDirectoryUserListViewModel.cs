using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using TIG.TotalLink.Client.Core.Command;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.DataModel.ActiveDirectory;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;
using TIG.TotalLink.Shared.Facade.ActiveDirectory;
using TIG.TotalLink.Shared.Facade.Admin;

namespace TIG.TotalLink.Client.Module.ActiveDirectory.ViewModel.Widget
{
    public class ActiveDirectoryUserListViewModel : ListViewModelBase<ActiveDirectoryUser>
    {
        #region Private Fields

        private readonly IActiveDirectoryFacade _activeDirectoryFacade;
        private readonly IAdminFacade _adminFacade;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor.
        /// </summary>
        public ActiveDirectoryUserListViewModel() { }

        /// <summary>
        /// Constructor with facades.
        /// </summary>
        /// <param name="activeDirectoryFacade">Active directory facade for invoke service.</param>
        /// <param name="adminFacade">Admin facade for invoke service.</param>
        public ActiveDirectoryUserListViewModel(IActiveDirectoryFacade activeDirectoryFacade, IAdminFacade adminFacade)
        {
            // Store services.
            _activeDirectoryFacade = activeDirectoryFacade;
            _adminFacade = adminFacade;

            // Initialize commands
            AddUsersFromAdCommand = new AsyncCommandEx(OnAddUsersFromAdExecuteAsync, OnAddUsersFromAdCanExecute);
        }

        #endregion


        #region Overrides

        /// <summary>
        /// Override to hide the AddCommand.
        /// </summary>
        public override ICommand AddCommand { get { return null; } }

        /// <summary>
        /// Override to hide the DeleteCommand.
        /// </summary>
        public override ICommand DeleteCommand { get { return null; } }

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the TestFacade
                ConnectToFacade(_activeDirectoryFacade);

                // Initialize the data source
                ItemsSource = _activeDirectoryFacade.CreateInstantFeedbackSource<ActiveDirectoryUser>();
            });
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to add an active directory user to the main user list.
        /// </summary>
        [WidgetCommand("Add Users From AD", "Active Directory", RibbonItemType.ButtonItem, "Add the selected Active Directory users to the main system users.")]
        public virtual ICommand AddUsersFromAdCommand { get; private set; }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the Execute method for the AddUsers command.
        /// </summary>
        /// <returns>A Task.</returns>
        private async Task OnAddUsersFromAdExecuteAsync()
        {
            // Create a new User for each ActiveDirectoryUser that is selected
            await _adminFacade.ExecuteUnitOfWorkAsync(uow =>
            {
                uow.StartUiTracking(this, true, true, true);
                foreach (var selectedItem in SelectedItems.ToList())
                {
                    new Shared.DataModel.Admin.User(uow)
                    {
                        UserName = selectedItem.LoginName,
                        DisplayName = selectedItem.DisplayName,
                        UserType = UserType.ActiveDirectory,
                        ActiveDirectoryId = selectedItem.Oid
                    };
                }
            });
        }

        /// <summary>
        /// Handles the CanExecute method for the AddUsers command.
        /// </summary>
        /// <returns>True if any users are selected.</returns>
        private bool OnAddUsersFromAdCanExecute()
        {
            return SelectedItems.Any();
        }

        #endregion


        #region Overrides

        protected override bool OnAddCanExecute()
        {
            return false;
        }

        protected override bool OnDeleteCanExecute()
        {
            return false;
        }

        #endregion
    }
}