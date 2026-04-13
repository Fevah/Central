using System;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.Facade.Inventory;

namespace TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget
{
    public class SkuListViewModel : ListViewModelBase<Shared.DataModel.Inventory.Sku>
    {
        #region Private Fields

        private readonly IInventoryFacade _inventoryFacade;

        #endregion


        #region Constructors

        public SkuListViewModel()
        {
        }

        public SkuListViewModel(IInventoryFacade inventoryFacade)
            :this()
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
                ItemsSource = _inventoryFacade.CreateInstantFeedbackSource<Shared.DataModel.Inventory.Sku>();
            });
        }

        #endregion
    }
}