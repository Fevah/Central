using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DevExpress.Data;
using DevExpress.Data.Filtering;
using DevExpress.Mvvm;
using DevExpress.Mvvm.Native;
using DevExpress.Xpf.Grid;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.AppContext;
using TIG.TotalLink.Client.Core.Command;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Core.Helper;
using TIG.TotalLink.Client.Core.Interface.MVVMService;
using TIG.TotalLink.Client.Core.Message;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Core.Editor;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Client.Editor.Helper;
using TIG.TotalLink.Client.Editor.Wrapper.Editor;
using TIG.TotalLink.Client.Editor.Wrapper.Type;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Client.Undo.Helper;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.DataSource;

namespace TIG.TotalLink.Client.Editor.View
{
    public partial class GridEdit : INotifyPropertyChanged
    {
        #region Dependency Properties

        public static readonly DependencyProperty EditorDefinitionProperty = DependencyProperty.RegisterAttached(
            "EditorDefinition", typeof(EditorDefinitionBase), typeof(GridEdit));

        public static readonly DependencyProperty ColumnTypeProperty = DependencyProperty.RegisterAttached(
            "ColumnType", typeof(GridEditColumnType), typeof(GridEdit));

        public static readonly DependencyProperty ShowBorderProperty = DependencyProperty.RegisterAttached(
            "ShowBorder", typeof(bool), typeof(GridEdit), new PropertyMetadata(true));

        public static readonly DependencyProperty EditBarVisibleProperty = DependencyProperty.Register(
            "EditBarVisible", typeof(bool), typeof(GridEdit), new PropertyMetadata(false));

        /// <summary>
        /// Describes the configuration for this GridEdit.
        /// </summary>
        public EditorDefinitionBase EditorDefinition
        {
            get { return (EditorDefinitionBase)GetValue(EditorDefinitionProperty); }
            set { SetValue(EditorDefinitionProperty, value); }
        }

        /// <summary>
        /// Describes the type of columns that this grid will generate.
        /// </summary>
        public GridEditColumnType ColumnType
        {
            get { return (GridEditColumnType)GetValue(ColumnTypeProperty); }
            set { SetValue(ColumnTypeProperty, value); }
        }

        /// <summary>
        /// Indicates if a border will be displayed around the grid.
        /// </summary>
        public bool ShowBorder
        {
            get { return (bool)GetValue(ShowBorderProperty); }
            set { SetValue(ShowBorderProperty, value); }
        }

        /// <summary>
        /// The EditBarVisible indicate show bar manager or not.
        /// </summary>
        public bool EditBarVisible
        {
            get { return (bool)GetValue(EditBarVisibleProperty); }
            set { SetValue(EditBarVisibleProperty, value); }
        }

        #endregion


        #region Public Enums

        public enum GridEditColumnType
        {
            Grid,
            LookUp
        }

        #endregion


        #region Private Fields

        private object _itemsSource;
        private ObservableCollection<GridColumnWrapperBase> _columns;
        private readonly EditorInitializer _editorInitializer;
        //private readonly IEntityTypeProvider _entityTypeProvider;
        private Type _itemType;
        private ICommand _addCommand;
        private ICommand _refreshCommand;
        private ICommand _deleteCommand;
        private IList _selectedItems;
        private readonly List<object> _selectedRows = new List<object>();
        private int _selectedRowsTotal;
        private int _selectedRowsProcessed;
        private Func<Session, object> _buildNewRowMethod;
        private Session _updateSession;
        private bool _canEditGrid;
        private CriteriaOperator _filterCriteria;
        private bool _isMultiSelect = true;
        private bool _useAddDialog = true;
        private bool _showCheckBoxSelectorColumn;
        private bool _autoExpandAllGroups;
        private PropertyChangedEventHandler _editorDefinitionHandler;
        private TableViewWrapper _typeWrapper;
        private IList _editValueSelectedItems;
        private IList _selectedItemsSource;

        #endregion


        #region Constructors

