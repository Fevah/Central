using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Core.Message;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Message;
using TIG.TotalLink.Client.Module.Admin.MvvmService;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Global;
using TIG.TotalLink.Client.Module.Sale.ViewModel.DocumentModel.Delivery;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.Contract.Sale;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Helper;
using TIG.TotalLink.Shared.Facade.Inventory;
using TIG.TotalLink.Shared.Facade.Sale;

namespace TIG.TotalLink.Client.Module.Sale.ViewModel.DocumentModel
{
    [DocumentDataModel("Delivery Release", "Contains data for for releasing one or more Deliveries.")]
    public class DeliveryReleaseViewModel : DocumentDataModelBase
    {
        #region Private Fields

        private readonly ISaleFacade _saleFacade;
        private readonly IInventoryFacade _inventoryFacade;
        private UnitOfWork _unitOfWork;
        private DetailViewModel _detailViewModel;
        private readonly List<Shared.DataModel.Sale.Delivery> _initialDeliveries;
        private ObservableCollection<DeliveryReleaseDataViewModel> _deliveries;
        private ObservableCollection<PickItemReleaseDataViewModel> _pickItems;
        private readonly ObservableCollection<PickItemReleaseDataViewModel> _selectedPickItems = new ObservableCollection<PickItemReleaseDataViewModel>();
        private string _summary;
        private ICommand _confirmReleaseCommand;
        private ICommand _cancelReleaseCommand;
        private ICommand _clearReleaseCommand;
        private ICommand _markAllCommand;
        private ICommand _markSelectedCommand;
        private string _releaseStatus;
        private bool _isReleaseActive;
        private int _totalReleaseCount;
        private int _completedReleaseCount;
        private CancellationTokenSource _releaseCancellation;
        private bool _isReleaseCancelling;

        #endregion


        #region Constructors

        public DeliveryReleaseViewModel()
        {
        }

        public DeliveryReleaseViewModel(ISaleFacade saleFacade, IInventoryFacade inventoryFacade)
            : this()
        {
            // Store services
            _saleFacade = saleFacade;
            _inventoryFacade = inventoryFacade;
            _saleFacade.Connect(ServiceTypes.Data);
            _inventoryFacade.Connect(ServiceTypes.Data);
        }

        public DeliveryReleaseViewModel(ISaleFacade saleFacade, IInventoryFacade inventoryFacade, List<Shared.DataModel.Sale.Delivery> deliveries)
            : this(saleFacade, inventoryFacade)
        {
            _initialDeliveries = deliveries;
            IsEmpty = false;
        }

        #endregion


        #region Mvvm Services

        [Display(AutoGenerateField = false)]
        public IPageViewNavigationService PageViewNavigationService { get { return GetService<IPageViewNavigationService>(); } }

        [Display(AutoGenerateField = false)]
        public IMessageBoxService MessageBoxService { get { return GetService<IMessageBoxService>(); } }

        #endregion


        #region Commands

        /// <summary>
        /// Command to confirm the release.
        /// </summary>
        [DoNotCopy]
        public ICommand ConfirmReleaseCommand
        {
            get { return _confirmReleaseCommand ?? (_confirmReleaseCommand = new AsyncCommand(OnConfirmReleaseExecuteAsync, OnConfirmReleaseCanExecute)); }
        }

        /// <summary>
        /// Command to cancel the release.
        /// </summary>
        [DoNotCopy]
        public ICommand CancelReleaseCommand
        {
            get { return _cancelReleaseCommand ?? (_cancelReleaseCommand = new DelegateCommand(OnCancelReleaseExecute, OnCancelReleaseCanExecute)); }
        }

        /// <summary>
        /// Command to clear all picked quantities.
        /// </summary>
        [DoNotCopy]
        public ICommand ClearReleaseCommand
        {
            get { return _clearReleaseCommand ?? (_clearReleaseCommand = new DelegateCommand(OnClearReleaseExecute, OnClearReleaseCanExecute)); }
        }

