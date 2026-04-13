using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Autofac;
using DevExpress.Mvvm;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Core.Command;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Core.Message;
using TIG.TotalLink.Client.Editor.Extension;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Message;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Sale.ViewModel.DocumentModel;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Options;
using TIG.TotalLink.Shared.Contract.Sale;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.DataModel.Inventory;
using TIG.TotalLink.Shared.DataModel.Sale;
using TIG.TotalLink.Shared.Facade.Core.Helper;
using TIG.TotalLink.Shared.Facade.Sale;

namespace TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.SalesOrder
{
    [SendsDocumentMessage(typeof(AppendLogMessage))]
    public sealed class SalesOrderListViewModel : ListViewModelBase<Shared.DataModel.Sale.SalesOrder>
    {
        #region Private Fields

        private readonly ISaleFacade _saleFacade;
        private readonly List<BinLocation> _selectedBins = new List<BinLocation>();
        private readonly List<PhysicalStockType> _selectedStockTypes = new List<PhysicalStockType>();
        private string _releaseStatus;
        private bool _isReleaseActive;
        private int _totalReleaseCount;
        private int _completedReleaseCount;
        private CancellationTokenSource _releaseCancellation;
        private bool _isReleaseCancelling;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor.
        /// </summary>
        public SalesOrderListViewModel()
        {
        }

