using System;
using System.Threading.Tasks;
using System.Windows.Input;
using TIG.TotalLink.Client.Core.Command;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;
using TIG.TotalLink.Shared.DataModel.Crm;
using TIG.TotalLink.Shared.Facade.Crm;

namespace TIG.TotalLink.Client.Module.Crm.ViewModel.Widget.Contact
{
    public class BusinessListViewModel : ListViewModelBase<Business>
    {
        #region Private Fields

        private readonly ICrmFacade _crmFacade;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor.
        /// </summary>
        public BusinessListViewModel() { }

        /// <summary>
        /// Constructor with crm facade.
        /// </summary>
        /// <param name="crmFacade">Crm facade for invoke service.</param>
        public BusinessListViewModel(ICrmFacade crmFacade)
        {
            // Store services.
            _crmFacade = crmFacade;

            // Initialize commands
            AddChainCommand = new AsyncCommandEx(OnAddChainExecuteAsync, OnAddChainCanExecute);
            AddCompanyCommand = new AsyncCommandEx(OnAddCompanyExecuteAsync, OnAddCompanyCanExecute);
            AddBranchCommand = new AsyncCommandEx(OnAddBranchExecuteAsync, OnAddBranchCanExecute);
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to add a new Chain to the list.
        /// </summary>
        [WidgetCommand("Add Chain", "Edit", RibbonItemType.ButtonItem, "Add a new Chain.")]
        public ICommand AddChainCommand { get; private set; }

        /// <summary>
        /// Command to add a new Company to the list.
        /// </summary>
        [WidgetCommand("Add Company", "Edit", RibbonItemType.ButtonItem, "Add a new Company.")]
        public ICommand AddCompanyCommand { get; private set; }

        /// <summary>
        /// Command to add a new Branch to the list.
        /// </summary>
        [WidgetCommand("Add Branch", "Edit", RibbonItemType.ButtonItem, "Add a new Branch.")]
        public ICommand AddBranchCommand { get; private set; }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the AddChainCommand.
        /// </summary>
        protected async Task OnAddChainExecuteAsync()
        {
            await AddItemAsync<Chain>();
        }

        /// <summary>
        /// CanExecute method for the AddChainCommand.
        /// </summary>
        protected bool OnAddChainCanExecute()
        {
            return CanExecuteWidgetCommand;
        }

        /// <summary>
        /// Execute method for the AddCompanyCommand.
        /// </summary>
        protected async Task OnAddCompanyExecuteAsync()
        {
            await AddItemAsync<Company>();
        }

        /// <summary>
        /// CanExecute method for the AddCompanyCommand.
        /// </summary>
        protected bool OnAddCompanyCanExecute()
        {
            return CanExecuteWidgetCommand;
        }

        /// <summary>
        /// Execute method for the AddBranchCommand.
        /// </summary>
        protected async Task OnAddBranchExecuteAsync()
        {
            await AddItemAsync<Branch>();
        }

        /// <summary>
        /// CanExecute method for the AddBranchCommand.
        /// </summary>
        protected bool OnAddBranchCanExecute()
        {
            return CanExecuteWidgetCommand;
        }

        #endregion


        #region Overrides

        /// <summary>
        /// Indicates if a widget command can be executed, based on whether any related operations are in progress.
        /// </summary>
        public override bool CanExecuteWidgetCommand
        {
            get
            {
                if (!base.CanExecuteWidgetCommand)
                {
                    return false;
                }

                return !(
                    (AddChainCommand != null && ((AsyncCommandEx)AddChainCommand).IsExecuting)
                    || (AddCompanyCommand != null && ((AsyncCommandEx)AddCompanyCommand).IsExecuting)
                    || (AddBranchCommand != null && ((AsyncCommandEx)AddBranchCommand).IsExecuting)
                    );
            }
        }

        /// <summary>
        /// Override to hide the AddCommand.
        /// </summary>
        public override ICommand AddCommand { get { return null; } }

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the CrmFacade
                ConnectToFacade(_crmFacade);

                // Initialize the data source
                ItemsSource = _crmFacade.CreateInstantFeedbackSource<Business>();
            });
        }

        #endregion
    }
}
