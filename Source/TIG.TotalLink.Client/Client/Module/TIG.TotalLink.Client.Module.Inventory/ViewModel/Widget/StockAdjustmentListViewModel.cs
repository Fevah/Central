using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Core.Message;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Undo.Helper;
using TIG.TotalLink.Shared.DataModel.Inventory;
using TIG.TotalLink.Shared.Facade.Core.Helper;
using TIG.TotalLink.Shared.Facade.Inventory;

namespace TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget
{
    public class StockAdjustmentListViewModel : ListViewModelBase<StockAdjustment>
    {
        #region Private Fields

        private readonly IInventoryFacade _inventoryFacade;

        #endregion


        #region Constructors

        public StockAdjustmentListViewModel()
        {
        }

        public StockAdjustmentListViewModel(IInventoryFacade inventoryFacade)
        {
            // Store services.
            _inventoryFacade = inventoryFacade;
        }

        #endregion


        #region Overrides

        /// <summary>
        /// Override to hide the DeleteCommand.
        /// </summary>
        public override ICommand DeleteCommand { get { return null; } }

        protected override async Task OnAddExecuteAsync()
        {
            using (var uow = _inventoryFacade.CreateUnitOfWork())
            {
                // Create a temporary StockAdjustment
                // This StockAdjustment will never be saved, but it must exist in a connected UnitOfWork so that LookUps can be resolved
                var stockAdjustment = DataObjectHelper.CreateDataObject<StockAdjustment>(uow);

                // Show a dialog to configure the new item, and abort if the dialog is cancelled
                if (!DetailDialogService.ShowDialog(DetailEditMode.Add, stockAdjustment))
                    return;

                try
                {
                    // Add the StockAdjustment
                    var changes = await _inventoryFacade.AddStockAdjustmentAsync(stockAdjustment);

                    // The server has made changes that the client is not aware of, so we have to force the cache to refresh the changed types
                    _inventoryFacade.NotifyDirtyTypes(changes);

                    // Notify other widgets of the changed entities
                    EntityChangedMessage.Send(this, changes);
                }
                catch (Exception ex)
                {
                    var serviceException = new ServiceExceptionHelper(ex);
                    MessageBoxService.Show(string.Format("Add failed!\r\n\r\n{0}", serviceException.Message), "Add Stock Adjustment", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the InventoryFacade
                ConnectToFacade(_inventoryFacade);

                // Initialize the data source
                ItemsSource = _inventoryFacade.CreateInstantFeedbackSource<StockAdjustment>();
            });
        }

        #endregion
    }
}