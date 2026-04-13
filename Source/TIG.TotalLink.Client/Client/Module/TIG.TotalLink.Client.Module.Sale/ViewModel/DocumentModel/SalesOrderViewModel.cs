using System;
using System.Collections;
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
using DevExpress.Xpf.LayoutControl;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Core.Helper;
using TIG.TotalLink.Client.Core.Message;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Client.Editor.Extension;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Extension;
using TIG.TotalLink.Client.Module.Admin.MvvmService;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Global;
using TIG.TotalLink.Client.Module.Sale.ViewModel.DocumentModel.SalesOrder;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.Contract.Sale;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.DataModel.Crm;
using TIG.TotalLink.Shared.DataModel.Inventory;
using TIG.TotalLink.Shared.DataModel.Sale;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Helper;
using TIG.TotalLink.Shared.Facade.Sale;

namespace TIG.TotalLink.Client.Module.Sale.ViewModel.DocumentModel
{
    [DocumentDataModel("Sales Order", "Contains data for creating and viewing Sales Orders.")]
    public class SalesOrderViewModel : DocumentDataModelBase
    {
        #region Public Enums

        public enum ContactTypes
        {
            Company,
            Branch,
            Person
        }

        #endregion


        #region Private Fields

        private readonly ISaleFacade _saleFacade;
        private UnitOfWork _unitOfWork;
        private DetailViewModel _detailViewModel;
        private ContactTypes _contactType;
        private readonly Quote _initialQuote;
        private readonly Shared.DataModel.Sale.SalesOrder _initialSalesOrder;
        private readonly bool _createNewSalesOrder;
        private Quote _quote;
        private Shared.DataModel.Sale.SalesOrder _salesOrder;
        private Contact _contact;
        private bool _isNew;
        private string _summary;
        private bool _isSalesOrderModified;
        private bool _allowPartialDelivery;
        private int _quantityReleased;
        private SalesOrderStatus _salesOrderStatus;
        private readonly ObservableCollection<Company> _selectedCompanies = new ObservableCollection<Company>();
        private readonly ObservableCollection<Branch> _selectedBranches = new ObservableCollection<Branch>();
        private readonly ObservableCollection<Person> _selectedPeople = new ObservableCollection<Person>();
        private readonly ObservableCollection<Sku> _selectedSkus = new ObservableCollection<Sku>();
        private readonly ObservableCollection<BinLocation> _selectedBins = new ObservableCollection<BinLocation>();
        private readonly ObservableCollection<PhysicalStockType> _selectedStockTypes = new ObservableCollection<PhysicalStockType>();
        private ObservableCollection<SalesOrderItemReleaseDataViewModel> _releaseItems;
        private readonly ObservableCollection<SalesOrderItemReleaseDataViewModel> _selectedReleaseItems = new ObservableCollection<SalesOrderItemReleaseDataViewModel>();
        private Contact _selectedContact;
        private ICommand _selectContactCommand;
        private ICommand _saveSalesOrderCommand;
        private ICommand _clearReleaseCommand;
        private ICommand _markAllCommand;
        private ICommand _markSelectedCommand;
        private ICommand _confirmReleaseCommand;
        private CancellationTokenSource _availableStockCancellation;

        #endregion


        #region Constructors

        public SalesOrderViewModel()
        {
        }

        public SalesOrderViewModel(ISaleFacade saleFacade)
            : this()
        {
            // Store services
            _saleFacade = saleFacade;
            _saleFacade.Connect(ServiceTypes.Data);
        }

        public SalesOrderViewModel(ISaleFacade saleFacade, bool createNewSalesOrder)
            : this(saleFacade)
        {
            _createNewSalesOrder = createNewSalesOrder;
            IsEmpty = !createNewSalesOrder;
        }

        public SalesOrderViewModel(ISaleFacade saleFacade, Quote quote)
            : this(saleFacade)
        {
            _initialQuote = quote;
            IsEmpty = false;
        }

