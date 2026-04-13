using System;
using System.Collections;
using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Definition
{
    public class GridEditorDefinition : EditorDefinitionBase
    {
        #region Private Fields

        private Func<object, CriteriaOperator> _filterMethod;
        private bool _showToolBar;
        private Func<object, Session, object> _buildNewRowMethod;
        private Func<object, Session> _getUpdateSessionMethod;
        private Type _entityType;
        private object _itemsSource;
        private Func<object, object> _itemsSourceMethod;
        private bool _usePropertyAsSelectedItems;
        private IList _selectedItemsSource;
        private Func<object, IList> _selectedItemsSourceMethod;
        private bool _isMultiSelect = true;
        private bool _useAddDialog = true;
        private bool _showCheckBoxSelectorColumn;
        private bool _autoExpandAllGroups;

        #endregion


        #region Public Properties

        /// <summary>
        /// Indicates whether the toolbar is visible.
        /// Defaults to false.
        /// </summary>
        public bool ShowToolBar
        {
            get { return _showToolBar; }
            set { SetProperty(ref _showToolBar, value, () => ShowToolBar); }
        }

        /// <summary>
        /// The source of items for this grid when the data does not need to be dynamically loaded.
        /// </summary>
        public object ItemsSource
        {
            get { return _itemsSource; }
            set { SetProperty(ref _itemsSource, value, () => ItemsSource); }
        }

        /// <summary>
        /// A method that returns a value to be used as the ItemsSource.
        /// If ItemsSourceMethod is supplied, it will override any value stored in the ItemsSource property.
        /// </summary>
        public Func<object, object> ItemsSourceMethod
        {
            get { return _itemsSourceMethod; }
            set { SetProperty(ref _itemsSourceMethod, value, () => ItemsSourceMethod); }
        }

        /// <summary>
        /// The type of entity that will be displayed in the grid.
        /// </summary>
        public virtual Type EntityType
        {
            get { return _entityType; }
            set { SetProperty(ref _entityType, value, () => EntityType); }
        }

        /// <summary>
        /// A method that returns a CriteriaOperator to filter the data in a grid.
        /// </summary>
        public Func<object, CriteriaOperator> FilterMethod
        {
            get { return _filterMethod; }
            set { SetProperty(ref _filterMethod, value, () => FilterMethod); }
        }

        /// <summary>
        /// A method that builds a new data row for this grid.
        /// If this property is not set, the GridEdit will create new rows via the default constructor (with no parameters).
        /// This method may return an IEnumerable containing multiple rows to add.
        /// </summary>
        public Func<object, Session, object> BuildNewRowMethod
        {
            get { return _buildNewRowMethod; }
            set { SetProperty(ref _buildNewRowMethod, value, () => BuildNewRowMethod); }
        }

        /// <summary>
        /// A method which returns a session to execute udpates in.
        /// If this property is not set, the GridEdit will create and commit a new session for each update.
        /// </summary>
        public Func<object, Session> GetUpdateSessionMethod
        {
            get { return _getUpdateSessionMethod; }
            set { SetProperty(ref _getUpdateSessionMethod, value, () => GetUpdateSessionMethod); }
        }

        /// <summary>
        /// Indicates if this GridEdit should populate the bound property with the selected items, instead of assuming that the bound property contains the same items that are displayed in the grid.
        /// Defaults to false.
        /// </summary>
        public bool UsePropertyAsSelectedItems
        {
            get { return _usePropertyAsSelectedItems; }
            set { SetProperty(ref _usePropertyAsSelectedItems, value, () => UsePropertyAsSelectedItems); }
        }

        /// <summary>
        /// A list that the selected items should be synchronized to.
        /// </summary>
        public IList SelectedItemsSource
        {
            get { return _selectedItemsSource; }
            set { SetProperty(ref _selectedItemsSource, value, () => SelectedItemsSource); }
        }

        /// <summary>
        /// A method that returns a value to be used as the SelectedItemsSource.
        /// If SelectedItemsSourceMethod is supplied, it will override any value stored in the SelectedItemsSource property.
        /// </summary>
        public Func<object, IList> SelectedItemsSourceMethod
        {
            get { return _selectedItemsSourceMethod; }
            set { SetProperty(ref _selectedItemsSourceMethod, value, () => SelectedItemsSourceMethod); }
        }

        /// <summary>
        /// Indicates if this GridEdit should allow multiple items to be selected.
        /// Defaults to true.
        /// </summary>
        public bool IsMultiSelect
        {
            get { return _isMultiSelect; }
            set { SetProperty(ref _isMultiSelect, value, () => IsMultiSelect); }
        }

        /// <summary>
        /// Indicates if this GridEdit should display a dialog when adding new items.
        /// Defaults to true.
        /// The add dialog will never be displayed if BuildNewRowMethod returns an IEnumerable containing more that one item.
        /// </summary>
        public bool UseAddDialog
        {
            get { return _useAddDialog; }
            set { SetProperty(ref _useAddDialog, value, () => UseAddDialog); }
        }

        /// <summary>
        /// Indicates if this GridEdit should display the checkbox selector column.
        /// Defaults to false.
        /// </summary>
        public bool ShowCheckBoxSelectorColumn
        {
            get { return _showCheckBoxSelectorColumn; }
            set { SetProperty(ref _showCheckBoxSelectorColumn, value, () => ShowCheckBoxSelectorColumn); }
        }

        /// <summary>
        /// Indicates if this GridEdit should automatically expand all groups as soon as it is loaded.
        /// Defaults to false.
        /// </summary>
        public bool AutoExpandAllGroups
        {
            get { return _autoExpandAllGroups; }
            set { SetProperty(ref _autoExpandAllGroups, value, () => AutoExpandAllGroups); }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Raises a PropertyChanged event for the ItemsSourceMethod so it will be re-evaluated.
        /// </summary>
        public void RefreshItemsSourceMethod()
        {
            RaisePropertyChanged(() => ItemsSourceMethod);
        }

        #endregion
    }
}
