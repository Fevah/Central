using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DevExpress.Data;
using DevExpress.Xpf.Grid;
using TIG.TotalLink.Client.Core.Helper;
using TIG.TotalLink.Client.Editor.Control;
using TIG.TotalLink.Client.Module.Admin.Interface;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.Facade.Core.DataSource;

namespace TIG.TotalLink.Client.Module.Admin.View.Core
{
    public partial class ListView : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.RegisterAttached(
            "ItemsSource", typeof(object), typeof(ListView), new PropertyMetadata((s, e) => ((ListView)s).OnItemsSourceChanged(e)));
        public static readonly DependencyProperty SelectedItemsProperty = DependencyProperty.RegisterAttached(
            "SelectedItems", typeof(IList), typeof(ListView));
        public static readonly DependencyProperty CurrentItemProperty = DependencyProperty.RegisterAttached(
            "CurrentItem", typeof(object), typeof(ListView), new FrameworkPropertyMetadata() { BindsTwoWayByDefault = true, DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        /// <summary>
        /// The source collection to obtain items from to display in the grid.
        /// </summary>
        public object ItemsSource
        {
            get { return GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        /// <summary>
        /// A collection of all selected items.
        /// </summary>
        public IList SelectedItems
        {
            get { return (IList)GetValue(SelectedItemsProperty); }
            set { SetValue(SelectedItemsProperty, value); }
        }

        /// <summary>
        /// The item that is currently focused.
        /// </summary>
        public object CurrentItem
        {
            get { return GetValue(CurrentItemProperty); }
            set { SetValue(CurrentItemProperty, value); }
        }

        #endregion


        #region Private Fields

        private readonly List<object> _selectedRows = new List<object>();
        private int _selectedRowsTotal;
        private int _selectedRowsProcessed;
        private List<Guid> _oidsToReselect;
        private List<Guid> _oidsBeingReselected;
        private int _reselectedRowsProcessed;
        private readonly List<PropertyChangeNotifier> _modifiedNotifiers = new List<PropertyChangeNotifier>();
        private int _previousColumnSummaryCount;
        private bool _firstDataLoadCompleted;

        #endregion


        #region Constructors

        public ListView()
        {
            InitializeComponent();
        }

        #endregion


        #region Protected Properties

        /// <summary>
        /// Indicates if the parent document has been modified.
        /// </summary>
        protected bool IsDocumentModified
        {
            get
            {
                var widget = DataContext as WidgetViewModelBase;
                if (widget == null)
                    return false;

                return widget.IsDocumentModified;
            }
            set
            {
                var widget = DataContext as WidgetViewModelBase;
                if (widget == null)
                    return;

                widget.IsDocumentModified = value;
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Processes each row after it is loaded, when the selection was modified by the system.
        /// </summary>
        /// <param name="row">The row to process.</param>
        private void ProcessRowOnAutoSelect(object row)
        {
            // Make sure we received a row handle
            System.Diagnostics.Debug.Assert(row is int, "ProcessRowOnAutoSelect expects a row handle but it received some other object type!");
            var rowHandle = (int)row;
            //System.Diagnostics.Debug.WriteLine("ProcessRowOnAutoSelect  RowHandle={0}", rowHandle);

            // Abort if the row is not loaded yet
            if (!GridControl.IsValidRowHandle(rowHandle))
                return;

            // Select the row
            GridControl.SelectItem(rowHandle);

            // TODO : Auto selection will never complete if some of the rows were not loaded successfully
            // If all expected rows have been re-selected, clear the list of rows being re-selected
            if (++_reselectedRowsProcessed >= _oidsBeingReselected.Count)
            {
                _reselectedRowsProcessed = 0;
                _oidsBeingReselected = null;
            }
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
            //System.Diagnostics.Debug.WriteLine("ProcessRowOnManualSelect  Row={0}", row);
            var dataObject = DataModelHelper.GetDataObject(row) ?? row;
            _selectedRows.Add(dataObject);

            // TODO : Manual selection will never complete if some of the rows were not loaded successfully
            // If all expected rows have been processed, sync the internal list with the external list
            if (++_selectedRowsProcessed >= _selectedRowsTotal)
                SyncGridToSelectedItems();
        }

        /// <summary>
        /// Updates the external SelectedItems to match the rows selected in the grid.
        /// </summary>
        private void SyncGridToSelectedItems()
        {
            // Abort if SelectedItems is not bound
            if (SelectedItems == null)
                return;

            // Remove all old items that are no longer selected
            for (var i = SelectedItems.Count - 1; i > -1; i--)
            {
                if (!_selectedRows.Contains(SelectedItems[i]))
                    SelectedItems.RemoveAt(i);
            }

            // Add all new items that are selected
            foreach (var row in _selectedRows)
            {
                if (!SelectedItems.Contains(row))
                    SelectedItems.Add(row);
            }

            // Reset the selection tracking
            _selectedRows.Clear();
            _selectedRowsTotal = 0;
            _selectedRowsProcessed = 0;
        }

        /// <summary>
        /// Creates a PropertyChangeNotifier to watch the specified property and flag the document as modified when the property changes.
        /// </summary>
        /// <param name="propertySource">The object to watch for property changes on.</param>
        /// <param name="property">The property to watch for changes.</param>
        private void AddModifiedNotifier(DependencyObject propertySource, DependencyProperty property)
        {
            var notifier = new PropertyChangeNotifier(propertySource, property);
            notifier.ValueChanged += ModifiedNotifier_ValueChanged;
            _modifiedNotifiers.Add(notifier);
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the Loaded event for the ListViewControl.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ListViewControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Abort if the modified notifiers are already being handled
            if (_modifiedNotifiers.Count > 0)
                return;

            // Handle GridControl modifications
            AddModifiedNotifier(GridControl, DataControlBase.SelectionModeProperty);

            // Handle TableView modifications
            AddModifiedNotifier(TableView, GridViewBase.VisibleColumnsProperty);
            AddModifiedNotifier(TableView, DataViewBase.ActualShowSearchPanelProperty);
            AddModifiedNotifier(TableView, GridViewBase.ShowGroupPanelProperty);
            AddModifiedNotifier(TableView, DataViewBase.ShowColumnHeadersProperty);
            AddModifiedNotifier(TableView, DevExpress.Xpf.Grid.TableView.ShowAutoFilterRowProperty);
            AddModifiedNotifier(TableView, DevExpress.Xpf.Grid.TableView.ActualShowIndicatorProperty);
            AddModifiedNotifier(TableView, DevExpress.Xpf.Grid.TableView.ShowCheckBoxSelectorColumnProperty);
            AddModifiedNotifier(TableView, DevExpress.Xpf.Grid.TableView.ShowHorizontalLinesProperty);
            AddModifiedNotifier(TableView, DevExpress.Xpf.Grid.TableView.ShowVerticalLinesProperty);
            AddModifiedNotifier(TableView, DataViewBase.ShowFixedTotalSummaryProperty);
            AddModifiedNotifier(TableView, DataViewBase.ShowTotalSummaryProperty);
            AddModifiedNotifier(TableView, DevExpress.Xpf.Grid.TableView.AutoWidthProperty);
            AddModifiedNotifier(TableView, DevExpress.Xpf.Grid.TableView.UseEvenRowBackgroundProperty);
            AddModifiedNotifier(TableView, DataViewBase.AllowSortingProperty);
            AddModifiedNotifier(TableView, GridViewBase.AllowGroupingProperty);
            AddModifiedNotifier(TableView, DataViewBase.AllowColumnFilteringProperty);
            AddModifiedNotifier(TableView, DataViewBase.AllowColumnMovingProperty);
            AddModifiedNotifier(TableView, DevExpress.Xpf.Grid.TableView.AllowResizingProperty);
            AddModifiedNotifier(TableView, DevExpress.Xpf.Grid.TableView.AllowBestFitProperty);
            AddModifiedNotifier(TableView, TableViewEx.UseAddDialogProperty);

            // Handle Column modifications
            foreach (var column in GridControl.Columns)
            {
                AddModifiedNotifier(column, BaseColumn.ActualWidthProperty);
                AddModifiedNotifier(column, ColumnBase.SortOrderProperty);
                AddModifiedNotifier(column, ColumnBase.TotalSummariesProperty);
            }
        }

        /// <summary>
        /// Handles the Unloaded event for the ListViewControl.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ListViewControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Stop handling all modified notifiers
            foreach (var modifiedNotifier in _modifiedNotifiers)
            {
                modifiedNotifier.ValueChanged -= ModifiedNotifier_ValueChanged;
                modifiedNotifier.Dispose();
            }
            _modifiedNotifiers.Clear();
        }

        /// <summary>
        /// Handles the ValueChanged event for all dependency properties which should flag the document as modified.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ModifiedNotifier_ValueChanged(object sender, PropertyChangeNotifierEventArgs e)
        {
            // Ignore property changes if the first data load hasn't completed yet
            // (This is to avoid marking the document as modified when sorting and grouping is applied, which can happen long after the layout is applied.)
            if (!_firstDataLoadCompleted)
                return;

            // Special handling for the Column.TotalSummaries property
            // This property will have its value replaced every time the column summaries are modified, even if only the summary values are being updated
            // Therefore we will only flag the document as modified if the total count of column summaries has actually changed
            if (e.Property == ColumnBase.TotalSummariesProperty)
            {
                // Get the total count of all existing column summaries
                var columnSummaryCount = GridControl.Columns.Sum(c => c.TotalSummaries != null ? c.TotalSummaries.Count : 0);

                // Abort if the summary count has not changed
                if (_previousColumnSummaryCount == columnSummaryCount)
                    return;

                // Store the new summary count
                _previousColumnSummaryCount = columnSummaryCount;
            }

            // Flag the document as modified
            IsDocumentModified = true;
        }

        /// <summary>
        /// Handles the DataContextChanged event for the ListViewControl.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ListViewControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Attempt to get the OldValue as an ISupportLayoutData
            var oldSupportLayout = e.OldValue as ISupportLayoutData;
            if (oldSupportLayout != null)
            {
                // Clear viewmodel delegates
                oldSupportLayout.GetLayout = null;
                oldSupportLayout.SetLayout = null;
            }

            // Attempt to get the NewValue as an ISupportLayoutData
            var newSupportLayout = e.NewValue as ISupportLayoutData;
            if (newSupportLayout != null)
            {
                // Initialize viewmodel delegates
                newSupportLayout.GetLayout = GridControl.GetLayout;
                newSupportLayout.SetLayout = GridControl.SetLayout;
            }
        }

        ////private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        ////{
        ////    // Remove event handlers from the old SelectedItems collection
        ////    var oldCollection = e.OldValue as INotifyCollectionChanged;
        ////    if (oldCollection != null)
        ////        oldCollection.CollectionChanged -= ((ListView)d).SelectedItems_CollectionChanged;

        ////    // Attach event handlers to the new SelectedItems collection
        ////    var newCollection = e.NewValue as INotifyCollectionChanged;
        ////    if (newCollection != null)
        ////        newCollection.CollectionChanged += ((ListView)d).SelectedItems_CollectionChanged;
        ////}

        /////// <summary>
        /////// Handles the CollectionChanged event for the SelectedItems collection.
        /////// </summary>
        /////// <param name="sender">The object that raised the event.</param>
        /////// <param name="e">Event arguments.</param>
        ////private void SelectedItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        ////{
        ////    // Ignore internal changes to the SelectedItems collection
        ////    if (_selectedItemsChanging)
        ////        return;

        ////    // Update the rows selected in the grid to match SelectedItems
        ////    switch (e.Action)
        ////    {
        ////        case NotifyCollectionChangedAction.Add:
        ////            foreach (var item in e.NewItems)
        ////            {
        ////                var oidProperty = item.GetType().GetProperty("Oid");
        ////                if (oidProperty == null)
        ////                    continue;

        ////                var oid = (Guid)oidProperty.GetValue(item);
        ////                //GridControl.DataController.FindRowByValue("Oid", oid, LoadRowToSelect);
        ////            }
        ////            break;

        ////        case NotifyCollectionChangedAction.Remove:
        ////            foreach (var item in e.OldItems)
        ////            {
        ////                var oidProperty = item.GetType().GetProperty("Oid");
        ////                if (oidProperty == null)
        ////                    continue;

        ////                //var oid = (Guid)oidProperty.GetValue(item);
        ////                //var rowHandle = GridControl.FindRowByValue("Oid", oid);
        ////                //Application.Current.Dispatcher.BeginInvoke(new Action(() => GridControl.UnselectItem(rowHandle)));
        ////            }
        ////            break;

        ////        case NotifyCollectionChangedAction.Reset:
        ////            GridControl.UnselectAll();
        ////            break;
        ////    }
        ////}

        /// <summary>
        /// Handles the SelectionChanged event for the GridControl.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void GridControl_SelectionChanged(object sender, GridSelectionChangedEventArgs e)
        {
            // Get the grid control that raised the event
            var gridControl = sender as GridControl;
            if (gridControl == null)
                return;

            // Get all selected row handles
            var selectedRowHandles = gridControl.GetSelectedRowHandles();
            _selectedRowsTotal = selectedRowHandles.Length;

            // If there are no selected rows, just clear the SelectedItems
            if (_selectedRowsTotal == 0)
            {
                if (SelectedItems != null)
                {
                    SelectedItems.Clear();
                }
                return;
            }

            // Get the row for each selected row handle, and force it to load if it isn't loaded already
            // TODO : Select All does not work
            foreach (var rowHandle in selectedRowHandles)
            {
                var row = gridControl.DataController.GetRow(rowHandle, ProcessRowOnManualSelect);
                ProcessRowOnManualSelect(row);
            }
        }

        /// <summary>
        /// Handles the ItemsSourceChanged event for the GridControl.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void GridControl_ItemsSourceChanged(object sender, ItemsSourceChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("GridControl_ItemsSourceChanged");

            // Flag that the first data load is complete so document modification tracking starts
            // This case only applies when ItemsSource is not an XPInstantFeedbackSourceEx
            if (!(e.NewItemsSource is XPInstantFeedbackSourceEx))
                _firstDataLoadCompleted = true;
        }

        /// <summary>
        /// Handles the FocusedRowChanged event for the TableView.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void TableView_FocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
        {
            // We only need to handle this event when in single select mode
            if (GridControl.SelectionMode != MultiSelectMode.None)
                return;

            // Get the TableView that raised the event
            var tableView = sender as TableView;
            if (tableView == null)
                return;

            // If the focused row handle is invalid, just clear the SelectedItems
            if (!tableView.Grid.IsValidRowHandle(tableView.FocusedRowHandle))
            {
                SelectedItems.Clear();
                return;
            }

            // Get the focused row, and force it to load if it isn't loaded already
            _selectedRowsTotal = 1;
            var row = tableView.Grid.DataController.GetRow(tableView.FocusedRowHandle, ProcessRowOnManualSelect);
            ProcessRowOnManualSelect(row);
        }

        /// <summary>
        /// Handles the AsyncOperationCompleted event for the GridControl.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void GridControl_AsyncOperationCompleted(object sender, RoutedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("GridControl_AsyncOperationCompleted");

            // Flag that the first data load is complete so document modification tracking starts
            // This case only applies when ItemsSource is an XPInstantFeedbackSourceEx
            _firstDataLoadCompleted = true;

            // Abort if we do not need to re-select any rows
            if (_oidsToReselect == null || _oidsToReselect.Count == 0)
            {
                _oidsToReselect = null;
                return;
            }

            // Make a copy of the re-select list and clear it so we don't double up on the re-selection process
            _oidsBeingReselected = _oidsToReselect.ToList();
            _oidsToReselect = null;

            // Get the grid control that raised the event
            var gridControl = sender as GridControl;
            if (gridControl == null)
                return;

            // Find and re-select all the relevant rows
            foreach (var oid in _oidsBeingReselected)
            {
                var rowHandle = gridControl.DataController.FindRowByValue("Oid", oid, ProcessRowOnAutoSelect);
                //System.Diagnostics.Debug.WriteLine("GridControl_AsyncOperationCompleted  RowHandle={0}", rowHandle);
                ProcessRowOnAutoSelect(rowHandle);
            }
        }

        /// <summary>
        /// Handles the RefreshStarting event for the XPInstantFeedbackSourceEx.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void InstantFeedbackSource_RefreshStarting(object sender, EventArgs e)
        {
            // If another refresh starts before we have reselected all required rows, re-initialize the list of items to select with the original list
            if (_oidsBeingReselected != null && _oidsBeingReselected.Count > 0)
            {
                _reselectedRowsProcessed = 0;
                _oidsToReselect = _oidsBeingReselected.ToList();
                return;
            }

            // Otherwise, initialize the list of items to select with the selected oids
            _oidsToReselect = SelectedItems.OfType<DataObjectBase>().Select(i => i.Oid).ToList();
        }

        /// <summary>
        /// Called when the ItemsSource property changes
        /// </summary>
        /// <param name="e">Event arguments</param>
        private void OnItemsSourceChanged(DependencyPropertyChangedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("OnItemsSourceChanged");

            // Remove event handlers from the old datasource
            var oldInstantFeedbackSource = e.OldValue as XPInstantFeedbackSourceEx;
            if (oldInstantFeedbackSource != null)
                oldInstantFeedbackSource.RefreshStarting -= InstantFeedbackSource_RefreshStarting;

            // Add event handlers to the old datasource
            var newInstantFeedbackSource = e.NewValue as XPInstantFeedbackSourceEx;
            if (newInstantFeedbackSource != null)
                newInstantFeedbackSource.RefreshStarting += InstantFeedbackSource_RefreshStarting;
        }

        #endregion
    }
}
