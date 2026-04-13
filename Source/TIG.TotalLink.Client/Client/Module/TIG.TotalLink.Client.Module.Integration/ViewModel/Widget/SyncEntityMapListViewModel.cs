using System;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.DataModel.Integration;
using TIG.TotalLink.Shared.Facade.Integration;

namespace TIG.TotalLink.Client.Module.Integration.ViewModel.Widget
{
    public class SyncEntityMapListViewModel : ListViewModelBase<SyncEntityMap>
    {
        #region Private Fields

        private readonly IIntegrationFacade _integrationFacade;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor.
        /// </summary>
        public SyncEntityMapListViewModel() { }

        /// <summary>
        /// Constructor with integration facade.
        /// </summary>
        /// <param name="integrationFacade">Integration facade for invoke service.</param>
        public SyncEntityMapListViewModel(IIntegrationFacade integrationFacade)
        {
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
                ItemsSource = _integrationFacade.CreateInstantFeedbackSource<SyncEntityMap>();
            });
        }

        #endregion
    }
}