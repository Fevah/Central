using System;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.Facade.Admin;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Document
{
    public class PanelListViewModel : ListViewModelBase<Panel>
    {
        #region Private Fields

        private readonly IAdminFacade _adminFacade;

        #endregion


        #region Constructors

        public PanelListViewModel()
        {
        }

        public PanelListViewModel(IAdminFacade adminFacade)
            : this()
        {
            // Store services
            _adminFacade = adminFacade;
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the AdminFacade
                ConnectToFacade(_adminFacade);

                // Initialize the data source
                ItemsSource = _adminFacade.CreateInstantFeedbackSource<Panel>();
            });
        }

        #endregion
    }
}
