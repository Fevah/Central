using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using DevExpress.Data.Async.Helpers;
using DevExpress.Data.Filtering;
using DevExpress.Utils.Serializing;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core.Serialization;
using DevExpress.Xpf.Grid;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Core.Helper;
using TIG.TotalLink.Client.Core.Message;
using TIG.TotalLink.Client.Editor.Control.EventArgs;
using TIG.TotalLink.Client.Editor.Core.Editor;
using TIG.TotalLink.Client.Editor.Definition.Interface;
using TIG.TotalLink.Client.Editor.Helper;
using TIG.TotalLink.Client.Editor.Wrapper.Type;
using TIG.TotalLink.Client.Undo.AppContext;
using TIG.TotalLink.Client.Undo.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.Facade.Core.DataSource;

namespace TIG.TotalLink.Client.Editor.Control
{
    /// <summary>
    /// An extended TableView which will automatically adds datamodel changes to the undo stack.
    /// </summary>
    public class TableViewEx : TableView
    {
        #region Dependency Properties

        public static readonly DependencyProperty UseAddDialogProperty =
                DependencyProperty.Register("UseAddDialog", typeof(bool), typeof(TableViewEx), new FrameworkPropertyMetadata(true) { BindsTwoWayByDefault = true, DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        public static readonly DependencyProperty AutoFilterProperty =
                DependencyProperty.Register("AutoFilter", typeof(WidgetAutoFilter), typeof(TableViewEx));
        public static readonly DependencyProperty TypeWrapperProperty = DependencyProperty.RegisterAttached(
            "TypeWrapper", typeof(TableViewWrapper), typeof(TableViewEx), new PropertyMetadata((s, e) => ((TableViewEx)s).OnTypeWrapperChanged(e)));

        /// <summary>
        /// Indicates if the widget which contains this list should display a dialog box when adding new items.
        /// If this value is True, when Add is pressed a dialog will be displayed to populate values and the item will not be saved until Ok is pressed.
        /// If this value is False, when Add is pressed a new item will be created and saved immediately containing default values for all fields.
        /// </summary>
        [XtraSerializableProperty]
        [GridUIProperty]
        public bool UseAddDialog
        {
            get { return (bool)GetValue(UseAddDialogProperty); }
            set { SetValue(UseAddDialogProperty, value); }
        }

        /// <summary>
        /// Bind to the widgets AutoFilter property to make this table view re-format filters using display values.
        /// </summary>
        public WidgetAutoFilter AutoFilter
        {
            get { return (WidgetAutoFilter)GetValue(AutoFilterProperty); }
            set { SetValue(AutoFilterProperty, value); }
        }

        /// <summary>
        /// The type wrapper that contains conditions for this TableView.
        /// </summary>
        public TableViewWrapper TypeWrapper
        {
            get { return (TableViewWrapper)GetValue(TypeWrapperProperty); }
            set { SetValue(TypeWrapperProperty, value); }
        }

        #endregion


        #region Public Events

        public delegate void CanSelectRowEventHandler(object sender, CanSelectRowEventArgs e);

        public event CanSelectRowEventHandler CanSelectRow;

        #endregion


        #region Private Fields

        private DisplayCriteriaHelper _displayCriteriaHelper;
        private object _newCellValue;
        private XPInstantFeedbackSourceEx _instantFeedbackSource;
        private int _modifiedRowHandle = DataControlBase.InvalidRowHandle;
        private bool _writing;
        private bool _refreshAfterWrite;
        private bool _editing;

        #endregion


        #region Constructors

        static TableViewEx()
        {
            // Attach a CoerceValueCallback to the TableView.FocusedRowHandle property so we can control whenther the user can move the selection
            var focusedRowHandlePropertyDescriptor = DependencyPropertyDescriptor.FromProperty(FocusedRowHandleProperty, typeof(TableViewEx));
            focusedRowHandlePropertyDescriptor.DesignerCoerceValueCallback = (d, o) =>
            {
                var tableView = d as TableViewEx;
                if (tableView == null)
                    return o;

                return tableView.RaiseCanSelectRow(tableView.FocusedRowHandle, (int)o) ? o : tableView.FocusedRowHandle;
            };
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Indicates if the search panel is visible.
        /// </summary>
        [Category("Appearance ")]
        [XtraSerializableProperty]
        [GridUIProperty]
        public bool IsSearchPanelVisible
        {
            get { return ActualShowSearchPanel; }
            set
            {
                if (value)
                    ShowSearchPanel(true);
                else
                    HideSearchPanel();
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Raises the CanSelectRow event.
        /// </summary>
        private bool RaiseCanSelectRow(int oldRowHandle, int newRowHandle)
        {
            var e = new CanSelectRowEventArgs(oldRowHandle, newRowHandle);
            OnCanSelectRow(e);
            return e.Allow;
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Called when a row is about to be selected.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected virtual void OnCanSelectRow(CanSelectRowEventArgs e)
        {
            if (CanSelectRow != null)
                CanSelectRow(this, e);
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Called when the TypeWrapper property changes
        /// </summary>
        /// <param name="e">Event arguments</param>
        private void OnTypeWrapperChanged(DependencyPropertyChangedEventArgs e)
        {
            // Abort if the TypeWrapper is null
            var typeWrapper = e.NewValue as TableViewWrapper;
            if (typeWrapper == null)
                return;

            // Store the TableView on the TypeWrapper
            typeWrapper.TableView = this;
        }

        ///// <summary>
        ///// Handles the CellValueChanged event for the TableView.
        ///// </summary>
        ///// <param name="sender">The object that raised the event.</param>
        ///// <param name="e">Event arguments.</param>
        //private async void TableViewEx_CellValueChanged(object sender, CellValueChangedEventArgs e)
        //{
        //    // Get the row as a datamodel
        //    var dataModel = e.Row as EntityDataModelBase;
        //    if (dataModel == null)
        //        return;

        //    // Abort if the value has not changed
        //    if (Equals(e.OldValue, e.Value))
        //        return;

        //    // If the datamodel is being tracked by the context, record the change with the undo service and save it
        //    if (dataModel.IsTracked)
        //    {
        //        using (new UndoBatchEx(AppContextViewModel.Instance.UndoRoot, string.Format("Edit {0} : {1}", dataModel.GetType().Name, dataModel), true))
        //        {
        //            ((ChangeFactoryEx)DefaultChangeFactory.Current).OnDataModelPropertyChanged(AppContextViewModel.Instance, dataModel, e.Column.FieldName, e.OldValue, e.Value, true);
        //        }

        //        await dataModel.SaveAsync().ConfigureAwait(false);
        //    }
        //}

        /// <summary>
        /// Handles the ItemsSourceChanged event for the GridControl.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Grid_ItemsSourceChanged(object sender, ItemsSourceChangedEventArgs e)
        {
            // Attempt to get the ItemsSource as an XPInstantFeedbackSourceEx
            var instantFeedbackSource = e.NewItemsSource as XPInstantFeedbackSourceEx;
            if (instantFeedbackSource == null)
                return;

            // Store the XPInstantFeedbackSourceEx
            _instantFeedbackSource = instantFeedbackSource;
        }

        #endregion


        #region Overrides

        [Category("Appearance ")]
        [XtraSerializableProperty]
        [GridUIProperty]
        public new bool UseEvenRowBackground
        {
            get { return base.UseEvenRowBackground; }
            set { base.UseEvenRowBackground = value; }
        }

        [Category("Appearance ")]
        [XtraSerializableProperty]
        [GridUIProperty]
        public new bool ShowCheckBoxSelectorColumn
        {
            get { return base.ShowCheckBoxSelectorColumn; }
            set { base.ShowCheckBoxSelectorColumn = value; }
        }

        protected override bool OnDeserializeAllowProperty(AllowPropertyEventArgs e)
        {
            // Add these additional properties to the serialized layout
            if (e.DependencyProperty == ShowIndicatorProperty ||
                e.DependencyProperty == ShowHorizontalLinesProperty ||
                e.DependencyProperty == ShowVerticalLinesProperty ||
                e.DependencyProperty == AutoWidthProperty ||
                e.DependencyProperty == AllowSortingProperty ||
                e.DependencyProperty == AllowGroupingProperty ||
                e.DependencyProperty == AllowColumnMovingProperty ||
                e.DependencyProperty == AllowResizingProperty ||
                e.DependencyProperty == AllowColumnFilteringProperty ||
                e.DependencyProperty == AllowBestFitProperty ||
                e.DependencyProperty == ShowColumnHeadersProperty ||
                e.DependencyProperty == ShowAutoFilterRowProperty ||
                e.DependencyProperty == ShowTotalSummaryProperty ||
                e.DependencyProperty == ShowFixedTotalSummaryProperty)
                return true;

            return base.OnDeserializeAllowProperty(e);
        }

        protected override void RaiseShowGridMenu(GridMenuEventArgs e)
        {
            if (e.MenuType == GridMenuType.Column)
            {
                // Remove the Column Chooser menu item because we display it within the WidgetCustomizationControl instead
                e.Customizations.Add(new RemoveBarItemAndLinkAction()
                {
                    ItemName = DefaultColumnMenuItemNamesBase.ColumnChooser
                });
            }

            base.RaiseShowGridMenu(e);
        }

        protected override void OnValidation(ColumnBase column, DataValidationEventArgsBase e)
        {
            base.OnValidation(column, e);

            // Attempt to get the column DataContext as an EditorWrapperBase
            var wrapper = column.DataContext as EditorWrapperBase;
            if (wrapper == null)
                return;

            // Attempt to get the row being edited
            object context = null;
            var gridCellValidationEventArgs = e as GridCellValidationEventArgs;
            if (gridCellValidationEventArgs != null)
                context = DataModelHelper.GetDataObject(gridCellValidationEventArgs.Row) ?? gridCellValidationEventArgs.Row;
            
            // Validate the value and display any error
            var errorString = wrapper.Validate(e.Value, context);
            if (!string.IsNullOrWhiteSpace(errorString))
                e.SetError(errorString);
        }

        protected override void RaiseCustomFilterDisplayText(CustomFilterDisplayTextEventArgs e)
        {
            base.RaiseCustomFilterDisplayText(e);

            // Abort if there is no current filter criteria or auto filter
            var filterCriteria = e.Value as CriteriaOperator;
            if (ReferenceEquals(filterCriteria, null) || AutoFilter == null || ReferenceEquals(AutoFilter.DisplayFilterCriteria, null))
                return;

            // Create a DisplayCriteriaHelper if we don't have one yet
            if (_displayCriteriaHelper == null)
                _displayCriteriaHelper = new DisplayCriteriaHelper(this);

            // Replace the display filter with the re-formatted version
            e.Value = filterCriteria.ReplaceOperator(_displayCriteriaHelper.Process(AutoFilter.FilterCriteria), AutoFilter.DisplayFilterCriteria);
            e.Handled = true;
        }

        protected override void OnHideEditor(CellEditorBase editor, bool closeEditor)
        {
            base.OnHideEditor(editor, closeEditor);

            //System.Diagnostics.Debug.WriteLine("OnHideEditor");

            // Flag that editing has ended
            _editing = false;
        }

        protected override void RaiseCellValueChanging(CellValueChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("RaiseCellValueChanging");

            // Since XpInstantFeedbackSource is read-only we won't receive the updated cell value in the CellValueChanged event
            // So we store the new value here, and write it there
            _newCellValue = e.Value;

            // Flag that an edit has started
            _editing = true;

            base.RaiseCellValueChanging(e);
        }

        protected override void RaiseCellValueChanged(CellValueChangedEventArgs e)
        {
            base.RaiseCellValueChanged(e);

            // Abort if we reached this point without storing a newCellValue in RaiseCellValueChanging first
            if (!_editing)
                return;

            // Flag that this edit has been handled
            _editing = false;

            //System.Diagnostics.Debug.WriteLine("RaiseCellValueChanged");

            // Get a copy of the new value so it doesn't change while we are processing the edit
            var newCellValue = _newCellValue;
            _newCellValue = null;

            // If the grid is not using an instant feedback source, then we don't need to do anything to update the property.
            if (_instantFeedbackSource == null)
                return;

            // Attempt to get the editor wrapper
            var editorWrapper = e.Column.DataContext as EditorWrapperBase;
            if (editorWrapper == null || editorWrapper.Property == null)
                return;

            // Attempt to get the target property from the column wrapper
            var property = editorWrapper.Property.ContextObject as PropertyDescriptor;
            if (property == null)
                return;

            // Attempt to get the data object from the row
            var dataObject = DataModelHelper.GetDataObject(e.Row);
            if (dataObject == null)
                return;

            // Calculate the old cell value based on whether or not the editor wrapper contains an alias
            var oldCellValue = editorWrapper.ContainsAlias ? property.GetValue(dataObject) : e.OldValue;

            // Abort if the cell value hasn't changed
            if (Equals(newCellValue, oldCellValue))
                return;

            // Update the value in the proxy (if the row is one)
            var proxy = e.Row as ReadonlyThreadSafeProxyForObjectFromAnotherThread;
            if (proxy != null)
            {
                var columnIndex = Grid.DataController.Columns.GetColumnIndex(editorWrapper.PropertyName);
                if (columnIndex > -1)
                    proxy.Content[columnIndex] = newCellValue;

                // If the editor wrapper contains an alias, also update the value of the aliased field in the proxy
                if (editorWrapper.ContainsAlias)
                {
                    var aliasedEditorDefintion = editorWrapper.Editor as IAliasedEditorDefinition;
                    columnIndex = Grid.DataController.Columns.GetColumnIndex(editorWrapper.FieldName);
                    if (aliasedEditorDefintion != null && columnIndex > -1)
                        proxy.Content[columnIndex] = (newCellValue != null ? aliasedEditorDefintion.ActualDisplayProperty.GetValue(newCellValue) : null);
                }
            }

            // Update the value in the data object
            property.SetValue(dataObject, newCellValue);

            // Force the grid to refresh the row
            Grid.RefreshRow(e.RowHandle);

            // Store the current row handle so we know to refresh the data source after focus moves away from it
            _modifiedRowHandle = FocusedRowHandle;

            // Commit the changes
            _writing = true;
            _instantFeedbackSource.Session.CommitChangesAsync(ex =>
            {
                _writing = false;

                if (ex != null)
                {
                    // TODO : Handle write errors
                    return;
                }

                // Record the change on the undo stack
                using (new UndoBatchEx(AppUndoRootViewModel.Instance.UndoRoot, ActionMessageHelper.GetTitle(dataObject, "edit"), true))
                {
                    AppUndoRootViewModel.Instance.ChangeFactory.OnDataObjectPropertyChanged(AppUndoRootViewModel.Instance, dataObject, editorWrapper.PropertyName, oldCellValue, newCellValue);
                }

                // Notify other widgets of the changed entity
                EntityChangedMessage.Send(DataContext, dataObject, EntityChange.ChangeTypes.Modify);

                // If the flag is set to refresh after the write, then refresh now
                if (_refreshAfterWrite)
                {
                    _refreshAfterWrite = false;

                    //System.Diagnostics.Debug.WriteLine("CommitCallback - Refresh");
                    Application.Current.Dispatcher.Invoke(() =>
                        _instantFeedbackSource.Refresh()
                    );
                }
            });
        }

        protected override void OnFocusedRowHandleChangedCore(int oldRowHandle)
        {
            base.OnFocusedRowHandleChangedCore(oldRowHandle);

            // Abort if there is no stored row that was recently modified
            if (_modifiedRowHandle == DataControlBase.InvalidRowHandle)
                return;

            // Get and reset the modified row handle
            var modifiedRowHandle = _modifiedRowHandle;
            _modifiedRowHandle = DataControlBase.InvalidRowHandle;

            // If the focus was just moved from the modified row handle...
            if (oldRowHandle == modifiedRowHandle)
            {
                // If the data source is still saving, then set a flag to refresh the data source after the save is complete
                if (_writing)
                {
                    //System.Diagnostics.Debug.WriteLine("FocusedRowHandleChanged - Set RefreshAfterWrite");
                    _refreshAfterWrite = true;
                }
                else // ... otherwise refresh now
                {
                    //System.Diagnostics.Debug.WriteLine("FocusedRowHandleChanged - Refresh");
                    _instantFeedbackSource.Refresh();
                }
            }
        }

        protected override void OnDataControlChanged(DataControlBase oldValue)
        {
            base.OnDataControlChanged(oldValue);

            // Abort if the Grid has not been set yet
            if (Grid == null)
                return;

            // Handle grid events
            Grid.ItemsSourceChanged += Grid_ItemsSourceChanged;
        }

        #endregion
    }
}
