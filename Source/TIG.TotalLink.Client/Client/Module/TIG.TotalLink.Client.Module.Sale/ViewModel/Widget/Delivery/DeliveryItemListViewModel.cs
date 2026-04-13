using System;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.Facade.Sale;

namespace TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Delivery
{
    public class DeliveryItemListViewModel : ListViewModelBase<Shared.DataModel.Sale.DeliveryItem>
    {
        #region Private Fields

        private readonly ISaleFacade _saleFacade;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor.
        /// </summary>
        public DeliveryItemListViewModel()
        {
        }

        /// <summary>
        /// Constructor with Sale facade.
        /// </summary>
        /// <param name="saleFacade">Sale facade for invoke service.</param>
        public DeliveryItemListViewModel(ISaleFacade saleFacade)
        {
            // Store services.
            _saleFacade = saleFacade;
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the SaleFacade
                ConnectToFacade(_saleFacade);

                // Initialize the data source
                ItemsSource = _saleFacade.CreateInstantFeedbackSource<Shared.DataModel.Sale.DeliveryItem>();
            });
        }

        #endregion
    }
}
