using System;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.DataModel.Inventory;
using TIG.TotalLink.Shared.Facade.Inventory;

namespace TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget
{
    public class PhysicalStockTypeListViewModel : ListViewModelBase<PhysicalStockType>
    {
        #region Private Fields

        private readonly IInventoryFacade _inventoryFacade;

        #endregion


        #region Constructors

        public PhysicalStockTypeListViewModel()
        {
        }

        public PhysicalStockTypeListViewModel(IInventoryFacade inventoryFacade)
        {
            // Store services.
            _inventoryFacade = inventoryFacade;
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the InventoryFacade
                ConnectToFacade(_inventoryFacade);

                // Initialize the data source
                ItemsSource = _inventoryFacade.CreateInstantFeedbackSource<PhysicalStockType>();
            });
        }

        #endregion
    }
}