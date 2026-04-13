using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Autofac;
using DevExpress.Mvvm;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Extension;
using TIG.TotalLink.Client.Module.Admin.Message;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Sale.ViewModel.DocumentModel;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;
using TIG.TotalLink.Shared.DataModel.Sale;
using TIG.TotalLink.Shared.Facade.Sale;

namespace TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Enquiry
{
    public class EnquiryListViewModel : ListViewModelBase<Shared.DataModel.Sale.Enquiry>
    {
        #region Private Fields

        private readonly ISaleFacade _saleFacade;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor.
        /// </summary>
        public EnquiryListViewModel()
        {
        }

        /// <summary>
        /// Constructor with Sale facade.
        /// </summary>
        /// <param name="saleFacade">Sale facade for invoke service.</param>
        public EnquiryListViewModel(ISaleFacade saleFacade)
        {
            // Store services.
            _saleFacade = saleFacade;

            // Initialize commands
            CreateQuoteCommand = new DelegateCommand(OnCreateQuoteExecute, OnCreateQuoteCanExecute);
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to create quotes from the selected enquiries.
        /// </summary>
        [WidgetCommand("Create Quote", "Quote", RibbonItemType.ButtonItem, "Create new quotes from the selected enquiries.")]
        public ICommand CreateQuoteCommand { get; private set; }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the CreateQuoteCommand.
        /// </summary>
        private void OnCreateQuoteExecute()
        {
            string enquiryString = null;
            try
            {
                foreach (var enquiry in SelectedItems.Where(e => e.Status != null && !e.Status.IsCompleted))
                {
                    enquiryString = enquiry.ToString();
                    var quoteViewModel = AutofacViewLocator.Default.Resolve<QuoteViewModel>(
                        new TypedParameter(typeof(Shared.DataModel.Sale.Enquiry), enquiry)
                    );
                    ShowDocumentMessage.Send(this, "Quote", quoteViewModel);
                }
            }
            catch (Exception ex)
            {
                MessageBoxService.Show(string.Format("Failed to create quote from enquiry {0}!\r\n\r\n{1}", enquiryString, ex.Message), "Create Quote", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// CanExecute method for the CreateQuoteCommand.
        /// </summary>
        private bool OnCreateQuoteCanExecute()
        {
            return CanExecuteWidgetCommand && SelectedItems.Any(e => e.Status != null && !e.Status.IsCompleted);
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
                ItemsSource = _saleFacade.CreateInstantFeedbackSource<Shared.DataModel.Sale.Enquiry>();
            });
        }

        protected override async Task OnAddExecuteAsync()
        {
            await _saleFacade.ExecuteUnitOfWorkAsync(uow =>
            {
                uow.StartUiTracking(this);

                // Create a new enquiry
                var enquiry = new Shared.DataModel.Sale.Enquiry(uow)
                {
                    // TODO : This should find the first EnquiryStatus based on Order instead of finding it by Name
                    Status = uow.Query<EnquiryStatus>().FirstOrDefault(s => s.Name == "Submitted")
                };
                enquiry.GenerateReferenceNumber();

                // If UseAddDialog = true, show a dialog to configure the new item
                if (UseAddDialog)
                {
                    return DetailDialogService.ShowDialog(DetailEditMode.Add, enquiry);
                }

                // If UseAddDialog = false, save the item immediately
                return true;
            });
        }

        #endregion
    }
}
