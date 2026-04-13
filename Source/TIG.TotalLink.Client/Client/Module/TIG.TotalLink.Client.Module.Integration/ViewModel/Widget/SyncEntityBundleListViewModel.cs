using System;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.DataModel.Integration;
using TIG.TotalLink.Shared.Facade.Integration;

namespace TIG.TotalLink.Client.Module.Integration.ViewModel.Widget
{
    public class SyncEntityBundleListViewModel : ListViewModelBase<SyncEntityBundle>
    {
        #region Private Fields

        private readonly IIntegrationFacade _integrationFacade;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor.
        /// </summary>
        public SyncEntityBundleListViewModel() { }

        /// <summary>
        /// Constructor with integration facade.
        /// </summary>
        /// <param name="integrationFacade">Integration facade for invoke service.</param>
        public SyncEntityBundleListViewModel(IIntegrationFacade integrationFacade)
        {
            // Store services.
            _integrationFacade = integrationFacade;
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the TestFacade
                ConnectToFacade(_integrationFacade);

                // Initialize the data source
                ItemsSource = _integrationFacade.CreateInstantFeedbackSource<SyncEntityBundle>();
            });
        }

        #endregion
    }
}
