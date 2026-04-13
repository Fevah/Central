using System;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.DataModel.Crm;
using TIG.TotalLink.Shared.Facade.Crm;

namespace TIG.TotalLink.Client.Module.Crm.ViewModel.Widget.Contact
{
    public class CompanyListViewModel : ListViewModelBase<Company>
    {
        #region Private Fields

        private readonly ICrmFacade _crmFacade;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor.
        /// </summary>
        public CompanyListViewModel() { }

        /// <summary>
        /// Constructor with crm facade.
        /// </summary>
        /// <param name="crmFacade">Crm facade for invoke service.</param>
        public CompanyListViewModel(ICrmFacade crmFacade)
        {
            // Store services.
            _crmFacade = crmFacade;
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Attempt to connect to the CrmFacade
                ConnectToFacade(_crmFacade);

                // Initialize the data source
                ItemsSource = _crmFacade.CreateInstantFeedbackSource<Company>();
            });
        }

        #endregion
    }
}