        /// <summary>
        /// Constructor with Sale facade.
        /// </summary>
        /// <param name="saleFacade">Sale facade for invoke service.</param>
        public SalesOrderListViewModel(ISaleFacade saleFacade)
        {
            // Store services.
            _saleFacade = saleFacade;

            // Initialize commands
            ViewCommand = new DelegateCommand(OnViewExecute, OnViewCanExecute);
            ReleaseCommand = new AsyncCommandEx(OnReleaseExecuteAsync, OnReleaseCanExecute);
            AddCommand = new DelegateCommand(OnAddExecute, OnAddCanExecute);
            CancelReleaseCommand = new DelegateCommand(OnCancelReleaseExecute, OnCancelReleaseCanExecute);
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to view the selected sales orders.
        /// </summary>
        [WidgetCommand("View", "Sales Order", RibbonItemType.ButtonItem, "View full details for the selected Sales Orders.")]
        public ICommand ViewCommand { get; private set; }

        /// <summary>
        /// Command to release the selected sales orders.
        /// </summary>
        [WidgetCommand("Release", "Sales Order", RibbonItemType.ButtonItem, "Release the selected Sales Orders.")]
        public ICommand ReleaseCommand { get; private set; }

        /// <summary>
        /// Command to cancel the current release.
        /// </summary>
        public ICommand CancelReleaseCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// A string describing the current status of the release.
        /// </summary>
        public string ReleaseStatus
        {
            get { return _releaseStatus; }
            private set { SetProperty(ref _releaseStatus, value, () => ReleaseStatus); }
        }

        /// <summary>
        /// Indicates whether a release is currently in progress or not.
        /// </summary>
        public bool IsReleaseActive
        {
            get { return _isReleaseActive; }
            private set { SetProperty(ref _isReleaseActive, value, () => IsReleaseActive); }
        }

        /// <summary>
        /// The total number of rows that will be released.
        /// </summary>
        public int TotalReleaseCount
        {
            get { return _totalReleaseCount; }
            private set { SetProperty(ref _totalReleaseCount, value, () => TotalReleaseCount); }
        }

        /// <summary>
        /// The number of rows that have been released so far.
        /// </summary>
        public int CompletedReleaseCount
        {
            get { return _completedReleaseCount; }
            private set { SetProperty(ref _completedReleaseCount, value, () => CompletedReleaseCount); }
        }

        /// <summary>
        /// Indicates whether a release is currently being cancelled.
        /// </summary>
        public bool IsReleaseCancelling
        {
            get { return _isReleaseCancelling; }
            private set { SetProperty(ref _isReleaseCancelling, value, () => IsReleaseCancelling); }
        }

        #endregion


        #region Private Properties

        /// <summary>
        /// Indicates if an sales order command can be executed, based on whether any related operations are in progress.
        /// </summary>
        private bool CanExecuteSalesOrderCommand
        {
            get
            {
                return !(
                    ((AsyncCommandEx)ReleaseCommand).IsExecuting
                );
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Asyncronously releases SalesOrders.
        /// </summary>
        /// <param name="options">The options for the release.</param>
        /// <param name="cancellationToken">A token for cancellation.</param>
        private async Task ReleaseSalesOrdersAsync(ReleaseSalesOrderOptionsViewModel options, CancellationToken cancellationToken)
        {
            LogStart();

            ReleaseStatus = "Estimating time remaining...";
            TotalReleaseCount = options.SalesOrders.Count;

            var successCount = 0;
            var failCount = 0;
            var itemCount = 0;
            var releaseStartTime = DateTime.Now;
            var totalDuration = new TimeSpan();
            var binLocationOids = options.BinLocations.Select(b => b.Oid).ToArray();
            var physicalStockTypeOids = options.PhysicalStockTypes.Select(p => p.Oid).ToArray();
            Guid? salesOrderReleaseOid = null;
            var changes = new List<EntityChange>();

            foreach (var salesOrder in options.SalesOrders)
            {
                // Abort if cancellation has been requested
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    // Prepare release parameters and call the server to perform the release
                    var releaseParameters = new ReleaseSalesOrderParameters(salesOrder.Oid, binLocationOids, physicalStockTypeOids, salesOrderReleaseOid)
                    {
                        AllowPartialDelivery = options.AllowPartialDelivery
                    };
                    var releaseResult = await _saleFacade.ReleaseSalesOrderAsync(releaseParameters).ConfigureAwait(false);

                    // If this was the first release, store the SalesOrderReleaseOid
                    if (!salesOrderReleaseOid.HasValue)
                        salesOrderReleaseOid = releaseResult.SalesOrderReleaseOid;

                    // Store changes se we can send notifications at the end
                    changes.AddRange(releaseResult.Changes);

                    // Increment the itemCount
                    itemCount += releaseResult.TotalQuantityReleased;

                    // Increment the successCount or failCount based on whether any items were successfully released
                    if (releaseResult.TotalQuantityReleased > 0)
                        successCount++;
                    else
                        failCount++;
                }
                catch (Exception ex)
                {
                    // Increment the failCount
                    failCount++;

                    // Log the error
                    var serviceException = new ServiceExceptionHelper(ex);
                    LogMessage(string.Format("ERROR: Sales Order {0}\r\n{1}", salesOrder, serviceException.Message));
                }

                // Calculate the time this release took to execute and add it to the total time
                var releaseEndTime = DateTime.Now;
                var releaseDuration = releaseEndTime - releaseStartTime;
                releaseStartTime = releaseEndTime;
                totalDuration += releaseDuration;

                // Update release counts
                CompletedReleaseCount++;

                // Calculate and display the time remaining
                var averageReleaseTicks = Math.Round((double)totalDuration.Ticks / (double)CompletedReleaseCount);
                var remainingDuration = new TimeSpan((long)Math.Round((double)(TotalReleaseCount - CompletedReleaseCount) * averageReleaseTicks));
                ReleaseStatus = string.Format("About {0} remaining", remainingDuration.Format());
            }

            // The server has made changes that the client is not aware of, so we have to force the cache to refresh the changed types
            var changesArray = changes.ToArray();
            _saleFacade.NotifyDirtyTypes(changesArray);

            // Notify other widgets of the changed entities
            EntityChangedMessage.Send(this, changesArray);

            // Log the number of sales orders released
            LogMessage(string.Format("Released {0:N0} {1} from {2:N0} {3} in {4}.", itemCount, itemCount.Pluralize("item"), CompletedReleaseCount, CompletedReleaseCount.Pluralize("Sales Order"), totalDuration.Format()));

            // Log the count of successful and failed releases
            LogMessage(string.Format("Successful = {0:N0}", successCount));
            LogMessage(string.Format("Failed = {0:N0}", failCount));

            // Send a SelectedItemsChangeMessage to display the release results
            if (salesOrderReleaseOid.HasValue)
            {
                Application.Current.Dispatcher.Invoke(async () =>
                {
                    var salesOrderRelease = (await _saleFacade.ExecuteQueryAsync(uow =>
                        uow.Query<SalesOrderRelease>().Where(s => s.Oid == salesOrderReleaseOid.Value)
                        ).ConfigureAwait(false)).FirstOrDefault();
                    SendDocumentMessage(new SelectedItemsChangedMessage(this, new[] { salesOrderRelease }));
                });
            }

            // If cancellation was requested, log it and exit
            if (cancellationToken.IsCancellationRequested)
            {
                LogCancelled();
                return;
            }

            // Log success if at least one row was successful
            if (successCount > 0)
            {
                LogSuccess();
                return;
            }

            LogFail();
        }

        /// <summary>
        /// Sends a message to the log widget.
        /// </summary>
        /// <param name="message">The message to send.</param>
        private void LogMessage(string message)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                SendDocumentMessage(new AppendLogMessage(this, message))
            ));
        }

        /// <summary>
        /// Sends a message to the log widget to indicate that the release has started.
        /// </summary>
        private void LogStart()
        {
            CompletedReleaseCount = 0;
            TotalReleaseCount = 0;
            IsReleaseActive = true;
            ReleaseStatus = "Preparing...";

            LogMessage("*** RELEASE START ***");
        }

        /// <summary>
        /// Sends a message to the log widget to indicate that the release was successful.
        /// </summary>
        private void LogSuccess()
        {
            IsReleaseActive = false;
            CompletedReleaseCount = 0;
            TotalReleaseCount = 0;

            LogMessage("*** RELEASE SUCCESSFUL ***");
        }

        /// <summary>
        /// Sends a message to the log widget to indicate that the release has failed.
        /// </summary>
        private void LogFail()
        {
            IsReleaseActive = false;
            CompletedReleaseCount = 0;
            TotalReleaseCount = 0;

            LogMessage("*** RELEASE FAILED ***");
        }

