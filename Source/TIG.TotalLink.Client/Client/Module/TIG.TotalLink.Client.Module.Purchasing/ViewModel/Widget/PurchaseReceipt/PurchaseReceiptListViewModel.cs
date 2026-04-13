using System;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.Facade.Purchasing;

namespace TIG.TotalLink.Client.Module.Purchasing.ViewModel.Widget.PurchaseReceipt
{
    public class PurchaseReceiptListViewModel : ListViewModelBase<Shared.DataModel.Purchasing.PurchaseReceipt>
    {
        #region Private Fields

        private readonly IPurchasingFacade _purchasingFacade;

        #endregion


        #region Constructors

        public PurchaseReceiptListViewModel()
        {
        }

        public PurchaseReceiptListViewModel(IPurchasingFacade purchasingFacade)
        {
            // Store services.
            _purchasingFacade = purchasingFacade;
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the PurchasingFacade
                ConnectToFacade(_purchasingFacade);

                // Initialize the data source
                ItemsSource = _purchasingFacade.CreateInstantFeedbackSource<Shared.DataModel.Purchasing.PurchaseReceipt>();
            });
        }

        #endregion
    }
}