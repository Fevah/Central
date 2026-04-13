using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DevExpress.Data.Filtering;
using DevExpress.Mvvm;
using DevExpress.Mvvm.Native;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Printing;
using DevExpress.XtraPrinting;
using TIG.TotalLink.Client.Core.Command;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Core.Helper;
using TIG.TotalLink.Client.Core.Message;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Extension;
using TIG.TotalLink.Client.Editor.Helper;
using TIG.TotalLink.Client.Editor.Interface;
using TIG.TotalLink.Client.Editor.Wrapper.Editor;
using TIG.TotalLink.Client.Editor.Wrapper.Type;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Enum.KeyedData;
using TIG.TotalLink.Client.Module.Admin.Extension;
using TIG.TotalLink.Client.Module.Admin.Helper;
using TIG.TotalLink.Client.Module.Admin.Interface;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Client.Undo.Helper;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.DataModel.Core.Interface;
using TIG.TotalLink.Shared.Facade.Core.DataSource;
using TypeExtension = TIG.TotalLink.Client.Editor.Extension.TypeExtension;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Core
{
    [SendsDocumentMessage(typeof(SelectedItemsChangedMessage))]
    public abstract class ListViewModelBase<TEntity> : WidgetViewModelBase, ISupportLayoutData, ISupportFilterData
        where TEntity : INotifyPropertyChanged, new()
    {
        #region Private Constants

        private const int RefreshDelay = 30000;

        #endregion


        #region Private Fields

        private object _itemsSource;
        private static byte[] _defaultLayout;
        ////private readonly List<T> _itemsToSelect = new List<T>();
        private TEntity _currentItem;
        private bool _useAddDialog = true;
        private WidgetAutoFilter _autoFilter;
        private CriteriaOperator _filterCriteria;
        private bool _refreshing;
        private readonly Timer _refreshTimer;
        private bool _canRefresh = true;
        private static IPrintableControl _printableControl;
        private bool _isPageSettingChanged = false;
        private TableViewWrapper _typeWrapper;

        #endregion


        #region Constructors

        protected ListViewModelBase()
        {
            // Initialize collections
            SelectedItems = new ObservableCollection<TEntity>();
            SelectedItems.CollectionChanged += SelectedItems_CollectionChanged;

            // Initialize timers
            _refreshTimer = new Timer(EnableRefresh);

            // Initialize commands
            RefreshCommand = new DelegateCommand(OnRefreshExecute, OnRefreshCanExecute);
            AddCommand = new AsyncCommandEx(OnAddExecuteAsync, OnAddCanExecute);
            DeleteCommand = new AsyncCommandEx(OnDeleteExecuteAsync, OnDeleteCanExecute);
            GridLoadedCommand = new DelegateCommand<IPrintableControl>(OnGridLoadedExecute);
            PrintPreviewCommand = new DelegateCommand(OnPrintPreviewExecute, OnPrintPreviewCanExecute);

            // Initialize messages
            DefaultMessenger.Register<EntityChangedMessage>(this, OnEntityChangedMessage);

            // Initialize the grid
            PopulateColumns();
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to refresh the list.
        /// </summary>
        [WidgetCommand("Refresh {TypePlural}", "Edit", RibbonItemType.ButtonItem, "Refresh the {Type} list.")]
        public virtual ICommand RefreshCommand { get; protected set; }

        /// <summary>
        /// Command to add a new item to the list.
        /// </summary>
        [WidgetCommand("Add {Type}", "Edit", RibbonItemType.ButtonItem, "Add a new {Type}.")]
        public virtual ICommand AddCommand { get; protected set; }

        /// <summary>
        /// Command to delete all selected items from the list.
        /// </summary>
        [WidgetCommand("Delete {TypePlural}", "Edit", RibbonItemType.ButtonItem, "Delete the selected {TypePlural}.")]
        public virtual ICommand DeleteCommand { get; protected set; }

        /// <summary>
        /// Command to Show print preview dialog for grid items.
        /// </summary>
        [WidgetCommand("Print Preview {TypePlural}", "Edit", RibbonItemType.ButtonItem, "Print {TypePlural} Preview.")]
        public virtual ICommand PrintPreviewCommand { get; protected set; }

        [Display(AutoGenerateField = false)]
        public virtual ICommand GridLoadedCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// The type wrapper that contains conditions for this list.
        /// </summary>
        public TableViewWrapper TypeWrapper
        {
            get { return _typeWrapper; }
            private set { SetProperty(ref _typeWrapper, value, () => TypeWrapper); }
        }

        /// <summary>
        /// The primary source of items for this list.
        /// </summary>
        public object ItemsSource
        {
            get { return _itemsSource; }
            protected set
            {
                var oldValue = _itemsSource;
                SetProperty(ref _itemsSource, value, () => ItemsSource, () =>
                {
                    var oldInstantFeedbackSource = oldValue as XPInstantFeedbackSourceEx;
                    if (oldInstantFeedbackSource != null)
                        oldInstantFeedbackSource.RefreshStarting -= InstantFeedbackSource_RefreshStarting;

                    var newInstantFeedbackSource = _itemsSource as XPInstantFeedbackSourceEx;
                    if (newInstantFeedbackSource != null)
                        newInstantFeedbackSource.RefreshStarting += InstantFeedbackSource_RefreshStarting;
                });
            }
        }

        /// <summary>
        /// All selected items.
        /// </summary>
        public ObservableCollection<TEntity> SelectedItems { get; private set; }

        /// <summary>
        /// Columns that represent all the fields that are available on the type this list displays.
        /// </summary>
        public ObservableCollection<GridColumnWrapper> Columns { get; protected set; }

        /// <summary>
        /// The item that is currently focused.
        /// </summary>
        public TEntity CurrentItem
        {
            get { return _currentItem; }
            set { SetProperty(ref _currentItem, value, () => CurrentItem); }
        }

        /// <summary>
        /// Indicates if this list should display a dialog box when adding new items.
        /// If this value is True, when Add is pressed a dialog will be displayed to populate values and the item will not be saved until Ok is pressed.
        /// If this value is False, when Add is pressed a new item will be created and saved immediately containing default values for all fields.
        /// </summary>
        public bool UseAddDialog
        {
            get { return _useAddDialog; }
            set { SetProperty(ref _useAddDialog, value, () => UseAddDialog); }
        }

        /// <summary>
        /// The filter criteria that is currently applied to this list.
        /// </summary>
        public CriteriaOperator FilterCriteria
        {
            get { return _filterCriteria; }
            set
            {
                var oldFilterCriteria = _filterCriteria;
                SetProperty(ref _filterCriteria, value, () => FilterCriteria, () => OnFilterCriteriaChanged(oldFilterCriteria, _filterCriteria));
            }
        }

        /// <summary>
        /// The active auto filter.
        /// </summary>
        public WidgetAutoFilter AutoFilter
        {
            get { return _autoFilter; }
            set
            {
                var oldAutoFilter = _autoFilter;
                SetProperty(ref _autoFilter, value, () => AutoFilter, () => OnAutoFilterChanged(oldAutoFilter, _autoFilter));
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Refreshes the ItemsSource.
        /// </summary>
        public void Refresh()
        {
            // If the ItemsSource is an XPInstantFeedbackSourceEx, call the Refresh method
            var instantFeedbackSource = ItemsSource as XPInstantFeedbackSourceEx;
            if (instantFeedbackSource != null)
            {
                instantFeedbackSource.Refresh();
                return;
            }

            // For any other source, just remove and replace the ItemsSource to make sure it is fresh
            var itemsSource = ItemsSource;
            ItemsSource = null;
            ItemsSource = itemsSource;
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Creates columns for all properties on the data model.
        /// </summary>
        protected void PopulateColumns()
        {
            // Create column wrappers for each visible property on the entity being displayed
            var columns = new ObservableCollection<GridColumnWrapper>();
            foreach (var propertyWrapper in typeof(TEntity).GetVisibleAndAliasedProperties(LayoutType.Table))
            {
                columns.Add(new GridColumnWrapper(propertyWrapper));
            }

            // Create a table view wrapper for the type of entity this list contains
            TypeWrapper = new TableViewWrapper(typeof(TEntity));

            // Call the EditorMetadataBuilder to allow extended editor customisation
            EditorMetadataBuilder.Build<TEntity>(columns, TypeWrapper);

            // Store the columns
            Columns = columns;
        }

        /// <summary>
        /// Adds an item of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of item to add.</typeparam>
        protected async Task AddItemAsync<T>()
        {
            await AddItemAsync(typeof(T));
        }

        /// <summary>
        /// Adds an item of the specified type.
        /// </summary>
        /// <param name="type">The type of item to add.</param>
        protected async Task AddItemAsync(Type type)
        {
            // If the entity is a DataObjectBase, use a facade to add a new item
            if (typeof(DataObjectBase).IsAssignableFrom(type))
            {
                // Attempt to get a facade to write the change
                var facade = DataObjectHelper.GetFacade(type);
                if (facade == null)
                    throw new Exception(string.Format("Failed to find facade to add a {0}", type.Name.AddSpaces()));

                await facade.ExecuteUnitOfWorkAsync(uow =>
                {
                    uow.StartUiTracking(this);
                    var itemDataObject = DataObjectHelper.CreateDataObject(type, uow);

                    // If the new object is an IReferenceDataObject, generate a new reference number
                    var referenceDataObject = itemDataObject as IReferenceDataObject;
                    if (referenceDataObject != null)
                        referenceDataObject.GenerateReferenceNumber();

                    // If UseAddDialog = true, show a dialog to configure the new item
                    if (UseAddDialog)
                    {
                        return DetailDialogService.ShowDialog(DetailEditMode.Add, itemDataObject);
                    }

                    // If UseAddDialog = false, save the item immediately
                    return true;
                });
            }
            else // If the entity is not a DataObjectBase, add a new item to the ItemsSource directly
            {
                // If the ItemsSource is not an IList, then we don't know how to add items to it, so abort
                var listSource = ItemsSource as IList;
                if (listSource == null)
                    return;

                // Create a new item
                var newItem = Activator.CreateInstance(type) as INotifyPropertyChanged;

                // If UseAddDialog = true, show a dialog to configure the new item
                var result = true;
                if (UseAddDialog)
                    result = DetailDialogService.ShowDialog(DetailEditMode.Add, newItem);

                if (result)
                {
                    // Add the new item to the list
                    listSource.Add(newItem);
                }
            }
        }

        /// <summary>
        /// Called when the FilterCriteria property changes.
        /// </summary>
        protected virtual void OnFilterCriteriaChanged(CriteriaOperator oldValue, CriteriaOperator newValue)
        {
            // Abort if there is no active auto filter
            if (AutoFilter == null || ReferenceEquals(AutoFilter.FilterCriteria, null))
                return;

            // If there is no active filter criteria, then the auto filter must have been removed, so clear it
            if (ReferenceEquals(FilterCriteria, null))
            {
                AutoFilter = null;
                return;
            }

            // If the active filter criteria no longer contains the auto filter, then the auto filter must have been removed, so clear it
            var foundCriteria = FilterCriteria.FindOperator(AutoFilter.FilterCriteria);
            if (ReferenceEquals(foundCriteria, null))
                AutoFilter = null;
        }

        /// <summary>
        /// Called when the AutoFilter property changes.
        /// </summary>
        protected virtual void OnAutoFilterChanged(WidgetAutoFilter oldValue, WidgetAutoFilter newValue)
        {
            // Get the current filter criteria with the old auto filter removed
            var clearedCriteria = FilterCriteria;
            if (!ReferenceEquals(clearedCriteria, null) && oldValue != null && !ReferenceEquals(oldValue.FilterCriteria, null))
                clearedCriteria = clearedCriteria.RemoveOperator(oldValue.FilterCriteria);

            // Abort if the new auto filter is null
            if (newValue == null || ReferenceEquals(newValue.FilterCriteria, null))
            {
                FilterCriteria = clearedCriteria;
                return;
            }

            // Replace or combine the filter criteria with the new auto filter
            IgnoreDocumentModifications = true;
            if (ReferenceEquals(clearedCriteria, null))
                FilterCriteria = CriteriaOperator.Clone(newValue.FilterCriteria);
            else
                FilterCriteria = CriteriaOperator.And(clearedCriteria, CriteriaOperator.Clone(newValue.FilterCriteria));
            IgnoreDocumentModifications = false;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Re-enables the Refresh button after the refresh delay has expired.
        /// </summary>
        /// <param name="state">State information that has been passed into the timer.</param>
        private void EnableRefresh(object state)
        {
            _refreshTimer.Change(-1, -1);
            _canRefresh = true;

            Application.Current.Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Called when the selected items have changed in a related widget.
        /// </summary>
        /// <param name="message">The message that triggered this method.</param>
        protected virtual void OnSelectedItemsChangedMessage(SelectedItemsChangedMessage message)
        {
            // Abort if this widget doesn't allow any filters, or the message came from this widget
            if (FilterData == null || !FilterData.HasFilters || ReferenceEquals(message.Sender, this))
                return;

            // Abort if this widget is not configured to filter on the selected type
            var selectedType = message.GetPrimaryType();
            var widgetFilter = FilterData.GetFilterIfAllowed(selectedType);
            if (widgetFilter == null)
                return;

            // Get the selected items to filter on
            var selectedItems = message.GetEntitiesOfType(selectedType);

            // Apply the widget filter
            AutoFilter = new WidgetAutoFilter(widgetFilter, selectedItems);
        }

        /// <summary>
        /// Handler for the EntityChangedMessage.
        /// </summary>
        /// <param name="message">The message that triggered this method.</param>
        private void OnEntityChangedMessage(EntityChangedMessage message)
        {
            // Ignore the message if it came from within this widget, unless the message contains some adds or deletes
            if (ReferenceEquals(message.Sender, this) && !message.ContainsChangeTypes(EntityChange.ChangeTypes.Add, EntityChange.ChangeTypes.Delete))
                return;

            // If the message contains the entity type that this list displays, refresh the grid
            if (message.ContainsEntitiesOfType<TEntity>())
                Refresh();
        }

        /// <summary>
        /// Execute method for the RefreshCommand.
        /// </summary>
        protected virtual void OnRefreshExecute()
        {
            // If the displayed type is a DataObjectBase
            if (typeof(DataObjectBase).IsAssignableFrom(typeof(TEntity)))
            {
                // Attempt to get a facade for the displayed type
                var facade = DataObjectHelper.GetFacade(typeof(TEntity));
                if (facade == null)
                    throw new Exception(string.Format("Failed to find facade to refresh {0}", typeof(TEntity).Name.AddSpaces().Pluralize()));

                // Clear the displayed type from the cache so up-to-date data will be collected
                facade.NotifyDirtyTypes(typeof(TEntity));
            }

            // Disable the Refresh button for a while
            _canRefresh = false;
            _refreshTimer.Change(RefreshDelay, -1);

            // Refresh the grid
            Refresh();
        }

        /// <summary>
        /// CanExecute method for the RefreshCommand.
        /// </summary>
        protected virtual bool OnRefreshCanExecute()
        {
            return CanExecuteWidgetCommand && _canRefresh;
        }

        /// <summary>
        /// CanExecute method for the RefreshCommand.
        /// </summary>
        private static void OnGridLoadedExecute(IPrintableControl printableControl)
        {
            _printableControl = printableControl;
        }

        /// <summary>
        /// Execute method for PrintPreviewCommand.
        /// </summary>
        protected virtual void OnPrintPreviewExecute()
        {
            IgnoreDocumentModifications = true;

            // Find owner window.
            var owner = LayoutHelper.FindParentObject<Window>((System.Windows.Controls.Control)_printableControl);

            // create a print link with print control.
            var printLink = new PrintableControlLink(_printableControl);

            try
            {
                // Set page print settings
                SetPagePrintSettings(printLink.PrintingSystem.PageSettings);

                // Print issue https://www.devexpress.com/Support/Center/Question/Details/Q576968
                printLink.Landscape = printLink.PrintingSystem.PageSettings.Landscape;
                printLink.PaperKind = printLink.PrintingSystem.PageSettings.PaperKind;
                printLink.Margins = printLink.PrintingSystem.PageSettings.Margins;
                printLink.MinMargins = printLink.PrintingSystem.PageSettings.MinMargins;

                printLink.CreateDocument();

                // Hooks page settings change event.
                printLink.PrintingSystem.PageSettingsChanged += (sender, e) => _isPageSettingChanged = true;

                _isPageSettingChanged = false;
                printLink.ShowRibbonPrintPreviewDialog(owner);
            }
            finally
            {
                IgnoreDocumentModifications = false;
            }

            // If any change be catched, system will save settings to database.
            if (_isPageSettingChanged)
            {
                SavePagePrintSettings(printLink.PrintingSystem.PageSettings);
            }
        }

        /// <summary>
        /// CanExecute method for the PrintPreviewCommand.
        /// </summary>
        protected virtual bool OnPrintPreviewCanExecute()
        {
            return _printableControl != null;
        }

        /// <summary>
        /// Execute method for the AddCommand.
        /// </summary>
        protected virtual async Task OnAddExecuteAsync()
        {
            await AddItemAsync<TEntity>();
        }

        /// <summary>
        /// CanExecute method for the AddCommand.
        /// </summary>
        protected virtual bool OnAddCanExecute()
        {
            return CanExecuteWidgetCommand;
        }

        /// <summary>
        /// Execute method for the DeleteCommand.
        /// </summary>
        protected virtual async Task OnDeleteExecuteAsync()
        {
            // Show a warning before deleting the items
            var selectedItems = SelectedItems.ToList();
            if (MessageBoxService.Show(ActionMessageHelper.GetWarningMessage(selectedItems, "delete"), ActionMessageHelper.GetTitle(selectedItems, "delete"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                // If the entity is a DataObjectBase, use a facade to delete the items
                if (typeof(DataObjectBase).IsAssignableFrom(typeof(TEntity)))
                {
                    // Attempt to get a facade to write the change
                    var facade = DataObjectHelper.GetFacade(typeof(TEntity));
                    if (facade == null)
                        throw new Exception(string.Format("Failed to find facade to delete a {0}", selectedItems[0].GetType().Name.AddSpaces()));

                    // Delete the selected items
                    await facade.ExecuteUnitOfWorkAsync(uow =>
                    {
                        uow.StartUiTracking(this, true, true, true);

                        foreach (var item in selectedItems)
                        {
                            // Get a copy of the data object in this session
                            var sessionDataObject = uow.GetDataObject(item, item.GetType());
                            if (sessionDataObject == null)
                                continue;

                            // Delete the object
                            sessionDataObject.Delete();
                        }
                    });
                }
                else // If the entity is not a DataObjectBase, delete the items from the ItemsSource directly
                {
                    // If the ItemsSource is not an IList, then we don't know how to delete items from it, so abort
                    var listSource = ItemsSource as IList;
                    if (listSource == null)
                        return;

                    // Remove each of the selected items from the ItemsSource
                    foreach (var item in selectedItems)
                    {
                        listSource.Remove(item);
                    }
                }
            }
        }

        /// <summary>
        /// CanExecute method for the DeleteCommand.
        /// </summary>
        protected virtual bool OnDeleteCanExecute()
        {
            return SelectedItems.Count > 0 && CanExecuteWidgetCommand;
        }

        /// <summary>
        /// Handles the SelectedItems.CollectionChanged event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void SelectedItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Abort if the grid is clearing the selection after a refresh
            if (_refreshing)
            {
                _refreshing = false;
                return;
            }

            // Notify widgets that the selection has changed
            SendDocumentMessage(new SelectedItemsChangedMessage(this, SelectedItems.ToList()));
        }

        /// <summary>
        /// Handles the XpInstantFeedbakcSourceEx.RefreshStarting event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void InstantFeedbackSource_RefreshStarting(object sender, EventArgs e)
        {
            // Flag that the grid is refreshing so that a SelectedItemsChangedMessage will not be sent when the selection is cleared
            _refreshing = true;
        }

        /// <summary>
        /// Save page print settings
        /// </summary>
        /// <param name="pageSettings">Page settings Object</param>
        private void SavePagePrintSettings(XtraPageSettingsBase pageSettings)
        {
            using (var settingsStream = new MemoryStream())
            {
                // Get stream from page settings object.
                pageSettings.SaveToStream(settingsStream);
                settingsStream.Seek(0, SeekOrigin.Begin);

                // Save page settings
                SetKeyedData(KeyedDataScopes.Panel, KeyedDataGroupKeys.PrintSettings, GetType().FullName, settingsStream);
            }
        }

        /// <summary>
        /// Set page print settings
        /// </summary>
        /// <param name="pageSettings">Page settings Object</param>
        private void SetPagePrintSettings(XtraPageSettingsBase pageSettings)
        {
            // Get page setting from database.
            var settingsStream = GetKeyedData(KeyedDataScopes.Panel, KeyedDataGroupKeys.PrintSettings, GetType().FullName);
            if (settingsStream == null)
            {
                return;
            }

            // Apply settings from page.
            pageSettings.RestoreFromStream(settingsStream);
        }

        #endregion


        #region Overrides

        protected override void OnRegisterDocumentMessages(object documentId)
        {
            base.OnRegisterDocumentMessages(documentId);

            RegisterDocumentMessage<SelectedItemsChangedMessage>(documentId, OnSelectedItemsChangedMessage);
        }

        protected override void OnUnregisterDocumentMessages(object documentId)
        {
            base.OnUnregisterDocumentMessages(documentId);

            UnregisterDocumentMessage<SelectedItemsChangedMessage>(documentId);
        }

        /// <summary>
        /// Indicates if a widget command can be executed, based on whether any related operations are in progress.
        /// </summary>
        public override bool CanExecuteWidgetCommand
        {
            get
            {
                if (!base.CanExecuteWidgetCommand)
                {
                    return false;
                }

                var addCommand = AddCommand as AsyncCommand;
                var deleteCommand = DeleteCommand as AsyncCommand;
                return !(
                    (addCommand != null && addCommand.IsExecuting)
                    || (deleteCommand != null && deleteCommand.IsExecuting)
                    );
            }
        }

        public override WidgetCommandData GetWidgetCommandData()
        {
            var data = base.GetWidgetCommandData();

            data.TextReplacements.Add("Type", typeof(TEntity).Name.AddSpaces());
            data.TextReplacements.Add("TypePlural", typeof(TEntity).Name.AddSpaces().Pluralize());

            return data;
        }

        public override void StoreKeyedData()
        {
            base.StoreKeyedData();

            // Clear the auto filter so its not saved with the grid layout
            var autoFilter = AutoFilter;
            AutoFilter = null;

            // Store the grid layout
            SetKeyedData(KeyedDataScopes.Panel, KeyedDataGroupKeys.GridLayout, GetType().FullName, GetLayout());

            // Restore the auto filter
            AutoFilter = autoFilter;
        }

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            // Store the default grid layout
            if (_defaultLayout == null)
                _defaultLayout = ((MemoryStream)GetLayout()).ToArray();

            // Restore the grid layout
            SetLayout(GetKeyedData(KeyedDataScopes.Panel, KeyedDataGroupKeys.GridLayout, GetType().FullName));
        }

        protected override void OnWidgetClosed(EventArgs e)
        {
            base.OnWidgetClosed(e);

            // Dispose the TypeWrapper
            if (TypeWrapper != null)
                TypeWrapper.Dispose();

            // Get the ItemsSource as a XPInstantFeedbackSourceEx
            // Also clear the ItemsSource so the grid doesn't try to retrieve items after the datasource is disposed
            var instantFeedbackSource = ItemsSource as XPInstantFeedbackSourceEx;
            ItemsSource = null;
            if (instantFeedbackSource == null)
                return;

            // Dispose the XPInstantFeedbackSourceEx
            instantFeedbackSource.Dispose();
        }

        #endregion


        #region ISupportLayoutData

        public GetLayoutDelegate GetLayout { get; set; }

        public SetLayoutDelegate SetLayout { get; set; }

        public void ApplyDefaultLayout()
        {
            SetLayout(new MemoryStream(_defaultLayout));
        }

        public void ApplySavedLayout()
        {
            // Attempt to get and restore the last saved layout
            // If no saved layout was found, we will restore the default layout instead
            var savedLayout = GetKeyedData(KeyedDataScopes.Panel, KeyedDataGroupKeys.GridLayout, GetType().FullName);
            if (savedLayout != null)
                SetLayout(savedLayout);
            else
                ApplyDefaultLayout();
        }

        #endregion


        #region ISupportFilterData

        public IEnumerable<WidgetFilter> GetWidgetFilters()
        {
            return typeof(TEntity).GetWidgetFilters(TypeExtension.FilterTypes.All);
        }

        #endregion
    }
}