        /// <summary>
        /// Sends a message to the log widget to indicate that the release was cancelled.
        /// </summary>
        private void LogCancelled()
        {
            IsReleaseActive = false;
            IsReleaseCancelling = false;
            CompletedReleaseCount = 0;
            TotalReleaseCount = 0;

            LogMessage("*** RELEASE CANCELLED ***");
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the ViewCommand.
        /// </summary>
        private void OnViewExecute()
        {
            string salesOrderString = null;
            try
            {
                foreach (var salesOrder in SelectedItems)
                {
                    salesOrderString = salesOrder.ToString();
                    var salesOrderViewModel = AutofacViewLocator.Default.Resolve<SalesOrderViewModel>(
                        new TypedParameter(typeof(Shared.DataModel.Sale.SalesOrder), salesOrder)
                    );
                    ShowDocumentMessage.Send(this, "Sales Order", salesOrderViewModel);
                }
            }
            catch (Exception ex)
            {
                MessageBoxService.Show(string.Format("Failed to view Sales Order {0}!\r\n\r\n{1}", salesOrderString, ex.Message), "View Sales Order", MessageBoxButton.OK, MessageBoxImage.Error);
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
        /// Execute method for the ReleaseCommand.
        /// </summary>
        private async Task OnReleaseExecuteAsync()
        {
            // Collect a list of selected SalesOrders which still have items to release
            var unreleasedSalesOrders = SelectedItems.Where(s => !s.Status.IsCompleted && s.TotalQuantityReleased < s.TotalQuantity - s.TotalQuantityCancelled).ToList();

            // Create an object for the release options
            var options = new ReleaseSalesOrderOptionsViewModel()
            {
                SalesOrders = new ObservableCollection<Shared.DataModel.Sale.SalesOrder>(unreleasedSalesOrders),
                BinLocations = new ObservableCollection<BinLocation>(_selectedBins),
                PhysicalStockTypes = new ObservableCollection<PhysicalStockType>(_selectedStockTypes)
            };

            // Display a dialog to confirm the release
            if (!DetailDialogService.ShowDialog(options, "Release Sales Orders"))
                return;

            // Create a cancellation token
            _releaseCancellation = new CancellationTokenSource();
            var cancellationToken = _releaseCancellation.Token;

            // Perform the release
            try
            {
                await ReleaseSalesOrdersAsync(options, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation errors
            }

            // Clear the cancellation token
            _releaseCancellation = null;
        }

        /// <summary>
        /// CanExecute method for the ReleaseCommand.
        /// </summary>
        private bool OnReleaseCanExecute()
        {
            if (!CanExecuteSalesOrderCommand)
                return false;

            var unreleasedSalesOrders = SelectedItems.Where(s => !s.Status.IsCompleted && s.TotalQuantityReleased < s.TotalQuantity - s.TotalQuantityCancelled).ToList();
            return unreleasedSalesOrders.Count > 0 && _selectedBins.Count > 0 && _selectedStockTypes.Count > 0;
        }

        /// <summary>
        /// Execute method for the AddCommand.
        /// </summary>
        private void OnAddExecute()
        {
            var salesOrderViewModel = AutofacViewLocator.Default.Resolve<SalesOrderViewModel>(
                new TypedParameter(typeof(bool), true)
            );
            ShowDocumentMessage.Send(this, "Sales Order", salesOrderViewModel);
        }

        /// <summary>
        /// Execute method for the CancelReleaseCommand.
        /// </summary>
        private void OnCancelReleaseExecute()
        {
            if (MessageBoxService.Show(
                "Warning: If you cancel the release, sales orders that have already been processed will not be rolled back!\r\n\r\nAre you sure?",
                "Cancel Release", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                return;

            IsReleaseCancelling = true;
            ReleaseStatus = "Cancelling...";
            _releaseCancellation.Cancel();
        }

        /// <summary>
        /// CanExecute method for the CancelReleaseCommand.
        /// </summary>
        private bool OnCancelReleaseCanExecute()
        {
            return (IsReleaseActive && !IsReleaseCancelling && _releaseCancellation != null);
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
                ItemsSource = _saleFacade.CreateInstantFeedbackSource<Shared.DataModel.Sale.SalesOrder>();
            });
        }

        protected override void OnSelectedItemsChangedMessage(SelectedItemsChangedMessage message)
        {
            base.OnSelectedItemsChangedMessage(message);

            // Process the selected items based on the primary type
            var selectedType = message.GetPrimaryType();
            TypeSwitch.On(selectedType)
                .Case<BinLocation>(() =>
                {
                    // Process selected BinLocations
                    var selectedBinLocations = message.GetEntitiesOfType<BinLocation>();
                    selectedBinLocations.SyncTo(_selectedBins);
                })
                .Case<PhysicalStockType>(() =>
                {
                    // Process selected PhysicalStockTypes
                    var selectedPhysicalStockTypes = message.GetEntitiesOfType<PhysicalStockType>();
                    selectedPhysicalStockTypes.SyncTo(_selectedStockTypes);
                });
        }

        #endregion
    }
}
