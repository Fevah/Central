using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevExpress.Data.Filtering;
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
using TIG.TotalLink.Client.Module.Admin.ViewModel.Document;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Global;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.DataModel.Crm;
using TIG.TotalLink.Shared.DataModel.Inventory;
using TIG.TotalLink.Shared.DataModel.Sale;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Sale;
using TypeExtension = TIG.TotalLink.Client.Core.Extension.TypeExtension;

namespace TIG.TotalLink.Client.Module.Sale.ViewModel.DocumentModel
{
    [DocumentDataModel("Quote", "Contains data for creating and viewing Quotes.")]
    public class QuoteViewModel : DocumentDataModelBase
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
        private readonly Enquiry _initialEnquiry;
        private readonly Quote _initialQuote;
        private readonly bool _createNewQuote;
        private Enquiry _enquiry;
        private Quote _quote;
        private QuoteVersion _newVersion;
        private Contact _contact;
        private QuoteVersion _selectedVersion;
        private bool _isNew;
        private string _versionSummary;
        private bool _areQuoteItemsModified;
        private bool _updatingSelectedVersion;
        private readonly ObservableCollection<Company> _selectedCompanies = new ObservableCollection<Company>();
        private readonly ObservableCollection<Branch> _selectedBranches = new ObservableCollection<Branch>();
        private readonly ObservableCollection<Person> _selectedPeople = new ObservableCollection<Person>();
        private readonly ObservableCollection<Sku> _selectedSkus = new ObservableCollection<Sku>();
        private Contact _selectedContact;
        private ICommand _selectContactCommand;
        private ICommand _saveQuoteCommand;

        #endregion


        #region Constructors

        public QuoteViewModel()
        {
        }

        public QuoteViewModel(ISaleFacade saleFacade)
            : this()
        {
            // Store services
            _saleFacade = saleFacade;
            _saleFacade.Connect(ServiceTypes.Data);
        }

        public QuoteViewModel(ISaleFacade saleFacade, bool createNewQuote)
            : this(saleFacade)
        {
            _createNewQuote = createNewQuote;
            IsEmpty = !createNewQuote;
        }

        public QuoteViewModel(ISaleFacade saleFacade, Enquiry enquiry)
            : this(saleFacade)
        {
            _initialEnquiry = enquiry;
            IsEmpty = false;
        }

        public QuoteViewModel(ISaleFacade saleFacade, Quote quote)
            : this(saleFacade)
        {
            _initialQuote = quote;
            IsEmpty = false;
        }

        #endregion


        #region Mvvm Services

        [Display(AutoGenerateField = false)]
        public IMessageBoxService MessageBoxService { get { return GetService<IMessageBoxService>(); } }

