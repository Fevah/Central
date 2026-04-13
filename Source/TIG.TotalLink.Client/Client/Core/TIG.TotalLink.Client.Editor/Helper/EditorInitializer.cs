using System;
using System.ComponentModel;
using System.Windows;
using DevExpress.Data;
using DevExpress.Data.Async.Helpers;
using DevExpress.Mvvm;
using DevExpress.Xpf.Grid;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Client.Editor.Helper
{
    /// <summary>
    /// Helper class to assist with initializing editors.
    /// </summary>
    public class EditorInitializer: IDisposable
    {
        #region Private Fields

        private readonly FrameworkElement _element;
        private readonly OperationCompleted _completed;

        #endregion


        #region Public Methods

        /// <summary>
        /// Creates a new GridRowTracker.
        /// </summary>
        /// <param name="element">The editor to track changes on.</param>
        /// <param name="completed">An OperationCompleted to execute when the editor is moved to a new row.</param>
        public EditorInitializer(FrameworkElement element, OperationCompleted completed)
        {
            // Validate parameters
            if (element == null)
                throw new ArgumentException("Parameter cannot be null.", "element");

            if (completed == null)
                throw new ArgumentException("Parameter cannot be null.", "completed");

            // Store variables
            _element = element;
            _completed = completed;

            // Handle events
            element.DataContextChanged -= Element_DataContextChanged;
            element.DataContextChanged += Element_DataContextChanged;

            // Attempt to attach RowData events immediately in case the elements DataContext is already set
            AttachRowDataEvents(null, element.DataContext as EditGridCellData);
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the DataContext on the target element.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Element_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AttachRowDataEvents(e.OldValue as EditGridCellData, e.NewValue as EditGridCellData);
            AttemptInitialize();
        }

        /// <summary>
        /// Handles the PropertyChanged event on the RowData for the target element.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void RowData_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Attempt to get the sender as a RowData
            var rowData = sender as RowData;
            if (rowData == null)
                return;

            //System.Diagnostics.Debug.WriteLine("({0}) {1}", rowData.RowHandle.Value, e.PropertyName);

            // If the row has changed, and is loaded, initialize the data source
            if ((e.PropertyName == "IsReady" || e.PropertyName == "Row" || e.PropertyName == "DataContext") && rowData.IsReady)
                _completed(DataModelHelper.GetDataObject(rowData.Row) ?? (rowData.Row is ReadonlyThreadSafeProxyForObjectFromAnotherThread ? null : rowData.Row));
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Attempts to initialize the editor based on where it is hosted.
        /// </summary>
        public void AttemptInitialize()
        {
            // If the DataContext is EditGridCellData, and the row is already loaded, then we can initialize the editor immediately
            var editGridCellData = _element.DataContext as EditGridCellData;
            if (editGridCellData != null && editGridCellData.RowData != null && editGridCellData.RowData.IsReady)
            {
                _completed(DataModelHelper.GetDataObject(editGridCellData.RowData.Row) ?? (editGridCellData.RowData.Row is ReadonlyThreadSafeProxyForObjectFromAnotherThread ? null : editGridCellData.RowData.Row));
                return;
            }

            // If the DataContext is DataObjectBase or BindableBase, then we are displaying an item directly, so we can initialize the editor immediately
            var entity = DataModelHelper.GetDataObject(_element.DataContext);
            var bindable = _element.DataContext as BindableBase;
            if (entity != null || bindable != null)
            {
                _completed(entity ?? _element.DataContext);
            }

            // If neither of the above conditions are true, then the editor is hosted within a grid, but the row is not loaded yet, so we will initialize later when the row is ready
        }

        #endregion


        #region Private Methods

        private void AttachRowDataEvents(EditGridCellData oldValue, EditGridCellData newValue)
        {
            // Remove event handlers from the old cell data
            if (oldValue != null)
                oldValue.RowData.PropertyChanged -= RowData_PropertyChanged;

            // Attach event handlers to the new cell data
            if (newValue != null)
                newValue.RowData.PropertyChanged += RowData_PropertyChanged;
        }

        #endregion


        #region IDisposable

        public void Dispose()
        {
            // Attempt to get the element DataContext as EditGridCellData
            var editGridCellData = _element.DataContext as EditGridCellData;
            if (editGridCellData == null)
                return;

            // Detach events
            AttachRowDataEvents(editGridCellData, null);
            _element.DataContextChanged -= Element_DataContextChanged;
        }

        #endregion
    }
}
