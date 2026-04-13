using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AutoMapper;
using DevExpress.Mvvm;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Core.Command;
using TIG.TotalLink.Client.Core.Converter;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Core.Interface.MVVMService;
using TIG.TotalLink.Client.Core.Message;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Client.Module.Admin.Enum.KeyedData;
using TIG.TotalLink.Client.Module.Admin.Helper;
using TIG.TotalLink.Client.Module.Admin.Interface;
using TIG.TotalLink.Client.Module.Admin.Message;
using TIG.TotalLink.Client.Module.Admin.MvvmService;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon;
using TIG.TotalLink.Client.Undo.AppContext;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.Facade.Admin;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Document
{
    public class DocumentViewModel : EntityViewModelBase<Shared.DataModel.Admin.Document>, IDocumentContent, ISupportLayoutData
    {
        #region Public Events

        public delegate void DocumentLoadedEventHandler(object sender, EventArgs e);
        public delegate void DocumentClosingEventHandler(object sender, CancelEventArgs e);

        public event DocumentLoadedEventHandler DocumentLoaded;
        public event DocumentClosingEventHandler DocumentClosing;

        #endregion


        #region Private Fields

        private static readonly GuidToNameStringConverter GuidToNameStringConverter = new GuidToNameStringConverter();
        private readonly IAdminFacade _adminFacade;
        private Guid _tempOid;
        private IDocument _document;
        private bool _isLoadingPanelVisible;
        private bool _isModified;
        private bool _ignoreModifications;
        private int _ignoreModificationsCount;
        private bool _closeAfterSave;
        private PanelGroupViewModel _activeGroup;
        private readonly ObservableCollection<PanelGroupViewModel> _panelGroups = new ObservableCollection<PanelGroupViewModel>();
        private readonly ObservableCollection<PanelViewModel> _panels = new ObservableCollection<PanelViewModel>();
        private readonly ObservableCollection<RibbonGroupViewModel> _ribbonGroups = new ObservableCollection<RibbonGroupViewModel>();
        private readonly ObservableCollection<PanelViewModel> _filteredPanels = new ObservableCollection<PanelViewModel>();
        private UnitOfWork _unitOfWork;
        private InitializeDocumentMessage _initializeDocumentMessage;

        #endregion


        #region Constructors

        public DocumentViewModel()
        {
            // Assign a temporary id for this document until it is loaded or saved
            _tempOid = Guid.NewGuid();

            // Initialize commands
            DocumentModifiedCommand = new DelegateCommand(OnDocumentModifiedExecute);
            AddPanelCommand = new DelegateCommand(OnAddPanelExecute, OnAddPanelCanExecute);
            AddPanelGroupCommand = new DelegateCommand(OnAddPanelGroupExecute, OnAddPanelGroupCanExecute);
            EditDocumentCommand = new DelegateCommand(OnEditDocumentExecute, OnEditDocumentCanExecute);
            SaveDocumentCommand = new AsyncCommandEx<bool>(OnSaveDocumentExecuteAsync, OnSaveDocumentCanExecute);

            // Handle events
            _panels.CollectionChanged += Panels_CollectionChanged;

            // Initialize messages
            Messenger.Default.Register<EntityChangedMessage>(this, OnEntityChangedMessage);
        }

        public DocumentViewModel(IAdminFacade adminFacade)
            : this()
        {
            // Store services
            _adminFacade = adminFacade;
        }

        #endregion


        #region Mvvm Services

        private IDocumentManagerService DocumentManager { get { return GetService<IDocumentManagerService>(); } }

        private IMessageBoxService MessageBoxService { get { return GetService<IMessageBoxService>(); } }

        private IRenameDialogService RenameDialogService { get { return GetService<IRenameDialogService>(); } }

        private IDetailDialogService DetailDialogService { get { return GetService<IDetailDialogService>(); } }

        #endregion


        #region Commands

        /// <summary>
        /// Command to activate the ribbon page for this document.
        /// </summary>
        public ICommand ActivateDocumentRibbonCommand { get; set; }

        /// <summary>
        /// Command to flag that the document has been modified.
        /// This should be called when any change occurs to the document layout that requires it to be re-saved.
        /// </summary>
        public ICommand DocumentModifiedCommand { get; set; }

        /// <summary>
        /// Command to add a new panel to the document.
        /// </summary>
        public ICommand AddPanelCommand { get; private set; }

        /// <summary>
        /// Command to add a new panel group to the document.
        /// </summary>
        public ICommand AddPanelGroupCommand { get; private set; }

        /// <summary>
        /// Command to edit the document.
        /// </summary>
        public ICommand EditDocumentCommand { get; private set; }

        /// <summary>
        /// Command to save the document.
        /// </summary>
        public ICommand SaveDocumentCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// DocumentId that is used for messaging.
        /// </summary>
        public object DocumentId
        {
            get
            {
                if (IsInitializedWithItem)
                    return string.Format("{0}_{1}", Oid, _initializeDocumentMessage).ToControlName();

                return Oid;
            }
        }

        /// <summary>
        /// The main UnitOfWork that handles loading and saving Documents and Panels.
        /// </summary>
        public UnitOfWork UnitOfWork
        {
            get { return _unitOfWork; }
        }

        /// <summary>
        /// Indicates if this document has been saved yet.
        /// </summary>
        public bool IsNew
        {
            get { return DataObject == null || DataObject.Oid == Guid.Empty; }
        }

        /// <summary>
        /// Indicates if this document contains one fixed widget.
        /// </summary>
        public bool IsFixed { get; private set; }

        /// <summary>
        /// Indicates if this document was intialized with an InitializeDocumentMessage.
        /// </summary>
        public bool IsInitializedWithItem
        {
            get { return (_initializeDocumentMessage != null); }
        }

        /// <summary>
        /// Indicates if the loading panel is visible.
        /// </summary>
        public bool IsLoadingPanelVisible
        {
            get { return _isLoadingPanelVisible; }
            set { SetProperty(ref _isLoadingPanelVisible, value, () => IsLoadingPanelVisible, () => RaisePropertyChanged(() => Title)); }
        }

        /// <summary>
        /// Indicates if the document has been modified.
        /// </summary>
        public bool IsModified
        {
            get { return _isModified; }
            set
            {
                // Abort if the value hasn't changed, or the value is being set to true and ignoreModifications = true
                if (value == _isModified || (value && _ignoreModifications))
                    return;

                SetProperty(ref _isModified, value, () => IsModified, () => RaisePropertyChanged(() => Title));

                //System.Diagnostics.Debug.WriteLine("IsModified = {0}", _isModified);
            }
        }

        /// <summary>
        /// Indicates if the document should temporarily ignore changes to the document and panels.
        /// Note that calls to this property are counted each time the value is set.
        /// For each call to IgnoreModifications = false, there must be a matching call to IgnoreModifications = true.
        /// </summary>
        public bool IgnoreModifications
        {
            get { return _ignoreModifications; }
            set
            {
                // Increment or decrement the ignoreModificationsCount based on the new value
                if (value)
                    _ignoreModificationsCount++;
                else
                    _ignoreModificationsCount = Math.Max(_ignoreModificationsCount - 1, 0);

                // Only set the property if value = true, or value = false and ignoreModificationsCount = 0
                if (value || _ignoreModificationsCount == 0)
                    SetProperty(ref _ignoreModifications, value, () => IgnoreModifications);

                //System.Diagnostics.Debug.WriteLine("IgnoreModifications = {0}  Count = {1}", _ignoreModifications, _ignoreModificationsCount);
            }
        }

        /// <summary>
        /// All panel groups that this document contains.
        /// </summary>
        [AssignParentViewModel]
        [SyncFromDataObject]
        public ObservableCollection<PanelGroupViewModel> PanelGroups
        {
            get { return _panelGroups; }
        }

        /// <summary>
        /// All panels that this document contains.
        /// </summary>
        [AssignParentViewModel]
        [SyncFromDataObject]
        public ObservableCollection<PanelViewModel> Panels
        {
            get { return _panels; }
        }

        /// <summary>
        /// All RibbonGroups that are contained in widgets within this Document.
        /// </summary>
        public ObservableCollection<RibbonGroupViewModel> RibbonGroups
        {
            get { return _ribbonGroups; }
        }

        /// <summary>
        /// The subset of panels that belong to the flat view.
        /// </summary>
        public ObservableCollection<PanelViewModel> FilteredPanels
        {
            get { return _filteredPanels; }
        }

        /// <summary>
        /// The active group.
        /// </summary>
        public PanelGroupViewModel ActiveGroup
        {
            get { return _activeGroup; }
            set { SetProperty(ref _activeGroup, value, () => ActiveGroup, RefreshRibbon); }
        }

        #endregion


        #region Private Properties

        /// <summary>
        /// The DevExpress Document that contains this viewmodel.
        /// </summary>
        private IDocument Document
        {
            get
            {
                if (_document == null)
                    _document = DocumentManager.FindDocument(this);

                return _document;
            }
        }

        /// <summary>
        /// Indicates if a document command can be executed, based on whether any related operations are in progress.
        /// </summary>
        public bool CanExecuteDocumentCommand
        {
            get
            {
                return !(
                       ((AsyncCommandEx<bool>)SaveDocumentCommand).IsExecuting
                    || AppUndoRootViewModel.Instance.UndoRoot.IsUndoingOrRedoing
                    );
            }
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Called when the document is loaded.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected virtual void OnDocumentLoaded(EventArgs e)
        {
            if (DocumentLoaded != null)
                DocumentLoaded(this, e);
        }

        /// <summary>
        /// Called when the document is closing.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected virtual void OnDocumentClosing(CancelEventArgs e)
        {
            if (DocumentClosing != null)
                DocumentClosing(this, e);
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Saves the document asynchronously.
        /// </summary>
        public async Task SaveAsync()
        {
            await SaveAsync(null);
        }

        /// <summary>
        /// Saves the document asynchronously with a new name.
        /// </summary>
        public async Task SaveAsync(string newName)
        {
            IgnoreModifications = true;

            // If a new name was supplied, create a copy of the document and all contained panels
            if (!string.IsNullOrWhiteSpace(newName))
            {
                if (IsNew)
                {
                    // If the document hasn't been saved yet, just rename it
                    DataObject.Name = newName;
                }
                else
                {
                    // Store the old panel layout
                    var panelLayout = GetLayout();

                    // Store the old UnitOfWork, and create a new UnitOfWork to hold the cloned items
                    var oldUnitOfWork = _unitOfWork;
                    CreateUnitOfWork();

                    // Clone the document
                    var newDocumentDataObject = DataObject.Clone(_unitOfWork, "Oid", "Name", "Panels", "PanelGroups", "DocumentDatas", "DocumentActions") as Shared.DataModel.Admin.Document;
                    if (newDocumentDataObject == null)
                        throw new Exception("Error saving document!\r\nFailed to clone Document.");

                    // Clone all document data (except the panel layout)
                    foreach (var oldDocumentDataDataObject in DataObject.DocumentDatas.Where(d => d.GroupKey != KeyedDataGroupKeys.PanelLayout.ToString()))
                    {
                        // Clone the document data
                        var newDocumentDataDataObject = oldDocumentDataDataObject.Clone(_unitOfWork, "Oid", "Document") as DocumentData;
                        if (newDocumentDataDataObject == null)
                            throw new Exception("Error saving document!\r\nFailed to clone DocumentData.");

                        // Add the new document data to the new document
                        newDocumentDataObject.DocumentDatas.Add(newDocumentDataDataObject);
                    }

                    // Apply properties to the cloned document
                    newDocumentDataObject.Name = newName;

                    // Clone all panel groups
                    var panelGroupOids = new Dictionary<Guid, Guid>();
                    var newPanelGroupViewModels = new List<PanelGroupViewModel>();
                    foreach (var oldPanelGroupViewModel in PanelGroups)
                    {
                        // Clone the panel group
                        var newPanelGroupDataObject = oldPanelGroupViewModel.DataObject.Clone(_unitOfWork, "Oid", "Document", "Panels") as PanelGroup;
                        if (newPanelGroupDataObject == null)
                            throw new Exception("Error saving document!\r\nFailed to clone PanelGroup.");

                        // Add the new panel group to the new document
                        newDocumentDataObject.PanelGroups.Add(newPanelGroupDataObject);

                        // Convert the data object to a viewmodel and store it
                        var newPanelGroupViewModel = Mapper.Map<PanelGroupViewModel>(newPanelGroupDataObject);
                        newPanelGroupViewModels.Add(newPanelGroupViewModel);

                        // Store the oids of the old and new panel group so panels can be re-attached later
                        panelGroupOids.Add(oldPanelGroupViewModel.Oid, newPanelGroupViewModel.Oid);
                    }

                    // Clone all panels
                    var panelOids = new Dictionary<Guid, Guid>();
                    var newPanelViewModels = new List<PanelViewModel>();
                    foreach (var oldPanelViewModel in Panels)
                    {
                        // Clone the panel
                        var newPanelDataObject = oldPanelViewModel.DataObject.Clone(_unitOfWork, "Oid", "Document", "PanelDatas") as Panel;
                        if (newPanelDataObject == null)
                            throw new Exception("Error saving document!\r\nFailed to clone Panel.");

                        // Clone all panel data
                        foreach (var oldPanelDataDataObject in oldPanelViewModel.DataObject.PanelDatas)
                        {
                            // Clone the panel data
                            var newPanelDataDataObject =
                                oldPanelDataDataObject.Clone(_unitOfWork, "Oid", "Panel", "PanelGroup") as PanelData;
                            if (newPanelDataDataObject == null)
                                throw new Exception("Error saving document!\r\nFailed to clone PanelData.");

                            // Add the new panel data to the new panel
                            newPanelDataObject.PanelDatas.Add(newPanelDataDataObject);
                        }

                        // Add the new panel to the correct new panel group
                        if (oldPanelViewModel.PanelGroup != null)
                        {
                            Guid newPanelGroupOid;
                            if (panelGroupOids.TryGetValue(oldPanelViewModel.PanelGroup.Oid, out newPanelGroupOid))
                            {
                                var newPanelGroupViewModel = newPanelGroupViewModels.FirstOrDefault(g => g.Oid == newPanelGroupOid);
                                if (newPanelGroupViewModel != null)
                                    newPanelGroupViewModel.DataObject.Panels.Add(newPanelDataObject);
                            }
                        }

                        // Add the new panel to the new document
                        newDocumentDataObject.Panels.Add(newPanelDataObject);

                        // Convert the data object to a viewmodel and store it
                        var newPanelViewModel = Mapper.Map<PanelViewModel>(newPanelDataObject);
                        newPanelViewModels.Add(newPanelViewModel);

                        // Store the oids of the old and new panel for layout conversion later
                        panelOids.Add(oldPanelViewModel.Oid, newPanelViewModel.Oid);
                    }

                    // Store the old oid for the document and assign a new tempOid
                    var oldDocumentOid = Oid;
                    _tempOid = Guid.NewGuid();

                    // Update the oids in the the panel layout
                    panelLayout = ConvertPanelLayout(panelLayout,
                        new Dictionary<Guid, Guid>() { { oldDocumentOid, _tempOid } }.Concat(panelGroupOids)
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value), panelOids);

                    // Apply the new document
                    DataObject = newDocumentDataObject;

                    // Clear the old panel group viewmodels and replace them with the new panel group viewmodels
                    PanelGroups.Clear();
                    foreach (var newPanelGroupViewModel in newPanelGroupViewModels)
                    {
                        PanelGroups.Add(newPanelGroupViewModel);
                    }

                    // Force all panels to disconnect from the UI, then clear the old panel viewmodels and replace them with the new panel viewmodels
                    foreach (var oldPanelViewModel in Panels)
                    {
                        oldPanelViewModel.Release();
                    }
                    Panels.Clear();
                    foreach (var newPanelViewModel in newPanelViewModels)
                    {
                        Panels.Add(newPanelViewModel);
                    }

                    // Apply the panel layout to the new document
                    SetLayout(panelLayout);

                    // Discard all changes to the old document
                    DisposeUnitOfWork(oldUnitOfWork);
                }
            }

            // Commit the changes
            // (This first save is necessary before updating keyed data so that the document and panels have their temp Oids replaced with real Oids)
            await _unitOfWork.CommitChangesAsync();

            // Save all keyed data
            await Application.Current.Dispatcher.InvokeAsync(SaveKeyedData);

            // Commit the changes
            // (This second save will write any changes to the keyed data)
            await _unitOfWork.CommitChangesAsync();

            // Flag the document as unmodified
            IgnoreModifications = false;
            IsModified = false;

            // If the closeAfterSave flag was set, close the document
            if (_closeAfterSave)
            {
                _closeAfterSave = false;
                Application.Current.Dispatcher.Invoke(() =>
                    Document.Close()
                );
            }
        }

        /// <summary>
        /// Adds or updates keyed data.
        /// </summary>
        /// <param name="groupKey">A string key that defines the type of data being stored.</param>
        /// <param name="itemKey">A string key that defines a unique name within the group for the data item.</param>
        /// <param name="data">A Stream containing the data.</param>
        /// <param name="closeStream">Indicates if the stream should be closed.</param>
        public void SetKeyedData(KeyedDataGroupKeys groupKey, string itemKey, Stream data, bool closeStream = true)
        {
            KeyedDataHelper.SetData(DataObject.DocumentDatas, groupKey, itemKey, data, closeStream);
        }

        /// <summary>
        /// Gets keyed data.
        /// </summary>
        /// <param name="groupKey">A string key that defines the type of data being stored.</param>
        /// <param name="itemKey">A string key that defines a unique name within the group for the data item.</param>
        /// <returns>
        /// A new MemoryStream containing the data.
        /// After the stream has been used, it should be disposed.
        /// </returns>
        public MemoryStream GetKeyedData(KeyedDataGroupKeys groupKey, string itemKey)
        {
            return KeyedDataHelper.GetData(DataObject.DocumentDatas, groupKey, itemKey);
        }

        /// <summary>
        /// Removes keyed data.
        /// </summary>
        /// <param name="groupKey">A string key that defines the type of data being stored.</param>
        /// <param name="itemKey">A string key that defines a unique name within the group for the data item.</param>
        /// <returns>True if the keyed data was found and removed successfully; otherwise false.</returns>
        public bool RemoveKeyedData(KeyedDataGroupKeys groupKey, string itemKey)
        {
            return KeyedDataHelper.RemoveData(DataObject.DocumentDatas, groupKey, itemKey);
        }

        /// <summary>
        /// Sends an InitializeDocumentMessage to all contained widgets if one has been stored
        /// </summary>
        public void SendInitializeDocument()
        {
            if (IsInitializedWithItem)
                Messenger.Default.Send(_initializeDocumentMessage, DocumentId);
        }

        /// <summary>
        /// Flags that the DocumentId may have changed.
        /// </summary>
        public void UpdateDocumentId()
        {
            Document.Id = DocumentId;
            RaisePropertiesChanged(() => DocumentId, () => Title);
        }

        /// <summary>
        /// Refreshes WidgetLinkData in all contained widgets.
        /// </summary>
        public void RefreshWidgetLinkData()
        {
            foreach (var panel in Panels)
            {
                var element = panel.Content as FrameworkElement;
                if (element == null)
                    continue;

                var widget = element.DataContext as WidgetViewModelBase;
                if (widget == null)
                    continue;

                widget.RefreshWidgetLinkData();
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Raises the DocumentLoaded event.
        /// </summary>
        private void RaiseDocumentLoaded()
        {
            // Abort if no DataObject was ever loaded or created
            if (DataObject == null)
                return;

            // Apply all keyed data
            ApplyKeyedData();

            // Update the ribbon
            RefreshRibbon();

            // Hide the loading panel
            IsLoadingPanelVisible = false;

            // Raise the DocumentLoaded event
            OnDocumentLoaded(new EventArgs());

            // If an InitializeDocumentMessage has been stored, send it to all contained widgets
            SendInitializeDocument();
        }

        /// <summary>
        /// Raises the DocumentClosing event.
        /// </summary>
        private void RaiseDocumentClosing(CancelEventArgs e)
        {
            OnDocumentClosing(e);
        }

        /// <summary>
        /// Updates and saves all keyed data.
        /// </summary>
        private void SaveKeyedData()
        {
            // Update keyed data on all panels
            foreach (var panelViewModel in Panels.ToList())
            {
                panelViewModel.StoreKeyedData();
            }

            // Store the panel layout
            SetKeyedData(KeyedDataGroupKeys.PanelLayout, null, GetLayout());
        }

        /// <summary>
        /// Applies all keyed data.
        /// </summary>
        private void ApplyKeyedData()
        {
            IgnoreModifications = true;

            // Restore the panel layout
            SetLayout(GetKeyedData(KeyedDataGroupKeys.PanelLayout, null));

            IgnoreModifications = false;
        }

        /// <summary>
        /// Converts a panel layout from one document to be used by another document, by updating the oids in the layout.
        /// </summary>
        /// <param name="layoutStream">A stream containing the layout to convert.</param>
        /// <param name="layoutOids">A dictionary that contains the old and new oids for the document and all panel groups.</param>
        /// <param name="panelOids">A dictionary that contains the old and new oids for all panels.</param>
        /// <returns>A new stream containing the updated layout.</returns>
        private static Stream ConvertPanelLayout(Stream layoutStream, Dictionary<Guid, Guid> layoutOids, Dictionary<Guid, Guid> panelOids)
        {
            // Get the old layouts in a dictionary, and abort if it is null
            var oldLayouts = DocumentLayoutHelper.ConvertFromStream(layoutStream);
            if (oldLayouts == null)
                return null;

            // Create another dictionary for the new layouts
            var newLayouts = new Dictionary<Guid, string>();

            // Process each old layout
            foreach (var kvpLayout in oldLayouts)
            {
                // Get the layout string
                var layoutString = kvpLayout.Value;

                // Replace all the old panel oids in the layout with the new oids
                foreach (var kvpPanel in panelOids)
                {
                    var oldPanelOid = GuidToNameStringConverter.Convert(kvpPanel.Key, typeof(string), null, CultureInfo.CurrentCulture).ToString();
                    var newPanelOid = GuidToNameStringConverter.Convert(kvpPanel.Value, typeof(string), null, CultureInfo.CurrentCulture).ToString();
                    layoutString = layoutString.Replace(oldPanelOid, newPanelOid);
                }

                // Find the new layout oid and add the layoutString to the new layout dictionary
                Guid newLayoutOid;
                if (layoutOids.TryGetValue(kvpLayout.Key, out newLayoutOid))
                    newLayouts.Add(newLayoutOid, layoutString);
            }

            return DocumentLayoutHelper.ConvertToStream(newLayouts);
        }

        /// <summary>
        /// Updates the ribbon to contain only widget commands for panels in the active panel group.
        /// </summary>
        private void RefreshRibbon()
        {
            RibbonGroups.Clear();

            var activePanels = DataObject.View == DocumentView.Flat
                ? Panels.Where(p => p.PanelGroup == null).ToList()
                : Panels.Where(p => p.PanelGroup != null && ReferenceEquals(p.PanelGroup, ActiveGroup)).ToList();
            foreach (var panel in activePanels)
            {
                var element = panel.Content as FrameworkElement;
                if (element == null)
                    continue;

                var widget = element.DataContext as WidgetViewModelBase;
                if (widget == null)
                    continue;

                var widgetCommandData = widget.GetWidgetCommandData();

                // Process each widget RibbonGroup
                foreach (var widgetGroupViewModel in widget.RibbonGroups)
                {
                    // Perform text replacements in the group name
                    var groupName = widgetGroupViewModel.Name.FormatWith(widgetCommandData.TextReplacements);

                    // Attempt to find an existing group with a matching name
                    var localGroupViewModel = _ribbonGroups.SingleOrDefault(g => g.Name == groupName);
                    if (localGroupViewModel == null)
                    {
                        // If a group doesn't exist, create a new one
                        var localGroupDataModel = new RibbonGroup()
                        {
                            Name = groupName
                        };
                        localGroupViewModel = Mapper.Map<RibbonGroupViewModel>(localGroupDataModel);
                        RibbonGroups.Add(localGroupViewModel);
                    }
                    else
                    {
                        // If the group does exist, and it already contains items, add a separator
                        if (localGroupViewModel.RibbonItems.Count > 0)
                        {
                            var separatorDataObject = new RibbonItem()
                            {
                                ItemType = RibbonItemType.SeparatorItem
                            };
                            localGroupViewModel.DataObject.RibbonItems.Add(separatorDataObject);
                        }
                    }

                    // Copy all items from the widget group to the local group
                    foreach (var widgetItemViewModel in widgetGroupViewModel.RibbonItems)
                    {
                        // Clone the item data object
                        var localItemDataObject = (RibbonItem)widgetItemViewModel.DataObject.Clone(XpoDefault.Session);

                        // Perform text replacements on the item
                        localItemDataObject.Name = localItemDataObject.Name.FormatWith(widgetCommandData.TextReplacements);
                        localItemDataObject.Description = localItemDataObject.Description.FormatWith(widgetCommandData.TextReplacements);

                        // Add the item to the group
                        localGroupViewModel.DataObject.RibbonItems.Add(localItemDataObject);

                        // Get the local item view model and copy values that don't exist on the data object
                        var localItemViewModel = localGroupViewModel.RibbonItems.Single(i => ReferenceEquals(i.DataObject, localItemDataObject));
                        localItemViewModel.Command = widgetItemViewModel.Command;
                        localItemViewModel.CommandParameter = widgetItemViewModel.CommandParameter;
                    }
                }
            }
        }

        /// <summary>
        /// Generates and stores an InitializeDocumentMessage containing an empty copy of the default DataModel.
        /// </summary>
        private void UpdateInitializeDocumentMessage()
        {
            // If this Document has an InitializeDocumentMessage, and that message contains a ISupportParentViewModel, clear the ParentViewModel
            if (IsInitializedWithItem)
            {
                var oldSupportParentViewModel = _initializeDocumentMessage.Parameter as ISupportParentViewModel;
                if (oldSupportParentViewModel != null)
                    oldSupportParentViewModel.ParentViewModel = null;
            }

            // If no default DataModel is specified, clear the initializeDocumentMessage and exit
            if (DataObject.DataModel == null)
            {
                _initializeDocumentMessage = null;
                return;
            }

            // Attempt to resolve an instance of the default DataModel via Autofac
            var dataModelType = DataObject.DataModel.Type;
            object dataModel = null;
            try
            {
                dataModel = AutofacViewLocator.Default.Resolve(dataModelType);
            }
            catch (Exception)
            {
                // Ignore Resolve errors
            }

            // If Autofac did not resolve the default DataModel, create one via reflection
            if (dataModel == null)
                dataModel = Activator.CreateInstance(dataModelType);

            // If the data model is a DocumentDataModelBase, tell it to generate sample data
            var documentDataModel = dataModel as DocumentDataModelBase;
            if (documentDataModel != null)
                documentDataModel.GenerateSampleData();

            // If the dataModel supports storing a parent view model, assign this as the parent so the dataModel can access Mvvm Services
            var newSupportParentViewModel = dataModel as ISupportParentViewModel;
            if (newSupportParentViewModel != null)
                newSupportParentViewModel.ParentViewModel = this;

            // Store a new InitializeDocumentMessage containing the data model
            _initializeDocumentMessage = new InitializeDocumentMessage(this, dataModel);
        }

        /// <summary>
        /// Creates the primary UnitOfWork and starts tracking changes.
        /// </summary>
        private void CreateUnitOfWork()
        {
            _unitOfWork = _adminFacade.CreateUnitOfWork();
            _unitOfWork.StartUiTracking(this);
            _unitOfWork.ObjectChanged += UnitOfWork_ObjectChanged;
        }

        /// <summary>
        /// Disposes of the primary UnitOfWork.
        /// </summary>
        /// <param name="uow">The UnitOfWork to dispose.</param>
        private void DisposeUnitOfWork(UnitOfWork uow)
        {
            // Abort if the UnitOfWork is null
            if (uow == null)
                return;

            // Stop handling events
            uow.ObjectChanged -= UnitOfWork_ObjectChanged;

            // Dispose the UnitOfWork
            try
            {
                uow.Dispose();
            }
            catch
            {
                // Ignore dispose errors
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the DocumentModifiedCommand.
        /// </summary>
        private void OnDocumentModifiedExecute()
        {
            IsModified = true;
        }

        /// <summary>
        /// Execute method for the AddPanelCommand.
        /// </summary>
        private void OnAddPanelExecute()
        {
            Panel panel = null;
            _unitOfWork.ExecuteNestedUnitOfWork(nuow =>
            {
                // Create a new panel
                panel = new Panel(nuow) { DocumentName = DataObject.Name };

                // If the document is using PageView, set the PanelGroup
                if (DataObject.View == DocumentView.Pages && ActiveGroup != null)
                {
                    panel.PanelGroup = nuow.GetNestedObject(ActiveGroup.DataObject);
                    panel.PanelGroupName = ActiveGroup.Name;
                }

                // Show a dialog to configure the new panel
                return DetailDialogService.ShowDialog(DetailEditMode.Add, panel, "Widget");
            }, nuow =>
            {
                // If the panel was committed, add it to the document
                DataObject.Panels.Add(nuow.GetParentObject(panel));

                // If this document was initialized with an item, re-send it so the new panel displays it
                if (IsInitializedWithItem)
                    Messenger.Default.Send(_initializeDocumentMessage, DocumentId);
            });
        }

        /// <summary>
        /// CanExecute method for the AddPanelCommand.
        /// </summary>
        private bool OnAddPanelCanExecute()
        {
            return CanExecuteDocumentCommand && ((DataObject != null && DataObject.View == DocumentView.Flat) || ActiveGroup != null);
        }

        /// <summary>
        /// Execute method for the AddPanelGroupCommand.
        /// </summary>
        private void OnAddPanelGroupExecute()
        {
            PanelGroup panelGroup = null;
            _unitOfWork.ExecuteNestedUnitOfWork(nuow =>
            {
                // Show a dialog to configure the new panel group
                panelGroup = new PanelGroup(nuow) { DocumentName = DataObject.Name };
                return DetailDialogService.ShowDialog(DetailEditMode.Add, panelGroup, "Group");
            }, nuow =>
            {
                // If the panel group was committed, add it to the document
                DataObject.PanelGroups.Add(nuow.GetParentObject(panelGroup));
            });
        }

        /// <summary>
        /// CanExecute method for the AddPanelGroupCommand.
        /// </summary>
        private bool OnAddPanelGroupCanExecute()
        {
            return CanExecuteDocumentCommand && (DataObject != null && DataObject.View != DocumentView.Flat);
        }

        /// <summary>
        /// Execute method for the EditDocumentCommand.
        /// </summary>
        private void OnEditDocumentExecute()
        {
            var oldDataModelName = DataObject.DataModelName;

            // Create a NestedUnitOfWork to track the edit
            _unitOfWork.ExecuteNestedUnitOfWork(nuow =>
            {
                // Create another NestedUnitOfWork containing a clone to edit
                var cloneNuow = nuow.BeginNestedUnitOfWork();
                var clone = DataObject.Clone(cloneNuow);

                // Show a dialog to edit the clone
                var result = DetailDialogService.ShowDialog(DetailEditMode.Edit, clone);

                // If the dialog was accepted, copy the clone values back to the original data object
                if (result)
                    clone.CopyTo(DataObject);

                // Dispose the clone NestedUnitOfWork so the clone is not saved
                cloneNuow.Dispose();

                // Return the dialog result to commit or rollback the main edit NestedUnitOfWork
                return result;
            });

            // If the DataModelName has changed...
            if (DataObject.DataModelName != oldDataModelName)
            {
                // Update the InitializeDocumentMessage based on the new default DataModel
                UpdateInitializeDocumentMessage();
                RaisePropertyChanged(() => DocumentId);

                // Send either the new InitliazeDocumentMessage, or an empty one to clear the Document
                Messenger.Default.Send(_initializeDocumentMessage ?? new InitializeDocumentMessage(this, null), DocumentId);
            }
        }

        /// <summary>
        /// CanExecute method for the EditDocumentCommand.
        /// </summary>
        private bool OnEditDocumentCanExecute()
        {
            return CanExecuteDocumentCommand;
        }

        /// <summary>
        /// Execute method for the SaveDocumentCommand.
        /// </summary>
        private async Task OnSaveDocumentExecuteAsync(bool saveAsNew)
        {
            string newName = null;
            if (saveAsNew)
            {
                // Show a dialog to rename the document
                var result = RenameDialogService.ShowDialog("Save Document As", DataObject.Name);
                if (string.IsNullOrWhiteSpace(result))
                    return;

                newName = result;
            }

            // Save the document
            await SaveAsync(newName).ConfigureAwait(false);
        }

        /// <summary>
        /// CanExecute method for the SaveDocumentCommand.
        /// </summary>
        private bool OnSaveDocumentCanExecute(bool saveAsNew)
        {
            return CanExecuteDocumentCommand;
        }

        /// <summary>
        /// Handler for the EntityChangedMessage.
        /// </summary>
        /// <param name="message">The message that triggered this method.</param>
        private void OnEntityChangedMessage(EntityChangedMessage message)
        {
            // Abort if the message was sent by this document
            if (ReferenceEquals(message.Sender, this))
                return;

            // If the message is a change for this document, refresh the data object
            if (message.ContainsEntitiesWithOid(Oid))
            {
                IgnoreModifications = true;
                try
                {
                    DataObject.Reload();
                }
                catch
                {
                    // Ignore load errors
                }
                IgnoreModifications = false;
            }
        }

        /// <summary>
        /// Handles the Panels CollectionChanged event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Panels_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // If the LoadingPanel isn't visible, refresh the ribbon
            // The first ribbon refresh will occur after the whole document is loaded
            if (!IsLoadingPanelVisible)
                RefreshRibbon();

            // Sync the FilteredPanels collections on the Document and all child PageGroups as the Panels collection is changed
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    FilteredPanels.Clear();
                    foreach (var panelGroup in PanelGroups)
                    {
                        panelGroup.FilteredPanels.Clear();
                    }
                    break;

                case NotifyCollectionChangedAction.Add:
                    foreach (var newItem in e.NewItems.Cast<PanelViewModel>())
                    {
                        if (newItem.PanelGroup == null)
                            FilteredPanels.Add(newItem);
                        else
                            newItem.PanelGroup.FilteredPanels.Add(newItem);
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (var oldItem in e.OldItems.Cast<PanelViewModel>())
                    {
                        var panelGroup = PanelGroups.FirstOrDefault(g => ReferenceEquals(g.DataObject, oldItem.DataObject.PanelGroup));
                        if (panelGroup == null)
                            FilteredPanels.Remove(oldItem);
                        else
                            panelGroup.FilteredPanels.Remove(oldItem);
                    }
                    break;
            }
        }

        /// <summary>
        /// Handles the UnitOfWork.ObjectChanged event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UnitOfWork_ObjectChanged(object sender, ObjectChangeEventArgs e)
        {
            // Ignore changes to the Document.DataModel property (because it's modified on load and is tied to the Document.DataModelName property)
            if (e.Object is Shared.DataModel.Admin.Document && e.PropertyName == "DataModel")
                return;

            // Ignore changes to the Panel.Widget property (because it's modified on load and is tied to the Panel.ViewName property)
            if (e.Object is Panel && e.PropertyName == "Widget")
                return;

            // Flag the document as modified
            IsModified = true;
        }

        #endregion


        #region Overrides

        public override Guid Oid
        {
            get { return IsNew ? _tempOid : DataObject.Oid; }
        }

        protected override void OnParameterChanged(object parameter)
        {
            base.OnParameterChanged(parameter);

            // Abort if the parameter is not a ShowDocumentMessage
            var message = parameter as ShowDocumentMessage;
            if (message == null)
                return;

            IsLoadingPanelVisible = true;

            if (message.IsFixed)
            {
                // The document will never be saved, so create a UnitOfWork on the default session to track changes to this document
                _unitOfWork = new UnitOfWork(XpoDefault.Session.DataLayer);
            }
            else
            {
                // The document will be saved, so create a UnitOfWork on the admin facade to track changes to this document
                CreateUnitOfWork();
            }

            if (message.IsNew)
            {
                // If this is a new document, then create a new datamodel initialized with the name from the message
                DataObject = new Shared.DataModel.Admin.Document(_unitOfWork) { Name = message.Name };

                if (message.IsFixed)
                {
                    // If the message is for a document which shows a fixed set of widgets, add new panels to the document using values from the message
                    IsFixed = true;

                    var parameterParts = ((string)message.Parameter).Split('|');
                    for (var i = 0; i < parameterParts.Length; i += 2)
                    {
                        var panel = new Panel(_unitOfWork)
                        {
                            Name = parameterParts[i],
                            ViewName = parameterParts[i + 1]
                        };
                        DataObject.Panels.Add(panel);
                    }
                }
                else
                {
                    // If the message's parameter is a widget view model, add a new panel to the document using values from the widget
                    var widget = message.Parameter as WidgetViewModel;
                    if (widget != null && widget.WidgetAttribute != null && widget.Name != "Blank Document")
                    {
                        var panel = new Panel(_unitOfWork)
                        {
                            Name = widget.Name,
                            ViewName = widget.ViewName
                        };
                        DataObject.Panels.Add(panel);
                    }
                    else
                    {
                        // If the message's parameter is a list of widget view models, add a new panel to the document for each widget in the list
                        var widgetList = message.Parameter as IList<WidgetViewModel>;
                        if (widgetList != null)
                        {
                            foreach (var listWidget in widgetList)
                            {
                                if (listWidget.WidgetAttribute != null && listWidget.Name != "Blank Document")
                                {
                                    var panel = new Panel(_unitOfWork)
                                    {
                                        Name = listWidget.Name,
                                        ViewName = listWidget.ViewName
                                    };
                                    DataObject.Panels.Add(panel);
                                }
                            }
                        }
                    }
                }

                // Notify that the document has loaded
                RaiseDocumentLoaded();
            }
            else
            {
                // If this is not a new document, initialize it from the data store by colecting the Document and all contained Panels
                _unitOfWork.Query<Shared.DataModel.Admin.Document>().Where(d => d.Oid == message.Id)
                    .EnumerateAsync(delegate(IEnumerable<Shared.DataModel.Admin.Document> objects, Exception ex)
                    {
                        // Attempt to get the Document data object from the query results
                        DataObject = objects.FirstOrDefault();

                        if (ex != null || DataObject == null)
                        {
                            // If an exception occurred or the Document was not found, show an error
                            MessageBoxService.Show(string.Format("Error opening Document!\r\n\r\n{0}", (ex != null ? ex.Message : "The requested Document was not found.")), "Open Document", MessageBoxButton.OK, MessageBoxImage.Error);

                            // The Document is incomplete, so close it immediately
                            Application.Current.Dispatcher.Invoke(() =>
                                Document.Close()
                            );
                        }
                        else
                        {
                            _initializeDocumentMessage = message.Parameter as InitializeDocumentMessage;
                            RaisePropertyChanged(() => IsInitializedWithItem);
                            if (_initializeDocumentMessage != null)
                            {
                                // Create a new InitializeDocumentMessage with this DocumentViewModel as the sender
                                _initializeDocumentMessage = new InitializeDocumentMessage(this, _initializeDocumentMessage.Parameter);

                                // If the parameter is an InitializeDocumentMessage, ignore all document modifications
                                IgnoreModifications = true;

                                // If the dataModel in the InitializeDocumentMessage supports storing a parent view model, assign this as the parent so the dataModel can access Mvvm Services
                                var supportParentViewModel = _initializeDocumentMessage.Parameter as ISupportParentViewModel;
                                if (supportParentViewModel != null)
                                    supportParentViewModel.ParentViewModel = this;
                            }
                            else
                            {
                                // If the parameter is not an InitializeDocumentMessage, generate a new IntializeDocumentMessage (if a default DataModel is specified)
                                UpdateInitializeDocumentMessage();
                            }

                            // Pre-fetch all related entities
                            _unitOfWork.PreFetch(new[] { DataObject }, "DocumentDatas", "PanelGroups", "Panels.PanelDatas");

                            // Map all the PanelGroups to PanelGroupViewModels
                            Mapper.Map(DataObject.PanelGroups, PanelGroups);

                            // Map all the Panels to PanelViewModels
                            Mapper.Map(DataObject.Panels, Panels);

                            // If the View is not Flat, activate the first group
                            if (DataObject.View != DocumentView.Flat)
                                Application.Current.Dispatcher.InvokeAsync(() => ActiveGroup = PanelGroups[0], DispatcherPriority.ContextIdle);
                        }

                        // Notify that the document has loaded
                        Application.Current.Dispatcher.InvokeAsync(RaiseDocumentLoaded, DispatcherPriority.ContextIdle);
                    }
                );
            }
        }

        protected override void OnDataObjectPropertyChanged(ObjectChangeEventArgs e)
        {
            base.OnDataObjectPropertyChanged(e);

            // Reset will be sent when the data object is reloaded
            if (e.Reason == ObjectChangeReason.Reset)
            {
                RaisePropertyChanged(() => Title);
                return;
            }

            switch (e.PropertyName)
            {
                case "Oid":
                    Document.Id = Oid;
                    RaisePropertyChanged(() => Oid);
                    RaisePropertyChanged(() => IsNew);
                    break;

                case "Name":
                    RaisePropertyChanged(() => Title);
                    break;

                case "View":
                    RefreshRibbon();
                    break;
            }
        }

        #endregion


        #region IDocumentContent

        public IDocumentOwner DocumentOwner { get; set; }

        public object Title
        {
            get
            {
                if (IsLoadingPanelVisible)
                    return "Loading...";

                return string.Format("{0}{1}{2}", (IsModified && !IsFixed ? "*" : null), DataObject.Name, (_initializeDocumentMessage != null ? " : " + _initializeDocumentMessage.Title : null));
            }
        }

        public async void OnClose(CancelEventArgs e)
        {
            if (IsModified && !e.Cancel)
            {
                // Warn the user they are closing a modified document and allow them to save or cancel
                var result = MessageBoxService.Show(string.Format("Warning: The Document '{0}' has been modified!\r\n\r\nDo you want to save the changes?", DataObject.Name), "Close Document", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                switch (result)
                {
                    case MessageBoxResult.Yes:
                        // Stop the document from closing, set a flag to close after save, and save
                        e.Cancel = true;
                        _closeAfterSave = true;
                        await SaveAsync().ConfigureAwait(false);
                        break;

                    case MessageBoxResult.No:
                        // Allow the document to close
                        break;

                    case MessageBoxResult.Cancel:
                        // Stop the document from closing
                        e.Cancel = true;
                        break;
                }
            }

            if (!e.Cancel)
            {
                // If this Document has an InitializeDocumentMessage, and that message contains a ISupportParentViewModel, clear the ParentViewModel
                if (IsInitializedWithItem)
                {
                    var supportParentViewModel = _initializeDocumentMessage.Parameter as ISupportParentViewModel;
                    if (supportParentViewModel != null)
                        supportParentViewModel.ParentViewModel = null;
                }

                // Raise the DocumentClosing event
                IgnoreModifications = true;
                RaiseDocumentClosing(e);
                IgnoreModifications = false;

                // Stop handling events
                _panels.CollectionChanged -= Panels_CollectionChanged;

                // Dispose the primary UnitOfWork
                DisposeUnitOfWork(_unitOfWork);
            }
        }

        public void OnDestroy()
        {
        }

        #endregion


        #region ISupportLayoutData

        public GetLayoutDelegate GetLayout { get; set; }

        public SetLayoutDelegate SetLayout { get; set; }

        public void ApplyDefaultLayout()
        {
        }

        public void ApplySavedLayout()
        {
        }

        #endregion
    }
}