        public GridEdit()
        {
            InitializeComponent();

            //// Get the EntityTypeProvider to help with tracking entity set changes
            //_entityTypeProvider = AutofacViewLocator.Default.Resolve<IEntityTypeProvider>();

            // Create a EditorInitializer to help with initializing this editor
            _editorInitializer = new EditorInitializer(this, InitializeDataSource);

            // Initialize commands
            RefreshCommand = new DelegateCommand(OnRefreshExecute, OnRefreshCanExecute);
            AddCommand = new AsyncCommandEx(OnAddExecuteAsync, OnAddCanExecute);
            DeleteCommand = new AsyncCommandEx(OnDeleteExecuteAsync, OnDeleteCanExecute);

            // Handle events
            Loaded += GridEdit_Loaded;
            DataControlBase.AllowInfiniteGridSize = true;
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to refresh the list.
        /// </summary>
        public ICommand RefreshCommand
        {
            get { return _refreshCommand; }
            set
            {
                _refreshCommand = value;
                RaisePropertyChanged(() => RefreshCommand);
            }
        }

        /// <summary>
        /// Command to add a new item to the list.
        /// </summary>
        public ICommand AddCommand
        {
            get { return _addCommand; }
            set
            {
                _addCommand = value;
                RaisePropertyChanged(() => AddCommand);
            }
        }

        /// <summary>
        /// Command to delete all selected items from the list.
        /// </summary>
        public ICommand DeleteCommand
        {
            get { return _deleteCommand; }
            set
            {
                _deleteCommand = value;
                RaisePropertyChanged(() => DeleteCommand);
            }
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The type wrapper that contains conditions for this grid editor.
        /// </summary>
        public TableViewWrapper TypeWrapper
        {
            get { return _typeWrapper; }
            private set
            {
                if (Equals(_typeWrapper, value))
                    return;

                _typeWrapper = value;
                RaisePropertyChanged(() => TypeWrapper);
            }
        }

        /// <summary>
        /// A collection of all selected items.
        /// </summary>
        public IList SelectedItems
        {
            get { return _selectedItems; }
            set
            {
                if (Equals(_itemsSource, value))
                    return;

                _selectedItems = value;
                RaisePropertyChanged(() => SelectedItems);
            }
        }

        /// <summary>
        /// The primary source of items for this GridEdit.
        /// </summary>
        public object ItemsSource
        {
            get { return _itemsSource; }
            set
            {
                if (Equals(_itemsSource, value))
                    return;

                _itemsSource = value;
                RaisePropertyChanged(() => ItemsSource);
            }
        }

        /// <summary>
        /// All columns to display in the grid.
        /// </summary>
        public ObservableCollection<GridColumnWrapperBase> Columns
        {
            get { return _columns; }
            set
            {
                if (Equals(_columns, value))
                    return;

                _columns = value;
                RaisePropertyChanged(() => Columns);
            }
        }

        /// <summary>
        /// Active filter applied to the grid.
        /// </summary>
        public CriteriaOperator FilterCriteria
        {
            get { return _filterCriteria; }
            set
            {
                if (Equals(_filterCriteria, value))
                    return;

                _filterCriteria = value;
                RaisePropertyChanged(() => FilterCriteria);
            }
        }

        /// <summary>
        /// Indicates if this GridEdit should allow multiple items to be selected.
        /// Defaults to true.
        /// </summary>
        public bool IsMultiSelect
        {
            get { return _isMultiSelect; }
            set
            {
                if (_isMultiSelect == value)
                    return;

                _isMultiSelect = value;
                RaisePropertyChanged(() => IsMultiSelect);
            }
        }

        /// <summary>
        /// Indicates if this GridEdit should display a dialog when adding new items.
        /// Defaults to true.
        /// </summary>
        public bool UseAddDialog
        {
            get { return _useAddDialog; }
            set
            {
                if (_useAddDialog == value)
                    return;

                _useAddDialog = value;
                RaisePropertyChanged(() => UseAddDialog);
            }
        }

        /// <summary>
        /// Indicates if this GridEdit should display the checkbox selector column.
        /// Defaults to false.
        /// </summary>
        public bool ShowCheckBoxSelectorColumn
        {
            get { return _showCheckBoxSelectorColumn; }
            set
            {
                if (_showCheckBoxSelectorColumn == value)
                    return;

                _showCheckBoxSelectorColumn = value;
                RaisePropertyChanged(() => ShowCheckBoxSelectorColumn);
            }
        }


        /// <summary>
        /// Indicates if this GridEdit should automatically expand all groups as soon as it is loaded.
        /// Defaults to false.
        /// </summary>
        public bool AutoExpandAllGroups
        {
            get { return _autoExpandAllGroups; }
            set
            {
                if (_autoExpandAllGroups == value)
                    return;

                _autoExpandAllGroups = value;
                RaisePropertyChanged(() => AutoExpandAllGroups);
            }
        }

        #endregion


        #region Private Properties

        /// <summary>
        /// Returns the DetailDialogService from the AppContextViewModel.
        /// </summary>
        private IDetailDialogService DetailDialogService
        {
            get { return AppContextViewModel.Instance.GetDetailDialogService(); }
        }

        /// <summary>
        /// Returns the MessageBoxService from the AppContextViewModel.
        /// </summary>
        private IMessageBoxService MessageBoxService
        {
            get { return AppContextViewModel.Instance.GetMessageBoxService(); }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Indicates if a grid command can be executed, based on whether any related operations are in progress.
        /// </summary>
        public bool CanExecuteGridCommand
        {
            get
            {
                return !(
                    ((AsyncCommandEx)AddCommand).IsExecuting
                    || ((AsyncCommandEx)DeleteCommand).IsExecuting
                    || !_canEditGrid);
            }
        }

        /// <summary>
        /// Initializes the data source using the supplied data context.
        /// </summary>
        /// <param name="dataContext">The data context that will be used to configure the data source.</param>
        private void InitializeDataSource(object dataContext)
        {
            // Abort if the dataContext is not loaded yet
            if (dataContext == null || dataContext is NotLoadedObject)
                return;

            // Attempt to get the EditorDefinition as a GridEditorDefinition
            var gridEditorDefinition = EditorDefinition as GridEditorDefinition;
            if (gridEditorDefinition == null)
                return;

            // Copy values from the GridEditorDefinition
            IsMultiSelect = gridEditorDefinition.IsMultiSelect;
            UseAddDialog = gridEditorDefinition.UseAddDialog;
            ShowCheckBoxSelectorColumn = gridEditorDefinition.ShowCheckBoxSelectorColumn;
            AutoExpandAllGroups = gridEditorDefinition.AutoExpandAllGroups;

            // Abort if the EntityType is null
            if (gridEditorDefinition.EntityType == null)
                return;

            // Store the type of item being displayed in this grid
            _itemType = gridEditorDefinition.EntityType;

            // Get the context data object
            var dataObject = DataModelHelper.GetDataObject(dataContext) ?? dataContext;

            // Determine if selected items should be synced to the SelectedItemsSource
            if (gridEditorDefinition.SelectedItemsSourceMethod != null)
            {
                _selectedItemsSource = gridEditorDefinition.SelectedItemsSourceMethod(dataObject);
            }
            else
            {
                _selectedItemsSource = gridEditorDefinition.SelectedItemsSource;
            }

            // Determine if selected items should be synced to the EditValueProperty
            if (gridEditorDefinition.UsePropertyAsSelectedItems)
            {
                // Attempt to get the bound property as an IList
                var property = gridEditorDefinition.Wrapper.Property.ContextObject as PropertyDescriptor;
                if (property != null)
                    _editValueSelectedItems = property.GetValue(dataContext) as IList;
            }

            // The grid can only be edited if the context is not a DataObjectBase, or it is a DataObjectBase and is saved
            var dataObjectBase = dataObject as DataObjectBase;
            _canEditGrid = dataObjectBase == null || dataObjectBase.Oid != Guid.Empty;

            // If GetUpdateSessionMethod has been specified, execute it to get the session for adding and deleting objects
            if (gridEditorDefinition.GetUpdateSessionMethod != null)
            {
                _updateSession = gridEditorDefinition.GetUpdateSessionMethod(dataObject);

                // If a GetUpdateSessionMethod was specified, but it returned null, disable editing because it will not operate as expected
                if (_updateSession == null)
                    _canEditGrid = false;
            }

            // If BuildNewRowMethod has been specified, package a build new row method with data context
            if (gridEditorDefinition.BuildNewRowMethod != null)
            {
                _buildNewRowMethod = uow => gridEditorDefinition.BuildNewRowMethod(dataObject, uow);
            }

            // If a FilterMethod has been specified, execute it to get the filter criteria for the grid
            if (gridEditorDefinition.FilterMethod != null)
            {
                FilterCriteria = gridEditorDefinition.FilterMethod(dataObject);
            }

            // If an ItemsSourceMethod has been supplied, execute it to populate the ItemsSource
            if (gridEditorDefinition.ItemsSourceMethod != null)
            {
                ItemsSource = gridEditorDefinition.ItemsSourceMethod(dataObject);
            }
            else
            {
                if (gridEditorDefinition.ItemsSource != null)
                {
                    // If the definition contains an ItemsSource, use it directly
                    ItemsSource = gridEditorDefinition.ItemsSource;
                }
                else
                {
                    // If the definition does not contain an ItemsSource, create an instant feedback source

                    // Attempt to get the facade
                    var facade = DataObjectHelper.GetFacade(gridEditorDefinition.EntityType);
                    if (facade == null)
                        return;

                    // Handle facade events so we can re-create the datasource when the facade reconnects
                    facade.DataConnected += Facade_DataConnected;

                    // If the facade it already connected, create the datasource now, otherwise start connecting
                    if (facade.IsDataConnected)
                        CreateDatasource(facade, gridEditorDefinition.EntityType);
                    else
                        facade.Connect(ServiceTypes.Data);
                }
            }
        }

        /// <summary>
        /// Creates a datasource for the specified facade and entity type.
        /// </summary>
        /// <param name="facade">The facade to use to create the datasource.</param>
        /// <param name="entityType">The entity type that the datasource will contain.</param>
        private void CreateDatasource(IFacadeBase facade, Type entityType)
        {
            ItemsSource = facade.CreateInstantFeedbackSource(entityType);
        }

        /// <summary>
        /// Creates columns for all properties on the data model.
        /// </summary>
        /// <param name="type">The type to create columns for.</param>
        private void PopulateColumns(Type type)
        {
            // Create column wrappers for each visible property on the entity being displayed
            var columns = new ObservableCollection<GridColumnWrapperBase>();
            foreach (var propertyWrapper in type.GetVisibleAndAliasedProperties(LayoutType.Table))
            {
                GridColumnWrapperBase editorWrapper = null;
                switch (ColumnType)
                {
                    case GridEditColumnType.Grid:
                        editorWrapper = new GridEditColumnWrapper(propertyWrapper);
                        break;

                    case GridEditColumnType.LookUp:
                        editorWrapper = new LookUpEditColumnWrapper(propertyWrapper);
                        break;
                }

                if (editorWrapper != null)
                    columns.Add(editorWrapper);
            }

            // Create a table view wrapper for the type of entity this grid editor contains
            TypeWrapper = new TableViewWrapper(type);

            // Call the EditorMetadataBuilder to allow extended editor customisation
            EditorMetadataBuilder.Build(type, columns, TypeWrapper);

            // Store the columns
            Columns = columns;
        }

        /// <summary>
        /// Processes each row after it is loaded, when the selection was modified by the user.
        /// </summary>
        /// <param name="row">The row to process.</param>
        private void ProcessRowOnManualSelect(object row)
        {
            // Abort if the row is not loaded yet
            if (row == null || row is NotLoadedObject)
                return;

            // Add the row to the internal list
            _selectedRows.Add(DataModelHelper.GetDataObject(row) ?? row);

            // If all expected rows have been processed, sync the internal list with the external list
            if (++_selectedRowsProcessed >= _selectedRowsTotal)
                SyncGridToSelectedItems();
        }

        /// <summary>
        /// Updates the external SelectedItems to match the rows selected in the grid.
        /// </summary>
        private void SyncGridToSelectedItems()
        {
            ////_selectedItemsChanging = true;

            // If SelectedItems was never bound, assign a collection now so internal methods can track the selection
            if (SelectedItems == null)
                SelectedItems = new ObservableCollection<object>();

            // Remove all old items from SelectedItems that are no longer selected
            if (SelectedItems != null)
            {
                for (var i = SelectedItems.Count - 1; i > -1; i--)
                {
                    if (!_selectedRows.Contains(SelectedItems[i]))
                        SelectedItems.RemoveAt(i);
                }
            }

            // Remove all old items from editValueSelectedItems that are no longer selected
            if (_editValueSelectedItems != null)
            {
                for (var i = _editValueSelectedItems.Count - 1; i > -1; i--)
                {
                    if (!_selectedRows.Contains(_editValueSelectedItems[i]))
                        _editValueSelectedItems.RemoveAt(i);
                }
            }

            // Remove all old items from selectedItemsSource that are no longer selected
            if (_selectedItemsSource != null)
            {
                for (var i = _selectedItemsSource.Count - 1; i > -1; i--)
                {
                    if (!_selectedRows.Contains(_selectedItemsSource[i]))
                        _selectedItemsSource.RemoveAt(i);
                }
            }

            // Add all new items that are selected
            foreach (var row in _selectedRows)
            {
                if (SelectedItems != null && !SelectedItems.Contains(row))
                    SelectedItems.Add(row);

                if (_editValueSelectedItems != null && !_editValueSelectedItems.Contains(row))
                    _editValueSelectedItems.Add(row);

                if (_selectedItemsSource != null && !_selectedItemsSource.Contains(row))
                    _selectedItemsSource.Add(row);
            }

            // Reset the selection tracking
            _selectedRows.Clear();
            _selectedRowsTotal = 0;
            _selectedRowsProcessed = 0;
            //_selectedItemsChanging = false;        
        }

        /// <summary>
        /// Clears the SelectedItems.
        /// </summary>
        private void ClearSelectedItems()
        {
            // Determine if selected items should be synced to the EditValueProperty
            var gridEditorDefinition = EditorDefinition as GridEditorDefinition;
            if (gridEditorDefinition != null && gridEditorDefinition.UsePropertyAsSelectedItems)
            {
                // Attempt to get the bound property as an IList
                var property = gridEditorDefinition.Wrapper.Property.ContextObject as PropertyDescriptor;
                if (property != null)
                {
                    var editValueSelectedItems = property.GetValue(DataContext) as IList;

                    if (editValueSelectedItems != null)
                        editValueSelectedItems.Clear();
                }
            }

            // Clear the SelectedItems
            if (SelectedItems != null)
            {
                //_selectedItemsChanging = true;
                SelectedItems.Clear();
                //_selectedItemsChanging = false;
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the RefreshCommand.
        /// </summary>
        protected virtual void OnRefreshExecute()
        {
            Refresh();
        }

        /// <summary>
        /// CanExecute method for the RefreshCommand.
        /// </summary>
        protected virtual bool OnRefreshCanExecute()
        {
            return CanExecuteGridCommand;
        }

        /// <summary>
        /// Execute method for the AddCommand.
        /// </summary>
        private async Task OnAddExecuteAsync()
        {
            // If the entity is a DataObjectBase, we need to use a session to add a new item
            if (typeof(DataObjectBase).IsAssignableFrom(_itemType))
            {
                var newItems = new List<INotifyPropertyChanged>();

                // If a session has been collected from GridEditorDefinition.GetUpdateSessionMethod, use that to add the new item
                if (_updateSession != null)
                {
                    // Generate new items
                    var buildNewRowResult = _buildNewRowMethod != null
                        ? _buildNewRowMethod(_updateSession)
                        : DataObjectHelper.CreateDataObject(_itemType, _updateSession);

                    // Determine if the result is an IEnumerable
                    var enumerableResult = buildNewRowResult as IEnumerable;
                    if (enumerableResult != null)
                    {
                        // If the result is IEnumerable, make a list of all INotifyPropertyChanged it contains
                        newItems = enumerableResult.OfType<INotifyPropertyChanged>().ToList();
                    }
                    else
                    {
                        // If the result is not IEnumerable, add the result as long as it is INotifyPropertyChanged
                        var notifyPropertyChangedResult = buildNewRowResult as INotifyPropertyChanged;
                        if (notifyPropertyChangedResult != null)
                            newItems.Add(notifyPropertyChangedResult);
                    }

                    // If UseAddDialog = true, and only one item is being added, show a dialog to configure the new item
                    if (UseAddDialog && newItems.Count == 1)
                    {
                        // If the dialog is cancelled, delete the new object
                        if (!DetailDialogService.ShowDialog(DetailEditMode.Add, newItems[0]))
                        {
                            ((DataObjectBase)newItems[0]).Delete();
                            newItems.RemoveAt(0);
                        }
                    }
                }
                else // If updateSession is null, create a new session to add the new item
                {
                    // Attempt to get a facade to write the change
                    var facade = DataObjectHelper.GetFacade(_itemType);
                    if (facade == null)
                        throw new Exception(string.Format("Failed to find facade to add a {0}", _itemType.Name.AddSpaces()));

                    await facade.ExecuteUnitOfWorkAsync(uow =>
                    {
                        uow.StartUiTracking(this);

                        // Generate new items
                        var buildNewRowResult = _buildNewRowMethod != null
                            ? _buildNewRowMethod(uow)
                            : DataObjectHelper.CreateDataObject(_itemType, uow);

                        // Determine if the result is an IEnumerable
                        var enumerableResult = buildNewRowResult as IEnumerable;
                        if (enumerableResult != null)
                        {
                            // If the result is IEnumerable, make a list of all INotifyPropertyChanged it contains
                            newItems = enumerableResult.OfType<INotifyPropertyChanged>().ToList();
                        }
                        else
                        {
                            // If the result is not IEnumerable, add the result as long as it is INotifyPropertyChanged
                            var notifyPropertyChangedResult = buildNewRowResult as INotifyPropertyChanged;
                            if (notifyPropertyChangedResult != null)
                                newItems.Add(notifyPropertyChangedResult);
                        }

                        // If UseAddDialog = true, and only one item is being added, show a dialog to configure the new item
                        if (UseAddDialog && newItems.Count == 1)
                        {
                            if (DetailDialogService.ShowDialog(DetailEditMode.Add, newItems[0]))
                                return true;

                            newItems.RemoveAt(0);
                            return false;
                        }

                        // If UseAddDialog = false, or multiple items are being added, save the items immediately
                        return true;
                    });
                }

                // If the ItemsSource is not an XPInstantFeedbackSourceEx, we also have to add the new items to the list
                if (!(ItemsSource is XPInstantFeedbackSourceEx))
                {
                    // If the ItemsSource is not an IList, then we don't know how to add items to it, so abort
                    var listSource = ItemsSource as IList;
                    if (listSource == null)
                        return;

                    // Add the new items to the list
                    foreach (var newItem in newItems)
                    {
                        listSource.Add(newItem);
                    }
                }

                // Force the grid to refresh
                Refresh();
            }
            else // If the entity is not a DataObjectBase, add a new item to the ItemsSource directly
            {
                // If the ItemsSource is not an IList, then we don't know how to add items to it, so abort
                var listSource = ItemsSource as IList;
                if (listSource == null)
                    return;

                var newItems = new List<INotifyPropertyChanged>();

                // Generate new items
                var buildNewRowResult = _buildNewRowMethod != null
                    ? _buildNewRowMethod(null)
                    : Activator.CreateInstance(_itemType);

                // Determine if the result is an IEnumerable
                var enumerableResult = buildNewRowResult as IEnumerable;
                if (enumerableResult != null)
                {
                    // If the result is IEnumerable, make a list of all INotifyPropertyChanged it contains
                    newItems = enumerableResult.OfType<INotifyPropertyChanged>().ToList();
                }
                else
                {
                    // If the result is not IEnumerable, add the result as long as it is INotifyPropertyChanged
                    var notifyPropertyChangedResult = buildNewRowResult as INotifyPropertyChanged;
                    if (notifyPropertyChangedResult != null)
                        newItems.Add(notifyPropertyChangedResult);
                }

                // If UseAddDialog = true, and only one item is being added, show a dialog to configure the new item
                var dialogResult = true;
                if (UseAddDialog && newItems.Count == 1)
                    dialogResult = DetailDialogService.ShowDialog(DetailEditMode.Add, newItems[0]);

                if (dialogResult)
                {
                    // Add the new items to the list
                    foreach (var newItem in newItems)
                    {
                        listSource.Add(newItem);
                    }
                }
            }
        }

        /// <summary>
        /// CanExecute method for the AddCommand.
        /// </summary>
        protected virtual bool OnAddCanExecute()
        {
            return CanExecuteGridCommand;
        }

        /// <summary>
        /// Execute method for the DeleteCommand.
        /// </summary>
        private async Task OnDeleteExecuteAsync()
        {            // Show a warning before deleting the items
            var selectedItems = SelectedItems.OfType<INotifyPropertyChanged>().ToList();
            if (MessageBoxService.Show(ActionMessageHelper.GetWarningMessage(selectedItems, "delete"), ActionMessageHelper.GetTitle(selectedItems, "delete"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                // If the entity is a DataObjectBase, we need to use a session to delete items
                if (typeof(DataObjectBase).IsAssignableFrom(_itemType))
                {
                    // If a session has been collected from GridEditorDefinition.GetUpdateSessionMethod, use that to delete the items
                    if (_updateSession != null)
                    {
                        foreach (var item in selectedItems)
                        {
                            // Get a copy of the data object in this session
                            var sessionDataObject = _updateSession.GetDataObject(item, item.GetType());
                            if (sessionDataObject == null)
                                continue;

                            // Delete the object
                            sessionDataObject.Delete();
                        }
                    }
                    else // If updateSession is null, create a new session to delete the items
                    {
                        // Attempt to get a facade to write the change
                        var facade = DataObjectHelper.GetFacade(_itemType);
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

                    // If the ItemsSource is not an XPInstantFeedbackSourceEx, we also have to remove the items from the list
                    if (!(ItemsSource is XPInstantFeedbackSourceEx))
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

                    // Force the grid to refresh
                    Refresh();
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
            return SelectedItems != null && (SelectedItems.Count > 0 && CanExecuteGridCommand);
        }

        /// <summary>
        /// Handler for the EntitySetChangedMessage.
        /// </summary>
        /// <param name="message">The message that triggered this method.</param>
        private void OnEntityChangedMessage(EntityChangedMessage message)
        {
            // Ignore the message if it came from within this widget, unless the message contains some adds or deletes
            if (ReferenceEquals(message.Sender, this) && !message.ContainsChangeTypes(EntityChange.ChangeTypes.Add, EntityChange.ChangeTypes.Delete))
                return;

            // If the message contains the entity type that this list displays, refresh the grid
            if (message.ContainsEntitiesOfType(_itemType))
                Refresh();
        }

        /// <summary>
        /// Handles the Loaded event for the GridEdit.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void GridEdit_Loaded(object sender, RoutedEventArgs e)
        {
            // Stop handling the event
            Loaded -= GridEdit_Loaded;

            // Attempt to get the EditorDefinition as a GridEditorDefinition
            var gridEditorDefinition = EditorDefinition as GridEditorDefinition;
            if (gridEditorDefinition == null)
                return;

            // Populate the grid columns
            if (gridEditorDefinition.EntityType != null)
                PopulateColumns(gridEditorDefinition.EntityType);

            // Call the EditorInitializer to decide if we are ready to initialize the data source
            _editorInitializer.AttemptInitialize();
        }

        /// <summary>
        /// Handles the DataConnected event for the Facade.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Facade_DataConnected(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Create the datasource
                CreateDatasource(sender as IFacadeBase, _itemType);
            });
        }

        /// <summary>
        /// Handles the FocusedRowChanged event for the TableView.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void TableView_FocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
        {
            // We only need to handle this event when in single select mode
            if (IsMultiSelect)
                return;

            // Get the TableView that raised the event
            var tableView = sender as TableView;
            if (tableView == null)
                return;

            // If the focused row handle is invalid, just clear the SelectedItems
            if (!tableView.Grid.IsValidRowHandle(tableView.FocusedRowHandle))
            {
                ClearSelectedItems();
                return;
            }

            // Get the focused row, and force it to load if it isn't loaded already
            _selectedRowsTotal = 1;
            var row = tableView.Grid.DataController.GetRow(tableView.FocusedRowHandle, ProcessRowOnManualSelect);
            ProcessRowOnManualSelect(row);
        }

        /// <summary>
        /// Handles the SelectionChanged event for the GridControl.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void GridControl_SelectionChanged(object sender, GridSelectionChangedEventArgs e)
        {
            // Get the GridControl that raised the event
            var gridControl = sender as GridControl;
            if (gridControl == null)
                return;

            // Get all selected row handles
            var selectedRowHandles = gridControl.GetSelectedRowHandles();
            _selectedRowsTotal = selectedRowHandles.Length;

            // If there are no selected rows, just clear the SelectedItems
            if (_selectedRowsTotal == 0)
            {
                ClearSelectedItems();
                return;
            }

            // Get the row for each selected row handle, and force it to load if it isn't loaded already
            foreach (var rowHandle in selectedRowHandles)
            {
                var row = gridControl.DataController.GetRow(rowHandle, ProcessRowOnManualSelect);
                ProcessRowOnManualSelect(row);
            }
        }

        /// <summary>
        /// Handles the PropertyChanged event for the GridEditorDefinition.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void GridEditorDefinition_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // If the ItemsSource or ItemsSourceMethod change, re-initialize the data source
            if (e.PropertyName == "ItemsSource" || e.PropertyName == "ItemsSourceMethod")
                _editorInitializer.AttemptInitialize();
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Refreshes the ItemsSource.
        /// This will only have an effect if the ItemsSource is a WcfInstantFeedbackDataSourceEx.
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


        #region Overrides

        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            if (VisualParent != null)
            {
                // Handle events when the control is added to the visual tree
                Messenger.Default.Register<EntityChangedMessage>(this, OnEntityChangedMessage);

                var gridEditorDefinition = EditorDefinition as GridEditorDefinition;
                if (gridEditorDefinition != null && _editorDefinitionHandler == null)
                {
                    _editorDefinitionHandler = GridEditorDefinition_PropertyChanged;
                    gridEditorDefinition.PropertyChanged += _editorDefinitionHandler;
                }
            }
            else
            {
                // Stop handling events when the control is removed from the visual tree
                Messenger.Default.Unregister<EntityChangedMessage>(this, OnEntityChangedMessage);

                var gridEditorDefinition = EditorDefinition as GridEditorDefinition;
                if (gridEditorDefinition != null && _editorDefinitionHandler != null)
                    gridEditorDefinition.PropertyChanged -= _editorDefinitionHandler;

                // Dispose the TypeWrapper
                if (TypeWrapper != null)
                    TypeWrapper.Dispose();
            }

            base.OnVisualParentChanged(oldParent);
        }

        #endregion


        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged<T>(Expression<Func<T>> expression)
        {
            var changedEventHandler = PropertyChanged;
            if (changedEventHandler == null)
                return;
            changedEventHandler(this, new PropertyChangedEventArgs(BindableBase.GetPropertyName(expression)));
        }

        #endregion

    }
}
