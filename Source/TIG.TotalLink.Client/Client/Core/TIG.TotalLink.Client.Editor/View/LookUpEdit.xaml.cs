using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevExpress.Data;
using DevExpress.Data.Filtering;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Grid;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Editor.Control;
using TIG.TotalLink.Client.Editor.Control.EventArgs;
using TIG.TotalLink.Client.Editor.Core.Editor;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Client.Editor.Helper;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Client.Editor.View
{
    public partial class LookUpEdit
    {
        #region Dependency Properties

        public static readonly DependencyProperty EditorDefinitionProperty = DependencyProperty.Register(
            "EditorDefinition", typeof(EditorDefinitionBase), typeof(LookUpEdit), new PropertyMetadata((s, e) => ((LookUpEdit)s).OnEditorDefinitionPropertyChanged(e)));

        /// <summary>
        /// The LookUpEditorDefinition that describes the configuration for this LookUpEdit.
        /// </summary>
        public EditorDefinitionBase EditorDefinition
        {
            get { return (EditorDefinitionBase)GetValue(EditorDefinitionProperty); }
            set { SetValue(EditorDefinitionProperty, value); }
        }

        #endregion


        #region Private Enums

        private enum SelectionMovements
        {
            None = 0,
            Previous = 1,
            Next = 2
        }

        #endregion


        #region Private Fields

        private readonly EditorInitializer _editorInitializer;
        private DataObjectBase _contextDataObject;
        private PropertyInfo _displayProperty;
        private GridControlEx _gridControl;
        private TableViewEx _tableView;
        private SelectionMovements _pendingSelectionMovement;
        private string _filterFieldName;
        private bool _autoCompleting;
        private CriteriaOperator _activeFilter;
        private bool _autoSelectingRow;

        #endregion


        #region Constructors

        public LookUpEdit()
        {
            InitializeComponent();

            // Create a EditorInitializer to help with initializing this editor
            _editorInitializer = new EditorInitializer(this, InitializeDataSource);
        }

        #endregion


        #region Private Properties

        /// <summary>
        /// Returns the text value in the internal EditBox.
        /// </summary>
        private string EditBoxText
        {
            get { return (EditBox != null ? EditBox.Text : null); }
        }

        /// <summary>
        /// Indicates if the user has manually entered a value to filter on.
        /// </summary>
        private bool HasFilterValue
        {
            get { return !Equals(EditBoxText, GetDisplayValue(EditValue)); }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Initializes the data source using the supplied data context.
        /// </summary>
        /// <param name="dataContext">The data context that will be used to configure the data source.</param>
        private void InitializeDataSource(object dataContext)
        {
            // Abort if the dataContext is not loaded yet
            if (dataContext == null || dataContext is NotLoadedObject)
                return;

            // Store the data object that we are editing the property on
            _contextDataObject = DataModelHelper.GetDataObject(dataContext);
        }

        /// <summary>
        /// Makes sure a row is visible and selects it.
        /// </summary>
        /// <param name="args">The row handle to process.</param>
        private void SelectAndShowRow(object args)
        {
            //System.Diagnostics.Debug.WriteLine("SelectAndShowRow  RowHandle={0}", args);

            var rowHandle = (int)args;
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Abort if the tableView is null
                if (_tableView == null)
                    return;

                // Move the focused row
                _autoSelectingRow = true;
                _tableView.TopRowIndex = rowHandle;
                _tableView.FocusedRowHandle = rowHandle;
                _autoSelectingRow = false;

                // If there is a pending keyboard movement, apply it now
                if (_pendingSelectionMovement != SelectionMovements.None && !IsReadOnly)
                {
                    switch (_pendingSelectionMovement)
                    {
                        case SelectionMovements.Previous:
                            _tableView.MovePrevRow();
                            break;

                        case SelectionMovements.Next:
                            _tableView.MoveNextRow();
                            break;
                    }
                }
                _pendingSelectionMovement = SelectionMovements.None;
            }));
        }

        /// <summary>
        /// Applies a filter to the grid.
        /// </summary>
        /// <param name="filterValue">The value to filter by.</param>
        private void ApplyFilter(string filterValue)
        {
            //System.Diagnostics.Debug.WriteLine("ApplyFilter  FilterValue={0}", new object[] { filterValue });

            // Attempt to get the EditorDefinition as a LookUpEditorDefinition
            var lookUpEditorDefinition = EditorDefinition as LookUpEditorDefinition;
            if (lookUpEditorDefinition == null)
                return;

            // Make sure we have a filterFieldName
            if (string.IsNullOrWhiteSpace(_filterFieldName))
                return;

            // Generate a new filter based on the filterValue
            var newFilter = string.IsNullOrWhiteSpace(filterValue) ? null : CriteriaOperator.Parse(string.Format("StartsWith(Lower([{0}]), ?)", _filterFieldName), filterValue.ToLower());

            // Combine the existing grid filter and our auto filter...
            if (_gridControl.FilterCriteria == null)
            {
                if (newFilter != null)
                {
                    // The grid filter is empty, but the new filter is not
                    // Replace the grid filter with the new filter
                    _gridControl.FilterCriteria = newFilter;
                }
            }
            else
            {
                if (newFilter == null)
                {
                    if (_activeFilter != null)
                    {
                        // There is a grid filter and active filter, but no new filter
                        // Remove the active filter from the grid filter
                        _gridControl.FilterCriteria = _gridControl.FilterCriteria.RemoveOperator(_activeFilter);
                    }
                }
                else
                {
                    if (_activeFilter == null)
                    {
                        // There is a grid filter and a new filter, but no active filter
                        // Combine the new filter and the grid filter with an And operator
                        _gridControl.FilterCriteria = CriteriaOperator.And(_gridControl.FilterCriteria, newFilter);
                    }
                    else
                    {
                        // There is a grid filter, new filter, and active filter
                        // Replace the active filter within the grid filter with the new filter
                        _gridControl.FilterCriteria = _gridControl.FilterCriteria.ReplaceOperator(_activeFilter,
                            newFilter);
                    }
                }
            }

            // Store the new filter as the active filter
            _activeFilter = newFilter;
        }

        /// <summary>
        /// Stores a row in the EditValue.
        /// </summary>
        /// <param name="rowHandle">The handle of the row to store.</param>
        private void StoreRow(int rowHandle)
        {
            // Abort if the row handle is invalid
            if (rowHandle == DataControlBase.InvalidRowHandle)
                return;

            //System.Diagnostics.Debug.WriteLine("StoreRow  RowHandle={0}", rowHandle);

            // Make sure the row is loaded
            var row = _gridControl.DataController.GetRow(rowHandle, ProcessStoreRow);
            ProcessStoreRow(row);
        }

        /// <summary>
        /// Completes the StoreRow process after the target row is loaded.
        /// </summary>
        /// <param name="row">The row to process.</param>
        private void ProcessStoreRow(object row)
        {
            // Abort if the row is not loaded yet
            if (row == null || row is NotLoadedObject)
                return;

            // Get the data object from the row
            var dataObject = DataModelHelper.GetDataObject(row) ?? row;

            //System.Diagnostics.Debug.WriteLine("ProcessStoreRow  AlreadySelected={0}", Equals(EditValue, dataObject));

            // Abort if the EditValue already contains the specified row
            if (Equals(EditValue, dataObject))
            {
                //_storeSelection = true;
                return;
            }

            // Store the selected item in the EditValue
            if (dataObject is DataObjectBase && _contextDataObject != null && _contextDataObject.Session is UnitOfWork && _contextDataObject.Session.IsConnected)
                EditValue = ((UnitOfWork)(_contextDataObject.Session)).GetDataObject(dataObject);
            else
                EditValue = dataObject;

            PostEditor();
        }

        /// <summary>
        /// Auto-completes a value by updating the EditBox with the value from the focused row.
        /// </summary>
        private void AutoComplete()
        {
            // Get the handle of the row to auto-complete
            var rowHandle = _gridControl.View.FocusedRowHandle;

            //System.Diagnostics.Debug.WriteLine("AutoComplete  RowHandle={0}", rowHandle);

            // Abort if the row handle is invalid
            if (!_gridControl.IsValidRowHandle(rowHandle))
                return;

            // Make sure the row is loaded
            var row = _gridControl.DataController.GetRow(rowHandle, ProcessAutoComplete);
            ProcessAutoComplete(row);
        }

        /// <summary>
        /// Completes the AutoComplete process after the target row is loaded.
        /// </summary>
        /// <param name="row">The row to process.</param>
        private void ProcessAutoComplete(object row)
        {
            // Abort if the row is not loaded yet
            if (row == null || row is NotLoadedObject)
                return;

            // Get the existing value in the EditBox, and the new value we have auto completed
            var oldValue = EditBox.SelectionLength == 0 ? EditBox.Text : EditBox.Text.Remove(EditBox.SelectionStart, EditBox.SelectionLength);
            var dataObject = DataModelHelper.GetDataObject(row);
            var newValue = GetDisplayValue(dataObject ?? row);

            // If the newValue doesn't start with the old value, then just throw out the oldValue so that the whole value will be selected
            if ((string.IsNullOrWhiteSpace(newValue) || string.IsNullOrWhiteSpace(oldValue)) || !newValue.ToLower().StartsWith(oldValue.ToLower()))
                oldValue = string.Empty;

            //System.Diagnostics.Debug.WriteLine("ProcessAutoComplete  OldValue={0}  NewValue={1}", oldValue, newValue);

            // Put the new value in the EditBox and make sure the correct part is selected
            _autoCompleting = true;
            var newLength = (string.IsNullOrWhiteSpace(newValue) ? 0 : newValue.Length);
            var oldLength = (string.IsNullOrWhiteSpace(oldValue) ? 0 : oldValue.Length);
            EditBox.EditValue = newValue;
            EditBox.CaretIndex = oldLength;
            EditBox.SelectionStart = oldLength;
            EditBox.SelectionLength = Math.Max(newLength - oldLength, 0);
            _autoCompleting = false;
        }

        /// <summary>
        /// Forces the EditValue to be written to the data source.
        /// </summary>
        private void PostEditor()
        {
            //System.Diagnostics.Debug.WriteLine("PostEditor");

            var editData = DataContext as EditGridCellData;
            var dataViewBase = LayoutHelper.FindParentObject<DataViewBase>(this);
            if (dataViewBase != null && editData != null)
            {
                // If this editor is located within a grid, copy the EditValue to the EditGridCellData.Value and post it
                editData.Value = EditValue;
                dataViewBase.PostEditor();
            }

            // Close the popup
            ClosePopup();

            // Clear the selection in the EditBox
            EditBox.SelectionLength = 0;
            EditBox.SelectionStart = EditBox.Text.Length;
            EditBox.CaretIndex = EditBox.Text.Length;
        }

        /// <summary>
        /// Gets the display value for the supplied object.
        /// </summary>
        /// <param name="obj">The object to get the display value for.</param>
        /// <returns>A string describing the object.</returns>
        private string GetDisplayValue(object obj)
        {
            // Abort if the object is null
            if (obj == null)
                return null;

            // If no displayProperty has been set, return the string representation of the object
            if (_displayProperty == null)
                return obj.ToString();

            // Abort if the object type doesn't match the type the displayProperty belongs to
            if (_displayProperty.DeclaringType != null && !_displayProperty.DeclaringType.IsInstanceOfType(obj))
                return null;

            // Return the value of the displayProperty
            var displayValue = _displayProperty.GetValue(obj);
            if (displayValue != null)
                return displayValue.ToString();

            return null;
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the PropertyChanged event for the EditorDefinitionProperty.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        private void OnEditorDefinitionPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            // Attempt to get the EditorDefinition as a LookUpEditorDefinition
            var lookUpEditorDefinition = EditorDefinition as LookUpEditorDefinition;
            if (lookUpEditorDefinition == null)
                return;

            // Abort if the EntityType is not set
            if (lookUpEditorDefinition.EntityType == null)
                return;

            // Get the entity property that will be used for display
            _displayProperty = lookUpEditorDefinition.ActualDisplayProperty;

            // If a displayProperty is available, set the filterFieldName to the name of the displayProperty
            if (_displayProperty != null)
                _filterFieldName = _displayProperty.Name;
        }

        /// <summary>
        /// Handles the PreviewKeyDown event for the CellsControl.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void CellsControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // If Enter was pressed, mark it as handled so the grid doesn't handle it before the LookUpEdit is able to
            if (e.Key == Key.Enter)
                e.Handled = true;
        }

        /// <summary>
        /// Handles the PreviewKeyDown event for the LookUpEdit.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void LookUpEdit_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("LookUpEdit_PreviewKeyDown  Key={0}", e.Key);

            switch (e.Key)
            {
                case Key.Up:
                    e.Handled = true;

                    // If the popup is already open then move to the previous row in the grid, otherwise show the popup
                    if (IsPopupOpen)
                    {
                        if (!IsReadOnly)
                            _tableView.MovePrevRow();
                    }
                    else
                    {
                        _pendingSelectionMovement = SelectionMovements.Previous;
                        ShowPopup();
                    }

                    break;

                case Key.Down:
                    e.Handled = true;

                    // If the popup is already open then move to the next row in the grid, otherwise show the popup
                    if (IsPopupOpen)
                    {
                        if (!IsReadOnly)
                            _tableView.MoveNextRow();
                    }
                    else
                    {
                        _pendingSelectionMovement = SelectionMovements.Next;
                        ShowPopup();
                    }

                    break;

                case Key.Back:
                    e.Handled = true;

                    // Abort if the lookup is readonly
                    if (IsReadOnly)
                        break;

                    // Calculate the new edit value by removing the selected text, plus one more charater to the left of the caret
                    var newValue = EditBox.SelectionLength == 0
                        ? EditBox.Text.Remove(Math.Max(EditBox.SelectionStart - 1, 0), Math.Min(EditBox.Text.Length, 1))
                        : EditBox.Text.Remove(Math.Max(EditBox.SelectionStart - 1, 0), (EditBox.SelectionStart > 0 ? EditBox.SelectionLength + 1 : EditBox.SelectionLength));

                    // Update the EditBox
                    var newCaretIndex = Math.Max(EditBox.CaretIndex - 1, 0);
                    EditBox.EditValue = newValue;
                    EditBox.CaretIndex = newCaretIndex;

                    break;

                case Key.Enter:
                    e.Handled = true;

                    // Abort if the lookup is readonly or no text is selected
                    if (IsReadOnly)
                        break;

                    // Store the focused row
                    if (IsPopupOpen)
                    {
                        StoreRow(_tableView.FocusedRowHandle);
                    }

                    break;
            }
        }

        /// <summary>
        /// Handles the CurrentItemChanged event for the GridControl.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void GridControl_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
        {
            // Abort if the lookup is readonly
            if (IsReadOnly)
                return;

            //System.Diagnostics.Debug.WriteLine("GridControl_CurrentItemChanged  NewItem={0}", e.NewItem);

            AutoComplete();
        }

        /// <summary>
        /// Handles the PreviewMouseLeftButtonDown event for the GridControl.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void GridControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Abort if the lookup is readonly
            if (IsReadOnly)
                return;

            // Abort if the mouse is not over a row
            var hitInfo = _tableView.CalcHitInfo(e.OriginalSource as DependencyObject);
            if (!hitInfo.InRow)
                return;

            // Store the row that was clicked on
            StoreRow(hitInfo.RowHandle);
        }

        /// <summary>
        /// Handles the PreviewMouseMove event for the GridControl.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void GridControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            // Abort if the lookup is readonly
            if (IsReadOnly)
                return;

            // Abort if the mouse is not over a row
            var hitInfo = _tableView.CalcHitInfo(e.OriginalSource as DependencyObject);
            if (!hitInfo.InRow)
                return;

            // Abort if the row the mouse is over is already focused
            if (_tableView.FocusedRowHandle == hitInfo.RowHandle)
                return;

            // Select the row the mouse is over
            _gridControl.View.FocusedRowHandle = hitInfo.RowHandle;
        }

        /// <summary>
        /// Handles the CanSelectRow event for the TableViewEx.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void TableView_CanSelectRow(object sender, CanSelectRowEventArgs e)
        {
            // Don't allow if the focus is not being moved by SelectAndShowRow, and the LookUp is read-only
            if (!_autoSelectingRow && IsReadOnly)
                e.Allow = false;
        }

        #endregion


        #region Overrides

        protected override void OnLoadedInternal()
        {
            base.OnLoadedInternal();

            // Handle events on the LookUpEdit
            AddHandler(PreviewKeyDownEvent, new KeyEventHandler(LookUpEdit_PreviewKeyDown), true);

            // Handle events on the parent CellsControl
            var cellsControl = LayoutHelper.FindParentObject<CellsControl>(this);
            if (cellsControl != null)
                cellsControl.PreviewKeyDown += CellsControl_PreviewKeyDown;
        }

        protected override void OnPopupOpened()
        {
            base.OnPopupOpened();

            // Attempt to get the PopupContentControl
            var contentControl = PopupContentOwner.Child;
            if (contentControl == null)
                return;

            // Attempt to find the contained GridControl
            _gridControl = LayoutHelper.FindElementByType<GridControlEx>(contentControl);
            if (_gridControl == null)
                return;

            // Attempt to get the grid view as a TableView
            _tableView = _gridControl.View as TableViewEx;
            if (_tableView == null)
                return;

            // Configure the GridControl
            // TODO : AutoWidth doesn't seem to have any effect
            _tableView.AutoWidth = true;
            _tableView.AllowEditing = false;
            _gridControl.SelectionMode = MultiSelectMode.None;

            // Handle events
            _gridControl.CurrentItemChanged += GridControl_CurrentItemChanged;
            _gridControl.PreviewMouseLeftButtonDown += GridControl_PreviewMouseLeftButtonDown;
            _gridControl.PreviewMouseMove += GridControl_PreviewMouseMove;
            _tableView.CanSelectRow += TableView_CanSelectRow;

            if (HasFilterValue)
            {
                // If the user has typed a value to filter on, then filter the grid by the value
                ApplyFilter(EditBoxText);
            }
            else
            {
                var dataObject = EditValue as DataObjectBase;
                if (dataObject != null)
                {
                    // If the EditValue is a DataObjectBase, select it in the grid by matching the Oid
                    _gridControl.FindRowByValueAsync("Oid", dataObject.Oid)
                        .ContinueWith(t => SelectAndShowRow(t.Result));
                }
                else
                {
                    // If the EditValue is not a DataObjectBase, attempt to select the correct row by matching the entire object
                    SelectAndShowRow(_gridControl.DataController.FindRowByRowValue(EditValue));
                }
            }
        }

        protected override void OnPopupClosed()
        {
            //System.Diagnostics.Debug.WriteLine("OnPopupClosed");

            base.OnPopupClosed();

            // Stop handling events
            if (_gridControl != null)
            {
                _gridControl.CurrentItemChanged -= GridControl_CurrentItemChanged;
                _gridControl.PreviewMouseLeftButtonDown -= GridControl_PreviewMouseLeftButtonDown;
                _gridControl.PreviewMouseMove -= GridControl_PreviewMouseMove;
            }
            if (_tableView != null)
            {
                _tableView.CanSelectRow -= TableView_CanSelectRow;
            }

            _tableView = null;
            _gridControl = null;
        }

        /// <summary>
        /// Handles the TextChanged event for the core EditBox.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        protected override void OnEditBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            // Abort if the displayed value is not a manually entered value
            // or the value was set by the AutoComplete process
            // or the value is empty
            if (!HasFilterValue || _autoCompleting || string.IsNullOrWhiteSpace(EditBoxText))
                return;

            //System.Diagnostics.Debug.WriteLine("OnEditBoxTextChanged Text={0}", new object[] { EditBox.Text });

            if (IsPopupOpen)
            {
                // If the popup is already open, apply the filter
                ApplyFilter(EditBoxText);
            }
            else
            {
                // If the popup is not open, open it
                ShowPopup();
            }
        }

        protected override string GetDisplayText(object editValue, bool applyFormatting)
        {
            //System.Diagnostics.Debug.WriteLine("GetDisplayText  EditValue={0}  ApplyFormatting={1}", new object[] { editValue, applyFormatting });

            var newDisplayText = base.GetDisplayText(editValue, applyFormatting);

            // If the EditValue contains a string, then just return the value
            if (EditValue is string)
                return newDisplayText;

            // Return the display value for the data object in the EditValue
            var dataObject = DataModelHelper.GetDataObject(editValue);
            return GetDisplayValue(dataObject ?? editValue);
        }

        #endregion
    }
}
