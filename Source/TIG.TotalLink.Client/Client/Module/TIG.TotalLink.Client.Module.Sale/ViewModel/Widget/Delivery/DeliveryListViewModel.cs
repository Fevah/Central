using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autofac;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Message;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Sale.ViewModel.DocumentModel;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;
using TIG.TotalLink.Shared.Facade.Sale;

namespace TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Delivery
{
    public class DeliveryListViewModel : ListViewModelBase<Shared.DataModel.Sale.Delivery>
    {
        #region Private Fields

        private readonly ISaleFacade _saleFacade;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor.
        /// </summary>
        public DeliveryListViewModel()
        {
        }

        /// <summary>
        /// Constructor with Sale facade.
        /// </summary>
        /// <param name="saleFacade">Sale facade for invoke service.</param>
        public DeliveryListViewModel(ISaleFacade saleFacade)
        {
            // Store services.
            _saleFacade = saleFacade;

            // Initialize commands
            ReleaseCommand = new DelegateCommand(OnReleaseExecute, OnReleaseCanExecute);
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to release the selected deliveries.
        /// </summary>
        [WidgetCommand("Release", "Delivery", RibbonItemType.ButtonItem, "Release the selected Deliveries.")]
        public ICommand ReleaseCommand { get; private set; }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the ReleaseCommand.
        /// </summary>
        private void OnReleaseExecute()
        {
            var deliveries = SelectedItems.Where(d => d.Status.CanBePicked || d.Status.CanBeDispatched).ToList();

            try
            {
                var deliveryReleaseViewModel = AutofacViewLocator.Default.Resolve<DeliveryReleaseViewModel>(
                    new TypedParameter(typeof(List<>).MakeGenericType(typeof(Shared.DataModel.Sale.Delivery)), deliveries)
                );
                ShowDocumentMessage.Send(this, "Delivery Release", deliveryReleaseViewModel);
            }
            catch (Exception ex)
            {
                MessageBoxService.Show(string.Format("Failed to release Deliveries!\r\n\r\n{0}", ex.Message), "Release Deliveries", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// CanExecute method for the ReleaseCommand.
        /// </summary>
        private bool OnReleaseCanExecute()
        {
            return CanExecuteWidgetCommand && SelectedItems.Any(d => d.Status.CanBePicked || d.Status.CanBeDispatched);
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
                ItemsSource = _saleFacade.CreateInstantFeedbackSource<Shared.DataModel.Sale.Delivery>();
            });
        }

        #endregion
    }
}
