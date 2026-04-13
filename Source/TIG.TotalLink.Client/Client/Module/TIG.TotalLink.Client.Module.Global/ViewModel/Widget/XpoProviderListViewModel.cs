using System;
using System.Windows.Input;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.DataModel.Global;
using TIG.TotalLink.Shared.Facade.Global;

namespace TIG.TotalLink.Client.Module.Global.ViewModel.Widget
{
    public class XpoProviderListViewModel : ListViewModelBase<XpoProvider>
    {
        #region Private Fields

        private readonly IGlobalFacade _globalFacade;

        #endregion


        #region Constructors

        public XpoProviderListViewModel()
        {

        }

        public XpoProviderListViewModel(IGlobalFacade globalFacade)
            : this()
        {
            // Store services
            _globalFacade = globalFacade;
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
                // Attempt to connect to the GlobalFacade
                ConnectToFacade(_globalFacade);

                // Initialize the data source
                ItemsSource = _globalFacade.CreateInstantFeedbackSource<XpoProvider>();
            });
        }

        #endregion
    }
}
