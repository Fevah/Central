using System;
using System.Windows.Input;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.DataModel.Inventory;
using TIG.TotalLink.Shared.Facade.Inventory;

namespace TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget
{
    public class PhysicalStockListViewModel : ListViewModelBase<PhysicalStock>
    {
        #region Private Fields

        private readonly IInventoryFacade _inventoryFacade;

        #endregion


        #region Constructors

        public PhysicalStockListViewModel()
        {
        }

        public PhysicalStockListViewModel(IInventoryFacade inventoryFacade)
        {
            // Store services.
            _inventoryFacade = inventoryFacade;
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
                // Attempt to connect to the InventoryFacade
                ConnectToFacade(_inventoryFacade);

                // Initialize the data source
                ItemsSource = _inventoryFacade.CreateInstantFeedbackSource<PhysicalStock>();
            });
        }

        #endregion
    }
}