        public SalesOrderViewModel(ISaleFacade saleFacade, Shared.DataModel.Sale.SalesOrder salesOrder)
            : this(saleFacade)
        {
            _initialSalesOrder = salesOrder;
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
        /// Command to move to the next step once the Contact has been selected.
        /// </summary>
        [DoNotCopy]
        public ICommand SelectContactCommand
        {
            get { return _selectContactCommand ?? (_selectContactCommand = new DelegateCommand(OnSelectContactExecute, OnSelectContactCanExecute)); }
        }

        /// <summary>
        /// Command to save the SalesOrder.
        /// </summary>
        [DoNotCopy]
        public ICommand SaveSalesOrderCommand
        {
            get { return _saveSalesOrderCommand ?? (_saveSalesOrderCommand = new DelegateCommand(OnSaveSalesOrderExecute, OnSaveSalesOrderCanExecute)); }
        }

        /// <summary>
        /// Command to clear all release quantities.
        /// </summary>
        [DoNotCopy]
        public ICommand ClearReleaseCommand
        {
            get { return _clearReleaseCommand ?? (_clearReleaseCommand = new DelegateCommand(OnClearReleaseExecute, OnClearReleaseCanExecute)); }
        }

        /// <summary>
        /// Command to mark all available items for release.
        /// </summary>
        [DoNotCopy]
        public ICommand MarkAllCommand
        {
            get { return _markAllCommand ?? (_markAllCommand = new DelegateCommand(OnMarkAllExecute, OnMarkAllCanExecute)); }
        }

        /// <summary>
        /// Command to mark selected items for release.
        /// </summary>
        [DoNotCopy]
        public ICommand MarkSelectedCommand
        {
            get { return _markSelectedCommand ?? (_markSelectedCommand = new DelegateCommand(OnMarkSelectedExecute, OnMarkSelectedCanExecute)); }
        }

        /// <summary>
        /// Command to confirm the release.
        /// </summary>
        [DoNotCopy]
        public ICommand ConfirmReleaseCommand
        {
            get { return _confirmReleaseCommand ?? (_confirmReleaseCommand = new DelegateCommand(OnConfirmReleaseExecute, OnConfirmReleaseCanExecute)); }
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
        /// The type of Contact that the SalesOrder will be created for.
        /// </summary>
        public ContactTypes ContactType
        {
            get { return _contactType; }
            set
            {
                SetProperty(ref _contactType, value, () => ContactType, () =>
                {
                    if (IsRootModel)
                        UpdateSelectedContact();
                });
            }
        }

        /// <summary>
        /// The Quote that this SalesOrder was created from.
        /// </summary>
        public Quote Quote
        {
            get { return _quote; }
            set { SetProperty(ref _quote, value, () => Quote); }
        }

        /// <summary>
        /// The sales order.
        /// </summary>
        public Shared.DataModel.Sale.SalesOrder SalesOrder
        {
            get { return _salesOrder; }
            set
            {
                SetProperty(ref _salesOrder, value, () => SalesOrder, () =>
                    RaisePropertyChanged(() => IsNew)
                );
            }
        }

        /// <summary>
        /// Exposes all items on the SalesOrder.
        /// </summary>
        public XPCollection<SalesOrderItem> SalesOrderItems
        {
            get { return (SalesOrder != null ? SalesOrder.SalesOrderItems : null); }
        }

        /// <summary>
        /// Indicates if the SalesOrder has been modified in any way.
        /// </summary>
        public bool IsSalesOrderModified
        {
            get { return _isSalesOrderModified; }
            set { SetProperty(ref _isSalesOrderModified, value, () => IsSalesOrderModified); }
        }

        /// <summary>
        /// Exposes the contact on the SalesOrder.
        /// </summary>
        public Contact Contact
        {
            get { return _contact; }
            set { SetProperty(ref _contact, value, () => Contact); }
        }

        /// <summary>
        /// Exposes the status of the SalesOrder.
        /// </summary>
        public SalesOrderStatus SalesOrderStatus
        {
            get { return _salesOrderStatus; }
            set { SetProperty(ref _salesOrderStatus, value, () => SalesOrderStatus); }
        }

        /// <summary>
        /// Exposes SalesOrder.AllowPartialDelivery.
        /// </summary>
        public bool AllowPartialDelivery
        {
            get { return _allowPartialDelivery; }
            set
            {
                SetProperty(ref _allowPartialDelivery, value, () => AllowPartialDelivery, () =>
                {
                    if (!IsRootModel || SalesOrder == null)
                        return;

                    SalesOrder.AllowPartialDelivery = _allowPartialDelivery;
                    IsSalesOrderModified = true;

                    foreach (var releaseItem in ReleaseItems)
                    {
                        releaseItem.UpdateStatus();
                    }
                });
            }
        }

        /// <summary>
        /// Indicates if the Quote has been saved yet.
        /// </summary>
        public bool IsNew
        {
            get { return _isNew; }
            set
            {
                SetProperty(ref _isNew, value, () => IsNew, () =>
                {
                    if (!IsRootModel || _isNew)
                        return;

                    UpdateDocumentId();
                });
            }
        }

        /// <summary>
        /// Contains the selected Company when CustomerType = Company.
        /// Will always contain either zero or one item.
        /// </summary>
        public ObservableCollection<Company> SelectedCompanies
        {
            get { return _selectedCompanies; }
        }

        /// <summary>
        /// Contains the selected Branch when CustomerType = Branch.
        /// Will always contain either zero or one item.
        /// </summary>
        public ObservableCollection<Branch> SelectedBranches
        {
            get { return _selectedBranches; }
        }

        /// <summary>
        /// Contains the selected Person when CustomerType = Person.
        /// Will always contain either zero or one item.
        /// </summary>
        public ObservableCollection<Person> SelectedPeople
        {
            get { return _selectedPeople; }
        }

        /// <summary>
        /// The selected contact.
        /// </summary>
        public Contact SelectedContact
        {
            get { return _selectedContact; }
            set
            {
                SetProperty(ref _selectedContact, value, () => SelectedContact, () =>
                    RaisePropertyChanged(() => IsContactSelected));

                //System.Diagnostics.Debug.WriteLine("SelectedContact={0}  isRootModel={1}", _selectedContact, _isRootModel);
            }
        }

        /// <summary>
        /// Indicates if a valid contact is selected.
        /// </summary>
        public bool IsContactSelected
        {
            get { return (SelectedContact != null); }
        }

        /// <summary>
        /// Contains Skus collected via the DetailViewModel.SelectedItemsChanging event.
        /// </summary>
        public ObservableCollection<Sku> SelectedSkus
        {
            get { return _selectedSkus; }
        }

        /// <summary>
        /// A summary of the items on the SalesOrder.
        /// </summary>
        public string Summary
        {
            get { return _summary; }
            set { SetProperty(ref _summary, value, () => Summary); }
        }

        /// <summary>
        /// The quantity that has already been released on this SalesOrder.
        /// </summary>
        public int QuantityReleased
        {
            get { return _quantityReleased; }
            set { SetProperty(ref _quantityReleased, value, () => QuantityReleased, UpdateSummary); }
        }

        /// <summary>
        /// Contains information about items being released.
        /// </summary>
        public ObservableCollection<SalesOrderItemReleaseDataViewModel> ReleaseItems
        {
            get { return _releaseItems; }
            set { SetProperty(ref _releaseItems, value, () => ReleaseItems); }
        }

        /// <summary>
        /// Contains the selected ReleaseItems.
        /// </summary>
        public ObservableCollection<SalesOrderItemReleaseDataViewModel> SelectedReleaseItems
        {
            get { return _selectedReleaseItems; }
        }

        /// <summary>
        /// Indicates if any ReleaseItems have been selected.
        /// </summary>
        public bool AreReleaseItemsSelected
        {
            get { return SelectedReleaseItems.Count > 0; }
        }

        /// <summary>
        /// Contains the selected BinLocations.
        /// </summary>
        public ObservableCollection<BinLocation> SelectedBins
        {
            get { return _selectedBins; }
        }

        /// <summary>
        /// Indicates if any BinLocations have been selected.
        /// </summary>
        public bool AreBinsSelected
        {
            get { return SelectedBins.Count > 0; }
        }

        /// <summary>
        /// Contains the selected PhysicalStockTypes.
        /// </summary>
        public ObservableCollection<PhysicalStockType> SelectedStockTypes
        {
            get { return _selectedStockTypes; }
        }

        /// <summary>
        /// Indicates if any PhysicalStockTypes have been selected.
        /// </summary>
        public bool AreStockTypesSelected
        {
            get { return SelectedStockTypes.Count > 0; }
        }

        /// <summary>
        /// The label that appears at the top of the Release section.
        /// </summary>
        public string ReleaseLabel
        {
            get { return "Enter quantities for the items you wish to release on this Sales Order:"; }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Updates the SelectedContact based on the ContactType and the appropriate Selected collection.
        /// </summary>
        private void UpdateSelectedContact()
        {
            // Find the approprite collection to get the contact from based on the selected ContactType
            IList selectedCollection = null;
            switch (ContactType)
            {
                case ContactTypes.Company:
                    selectedCollection = SelectedCompanies;
                    break;

                case ContactTypes.Branch:
                    selectedCollection = SelectedBranches;
                    break;

                case ContactTypes.Person:
                    selectedCollection = SelectedPeople;
                    break;
            }

            // If the collection contains one item, set the item to be the SelectedContact
            if (selectedCollection != null && selectedCollection.Count == 1)
            {
                SelectedContact = selectedCollection[0] as Contact;
                return;
            }

            // Otherwise, set the SelectedContact to null
            SelectedContact = null;
        }

        /// <summary>
        /// Initializes the model when a Quote was provided.
        /// </summary>
        /// <returns>True if a Quote was provided; otherwise false;</returns>
        private bool InitializeFromQuote()
        {
            // Abort if no initialQuote was provided
            if (_initialQuote == null)
                return false;

            // Create a UnitOfWork
            UnitOfWork = _saleFacade.CreateUnitOfWork();
            UnitOfWork.StartUiTracking(this, true, true, true);

            // Initialize data
            IsNew = true;
            Quote = UnitOfWork.GetDataObject(_initialQuote);

            var currentVersion = Quote.QuoteVersions.OrderByDescending(v => v.Version).FirstOrDefault();
            SalesOrder = new Shared.DataModel.Sale.SalesOrder(UnitOfWork)
            {
                Oid = Guid.NewGuid(),
                QuoteVersion = currentVersion,
                Contact = Quote.Contact
            };

            Contact = SalesOrder.Contact;

            SalesOrder.SalesOrderItems.CollectionChanged += SalesOrderItems_CollectionChanged;

            if (currentVersion != null)
            {
                foreach (var quoteItem in currentVersion.QuoteItems)
                {
                    new SalesOrderItem(UnitOfWork)
                    {
                        Oid = Guid.NewGuid(),
                        SalesOrder = SalesOrder,
                        QuoteItem = quoteItem,
                        Sku = quoteItem.Sku,
                        Quantity = quoteItem.Quantity,
                        CostPrice = quoteItem.Sku.UnitCost,
                        SellPrice = quoteItem.Sku.UnitPrice,
                    };
                }
            }

            UpdateSummary();
            UpdateReleaseItems();

            // Return true to indicate the the initialization was completed
            return true;
        }

        /// <summary>
        /// Initializes the model when a SalesOrder was provided.
        /// </summary>
        /// <returns>True if a SalesOrder was provided; otherwise false;</returns>
        private bool InitializeFromSalesOrder()
        {
            // Abort if no initialSalesOrder was provided
            if (_initialSalesOrder == null)
                return false;

            // Create a UnitOfWork
            UnitOfWork = _saleFacade.CreateUnitOfWork();
            UnitOfWork.StartUiTracking(this, true, true, true);

            // Initialize data
            IsNew = false;
            SalesOrder = UnitOfWork.GetDataObject(_initialSalesOrder);
            Quote = (SalesOrder.QuoteVersion != null ? SalesOrder.QuoteVersion.Quote : null);

            Contact = SalesOrder.Contact;
            _allowPartialDelivery = SalesOrder.AllowPartialDelivery;
            QuantityReleased = SalesOrder.TotalQuantityReleased;
            SalesOrderStatus = SalesOrder.Status;

            SalesOrder.SalesOrderItems.CollectionChanged += SalesOrderItems_CollectionChanged;

            foreach (var salesOrderItem in SalesOrder.SalesOrderItems)
            {
                ((INotifyPropertyChanged)salesOrderItem).PropertyChanged += SalesOrderItem_PropertyChanged;
            }

            UpdateSummary();
            UpdateReleaseItems();

            // Return true to indicate the the initialization was completed
            return true;
        }

        /// <summary>
        /// Initializes the model when a new SalesOrder was requested.
        /// </summary>
        /// <returns>True if a new SalesOrder was requested; otherwise false;</returns>
        private bool InitializeNewSalesOrder()
        {
            // Abort if the flag was not set to create a new SalesOrder
            if (!_createNewSalesOrder)
                return false;

            // Create a UnitOfWork
            UnitOfWork = _saleFacade.CreateUnitOfWork();
            UnitOfWork.StartUiTracking(this, true, true, true);

            // Initialize data
            IsNew = true;
            SalesOrder = new Shared.DataModel.Sale.SalesOrder(UnitOfWork)
            {
                Oid = Guid.NewGuid()
            };
            SalesOrder.SalesOrderItems.CollectionChanged += SalesOrderItems_CollectionChanged;

            UpdateSummary();

            // Return true to indicate the the initialization was completed
            return true;
        }

        /// <summary>
        /// Update the value of the Summary.
        /// </summary>
        private void UpdateSummary()
        {
            // Abort if this is not the root model
            if (!IsRootModel)
                return;

            // Build a summary of the SalesOrder
            Summary = string.Format("{0} {1} for {2:C}\r\n{3} {4} released", SalesOrder.TotalQuantity, SalesOrder.TotalQuantity.Pluralize("item"), SalesOrder.TotalSellPrice, QuantityReleased, QuantityReleased.Pluralize("item"));
        }

        /// <summary>
        /// Updates the ReleaseItems to match the SalesOrderItems.
        /// </summary>
        private void UpdateReleaseItems()
        {
            // Stop handling events on the release items
            foreach (var releaseItem in ReleaseItems)
            {
                releaseItem.PropertyChanged -= ReleaseItem_PropertyChanged;
            }

            // Clear the ReleaseItems
            ReleaseItems.Clear();

            // Create a new ReleaseItem for each SalesOrderItem
            foreach (var salesOrderItem in SalesOrderItems)
            {
                var releaseItem = new SalesOrderItemReleaseDataViewModel()
                {
                    SalesOrderItem = salesOrderItem,
                    Sku = salesOrderItem.Sku,
                    Quantity = salesOrderItem.Quantity,
                    QuantityReleased = salesOrderItem.QuantityReleased,
                    QuantityCancelled = salesOrderItem.QuantityCancelled,
                    CostPrice = salesOrderItem.CostPrice,
                    SellPrice = salesOrderItem.SellPrice
                };
                releaseItem.PropertyChanged += ReleaseItem_PropertyChanged;
                ReleaseItems.Add(releaseItem);
            }

            UpdateAvailableStock();
        }

        /// <summary>
        /// Updates one property on a SalesOrderItem from the corresponding ReleaseItem.
        /// </summary>
        /// <param name="releaseItem">The ReleaseItem that has changed.</param>
        /// <param name="propertyName">The name of the property that has changed on the ReleaseItem.</param>
        private void UpdateSalesOrderItem(SalesOrderItemReleaseDataViewModel releaseItem, string propertyName)
        {
            // Attempt to get the source property from the ReleaseItem
            var releaseItemProperty = typeof(SalesOrderItemReleaseDataViewModel).GetProperty(propertyName);
            if (releaseItemProperty == null)
                return;

            // Attempt to get the destination property from the SalesOrderItem
            var salesOrderItemProperty = typeof(SalesOrderItem).GetProperty(propertyName);
            if (salesOrderItemProperty == null)
                return;

            // Copy the property value from the ReleaseItem to the SalesOrderItem
            salesOrderItemProperty.SetValue(releaseItem.SalesOrderItem, releaseItemProperty.GetValue(releaseItem));
        }

        /// <summary>
        /// Updates the AvailableStock on the ReleaseItems whenever the SelectedBins or SelectedStockTypes change.
        /// </summary>
        private void UpdateAvailableStock()
        {
            // If AvailableStock is already being calculated, cancel the old task
            if (_availableStockCancellation != null)
            {
                //System.Diagnostics.Debug.WriteLine("UpdateAvailableStock cancelling");
                _availableStockCancellation.Cancel();
            }

            // Create a cancellation token
            _availableStockCancellation = new CancellationTokenSource();
            var cancellationToken = _availableStockCancellation.Token;

            // Start the task
            //System.Diagnostics.Debug.WriteLine("UpdateAvailableStock starting");
            var releaseItems = new List<SalesOrderItemReleaseDataViewModel>(ReleaseItems);
            var selectedBinOids = SelectedBins.Select(b => b.Oid).ToList();
            var selectedStockTypeOids = SelectedStockTypes.Select(s => s.Oid).ToList();
            Task.Run(() => UpdateAvailableStockTask(releaseItems, selectedBinOids, selectedStockTypeOids, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// The task method which updates the AvailableStock on the ReleaseItems.
        /// </summary>
        private void UpdateAvailableStockTask(List<SalesOrderItemReleaseDataViewModel> releaseItems, List<Guid> binLocationOids, List<Guid> physicalStockTypeOids, CancellationToken cancellationToken)
        {
            // Process each ReleaseItem
            foreach (var releaseItem in releaseItems)
            {
                // Abort if cancellation has been requested
                if (cancellationToken.IsCancellationRequested)
                    break;

                // Calculate the AvailableStock for the ReleaseItem
                var releaseItem1 = releaseItem;
                var availableStock = UnitOfWork.Query<PhysicalStock>().Where(p => p.Sku.Oid == releaseItem1.Sku.Oid && binLocationOids.Contains(p.BinLocation.Oid) && physicalStockTypeOids.Contains(p.PhysicalStockType.Oid)).Sum(p => p.AvailableStock);

                // Abort if cancellation has been requested
                if (cancellationToken.IsCancellationRequested)
                    break;

                // Update the AvailableStock on the ReleaseItem
                Application.Current.Dispatcher.Invoke(() =>
                    releaseItem1.AvailableStock = availableStock
                );
            }
        }

        /// <summary>
        /// Marks the specified items for release.
        /// </summary>
        /// <param name="releaseItems">The items to mark for release.</param>
        private void MarkItemsForRelease(IEnumerable<SalesOrderItemReleaseDataViewModel> releaseItems)
        {
            foreach (var releaseItem in releaseItems)
            {
                releaseItem.QuantityToRelease = Math.Min(releaseItem.QuantityCanRelease, releaseItem.AvailableStock);
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the SelectContactCommand.
        /// </summary>
        private void OnSelectContactExecute()
        {
            // Only save if this is not a template
            if (!IsEmpty)
            {
                // Assign the selected contact
                Contact = UnitOfWork.GetDataObject(SelectedContact);
                SalesOrder.Contact = Contact;
                AllowPartialDelivery = Contact.AllowPartialDelivery;
                IsSalesOrderModified = true;
                EntityChangedMessage.Send(this, SalesOrder, EntityChange.ChangeTypes.Modify);
            }

            // Move to the next page if one is available
            if (PageViewNavigationService != null && PageViewNavigationService.CanGoForward)
                PageViewNavigationService.GoForward();
        }

        /// <summary>
        /// CanExecute method for the SelectContactCommand.
        /// </summary>
        private bool OnSelectContactCanExecute()
        {
            return IsContactSelected;
        }

        /// <summary>
        /// Execute method for the SaveSalesOrderCommand.
        /// </summary>
        private void OnSaveSalesOrderExecute()
        {
            // Only save if this is not a template
            if (!IsEmpty)
            {
                // If the SalesOrder hasn't been saved yet...
                if (IsNew)
                {
                    // Generate a reference number for it
                    SalesOrder.GenerateReferenceNumber();

                    // Set the initial status
                    // TODO : SalesOrderStatus should be found by Order instead of Name
                    SalesOrder.Status = UnitOfWork.Query<SalesOrderStatus>().FirstOrDefault(s => s.Name == "Awaiting Release");
                    SalesOrderStatus = SalesOrder.Status;
                }

                // Commit all changes
                UnitOfWork.CommitChanges();

                // Flag to indicate that the SalesOrder has now been saved
                IsNew = false;
                IsSalesOrderModified = false;
                UpdateReleaseItems();
            }

            // Move to the next page if one is available
            if (PageViewNavigationService != null && PageViewNavigationService.CanGoForward)
                PageViewNavigationService.GoForward();
        }

        /// <summary>
        /// CanExecute method for the SaveSalesOrderCommand.
        /// </summary>
        private bool OnSaveSalesOrderCanExecute()
        {
            return SalesOrder != null && SalesOrder.SalesOrderItems.Count > 0 && IsSalesOrderModified;
        }

        /// <summary>
        /// Execute method for the ClearReleaseCommand.
        /// </summary>
        private void OnClearReleaseExecute()
        {
            foreach (var releaseItem in ReleaseItems)
            {
                releaseItem.QuantityToRelease = 0;
            }
        }

        /// <summary>
        /// CanExecute method for the ClearReleaseCommand.
        /// </summary>
        private bool OnClearReleaseCanExecute()
        {
            return ReleaseItems.Sum(r => r.QuantityToRelease) > 0;
        }

        /// <summary>
        /// Execute method for the MarkAllCommand.
        /// </summary>
        private void OnMarkAllExecute()
        {
            MarkItemsForRelease(ReleaseItems);
        }

        /// <summary>
        /// CanExecute method for the MarkAllCommand.
        /// </summary>
        private bool OnMarkAllCanExecute()
        {
            return AreBinsSelected && AreStockTypesSelected && ReleaseItems.Count > 0;
        }

        /// <summary>
        /// Execute method for the MarkSelectedCommand.
        /// </summary>
        private void OnMarkSelectedExecute()
        {
            MarkItemsForRelease(SelectedReleaseItems);
        }

        /// <summary>
        /// CanExecute method for the MarkSelectedCommand.
        /// </summary>
        private bool OnMarkSelectedCanExecute()
        {
            return AreBinsSelected && AreStockTypesSelected && AllowPartialDelivery && AreReleaseItemsSelected;
        }

        /// <summary>
        /// Execute method for the ConfirmReleaseCommand.
        /// </summary>
        private void OnConfirmReleaseExecute()
        {
            // Commit changes to the SalesOrder
            UnitOfWork.CommitChanges();

            try
            {
                // Prepare parameters and call the server to perform the release
                var releaseParameters = new ReleaseSalesOrderParameters(SalesOrder.Oid, SelectedBins.Select(b => b.Oid).ToArray(), SelectedStockTypes.Select(b => b.Oid).ToArray());
                foreach (var releaseItem in ReleaseItems.Where(r => r.QuantityToRelease > 0))
                {
                    releaseParameters.AddSalesOrderItem(releaseItem.SalesOrderItem.Oid, releaseItem.QuantityToRelease);
                }
                var releaseResult = _saleFacade.ReleaseSalesOrder(releaseParameters);

                // The server has made changes that the client is not aware of, so we have to force the cache to refresh the changed types
                _saleFacade.NotifyDirtyTypes(releaseResult.Changes);

                // Notify other widgets of the changed entities
                EntityChangedMessage.Send(_detailViewModel, releaseResult.Changes);

                // Refresh the SalesOrder
                SalesOrder.SalesOrderItems.Reload();
                SalesOrder.Reload();
                QuantityReleased = SalesOrder.TotalQuantityReleased;
                SalesOrderStatus = SalesOrder.Status;
                UpdateReleaseItems();
                IsSalesOrderModified = false;

                // Send a SelectedItemsChangedMessage to display the release results
                var salesOrderRelease = UnitOfWork.GetObjectByKey<SalesOrderRelease>(releaseResult.SalesOrderReleaseOid);
                _detailViewModel.SendDocumentMessage(new SelectedItemsChangedMessage(this, new[] { salesOrderRelease }));

                // Move to the next page if one is available
                if (PageViewNavigationService != null && PageViewNavigationService.CanGoForward)
                    PageViewNavigationService.GoForward();
            }
            catch (Exception ex)
            {
                var serviceException = new ServiceExceptionHelper(ex);
                MessageBoxService.Show(string.Format("Release failed!\r\n\r\n{0}", serviceException.Message), "Release Sales Order", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// CanExecute method for the ConfirmReleaseCommand.
        /// </summary>
        private bool OnConfirmReleaseCanExecute()
        {
            return AreBinsSelected && AreStockTypesSelected && ReleaseItems.Sum(r => r.QuantityToRelease) > 0 && !ReleaseItems.Any(r => r.HasError);
        }

        /// <summary>
        /// Handles the SelectedCompanies.CollectionChanged event.
        /// Only active when the ParentViewModel is a DocumentViewModel.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void SelectedCompanies_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (ContactType == ContactTypes.Company)
                UpdateSelectedContact();
        }

        /// <summary>
        /// Handles the SelectedBranches.CollectionChanged event.
        /// Only active when the ParentViewModel is a DocumentViewModel.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void SelectedBranches_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (ContactType == ContactTypes.Branch)
                UpdateSelectedContact();
        }

        /// <summary>
        /// Handles the SelectedPeople.CollectionChanged event.
        /// Only active when the ParentViewModel is a DocumentViewModel.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void SelectedPeople_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (ContactType == ContactTypes.Person)
                UpdateSelectedContact();
        }

        /// <summary>
        /// Handles the DetailViewModel.SelectedItemsChanging event.
        /// Only active when the ParentViewModel is a DetailViewModel.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void DetailViewModel_SelectedItemsChanging(object sender, SelectedItemsChangingEventArgs e)
        {
            // Always mark the event as handled because we don't want the parent DetailViewModel to process the selected items
            e.Handled = true;

            // Process the selected items based on the primary type
            var selectedType = e.SelectedItemsChangedMessage.GetPrimaryType();
            TypeSwitch.On(selectedType)
                .Case<Sku>(() =>
                {
                    // Process selected Skus
                    var selectedSkus = e.SelectedItemsChangedMessage.GetEntitiesOfType<Sku>();
                    selectedSkus.SyncTo(SelectedSkus);
                })
                .Case<BinLocation>(() =>
                {
                    // Process selected BinLocations
                    var selectedBinLocations = e.SelectedItemsChangedMessage.GetEntitiesOfType<BinLocation>();
                    selectedBinLocations.SyncTo(SelectedBins);
                    UpdateAvailableStock();
                })
                .Case<PhysicalStockType>(() =>
                {
                    // Process selected PhysicalStockTypes
                    var selectedPhysicalStockTypes = e.SelectedItemsChangedMessage.GetEntitiesOfType<PhysicalStockType>();
                    selectedPhysicalStockTypes.SyncTo(SelectedStockTypes);
                    UpdateAvailableStock();
                });
        }

        /// <summary>
        /// Handles the CollectionChanged event on SalesOrder.SalesOrderItems.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void SalesOrderItems_CollectionChanged(object sender, XPCollectionChangedEventArgs e)
        {
            // Attach or remove PropertyChanged events on each SalesOrderItem as they are added or removed from SalesOrder.SalesOrderItems
            switch (e.CollectionChangedType)
            {
                case XPCollectionChangedType.AfterAdd:
                    ((INotifyPropertyChanged)e.ChangedObject).PropertyChanged += SalesOrderItem_PropertyChanged;
                    IsSalesOrderModified = true;
                    UpdateSummary();
                    break;

                case XPCollectionChangedType.AfterRemove:
                    ((INotifyPropertyChanged)e.ChangedObject).PropertyChanged -= SalesOrderItem_PropertyChanged;
                    IsSalesOrderModified = true;
                    UpdateSummary();
                    break;
            }
        }

        /// <summary>
        /// Handles the PropertyChanged event on each item in SalesOrder.SalesOrderItems.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void SalesOrderItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            IsSalesOrderModified = true;
            UpdateSummary();
        }

        /// <summary>
        /// Handles the PropertyChanged event on each item in ReleaseItems.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ReleaseItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "QuantityCancelled" || e.PropertyName == "CostPrice" || e.PropertyName == "SellPrice")
                UpdateSalesOrderItem(sender as SalesOrderItemReleaseDataViewModel, e.PropertyName);
        }

        /// <summary>
        /// Handles the CollectionChanged event on SelectedReleaseItems.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void SelectedReleaseItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(() => AreReleaseItemsSelected);
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
                    ReleaseItems = new ObservableCollection<SalesOrderItemReleaseDataViewModel>();

                    // Handle events
                    SelectedCompanies.CollectionChanged += SelectedCompanies_CollectionChanged;
                    SelectedBranches.CollectionChanged += SelectedBranches_CollectionChanged;
                    SelectedPeople.CollectionChanged += SelectedPeople_CollectionChanged;

                    // Prepare data when initialized with a Quote
                    if (InitializeFromQuote())
                        return;

                    // Prepare data when initialized with a SalesOrder
                    if (InitializeFromSalesOrder())
                        return;

                    // Prepare data when a new SalesOrder was requested
                    if (InitializeNewSalesOrder())
                        return;

                    UpdateSummary();
                }
                else
                {
                    SelectedReleaseItems.CollectionChanged += SelectedReleaseItems_CollectionChanged;

                    // Attempt to get the parentViewModel as a DetailViewModel
                    _detailViewModel = parentViewModel as DetailViewModel;
                    if (_detailViewModel == null)
                        return;

                    // Handle events
                    _detailViewModel.SelectedItemsChanging += DetailViewModel_SelectedItemsChanging;

                    // Register the message types this model can send
                    _detailViewModel.AddSendMessageType<SelectedItemsChangedMessage>();
                }
            }
            else
            {
                // The parent view model has been cleared

                // Clean up the model based on whether is it the root model or not
                if (IsRootModel)
                {
                    // Stop handling events
                    SelectedCompanies.CollectionChanged -= SelectedCompanies_CollectionChanged;
                    SelectedBranches.CollectionChanged -= SelectedBranches_CollectionChanged;
                    SelectedPeople.CollectionChanged -= SelectedPeople_CollectionChanged;

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
                    SelectedReleaseItems.CollectionChanged -= SelectedReleaseItems_CollectionChanged;

                    if (_detailViewModel != null)
                    {
                        // Stop handling events
                        _detailViewModel.SelectedItemsChanging -= DetailViewModel_SelectedItemsChanging;

                        // Unregister the message types this model can send
                        _detailViewModel.RemoveSendMessageType<SelectedItemsChangedMessage>();
                    }
                }
            }
        }

        public override void GenerateSampleData()
        {
            base.GenerateSampleData();

            Quote = new Quote();
            SalesOrder = new Shared.DataModel.Sale.SalesOrder();
        }

        public override string ToString()
        {
            if (!IsEmpty)
            {
                if (SalesOrder == null)
                {
                    if (_createNewSalesOrder)
                        return "New Sales Order";

                    if (_initialQuote != null)
                        return string.Format("New Sales Order from Quote {0}", _initialQuote);

                    if (_initialSalesOrder != null)
                        return _initialSalesOrder.ToString();
                }
                else
                {
                    if (IsNew)
                        return (Quote == null ? "New Sales Order" : string.Format("New Sales Order from Quote {0}", Quote));

                    return SalesOrder.ToString();
                }
            }

            return base.ToString();
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<SalesOrderViewModel> builder)
        {
            builder.DataFormLayout()
                .Group("Sales Order", Orientation.Vertical)
                    .ContainsProperty(p => p.Contact)
                    .ContainsProperty(p => p.Summary)
                    .ContainsProperty(p => p.SalesOrderStatus)
                    .ContainsProperty(p => p.AllowPartialDelivery)
                .EndGroup()
                .Group("Customer", Orientation.Vertical)
                    .ContainsProperty(p => p.ContactType)
                    .ContainsProperty(p => p.SelectedCompanies)
                    .ContainsProperty(p => p.SelectedBranches)
                    .ContainsProperty(p => p.SelectedPeople)
                    .ContainsProperty(p => p.SelectContactCommand)
                .EndGroup()
                .Group("Product", Orientation.Vertical)
                    .ContainsProperty(p => p.SalesOrderItems)
                    .ContainsProperty(p => p.SaveSalesOrderCommand)
                .EndGroup()
                .Group("Release", Orientation.Vertical)
                    .ContainsProperty(p => p.ReleaseLabel)
                    .Group("Release Buttons", Orientation.Horizontal)
                        .ContainsProperty(p => p.MarkAllCommand)
                        .ContainsProperty(p => p.MarkSelectedCommand)
                        .ContainsProperty(p => p.ClearReleaseCommand)
                    .EndGroup()
                    .ContainsProperty(p => p.ReleaseItems)
                    .ContainsProperty(p => p.ConfirmReleaseCommand);

            builder.Property(p => p.SelectedCompanies)
                .DisplayName("Select a Company for this Sales Order")
                .AutoGenerated();
            builder.Property(p => p.SelectedBranches)
                .DisplayName("Select a Branch for this Sales Order")
                .AutoGenerated();
            builder.Property(p => p.SelectedPeople)
                .DisplayName("Select a Person for this Sales Order")
                .AutoGenerated();
            builder.Property(p => p.SelectContactCommand)
                .DisplayName("Select Contact");
            builder.Property(p => p.SalesOrderItems)
                .DisplayName("Add or modify the items for this Sales Order")
                .AutoGenerated();
            builder.Property(p => p.SaveSalesOrderCommand)
                .DisplayName("Save Sales Order");
            builder.Property(p => p.ClearReleaseCommand)
                .DisplayName("Clear All Release Quantities");
            builder.Property(p => p.MarkAllCommand)
                .DisplayName("Mark All Items For Release");
            builder.Property(p => p.MarkSelectedCommand)
                .DisplayName("Mark Selected Items For Release");
            builder.Property(p => p.ReleaseItems).AutoGenerated();
            builder.Property(p => p.ConfirmReleaseCommand)
                .DisplayName("Confirm Release");

            builder.Property(p => p.UnitOfWork).NotAutoGenerated();
            builder.Property(p => p.Quote).NotAutoGenerated();
            builder.Property(p => p.SalesOrder).NotAutoGenerated();
            builder.Property(p => p.SelectedContact).NotAutoGenerated();
            builder.Property(p => p.IsContactSelected).NotAutoGenerated();
            builder.Property(p => p.IsNew).NotAutoGenerated();
            builder.Property(p => p.IsSalesOrderModified).NotAutoGenerated();
            builder.Property(p => p.QuantityReleased).NotAutoGenerated();
            builder.Property(p => p.SelectedBins).NotAutoGenerated();
            builder.Property(p => p.SelectedStockTypes).NotAutoGenerated();
            builder.Property(p => p.AreBinsSelected).NotAutoGenerated();
            builder.Property(p => p.AreStockTypesSelected).NotAutoGenerated();
            builder.Property(p => p.SelectedReleaseItems).NotAutoGenerated();
            builder.Property(p => p.AreReleaseItemsSelected).NotAutoGenerated();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<SalesOrderViewModel> builder)
        {
            builder.Condition(c => c != null && c.ContactType == ContactTypes.Company)
                .ContainsProperty(p => p.ContactType)
                .AffectsPropertyVisibility(p => p.SelectedCompanies);

            builder.Condition(c => c != null && c.ContactType == ContactTypes.Branch)
                .ContainsProperty(p => p.ContactType)
                .AffectsPropertyVisibility(p => p.SelectedBranches);

            builder.Condition(c => c != null && c.ContactType == ContactTypes.Person)
                .ContainsProperty(p => p.ContactType)
                .AffectsPropertyVisibility(p => p.SelectedPeople);

            builder.Property(p => p.Contact)
                .ReplaceEditor(new LabelEditorDefinition())
                .HideLabel();

            builder.Property(p => p.SalesOrderStatus)
                .ReplaceEditor(new LabelEditorDefinition())
                .HideLabel();

            builder.Property(p => p.Summary)
                .ReplaceEditor(new LabelEditorDefinition())
                .HideLabel();

            builder.Property(p => p.ContactType)
                .ReplaceEditor(new OptionEditorDefinition(typeof(ContactTypes)))
                .LabelPosition(LayoutItemLabelPosition.Top);

            builder.Property(p => p.SelectedCompanies)
                .ReplaceEditor(new GridEditorDefinition
                {
                    EntityType = typeof(Company),
                    UsePropertyAsSelectedItems = true,
                    IsMultiSelect = false,
                    ShowToolBar = true
                })
                .LabelPosition(LayoutItemLabelPosition.Top);

            builder.Property(p => p.SelectedBranches)
                .ReplaceEditor(new GridEditorDefinition
                {
                    EntityType = typeof(Branch),
                    UsePropertyAsSelectedItems = true,
                    IsMultiSelect = false,
                    ShowToolBar = true
                })
                .LabelPosition(LayoutItemLabelPosition.Top);

            builder.Property(p => p.SelectedPeople)
                .ReplaceEditor(new GridEditorDefinition
                {
                    EntityType = typeof(Person),
                    UsePropertyAsSelectedItems = true,
                    IsMultiSelect = false,
                    ShowToolBar = true
                })
                .LabelPosition(LayoutItemLabelPosition.Top);

            builder.Property(p => p.ReleaseLabel)
                .ReplaceEditor(new LabelEditorDefinition())
                .HideLabel();

            builder.Property(p => p.SelectContactCommand)
                .HideLabel();
            builder.Property(p => p.SaveSalesOrderCommand)
                .HideLabel();
            builder.Property(p => p.ClearReleaseCommand)
                .HideLabel();
            builder.Property(p => p.MarkAllCommand)
                .HideLabel();
            builder.Property(p => p.MarkSelectedCommand)
                .HideLabel();
            builder.Property(p => p.ConfirmReleaseCommand)
                .HideLabel();

            builder.Property(p => p.SalesOrderItems)
                 .ReplaceEditor(new GridEditorDefinition
                 {
                     EntityType = typeof(SalesOrderItem),
                     ItemsSourceMethod = context => ((SalesOrderViewModel)context).SalesOrderItems,
                     ShowToolBar = true,
                     GetUpdateSessionMethod = context => ((SalesOrderViewModel)context).UnitOfWork,
                     BuildNewRowMethod = (context, session) =>
                     {
                         // Generate a new SalesOrderItems for each item in SelectedSkus
                         var salesOrderViewModel = (SalesOrderViewModel)context;
                         return salesOrderViewModel.SelectedSkus.Select(s =>
                             new SalesOrderItem(session)
                             {
                                 Oid = Guid.NewGuid(),
                                 SalesOrder = salesOrderViewModel.SalesOrder,
                                 Sku = session.GetDataObject(s),
                                 Quantity = 1,
                                 CostPrice = s.UnitCost,
                                 SellPrice = s.UnitPrice
                             })
                             .ToList();
                     },
                     UseAddDialog = false
                 })
                 .LabelPosition(LayoutItemLabelPosition.Top);

            builder.Property(p => p.ReleaseItems)
                 .ReplaceEditor(new GridEditorDefinition
                 {
                     EntityType = typeof(SalesOrderItemReleaseDataViewModel),
                     ItemsSourceMethod = context => ((SalesOrderViewModel)context).ReleaseItems,
                     SelectedItemsSourceMethod = context => ((SalesOrderViewModel)context).SelectedReleaseItems
                 })
                 .HideLabel();
        }

        #endregion
    }
}