        /// <summary>
        /// Command to mark all available items as picked.
        /// </summary>
        [DoNotCopy]
        public ICommand MarkAllCommand
        {
            get { return _markAllCommand ?? (_markAllCommand = new DelegateCommand(OnMarkAllExecute, OnMarkAllCanExecute)); }
        }

        /// <summary>
        /// Command to mark selected items as picked.
        /// </summary>
        [DoNotCopy]
        public ICommand MarkSelectedCommand
        {
            get { return _markSelectedCommand ?? (_markSelectedCommand = new DelegateCommand(OnMarkSelectedExecute, OnMarkSelectedCanExecute)); }
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// A shared UnitOfWork for applying changes.
        /// </summary>
        public UnitOfWork UnitOfWork
        {
            get { return _unitOfWork; }
            set { SetProperty(ref _unitOfWork, value, () => UnitOfWork); }
        }

        /// <summary>
        /// Contains information about deliveries being released.
        /// </summary>
        public ObservableCollection<DeliveryReleaseDataViewModel> Deliveries
        {
            get { return _deliveries; }
            set { SetProperty(ref _deliveries, value, () => Deliveries); }
        }

        /// <summary>
        /// Contains information about pick items being released.
        /// </summary>
        public ObservableCollection<PickItemReleaseDataViewModel> PickItems
        {
            get { return _pickItems; }
            set { SetProperty(ref _pickItems, value, () => PickItems); }
        }

        /// <summary>
        /// Contains the selected PickItems.
        /// </summary>
        public ObservableCollection<PickItemReleaseDataViewModel> SelectedPickItems
        {
            get { return _selectedPickItems; }
        }

        /// <summary>
        /// A summary of the items on the deliveries.
        /// </summary>
        public string Summary
        {
            get { return _summary; }
            set { SetProperty(ref _summary, value, () => Summary); }
        }

        /// <summary>
        /// Indicates if any PickItems have been selected.
        /// </summary>
        public bool ArePickItemsSelected
        {
            get { return SelectedPickItems.Count > 0; }
        }

        /// <summary>
        /// The label that appears at the top of the Deliveries section.
        /// </summary>
        public string DeliveriesLabel
        {
            get { return "Enter Consignment Notes for the Deliveries that have been dispatched:"; }
        }

        /// <summary>
        /// The label that appears at the top of the PickItems section.
        /// </summary>
        public string PickItemsLabel
        {
            get { return "Enter quantities for the Pick Items that have been picked:"; }
        }

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


        #region Private Methods

        /// <summary>
        /// Initializes the delivery lists.
        /// </summary>
        private bool InitializeDeliveries()
        {
            // Abort if no initialDeliveries were provided
            if (_initialDeliveries == null)
                return false;

            // Create a UnitOfWork
            UnitOfWork = _saleFacade.CreateUnitOfWork();
            UnitOfWork.StartUiTracking(this, true, true, true);

            // Refresh the data
            UpdateDeliveries(_initialDeliveries);
            UpdateSummary();

            return true;
        }

        /// <summary>
        /// Updates the Deliveries and PickItems using the supplied list.
        /// </summary>
        /// <param name="deliveries">The deliveries to populate with.</param>
        private void UpdateDeliveries(List<Shared.DataModel.Sale.Delivery> deliveries)
        {
            // Stop handling events on the Deliveries and PickItems and break connections between then
            foreach (var deliveryData in Deliveries)
            {
                deliveryData.PropertyChanged -= Delivery_PropertyChanged;
                deliveryData.PickItems.Clear();
            }
            foreach (var pickItemData in PickItems)
            {
                pickItemData.PropertyChanged -= PickItem_PropertyChanged;
                pickItemData.DeliveryData = null;
            }

            // Clear the Deliveries and PickItems
            Deliveries.Clear();
            PickItems.Clear();

            foreach (var delivery in deliveries)
            {
                // Create a new data object for each Delivery
                var deliveryData = new DeliveryReleaseDataViewModel()
                {
                    Delivery = UnitOfWork.GetDataObject(delivery),
                    Contact = UnitOfWork.GetDataObject(delivery.Contact),
                    DeliveryStatus = UnitOfWork.GetDataObject(delivery.Status)
                };
                deliveryData.PropertyChanged += Delivery_PropertyChanged;
                Deliveries.Add(deliveryData);

                // Create a new data object for each PickItem attached to the Delivery
                foreach (var pickItem in delivery.DeliveryItems.SelectMany(d => d.PickItems))
                {
                    var pickItemData = new PickItemReleaseDataViewModel()
                    {
                        PickItem = UnitOfWork.GetDataObject(pickItem),
                        Delivery = UnitOfWork.GetDataObject(delivery),
                        DeliveryData = deliveryData,
                        Sku = UnitOfWork.GetDataObject(pickItem.DeliveryItem.Sku),
                        BinLocation = UnitOfWork.GetDataObject(pickItem.BinLocation),
                        PhysicalStockType = UnitOfWork.GetDataObject(pickItem.PhysicalStockType),
                        Quantity = pickItem.Quantity,
                        QuantityPicked = pickItem.QuantityPicked
                    };
                    pickItemData.PropertyChanged += PickItem_PropertyChanged;
                    deliveryData.PickItems.Add(pickItemData);
                    PickItems.Add(pickItemData);
                }
            }
        }

        /// <summary>
        /// Generates a title for this release.
        /// </summary>
        /// <param name="includeDeliveryCount">Indicates if the delivery count should be included.</param>
        /// <returns>A string representing the displayed deliveries.</returns>
        private string GetTitle(bool includeDeliveryCount)
        {
            if (!IsEmpty)
            {
                return includeDeliveryCount
                    ? string.Format("{0} {1}", _initialDeliveries.Count, _initialDeliveries.Count.Pluralize("Delivery"))
                    : "Delivery Release";
            }

            return base.ToString();
        }

        /// <summary>
        /// Updates the value of the Summary.
        /// </summary>
        private void UpdateSummary()
        {
            // Abort if this is not the root model
            if (!IsRootModel)
                return;

            // Build a summary of the Deliveries and PickItems
            var deliveriesDispatched = Deliveries.Count(d => d.Status == DeliveryReleaseDataViewModel.ReleaseStatuses.Dispatched);
            var deliveriesRemaining = Deliveries.Count - deliveriesDispatched;
            var deliveriesToDispatch = Deliveries.Count(d => d.Status == DeliveryReleaseDataViewModel.ReleaseStatuses.Dispatch || d.Status == DeliveryReleaseDataViewModel.ReleaseStatuses.CantDispatch);
            var erroredDeliveryCount = Deliveries.Count(d => d.HasError);
            var deliveryErrorString = (erroredDeliveryCount > 0 ? string.Format(" ({0} {1})", erroredDeliveryCount, erroredDeliveryCount.Pluralize("error")) : null);
            var totalCanPick = PickItems.Sum(p => p.QuantityCanPick);
            var totalPicked = PickItems.Sum(p => p.QuantityPicked);
            var totalToPick = PickItems.Sum(p => p.QuantityToPick);
            var totalShortShipped = PickItems.Sum(p => p.QuantityShortShipped);

            Summary = string.Format("{0} {1} previously dispatched\r\n{2} {3} remaining\r\n{4} marked as dispatched{5}\r\n\r\n{6} {7} previously picked\r\n{8} {9} remaining\r\n{10} marked as picked\r\n{11} will become short-shipped", deliveriesDispatched, deliveriesDispatched.Pluralize("delivery"), deliveriesRemaining, deliveriesRemaining.Pluralize("delivery"), deliveriesToDispatch, deliveryErrorString, totalPicked, totalPicked.Pluralize("item"), totalCanPick, totalCanPick.Pluralize("item"), totalToPick, totalShortShipped);
        }

        /// <summary>
        /// Marks the specified items as picked.
        /// </summary>
        /// <param name="pickItems">The items to mark as picked.</param>
        private void MarkItemsForRelease(IEnumerable<PickItemReleaseDataViewModel> pickItems)
        {
            foreach (var pickItem in pickItems)
            {
                pickItem.QuantityToPick = pickItem.QuantityCanPick;
            }
        }

        /// <summary>
        /// Asyncronously releases Deliveries.
        /// </summary>
        /// <param name="releasedDeliveries">A list of the Deliveries to release.</param>
        /// <param name="allDeliveries">A list of all displayed Deliveries to refresh.</param>
        /// <param name="cancellationToken">A token for cancellation.</param>
        private async Task ReleaseDeliveriesAsync(List<DeliveryReleaseDataViewModel> releasedDeliveries, List<DeliveryReleaseDataViewModel> allDeliveries, CancellationToken cancellationToken)
        {
            LogStart();

            ReleaseStatus = "Estimating time remaining...";
            TotalReleaseCount = releasedDeliveries.Count;

            var successCount = 0;
            var failCount = 0;
            var itemCount = 0;
            var dispatchedDeliveryCount = 0;
            var releaseStartTime = DateTime.Now;
            var totalDuration = new TimeSpan();
            var changes = new List<EntityChange>();

            foreach (var delivery in releasedDeliveries)
            {
                // Abort if cancellation has been requested
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    // Prepare release parameters and call the server to perform the release
                    var releaseParameters = new ReleaseDeliveryParameters(delivery.Delivery.Oid, delivery.ConsignmentNote);
                    foreach (var pickItem in delivery.PickItems)
                    {
                        releaseParameters.AddPickItem(pickItem.PickItem.Oid, pickItem.QuantityToPick);
                    }
                    var releaseResult = await _saleFacade.ReleaseDeliveryAsync(releaseParameters).ConfigureAwait(false);

                    // Store changes se we can send notifications at the end
                    changes.AddRange(releaseResult.Changes);

                    // Increment the itemCount and deliveryCount
                    itemCount += releaseResult.TotalQuantityPicked;
                    if (releaseResult.DeliveryDispatched)
                        dispatchedDeliveryCount++;

                    // Increment the successCount or failCount based on whether any items were successfully released or the delivery was dispatched
                    if (releaseResult.TotalQuantityPicked > 0 || releaseResult.DeliveryDispatched)
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
                    LogMessage(string.Format("ERROR: Delivery {0}\r\n{1}", delivery, serviceException.Message));
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
            _inventoryFacade.NotifyDirtyTypes(changesArray);

            // Notify other widgets of the changed entities
            EntityChangedMessage.Send(_detailViewModel, changesArray);

            // Log the number of deliveries released
            LogMessage(string.Format("Released {0:N0} {1} and dispatched {2:N0} {3}, from a total of {4:N0} {5} in {6}.", itemCount, itemCount.Pluralize("item"), dispatchedDeliveryCount, dispatchedDeliveryCount.Pluralize("Delivery"), CompletedReleaseCount, CompletedReleaseCount.Pluralize("Delivery"), totalDuration.Format()));

            // Log the count of successful and failed releases
            LogMessage(string.Format("Successful = {0:N0}", successCount));
            LogMessage(string.Format("Failed = {0:N0}", failCount));

            // Refresh the Deliveries
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                UpdateDeliveries(allDeliveries.Select(d => d.Delivery).ToList());
            });

            if (cancellationToken.IsCancellationRequested) // If cancellation was requested, log it
                LogCancelled();
            else if (successCount > 0) // Log success if at least one row was successful
                LogSuccess();
            else // Otherwise it must be a fail
                LogFail();

            // Move to the next page if one is available
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (PageViewNavigationService != null && PageViewNavigationService.CanGoForward)
                    PageViewNavigationService.GoForward();
            });
        }

        /// <summary>
        /// Sends a message to the log widget.
        /// </summary>
        /// <param name="message">The message to send.</param>
        private void LogMessage(string message)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                _detailViewModel.SendDocumentMessage(new AppendLogMessage(_detailViewModel, message))
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
        /// Execute method for the ConfirmReleaseCommand.
        /// </summary>
        private async Task OnConfirmReleaseExecuteAsync()
        {
            // Collect a list of Deliveries which have been marked as picked or dispatched
            var deliveries = Deliveries.Where(d => d.Status == DeliveryReleaseDataViewModel.ReleaseStatuses.Dispatch || d.PickItems.Any(p => p.QuantityToPick > 0)).ToList();

            // Create a cancellation token
            _releaseCancellation = new CancellationTokenSource();
            var cancellationToken = _releaseCancellation.Token;

            // Perform the release
            try
            {
                await ReleaseDeliveriesAsync(deliveries, Deliveries.ToList(), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation errors
            }

            // Clear the cancellation token
            _releaseCancellation = null;
        }

        /// <summary>
        /// CanExecute method for the ConfirmReleaseCommand.
        /// </summary>
        private bool OnConfirmReleaseCanExecute()
        {
            return !Deliveries.Any(d => d.HasError) && (Deliveries.Any(d => d.Status != DeliveryReleaseDataViewModel.ReleaseStatuses.None) || PickItems.Any(p => p.QuantityToPick > 0));
        }

        /// <summary>
        /// Execute method for the CancelReleaseCommand.
        /// </summary>
        private void OnCancelReleaseExecute()
        {
            if (MessageBoxService.Show(
                "Warning: If you cancel the release, deliveries that have already been processed will not be rolled back!\r\n\r\nAre you sure?",
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

        /// <summary>
        /// Execute method for the ClearReleaseCommand.
        /// </summary>
        private void OnClearReleaseExecute()
        {
            foreach (var pickItem in PickItems)
            {
                pickItem.QuantityToPick = 0;
            }
        }

        /// <summary>
        /// CanExecute method for the ClearReleaseCommand.
        /// </summary>
        private bool OnClearReleaseCanExecute()
        {
            return PickItems.Sum(r => r.QuantityToPick) > 0;
        }

        /// <summary>
        /// Execute method for the MarkAllCommand.
        /// </summary>
        private void OnMarkAllExecute()
        {
            MarkItemsForRelease(PickItems);
        }

        /// <summary>
        /// CanExecute method for the MarkAllCommand.
        /// </summary>
        private bool OnMarkAllCanExecute()
        {
            return PickItems.Count > 0;
        }

        /// <summary>
        /// Execute method for the MarkSelectedCommand.
        /// </summary>
        private void OnMarkSelectedExecute()
        {
            MarkItemsForRelease(SelectedPickItems);
        }

        /// <summary>
        /// CanExecute method for the MarkSelectedCommand.
        /// </summary>
        private bool OnMarkSelectedCanExecute()
        {
            return ArePickItemsSelected;
        }

        /// <summary>
        /// Handles the PropertyChanged event on each item in Deliveries.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Delivery_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Attempt to get the sender as a DeliveryReleaseDataViewModel
            var deliveryData = sender as DeliveryReleaseDataViewModel;
            if (deliveryData == null)
                return;

            if (e.PropertyName == "Status" || e.PropertyName == "HasError")
            {
                foreach (var pickItemData in deliveryData.PickItems)
                {
                    pickItemData.RefreshDeliveryData();
                }

                UpdateSummary();
            }
        }

        /// <summary>
        /// Handles the PropertyChanged event on each item in PickItems.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void PickItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "QuantityToPick")
                UpdateSummary();
        }

        /// <summary>
        /// Handles the CollectionChanged event on PickItems.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void PickItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateSummary();
        }

        /// <summary>
        /// Handles the CollectionChanged event on SelectedPickItems.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void SelectedPickItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(() => ArePickItemsSelected);
        }

        #endregion


        #region Overrides

        protected override void OnParentViewModelChanged(object parentViewModel)
        {
            base.OnParentViewModelChanged(parentViewModel);

            if (parentViewModel != null)
            {
                // The parent view model has been assigned

                // Intialize the model based on whether is it the root model or not
                if (IsRootModel)
                {
                    // Initialize collections
                    Deliveries = new ObservableCollection<DeliveryReleaseDataViewModel>();
                    PickItems = new ObservableCollection<PickItemReleaseDataViewModel>();
                    PickItems.CollectionChanged += PickItems_CollectionChanged;

                    // Prepare delivery data
                    if (InitializeDeliveries())
                        return;

                    UpdateSummary();
                }
                else
                {
                    SelectedPickItems.CollectionChanged += SelectedPickItems_CollectionChanged;

                    // Attempt to get the parentViewModel as a DetailViewModel
                    _detailViewModel = parentViewModel as DetailViewModel;
                    if (_detailViewModel == null)
                        return;

                    // Register the message types this model can send
                    _detailViewModel.AddSendMessageType<AppendLogMessage>();
                }
            }
            else
            {
                // The parent view model has been cleared

                // Clean up the model based on whether is it the root model or not
                if (IsRootModel)
                {
                    PickItems.CollectionChanged -= PickItems_CollectionChanged;

                    // Destroy the UnitOfWork
                    if (UnitOfWork != null)
                    {
                        try
                        {
                            UnitOfWork.Dispose();
                        }
                        catch (Exception)
                        {
                            // Ignore dispose errors
                        }
                        UnitOfWork = null;
                    }
                }
                else
                {
                    SelectedPickItems.CollectionChanged -= SelectedPickItems_CollectionChanged;

                    if (_detailViewModel != null)
                    {
                        // Unregister the message types this model can send
                        _detailViewModel.RemoveSendMessageType<AppendLogMessage>();
                    }
                }
            }
        }

        public override string Title
        {
            get { return GetTitle(true); }
        }

        public override string ToString()
        {
            return GetTitle(false);
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<DeliveryReleaseViewModel> builder)
        {
            builder.DataFormLayout()
                .Group("Summary", Orientation.Vertical)
                    .ContainsProperty(p => p.Summary)
                .EndGroup()
                .Group("Release", Orientation.Vertical)
                    .ContainsProperty(p => p.DeliveriesLabel)
                    .ContainsProperty(p => p.Deliveries)
                    .ContainsProperty(p => p.PickItemsLabel)
                    .Group("Release Buttons", Orientation.Horizontal)
                        .ContainsProperty(p => p.MarkAllCommand)
                        .ContainsProperty(p => p.MarkSelectedCommand)
                        .ContainsProperty(p => p.ClearReleaseCommand)
                    .EndGroup()
                    .ContainsProperty(p => p.PickItems)
                    .ContainsProperty(p => p.ConfirmReleaseCommand)
                    .Group("Release Progress", Orientation.Horizontal)
                        .ContainsProperty(p => p.CancelReleaseCommand)
                        .ContainsProperty(p => p.CompletedReleaseCount)
                        .ContainsProperty(p => p.ReleaseStatus);

            builder.Property(p => p.UnitOfWork).NotAutoGenerated();
            builder.Property(p => p.Deliveries).AutoGenerated();
            builder.Property(p => p.PickItems).AutoGenerated();
            builder.Property(p => p.SelectedPickItems).NotAutoGenerated();
            builder.Property(p => p.ConfirmReleaseCommand)
                .DisplayName("Confirm Release");
            builder.Property(p => p.CancelReleaseCommand)
                .DisplayName("Cancel");
            builder.Property(p => p.ArePickItemsSelected).NotAutoGenerated();
            builder.Property(p => p.ClearReleaseCommand)
                .DisplayName("Clear All Picked Quantities");
            builder.Property(p => p.MarkAllCommand)
                .DisplayName("Mark All Items As Picked");
            builder.Property(p => p.MarkSelectedCommand)
                .DisplayName("Mark Selected Items As Picked");
            builder.Property(p => p.IsReleaseActive).NotAutoGenerated();
            builder.Property(p => p.TotalReleaseCount).NotAutoGenerated();
            builder.Property(p => p.IsReleaseCancelling).NotAutoGenerated();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<DeliveryReleaseViewModel> builder)
        {
            builder.Condition(c => c != null && c.IsReleaseActive)
                .ContainsProperty(p => p.IsReleaseActive)
                .AffectsGroupVisibility("Release Progress")
                .AffectsPropertyVisibility(p => p.CancelReleaseCommand)
                .AffectsPropertyVisibility(p => p.CompletedReleaseCount)
                .AffectsPropertyVisibility(p => p.ReleaseStatus);

            builder.Condition(c => c != null && !c.IsReleaseActive)
                .ContainsProperty(p => p.IsReleaseActive)
                .AffectsPropertyEnabled(p => p.DeliveriesLabel)
                .AffectsPropertyEnabled(p => p.Deliveries)
                .AffectsPropertyEnabled(p => p.PickItemsLabel)
                .AffectsPropertyEnabled(p => p.MarkAllCommand)
                .AffectsPropertyEnabled(p => p.MarkSelectedCommand)
                .AffectsPropertyEnabled(p => p.ClearReleaseCommand)
                .AffectsPropertyEnabled(p => p.PickItems)
                .AffectsPropertyEnabled(p => p.ConfirmReleaseCommand);

            builder.Condition(i => true)
                .ContainsProperty(c => c.TotalReleaseCount)
                .InvokesInstanceMethod(p => p.CompletedReleaseCount, new Action<DeliveryReleaseViewModel, ProgressEditorDefinition>((context, editor) =>
                    editor.Maximum = context.TotalReleaseCount
                ));

            builder.Condition(c => c != null && c.IsReleaseActive && !c.IsReleaseCancelling)
                .ContainsProperty(p => p.IsReleaseActive)
                .ContainsProperty(p => p.IsReleaseCancelling)
                .AffectsPropertyEnabled(p => p.CancelReleaseCommand);

            builder.Condition(i => i.IsReleaseCancelling)
                .ContainsProperty(c => c.IsReleaseCancelling)
                .InvokesInstanceMethod(p => p.CancelReleaseCommand, new Action<DeliveryReleaseViewModel, ButtonEditorDefinition>((context, editor) =>
                    editor.ButtonText = "Cancelling..."
                ), (context, editor) =>
                    editor.ButtonText = "Cancel");

            builder.Property(p => p.Summary)
                .ReplaceEditor(new LabelEditorDefinition())
                .HideLabel();
            builder.Property(p => p.DeliveriesLabel)
                .ReplaceEditor(new LabelEditorDefinition())
                .HideLabel();
            builder.Property(p => p.PickItemsLabel)
                .ReplaceEditor(new LabelEditorDefinition())
                .HideLabel();

            builder.Property(p => p.ClearReleaseCommand)
                .HideLabel();
            builder.Property(p => p.MarkAllCommand)
                .HideLabel();
            builder.Property(p => p.MarkSelectedCommand)
                .HideLabel();
            builder.Property(p => p.ConfirmReleaseCommand)
                .HideLabel();
            builder.Property(p => p.CancelReleaseCommand)
                .ControlWidth(100)
                .HideLabel();
            builder.Property(p => p.CompletedReleaseCount)
                .ReplaceEditor(new ProgressEditorDefinition())
                .HideLabel();
            builder.Property(p => p.ReleaseStatus)
                .ReplaceEditor(new LabelEditorDefinition())
                .ControlWidth(250)
                .HideLabel();

            builder.Property(p => p.Deliveries)
                 .ReplaceEditor(new GridEditorDefinition
                 {
                     EntityType = typeof(DeliveryReleaseDataViewModel),
                     ItemsSourceMethod = context => ((DeliveryReleaseViewModel)context).Deliveries
                 })
                 .HideLabel();

            builder.Property(p => p.PickItems)
                 .ReplaceEditor(new GridEditorDefinition
                 {
                     EntityType = typeof(PickItemReleaseDataViewModel),
                     ItemsSourceMethod = context => ((DeliveryReleaseViewModel)context).PickItems,
                     SelectedItemsSourceMethod = context => ((DeliveryReleaseViewModel)context).SelectedPickItems,
                     AutoExpandAllGroups = true
                 })
                 .HideLabel();
        }

        #endregion
    }
}