        [Display(AutoGenerateField = false)]
        public IPageViewNavigationService PageViewNavigationService { get { return GetService<IPageViewNavigationService>(); } }

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
        /// Command to save the quote.
        /// </summary>
        [DoNotCopy]
        public ICommand SaveQuoteCommand
        {
            get { return _saveQuoteCommand ?? (_saveQuoteCommand = new DelegateCommand(OnSaveQuoteExecute, OnSaveQuoteCanExecute)); }
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
        /// The type of Contact that the quote will be created for.
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
        /// The enquiry that this quote was created from.
        /// </summary>
        public Enquiry Enquiry
        {
            get { return _enquiry; }
            set { SetProperty(ref _enquiry, value, () => Enquiry); }
        }

        /// <summary>
        /// The quote.
        /// </summary>
        public Quote Quote
        {
            get { return _quote; }
            set
            {
                SetProperty(ref _quote, value, () => Quote, () =>
                    RaisePropertyChanged(() => IsNew)
                );
            }
        }

        /// <summary>
        /// The new version for this quote.
        /// </summary>
        public QuoteVersion NewVersion
        {
            get { return _newVersion; }
            set
            {
                SetProperty(ref _newVersion, value, () => NewVersion, () =>
                    {
                        AreQuoteItemsModified = false;
                        UpdateVersionSummary();
                    });
            }
        }

        /// <summary>
        /// The quote version that is currently displayed.
        /// </summary>
        public QuoteVersion SelectedVersion
        {
            get { return _selectedVersion; }
            set
            {
                var oldSelectedVersion = _selectedVersion;
                SetProperty(ref _selectedVersion, value, () => SelectedVersion, () =>
                {
                    // If the SelectedVersion is being reset, don't process any further
                    if (_updatingSelectedVersion)
                        return;

                    // If the QuoteItems are modified, and the user choose not to lose them, reset SelectedVersion back to the previous value
                    if (!CanChangeVersion())
                    {
                        _updatingSelectedVersion = true;
                        SelectedVersion = oldSelectedVersion;
                        _updatingSelectedVersion = false;
                        return;
                    }

                    // Create a new version and refresh properties
                    CreateNewVersion();
                    RaisePropertyChanged(() => IsVersionSelected);
                });
            }
        }

        /// <summary>
        /// Indicates if a valid version is selected.
        /// </summary>
        public bool IsVersionSelected
        {
            get { return (SelectedVersion != null); }
        }

        /// <summary>
        /// Exposes all items on the quote version.
        /// </summary>
        public XPCollection<QuoteItem> QuoteItems
        {
            get { return (NewVersion != null ? NewVersion.QuoteItems : null); }
        }

        /// <summary>
        /// Indicates if any QuoteItems have been modified on the NewVersion.
        /// </summary>
        public bool AreQuoteItemsModified
        {
            get { return _areQuoteItemsModified; }
            set { SetProperty(ref _areQuoteItemsModified, value, () => AreQuoteItemsModified); }
        }

        /// <summary>
        /// Exposes the contact on the quote.
        /// </summary>
        public Contact Contact
        {
            get { return _contact; }
            set { SetProperty(ref _contact, value, () => Contact); }
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
        /// A summary of the items on the NewVersion.
        /// </summary>
        public string VersionSummary
        {
            get { return _versionSummary; }
            set { SetProperty(ref _versionSummary, value, () => VersionSummary); }
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
        /// Initializes the model when an Enquiry was provided.
        /// </summary>
        /// <returns>True if an Enquiry was provided; otherwise false;</returns>
        private bool InitializeFromEnquiry()
        {
            // Abort if no initialEnquiry was provided
            if (_initialEnquiry == null)
                return false;

            // Create a UnitOfWork
            UnitOfWork = _saleFacade.CreateUnitOfWork();
            UnitOfWork.StartUiTracking(this, true, true, true);

            // Initialize data
            IsNew = true;
            Enquiry = UnitOfWork.GetDataObject(_initialEnquiry);
            Quote = new Quote(UnitOfWork)
            {
                Oid = Guid.NewGuid(),
                Enquiry = Enquiry
            };
            var newVersion = new QuoteVersion(UnitOfWork)
            {
                Oid = Guid.NewGuid(),
                Quote = Quote,
                Version = 1
            };
            newVersion.QuoteItems.CollectionChanged += QuoteItems_CollectionChanged;

            foreach (var enquiryItem in Enquiry.EnquiryItems)
            {
                new QuoteItem(UnitOfWork)
                {
                    Oid = Guid.NewGuid(),
                    QuoteVersion = newVersion,
                    EnquiryItem = enquiryItem,
                    Sku = enquiryItem.Sku,
                    Quantity = enquiryItem.Quantity,
                    CostPrice = enquiryItem.Sku.UnitCost,
                    SellPrice = enquiryItem.Sku.UnitPrice,
                };
            }
            NewVersion = newVersion;
            AreQuoteItemsModified = true;

            // Return true to indicate the the initialization was completed
            return true;
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
            IsNew = false;
            Quote = UnitOfWork.GetDataObject(_initialQuote);
            Enquiry = Quote.Enquiry;
            SelectedVersion = Quote.QuoteVersions.OrderByDescending(v => v.Version).FirstOrDefault();
            Contact = Quote.Contact;
            CreateNewVersion();

            // Return true to indicate the the initialization was completed
            return true;
        }

        /// <summary>
        /// Initializes the model when a new Quote was requested.
        /// </summary>
        /// <returns>True if a new Quote was requested; otherwise false;</returns>
        private bool InitializeNewQuote()
        {
            // Abort if the flag was not set to create a new Quote
            if (!_createNewQuote)
                return false;

            // Create a UnitOfWork
            UnitOfWork = _saleFacade.CreateUnitOfWork();
            UnitOfWork.StartUiTracking(this, true, true, true);

            // Initialize data
            IsNew = true;
            Quote = new Quote(UnitOfWork)
            {
                Oid = Guid.NewGuid()
            };
            NewVersion = new QuoteVersion(UnitOfWork)
            {
                Oid = Guid.NewGuid(),
                Quote = Quote,
                Version = 1
            };
            NewVersion.QuoteItems.CollectionChanged += QuoteItems_CollectionChanged;

            // Return true to indicate the the initialization was completed
            return true;
        }

        /// <summary>
        /// Deletes an incomplete NewVersion.
        /// </summary>
        private void DeleteNewVersion()
        {
            if (NewVersion != null && !ReferenceEquals(NewVersion, SelectedVersion))
            {
                NewVersion.QuoteItems.CollectionChanged += QuoteItems_CollectionChanged;
                NewVersion.Delete();
            }
        }

        /// <summary>
        /// Creates a new QuoteVersion from the SelectedVersion.
        /// </summary>
        private void CreateNewVersion()
        {
            // Abort if this is not the root model
            if (!IsRootModel)
                return;

            // If we already have an incomplete NewVersion, delete it
            DeleteNewVersion();

            // Create the NewVersion
            var lastVersion = Quote.QuoteVersions.OrderByDescending(v => v.Version).FirstOrDefault();
            var newVersion = new QuoteVersion(UnitOfWork)
            {
                Oid = Guid.NewGuid(),
                Quote = Quote,
                Version = (lastVersion != null ? lastVersion.Version + 1 : 1)
            };
            newVersion.QuoteItems.CollectionChanged += QuoteItems_CollectionChanged;

            // If another version is selected, copy all items to the NewVersion
            if (IsVersionSelected)
            {
                foreach (var quoteItem in SelectedVersion.QuoteItems)
                {
                    var newQuoteItem = new QuoteItem(UnitOfWork)
                    {
                        Oid = Guid.NewGuid(),
                        QuoteVersion = newVersion,
                        Sku = UnitOfWork.GetDataObject(quoteItem.Sku),
                        Quantity = quoteItem.Quantity,
                        CostPrice = quoteItem.CostPrice,
                        SellPrice = quoteItem.SellPrice,
                    };
                }
            }

            // Assign the NewVersion
            NewVersion = newVersion;
            UpdateDocumentId();
        }

        /// <summary>
        /// Flags that the DocumentId may have changed.
        /// </summary>
        private void UpdateDocumentId()
        {
            // Abort if this is not the root model
            if (!IsRootModel)
                return;

            // Call the DocumentViewModel to update its title
            var documentViewModel = (DocumentViewModel)((ISupportParentViewModel)this).ParentViewModel;
            documentViewModel.UpdateDocumentId();
        }

        /// <summary>
        /// Update the value of the VersionSummary.
        /// </summary>
        private void UpdateVersionSummary()
        {
            // Abort if this is not the root model
            if (!IsRootModel)
                return;

            // Build a summary of the NewVersion
            var totalQuantity = 0;
            var totalSellPrice = 0m;
            if (NewVersion != null)
            {
                totalQuantity = NewVersion.TotalQuantity;
                totalSellPrice = NewVersion.TotalSellPrice;
            }

            VersionSummary = string.Format("{0} {1} for {2:C}", totalQuantity, totalQuantity.Pluralize("item"), totalSellPrice);
        }

        /// <summary>
        /// Displays a warning if any of the QuoteItems on the NewVersion have been modified.
        /// </summary>
        /// <returns>True if the QuoteItems have not changed or the user accepts that changes will be lost; otherwise false.</returns>
        private bool CanChangeVersion()
        {
            // Abort if this is not the root model
            if (!IsRootModel)
                return true;

            // Abort if the QuoteItems have not been modified
            if (!AreQuoteItemsModified)
                return true;

            // Show a message box to determine if the version change should continue
            return (MessageBoxService.Show("If you change to another version, all modifications to the Quote Items will be lost!\r\n\r\nDo you want to continue?", "Quote Version", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes);
        }

        /// <summary>
        /// Generates a title for this quote.
        /// </summary>
        /// <param name="includeVersion">Indicates if the version number should be included.</param>
        /// <returns>A string representing the displayed quote.</returns>
        private string GetTitle(bool includeVersion)
        {
            if (!IsEmpty)
            {
                if (Quote == null)
                {
                    if (_createNewQuote)
                        return "New Quote";

                    if (_initialEnquiry != null)
                        return string.Format("New Quote from Enquiry {0}", _initialEnquiry);

                    if (_initialQuote != null)
                    {
                        return includeVersion
                            ? _initialQuote.ToString()
                            : _initialQuote.BaseToString();
                    }
                }
                else
                {
                    if (IsNew)
                        return (Enquiry == null ? "New Quote" : string.Format("New Quote from Enquiry {0}", Enquiry));

                    return includeVersion
                        ? string.Format("{0}.{1}", Quote.BaseToString(), (SelectedVersion != null ? SelectedVersion.Version : NewVersion.Version))
                        : Quote.BaseToString();
                }
            }

            return base.ToString();
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
                Quote.Contact = Contact;
                EntityChangedMessage.Send(this, Quote, EntityChange.ChangeTypes.Modify);
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
        /// Execute method for the SaveQuoteCommand.
        /// </summary>
        private void OnSaveQuoteExecute()
        {
            // Only save if this is not a template
            if (!IsEmpty)
            {
                // If the Quote hasn't been saved yet...
                if (IsNew)
                {
                    // Generate a reference number for it
                    Quote.GenerateReferenceNumber();

                    // If this is a new Quote from an Enquiry, mark the Enquiry as completed
                    // TODO : This should find the last EnquiryStatus based on Order instead of finding it by Name
                    if (Enquiry != null)
                        Enquiry.Status = UnitOfWork.Query<EnquiryStatus>().FirstOrDefault(s => s.Name == "Completed");
                }

                // Commit all changes
                UnitOfWork.CommitChanges();

                // Cycle to the next new quote version
                AreQuoteItemsModified = false;
                SelectedVersion = NewVersion;

                // Flag to indicate that the Quote has now been saved
                IsNew = false;
            }

            // Move to the next page if one is available
            if (PageViewNavigationService != null && PageViewNavigationService.CanGoForward)
                PageViewNavigationService.GoForward();
        }

        /// <summary>
        /// CanExecute method for the SaveQuoteCommand.
        /// </summary>
        private bool OnSaveQuoteCanExecute()
        {
            return NewVersion != null && NewVersion.QuoteItems.Count > 0 && AreQuoteItemsModified;
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

            // Get the type of the list that contains the selected items
            var selectedItemListType = e.SelectedItemsChangedMessage.SelectedItems.GetType();

            // Only process the selected items list when it is a generic list and we can determine what type it contains
            if (TypeExtension.IsAssignableFromGeneric(typeof(List<>), selectedItemListType))
            {
                TypeSwitch.On(selectedItemListType.GenericTypeArguments[0])
                    .Case<Sku>(() =>
                    {
                        // Process selected Skus
                        var selectedSkus = e.SelectedItemsChangedMessage.SelectedItems.OfType<Sku>().ToList();
                        selectedSkus.SyncTo(SelectedSkus);
                    });
            }
        }

        /// <summary>
        /// Handles the CollectionChanged event on NewVersion.QuoteItems.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void QuoteItems_CollectionChanged(object sender, XPCollectionChangedEventArgs e)
        {
            // Attach or remove PropertyChanged events on each QuoteItem as they are added or removed from NewVersion.QuoteItems
            switch (e.CollectionChangedType)
            {
                case XPCollectionChangedType.AfterAdd:
                    ((INotifyPropertyChanged)e.ChangedObject).PropertyChanged += QuoteItem_PropertyChanged;
                    AreQuoteItemsModified = true;
                    UpdateVersionSummary();
                    break;

                case XPCollectionChangedType.AfterRemove:
                    ((INotifyPropertyChanged)e.ChangedObject).PropertyChanged -= QuoteItem_PropertyChanged;
                    AreQuoteItemsModified = true;
                    UpdateVersionSummary();
                    break;
            }
        }

        /// <summary>
        /// Handles the PropertyChanged event on each item in NewVersion.QuoteItems.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void QuoteItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            AreQuoteItemsModified = true;
            UpdateVersionSummary();
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
                    // Handle events
                    SelectedCompanies.CollectionChanged += SelectedCompanies_CollectionChanged;
                    SelectedBranches.CollectionChanged += SelectedBranches_CollectionChanged;
                    SelectedPeople.CollectionChanged += SelectedPeople_CollectionChanged;

                    // Prepare data when initialized with an Enquiry
                    if (InitializeFromEnquiry())
                        return;

                    // Prepare data when initialized with a Quote
                    if (InitializeFromQuote())
                        return;

                    // Prepare data when a new Quote was requested
                    if (InitializeNewQuote())
                        return;

                    UpdateVersionSummary();
                }
                else
                {
                    // Attempt to get the parentViewModel as a DetailViewModel
                    _detailViewModel = parentViewModel as DetailViewModel;
                    if (_detailViewModel == null)
                        return;

                    // Handle events
                    _detailViewModel.SelectedItemsChanging += DetailViewModel_SelectedItemsChanging;
                }
            }
            else
            {
                // The parent view model has been cleared

                // Clean up the model based on whether is it the root model or not
                if (IsRootModel)
                {
                    // Delete the NewVersion.
                    // The NewVersion will be thrown away when the UnitOfWork is disposed, but explicitly deleting it
                    // will remove each QuoteItem from NewVersion.QuoteItems and ensure all event handlers are detached.
                    DeleteNewVersion();

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
                    // Stop handling events
                    _detailViewModel.SelectedItemsChanging -= DetailViewModel_SelectedItemsChanging;
                }
            }
        }

        public override void GenerateSampleData()
        {
            base.GenerateSampleData();

            Enquiry = new Enquiry();
            Quote = new Quote();
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
        public static void BuildMetadata(MetadataBuilder<QuoteViewModel> builder)
        {
            builder.DataFormLayout()
                .Group("Quote", Orientation.Vertical)
                    .ContainsProperty(p => p.Contact)
                    .ContainsProperty(p => p.VersionSummary)
                    .ContainsProperty(p => p.SelectedVersion)
                .EndGroup()
                .Group("Customer", Orientation.Vertical)
                    .ContainsProperty(p => p.ContactType)
                    .ContainsProperty(p => p.SelectedCompanies)
                    .ContainsProperty(p => p.SelectedBranches)
                    .ContainsProperty(p => p.SelectedPeople)
                    .ContainsProperty(p => p.SelectContactCommand)
                .EndGroup()
                .Group("Product", Orientation.Vertical)
                    .ContainsProperty(p => p.QuoteItems)
                    .ContainsProperty(p => p.SaveQuoteCommand);

            builder.Property(p => p.SelectedCompanies)
                .DisplayName("Select a Company for this Quote")
                .AutoGenerated();
            builder.Property(p => p.SelectedBranches)
                .DisplayName("Select a Branch for this Quote")
                .AutoGenerated();
            builder.Property(p => p.SelectedPeople)
                .DisplayName("Select a Person for this Quote")
                .AutoGenerated();
            builder.Property(p => p.SelectContactCommand)
                .DisplayName("Select Contact");
            builder.Property(p => p.QuoteItems)
                .DisplayName("Add or modify the items for this Quote")
                .AutoGenerated();
            builder.Property(p => p.SaveQuoteCommand)
                .DisplayName("Save Quote");

            builder.Property(p => p.SelectedVersion).DisplayName("Version");
            builder.Property(p => p.IsVersionSelected).NotAutoGenerated();
            builder.Property(p => p.UnitOfWork).NotAutoGenerated();
            builder.Property(p => p.Enquiry).NotAutoGenerated();
            builder.Property(p => p.Quote).NotAutoGenerated();
            builder.Property(p => p.NewVersion).NotAutoGenerated();
            builder.Property(p => p.SelectedContact).NotAutoGenerated();
            builder.Property(p => p.IsContactSelected).NotAutoGenerated();
            builder.Property(p => p.IsNew).NotAutoGenerated();
            builder.Property(p => p.AreQuoteItemsModified).NotAutoGenerated();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<QuoteViewModel> builder)
        {
            builder.Condition(i => i != null && i.IsVersionSelected)
                .ContainsProperty(p => p.IsVersionSelected)
                .AffectsPropertyEnabled(p => p.SelectedVersion);

            builder.Condition(i => i != null && i.ContactType == ContactTypes.Company)
                .ContainsProperty(p => p.ContactType)
                .AffectsPropertyVisibility(p => p.SelectedCompanies);

            builder.Condition(i => i != null && i.ContactType == ContactTypes.Branch)
                .ContainsProperty(p => p.ContactType)
                .AffectsPropertyVisibility(p => p.SelectedBranches);

            builder.Condition(i => i != null && i.ContactType == ContactTypes.Person)
                .ContainsProperty(p => p.ContactType)
                .AffectsPropertyVisibility(p => p.SelectedPeople);

            builder.Condition(i => true)
                .ContainsProperty(c => c.NewVersion)
                .InvokesInstanceMethod(p => p.QuoteItems, new Action<QuoteViewModel, GridEditorDefinition>((context, editor) =>
                    editor.RefreshItemsSourceMethod()
                ));

            builder.Property(p => p.Contact)
                .ReplaceEditor(new LabelEditorDefinition())
                .HideLabel();

            builder.Property(p => p.VersionSummary)
                .ReplaceEditor(new LabelEditorDefinition())
                .HideLabel();

            builder.Property(p => p.SelectedVersion).GetEditor<LookUpEditorDefinition>()
                .FilterMethod = context => CriteriaOperator.Parse("Quote.Oid=?", ((QuoteViewModel)context).Quote.Oid);

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

            builder.Property(p => p.SelectContactCommand)
                .HideLabel();
            builder.Property(p => p.SaveQuoteCommand)
                .HideLabel();

            builder.Property(p => p.QuoteItems)
                 .ReplaceEditor(new GridEditorDefinition
                 {
                     EntityType = typeof(QuoteItem),
                     ItemsSourceMethod = context => ((QuoteViewModel)context).QuoteItems,
                     ShowToolBar = true,
                     GetUpdateSessionMethod = context => ((QuoteViewModel)context).UnitOfWork,
                     BuildNewRowMethod = (context, session) =>
                     {
                         // Generate a new QuoteItem for each item in SelectedSkus
                         var quoteViewModel = (QuoteViewModel)context;
                         return quoteViewModel.SelectedSkus.Select(s =>
                             new QuoteItem(session)
                             {
                                 Oid = Guid.NewGuid(),
                                 QuoteVersion = quoteViewModel.NewVersion,
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
        }

        #endregion
    }
}
