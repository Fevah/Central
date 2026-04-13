using System;
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

namespace TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Quote
{
    public sealed class QuoteListViewModel : ListViewModelBase<Shared.DataModel.Sale.Quote>
    {
        #region Private Fields

        private readonly ISaleFacade _saleFacade;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor.
        /// </summary>
        public QuoteListViewModel()
        {
        }

        /// <summary>
        /// Constructor with Sale facade.
        /// </summary>
        /// <param name="saleFacade">Sale facade for invoke service.</param>
        public QuoteListViewModel(ISaleFacade saleFacade)
        {
            // Store services.
            _saleFacade = saleFacade;

            // Initialize commands
            ViewCommand = new DelegateCommand(OnViewExecute, OnViewCanExecute);
            AddCommand = new DelegateCommand(OnAddExecute, OnAddCanExecute);
            CreateSalesOrderCommand = new DelegateCommand(OnCreateSalesOrderExecute, OnCreateSalesOrderCanExecute);
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to view the selected quotes.
        /// </summary>
        [WidgetCommand("View", "Quote", RibbonItemType.ButtonItem, "View full details for the selected Quotes.")]
        public ICommand ViewCommand { get; private set; }

        /// <summary>
        /// Command to create sales orders from the selected quotes.
        /// </summary>
        [WidgetCommand("Create Sales Order", "Sales Order", RibbonItemType.ButtonItem, "Create new Sales Orders from the selected Quotes.")]
        public ICommand CreateSalesOrderCommand { get; private set; }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the ViewCommand.
        /// </summary>
        private void OnViewExecute()
        {
            string quoteString = null;
            try
            {
                foreach (var quote in SelectedItems)
                {
                    quoteString = quote.ToString();
                    var quoteViewModel = AutofacViewLocator.Default.Resolve<QuoteViewModel>(
                        new TypedParameter(typeof(Shared.DataModel.Sale.Quote), quote)
                    );
                    ShowDocumentMessage.Send(this, "Quote", quoteViewModel);
                }
            }
            catch (Exception ex)
            {
                MessageBoxService.Show(string.Format("Failed to view Sales Order {0}!\r\n\r\n{1}", quoteString, ex.Message), "View Quote", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// CanExecute method for the ViewCommand.
        /// </summary>
        private bool OnViewCanExecute()
        {
            return CanExecuteWidgetCommand && SelectedItems.Count > 0;
        }

        /// <summary>
        /// Execute method for the CreateSalesOrderCommand.
        /// </summary>
        private void OnCreateSalesOrderExecute()
        {
            string quoteString = null;
            try
            {
                foreach (var quote in SelectedItems)
                {
                    quoteString = quote.ToString();
                    var salesOrderViewModel = AutofacViewLocator.Default.Resolve<SalesOrderViewModel>(
                        new TypedParameter(typeof(Shared.DataModel.Sale.Quote), quote)
                    );
                    ShowDocumentMessage.Send(this, "Sales Order", salesOrderViewModel);
                }
            }
            catch (Exception ex)
            {
                MessageBoxService.Show(string.Format("Failed to create sales order from quote {0}!\r\n\r\n{1}", quoteString, ex.Message), "Create Sales Order", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// CanExecute method for the CreateSalesOrderCommand.
        /// </summary>
        private bool OnCreateSalesOrderCanExecute()
        {
            return CanExecuteWidgetCommand && SelectedItems.Count > 0;
        }

        /// <summary>
        /// Execute method for the AddCommand.
        /// </summary>
        private void OnAddExecute()
        {
            var quoteViewModel = AutofacViewLocator.Default.Resolve<QuoteViewModel>(
                new TypedParameter(typeof(bool), true)
            );
            ShowDocumentMessage.Send(this, "Quote", quoteViewModel);
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
                ItemsSource = _saleFacade.CreateInstantFeedbackSource<Shared.DataModel.Sale.Quote>();
            });
        }

        #endregion
    }
}
