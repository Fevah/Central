using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Mvvm.Native;
using DevExpress.Spreadsheet;
using TIG.TotalLink.Client.Core.Command;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Wrapper.Editor;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Message;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Core;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Exception;
using TIG.TotalLink.Client.Module.Admin.Uploader.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Core
{
    [SendsDocumentMessage(typeof(AppendLogMessage))]
    [SendsDocumentMessage(typeof(ClearUploaderDataMessage))]
    [SendsDocumentMessage(typeof(AddUploaderDataMessage))]
    public abstract class ImporterViewModelBase<T> : WidgetViewModelBase
        where T : UploaderDataModelBase, new()
    {
        #region Private Constants

        private const int ImportBatchSize = 1000;

        #endregion


        #region Private Fields

        private Dictionary<string, GridColumnWrapper> _columns;
        private string _firstHeaderCellReference = "A1";
        private Worksheet _activeWorksheet;
        private bool _isSpreadsheetEnabled = true;
        private Range _headerRange;
        private Range _dataRange;

        #endregion


        #region Constructors

        protected ImporterViewModelBase()
        {
            // Initialize collections
            Fields = new List<SpreadsheetFieldBase>();

            // Initialize commands
            ImportCommand = new AsyncCommandEx(OnImportExecuteAsync, OnImportCanExecute);
            MoveToFirstErrorCommand = new DelegateCommand(OnMoveToFirstError, OnMoveToFirstErrorCanExecute);
            MoveToLastErrorCommand = new DelegateCommand(OnMoveToLastError, OnMoveToLastErrorCanExecute);
            MoveToPreviousErrorCommand = new DelegateCommand(OnMoveToPreviousError, OnMoveToPreviousErrorCanExecute);
            MoveToNextErrorCommand = new DelegateCommand(OnMoveToNextError, OnMoveToNextErrorCanExecute);

            // Get all properties from the generic type
            PopulateColumns();
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to import the active worksheet.
        /// </summary>
        [WidgetCommand("Import", "Import", RibbonItemType.ButtonItem, "Import data from the active worksheet.")]
        public ICommand ImportCommand { get; private set; }

        /// <summary>
        /// Command to move to the first error in the active worksheet.
        /// </summary>
        [WidgetCommand("First", "Errors", RibbonItemType.ButtonItem, "Move to the first import error.")]
        public ICommand MoveToFirstErrorCommand { get; private set; }

        /// <summary>
        /// Command to move to the last error in the active worksheet.
        /// </summary>
        [WidgetCommand("Last", "Errors", RibbonItemType.ButtonItem, "Move to the last import error.")]
        public ICommand MoveToLastErrorCommand { get; private set; }

        /// <summary>
        /// Command to move to the previous error in the active worksheet.
        /// </summary>
        [WidgetCommand("Previous", "Errors", RibbonItemType.ButtonItem, "Move to the previous import error.")]
        public ICommand MoveToPreviousErrorCommand { get; private set; }

        /// <summary>
        /// Command to move to the next error in the active worksheet.
        /// </summary>
        [WidgetCommand("Next", "Errors", RibbonItemType.ButtonItem, "Move to the next import error.")]
        public ICommand MoveToNextErrorCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// A list of all spreadsheet fields defined by this importer.
        /// </summary>
        public List<SpreadsheetFieldBase> Fields { get; private set; }

        /// <summary>
        /// A cell reference in A1 style which defines the top-left cell that contains column headers.
        /// </summary>
        public string FirstHeaderCellReference
        {
            get { return _firstHeaderCellReference; }
            protected set { SetProperty(ref _firstHeaderCellReference, value, () => FirstHeaderCellReference); }
        }

        /// <summary>
        /// The worksheet which is currently active in the spreadsheet control.
        /// </summary>
        public Worksheet ActiveWorksheet
        {
            get { return _activeWorksheet; }
            set { SetProperty(ref _activeWorksheet, value, () => ActiveWorksheet); }
        }

        /// <summary>
        /// Indicates if the spreadsheet control is currently enabled.
        /// </summary>
        public bool IsSpreadsheetEnabled
        {
            get { return _isSpreadsheetEnabled; }
            set { SetProperty(ref _isSpreadsheetEnabled, value, () => IsSpreadsheetEnabled); }
        }

        #endregion


        #region Private Properties

        /// <summary>
        /// A token that is used to send messages to the uploader.
        /// </summary>
        private string UploaderDataMessageToken
        {
            get
            {
                var documentViewModel = DocumentViewModel;
                if (documentViewModel == null)
                    return null;
                return string.Format("{0}|{1}", documentViewModel.DocumentId, typeof(T).FullName);
            }
        }

        protected bool CanExecuteSpreadsheetCommand
        {
            get { return !((AsyncCommandEx)ImportCommand).IsExecuting; }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the ImportCommand.
        /// </summary>
        private async Task OnImportExecuteAsync()
        {
            IsSpreadsheetEnabled = false;
            ClearUploaderData();
            LogStart();

            ActiveWorksheet.Workbook.BeginUpdate();
            await Task.Run(() =>
            {
                ValidateSpreadsheet();
                if (_headerRange != null && _dataRange != null)
                    ImportWorksheet();
            });
            ActiveWorksheet.Workbook.EndUpdate();

            IsSpreadsheetEnabled = true;
        }

        /// <summary>
        /// CanExecute method for the ImportCommand.
        /// </summary>
        private bool OnImportCanExecute()
        {
            return CanExecuteWidgetCommand && CanExecuteSpreadsheetCommand;
        }

        /// <summary>
        /// Execute method for the MoveToFirstErrorCommand.
        /// </summary>
        private void OnMoveToFirstError()
        {
            // Attempt to find the first cell that contains an error
            var cell = ActiveWorksheet.GetDataRange().FirstOrDefault(c => c.Tag != null);
            if (cell == null)
                return;

            // Select the cell and move it into view
            ActiveWorksheet.Selection = cell;
            ActiveWorksheet.ScrollTo(cell);
        }

        /// <summary>
        /// CanExecute method for the MoveToFirstErrorCommand.
        /// </summary>
        private bool OnMoveToFirstErrorCanExecute()
        {
            return CanExecuteWidgetCommand && CanExecuteSpreadsheetCommand;
        }

        /// <summary>
        /// Execute method for the MoveToLastErrorCommand.
        /// </summary>
        private void OnMoveToLastError()
        {
            // Attempt to find the last cell that contains an error
            var cell = ActiveWorksheet.GetDataRange().LastOrDefault(c => c.Tag != null);
            if (cell == null)
                return;

            // Select the cell and move it into view
            ActiveWorksheet.Selection = cell;
            ActiveWorksheet.ScrollTo(cell);
        }

        /// <summary>
        /// CanExecute method for the MoveToLastErrorCommand.
        /// </summary>
        private bool OnMoveToLastErrorCanExecute()
        {
            return CanExecuteWidgetCommand && CanExecuteSpreadsheetCommand;
        }

        /// <summary>
        /// Execute method for the MoveToPreviousErrorCommand.
        /// </summary>
        private void OnMoveToPreviousError()
        {
            // Attempt to find the previous cell that contains an error
            var selectedCell = ActiveWorksheet.Selection[0];
            var cell = ActiveWorksheet.GetDataRange().Reverse().SkipWhile(c => !Equals(c, selectedCell)).Skip(1).FirstOrDefault(c => c.Tag != null);
            if (cell == null)
                return;

            // Select the cell and move it into view
            ActiveWorksheet.Selection = cell;
            ActiveWorksheet.ScrollTo(cell);
        }

        /// <summary>
        /// CanExecute method for the MoveToPreviousErrorCommand.
        /// </summary>
        private bool OnMoveToPreviousErrorCanExecute()
        {
            return CanExecuteWidgetCommand && CanExecuteSpreadsheetCommand;
        }

        /// <summary>
        /// Execute method for the MoveToNextErrorCommand.
        /// </summary>
        private void OnMoveToNextError()
        {
            // Attempt to find the next cell that contains an error
            var selectedCell = ActiveWorksheet.Selection[0];
            var cell = ActiveWorksheet.GetDataRange().SkipWhile(c => !Equals(c, selectedCell)).Skip(1).FirstOrDefault(c => c.Tag != null);
            if (cell == null)
                return;

            // Select the cell and move it into view
            ActiveWorksheet.Selection = cell;
            ActiveWorksheet.ScrollTo(cell);
        }

        /// <summary>
        /// CanExecute method for the MoveToNextErrorCommand.
        /// </summary>
        private bool OnMoveToNextErrorCanExecute()
        {
            return CanExecuteWidgetCommand && CanExecuteSpreadsheetCommand;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Validates the data and header ranges, and all fields that have been defined for the import, in the active worksheet.
        /// </summary>
        private void ValidateSpreadsheet()
        {
            LogMessage("Validating spreadsheet...");

            // Fail if there is no active worksheet
            if (ActiveWorksheet == null)
            {
                LogMessage("ERROR!  There is no active worksheet!");
                LogFail();
                return;
            }

            // Attempt to get the range of data that exists in the worksheet
            _dataRange = ActiveWorksheet.GetDataRange();

            // Fail if the data range is only a single cell
            if (_dataRange.ColumnCount <= 1 && _dataRange.RowCount <= 1)
            {
                LogMessage("ERROR!  The active worksheet does not contain any data!");
                LogFail();
                return;
            }

            // Log the range of data found
            LogMessage(string.Format("Found data in range {0}.", _dataRange.GetRangeWithRelativeReference().GetReferenceA1()));

            // Clear all errors in the data range
            foreach (var dataCell in _dataRange)
            {
                dataCell.Tag = null;
            }

            // Attempt to find the range of column headers
            bool headerFound;
            var headerCount = 0;
            var firstHeaderCell = ActiveWorksheet[FirstHeaderCellReference][0];
            do
            {
                var headerCell = firstHeaderCell[0, headerCount];
                headerFound = !string.IsNullOrWhiteSpace(headerCell.DisplayText);
                if (headerFound)
                    headerCount++;
            } while (headerFound);

            // Fail if no headers were found
            if (headerCount == 0)
            {
                LogMessage("ERROR!  Failed to find valid column headers!");
                LogFail();
                return;
            }

            // Collect and log the range of column headers found
            _headerRange = ActiveWorksheet.Range.FromLTRB(firstHeaderCell.LeftColumnIndex, firstHeaderCell.TopRowIndex, firstHeaderCell.LeftColumnIndex + headerCount - 1, firstHeaderCell.TopRowIndex);
            LogMessage(string.Format("Found {0:N0} column {1} in range {2}.", headerCount, headerCount.Pluralize("header"), _headerRange.GetReferenceA1()));

            // Initialize all fields
            LogMessage("Validating fields...");
            var fieldsValid = true;
            foreach (var field in Fields)
            {
                if (!InitializeField(field))
                    fieldsValid = false;
            }

            // Fail if any of the fields failed to initialize
            if (!fieldsValid)
            {
                LogFail();
                return;
            }

            // Log that all fields are valid
            LogMessage("All fields are valid.");
        }

        /// <summary>
        /// Initializes a field or group.
        /// </summary>
        /// <param name="field">The field to initialize.</param>
        /// <param name="isInGroup">Indicates if the field is contained within a group.</param>
        /// <returns>True if the field was initialized successfully.</returns>
        private bool InitializeField(SpreadsheetFieldBase field, bool isInGroup = false)
        {
            // Attempt to get the field as a SpreadsheetFieldGroup
            var fieldGroup = field as SpreadsheetFieldGroup;
            if (fieldGroup != null)
            {
                // Process each field in the group
                var groupValid = true;
                foreach (var containedField in fieldGroup.Contents)
                {
                    if (!InitializeField(containedField, true))
                        groupValid = false;
                }

                // Return the result of processing the group
                return groupValid;
            }

            // Attempt to get the field as a SpreadsheetFieldDefinitionBase
            var fieldDefinition = field as SpreadsheetFieldDefinitionBase;
            if (fieldDefinition == null)
                return false;

            // Initialize the field
            try
            {
                if (isInGroup)
                    ((GroupableSpreadsheetFieldDefinitionBase)fieldDefinition).InitializeGroup(_dataRange, _headerRange);
                else
                    fieldDefinition.Initialize(_dataRange, _headerRange);
            }
            catch (SpreadsheetFieldException ex)
            {
                // If any error occurred during initialization, log it and return a fail
                LogMessage(string.Format("ERROR!  Failed to initialize field '{0}'!  {1}", fieldDefinition.FieldName, ex.Message));
                return false;
            }

            // Return success
            return true;
        }

        /// <summary>
        /// Imports all data in the active worksheet.
        /// </summary>
        private void ImportWorksheet()
        {
            // Import the data
            LogMessage("Importing data...");
            var rowIndex = 0;
            var actualRowIndex = 0;
            var errorCount = 0;
            var sourceRowCount = 0;
            var destRowCount = 0;
            var dataBuffer = new List<T>();

            var importWatch = new Stopwatch();
            importWatch.Start();

            do
            {
                // Get the range that represents the row and determine if its empty
                actualRowIndex = _headerRange.TopRowIndex + rowIndex + 1;
                var rowRange = ActiveWorksheet.Range.FromLTRB(_headerRange.LeftColumnIndex, actualRowIndex, _headerRange.RightColumnIndex, actualRowIndex);
                var isRowEmpty = rowRange.All(c => string.IsNullOrWhiteSpace(c.DisplayText));

                // If the row is not empty, attempt to import it into the buffer
                if (!isRowEmpty)
                {
                    var dataModels = ImportRow(rowIndex);
                    if (dataModels != null && dataModels.Count > 0)
                    {
                        foreach (var dataModel in dataModels)
                        {
                            dataBuffer.Add(dataModel);
                            destRowCount++;
                        }
                    }
                    else
                    {
                        errorCount++;
                    }

                    sourceRowCount++;
                }

                // When the buffer is full, send the contents to the uploader
                if (dataBuffer.Count >= ImportBatchSize)
                {
                    AddUploaderData(dataBuffer.ToList());
                    dataBuffer.Clear();
                }

                // Move to the next row
                rowIndex++;
            } while (actualRowIndex < _dataRange.BottomRowIndex);

            // If there are any items left in the buffer, send them
            if (dataBuffer.Count > 0)
            {
                AddUploaderData(dataBuffer.ToList());
                dataBuffer.Clear();
            }
            
            // Log the success or failure
            importWatch.Stop();
            LogMessage(string.Format("Generated {0:N0} new {1} from {2:N0} source {3} with {4:N0} {5} in {6}.", destRowCount, destRowCount.Pluralize("row"), sourceRowCount, sourceRowCount.Pluralize("row"), errorCount, errorCount.Pluralize("error"), importWatch.Elapsed.Format()));

            if (errorCount == rowIndex)
                LogFail();
            else
                LogSuccess();
        }

        /// <summary>
        /// Creates a list of uploader data models based on the data at the specified row index.
        /// </summary>
        /// <param name="rowIndex">The index of the row to collect data from.</param>
        /// <returns>A list of objects containing data to send to the uploader.</returns>
        private List<T> ImportRow(int rowIndex)
        {
            var rows = new List<Dictionary<string, object>>();
            var currentRow = new Dictionary<string, object>();
            var rowHasErrors = false;

            // Process each field
            foreach (var field in Fields)
            {
                // Process a field group
                var fieldGroup = field as SpreadsheetFieldGroup;
                if (fieldGroup != null)
                {
                    // Attempt to generate data for the grouped fields
                    var groupRows = ImportRowGroup(fieldGroup, rowIndex);
                    if (groupRows != null)
                    {
                        // If data was generated, create a row for each group by combining the grouped data with the ungrouped data
                        foreach (var groupRow in groupRows)
                        {
                            var combinedRow = new Dictionary<string, object>(currentRow);
                            foreach (var fieldValue in groupRow)
                            {
                                combinedRow.Add(fieldValue.Key, fieldValue.Value);
                            }
                            rows.Add(combinedRow);
                        }
                    }
                    else
                    {
                        // If no data was generated, then the row contains errors
                        rowHasErrors = true;
                    }
                }

                // Process a standard field
                var fieldDefinition = field as SpreadsheetFieldDefinitionBase;
                if (fieldDefinition != null)
                {
                    try
                    {
                        // Get details about the field
                        var fieldName = fieldDefinition.FieldName;
                        var fieldValue = fieldDefinition.GetValue(rowIndex, currentRow);
                        var wrapper = GetFieldWrapper(fieldName);

                        // Validate the field value
                        var errorMessage = wrapper.Validate(fieldValue, null);
                        if (errorMessage != null)
                        {
                            // If the field has errors, record the error
                            rowHasErrors = true;
                            fieldDefinition.AppendError(rowIndex, errorMessage);
                        }
                        else
                        {
                            // If the field didn't have errors, add the field value to the current row and any existing rows created by groups
                            currentRow.Add(fieldName, fieldValue);
                            foreach (var row in rows)
                            {
                                row.Add(fieldName, fieldValue);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        rowHasErrors = true;
                        fieldDefinition.AppendError(rowIndex, ex.Message);
                    }
                }
            }

            // Return null if any errors occurred during the import
            if (rowHasErrors)
                return null;

            // If no rows were created by grouped fields, create a row without grouped field values
            if (rows.Count == 0)
            {
                rows.Add(new Dictionary<string, object>(currentRow));
            }

            // Convert all rows to data models
            var dataModels = new List<T>();
            foreach (var row in rows)
            {
                // Create an instance of the uploader data
                var dataModel = Activator.CreateInstance<T>();

                foreach (var rowData in row)
                {
                    // Attempt to get the wrapper for the field
                    var wrapper = GetFieldWrapper(rowData.Key);

                    // Attempt to get a property descriptor from the wrapper
                    var propertyDescriptor = wrapper.Property.ContextObject as PropertyDescriptor;
                    if (propertyDescriptor == null)
                        throw new SpreadsheetFieldException(string.Format("Failed to collect property information for field '{0}'.", rowData.Key));

                    // Write the value to the property
                    propertyDescriptor.SetValue(dataModel, rowData.Value);
                }

                dataModels.Add(dataModel);
            }

            // Return the list of data models
            return dataModels;
        }

        /// <summary>
        /// Creates a list of rows based on the grouped data at the specified row index.
        /// </summary>
        /// <param name="fieldGroup">The field group to proces.</param>
        /// <param name="rowIndex">The index of the row to collect data from.</param>
        /// <returns>A list of objects containing grouped row data.</returns>
        private List<Dictionary<string, object>> ImportRowGroup(SpreadsheetFieldGroup fieldGroup, int rowIndex)
        {
            var rows = new List<Dictionary<string, object>>();
            var groupHasErrors = false;

            // Process each possible group index
            for (var groupIndex = 1; groupIndex <= fieldGroup.ColumnCount; groupIndex++)
            {
                // Create a new row
                var currentRow = new Dictionary<string, object>();
                var rowHasErrors = false;

                // Populate values in the row for each field in the group
                foreach (var field in fieldGroup.Contents)
                {
                    try
                    {
                        currentRow.Add(field.FieldName, field.GetValue(rowIndex, groupIndex, currentRow));
                    }
                    catch (Exception ex)
                    {
                        groupHasErrors = true;
                        rowHasErrors = true;
                        field.AppendError(rowIndex, groupIndex, ex.Message);
                    }
                }

                // If the row is not empty, validate it and add it to the result list if it is valid
                var isRowEmpty = currentRow.All(r => r.Value == null);
                if (!isRowEmpty)
                {
                    foreach (var field in fieldGroup.Contents)
                    {
                        var fieldName = field.FieldName;
                        if (!currentRow.ContainsKey(fieldName))
                            continue;

                        var fieldValue = currentRow[fieldName];
                        var wrapper = GetFieldWrapper(fieldName);
                        var errorMessage = wrapper.Validate(fieldValue, null);
                        if (errorMessage != null)
                        {
                            groupHasErrors = true;
                            rowHasErrors = true;
                            field.AppendError(rowIndex, groupIndex, errorMessage);
                        }
                    }

                    if (!rowHasErrors)
                        rows.Add(currentRow);
                }
            }

            // If the group has any errors, return null
            if (groupHasErrors)
                return null;

            // Return the result list
            return rows;
        }

        /// <summary>
        /// Attempts to get a column wrapper for the specified field.
        /// </summary>
        /// <param name="fieldName">The name of the field to collect the column wrapper for.</param>
        /// <returns>The GridColumnWrapper for the specified field.</returns>
        private GridColumnWrapper GetFieldWrapper(string fieldName)
        {
            GridColumnWrapper wrapper;
            if (!_columns.TryGetValue(fieldName, out wrapper))
                throw new SpreadsheetFieldException(string.Format("Failed to find target property for field '{0}'.", fieldName));

            return wrapper;
        }

        /// <summary>
        /// Sends a message to clear the uploader.
        /// </summary>
        private void ClearUploaderData()
        {
            var uploaderDataMessageToken = UploaderDataMessageToken;
            if (uploaderDataMessageToken != null)
                Application.Current.Dispatcher.BeginInvoke(new Action(() => SendDocumentMessage(uploaderDataMessageToken, new ClearUploaderDataMessage(this))));
        }

        /// <summary>
        /// Sends a message to add data to the uploader.
        /// </summary>
        /// <param name="data">A list of the data to send.</param>
        private void AddUploaderData(IEnumerable data)
        {
            var uploaderDataMessageToken = UploaderDataMessageToken;
            if (uploaderDataMessageToken != null)
                Application.Current.Dispatcher.BeginInvoke(new Action(() => SendDocumentMessage(uploaderDataMessageToken, new AddUploaderDataMessage(this, data))));
        }

        /// <summary>
        /// Sends a message to the log widget.
        /// </summary>
        /// <param name="message">The message to send.</param>
        private void LogMessage(string message)
        {
            var documentViewModel = DocumentViewModel;
            if (documentViewModel != null)
                Application.Current.Dispatcher.BeginInvoke(new Action(() => SendDocumentMessage(new AppendLogMessage(this, message))));
        }

        /// <summary>
        /// Sends a message to the log widget to indicate that the import has started.
        /// </summary>
        private void LogStart()
        {
            _headerRange = null;
            _dataRange = null;

            LogMessage("*** IMPORT START ***");
        }

        /// <summary>
        /// Sends a message to the log widget to indicate that the import was successful.
        /// </summary>
        private void LogSuccess()
        {
            _headerRange = null;
            _dataRange = null;

            LogMessage("*** IMPORT SUCCESSFUL ***");
        }

        /// <summary>
        /// Sends a message to the log widget to indicate that the import has failed.
        /// </summary>
        private void LogFail()
        {
            _headerRange = null;
            _dataRange = null;

            LogMessage("*** IMPORT FAILED ***");
        }

        /// <summary>
        /// Gets a property name from the supplied property expression.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <returns>The name of the property.</returns>
        private static string GetPropertyNameFromExpression<TProperty>(Expression<Func<T, TProperty>> propertyExpression)
        {
            // Get the property name
            var memberExpression = (MemberExpression)propertyExpression.Body;
            var property = memberExpression.Member as PropertyInfo;
            if (property == null)
                return null;

            return property.Name;
        }

        /// <summary>
        /// Populates column wrappers for all properties on the generic type.
        /// </summary>
        private void PopulateColumns()
        {
            // Create column wrappers for each visible property on the entity being displayed
            _columns = new Dictionary<string, GridColumnWrapper>();
            foreach (var propertyWrapper in typeof(T).GetVisibleAndAliasedProperties(LayoutType.Table))
            {
                _columns.Add(propertyWrapper.Property.Name, new GridColumnWrapper(propertyWrapper));
            }

            // Call the EditorMetadataBuilder to allow extended editor customisation
            EditorMetadataBuilder.Build<T>(_columns.Values);
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Create a SpreadsheetFieldGroup.
        /// </summary>
        /// <param name="contents">The contents of the field group.</param>
        /// <returns>A new SpreadsheetFieldGroup.</returns>
        protected SpreadsheetFieldGroup CreateFieldGroup(GroupableSpreadsheetFieldDefinitionBase[] contents)
        {
            return new SpreadsheetFieldGroup(contents);
        }

        /// <summary>
        /// Creates a FixedSpreadsheetFieldDefinition.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <param name="sourceType">The type of value that will be collected from the spreadsheet.</param>
        /// <param name="cellReference">A cell reference in A1 style which defines where the field will get its value from.</param>
        /// <returns>A new FixedSpreadsheetFieldDefinition.</returns>
        protected FixedSpreadsheetFieldDefinition CreateFixedField<TProperty>(Expression<Func<T, TProperty>> propertyExpression, SpreadsheetDataType sourceType, string cellReference)
        {
            return CreateFixedField(propertyExpression, sourceType, null, cellReference);
        }

        /// <summary>
        /// Creates a FixedSpreadsheetFieldDefinition with a converter.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <param name="sourceType">The type of value that will be collected from the spreadsheet.</param>
        /// <param name="convertValueMethod">A method that converts the spreadsheet value to a value that will be stored in the uploader data model.</param>
        /// <param name="cellReference">A cell reference in A1 style which defines where the field will get its value from.</param>
        /// <returns>A new FixedSpreadsheetFieldDefinition.</returns>
        protected FixedSpreadsheetFieldDefinition CreateFixedField<TProperty>(Expression<Func<T, TProperty>> propertyExpression, SpreadsheetDataType sourceType, SpreadsheetFieldDefinitionBase.ConvertValueDelegate convertValueMethod, string cellReference)
        {
            return new FixedSpreadsheetFieldDefinition(GetPropertyNameFromExpression(propertyExpression), sourceType, convertValueMethod, cellReference);
        }

        /// <summary>
        /// Creates a NamedSpreadsheetFieldDefinition.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <param name="sourceType">The type of value that will be collected from the spreadsheet.</param>
        /// <param name="columnNames">An array of column names that the field value can be collected from.</param>
        /// <returns>A new NamedSpreadsheetFieldDefinition.</returns>
        protected NamedSpreadsheetFieldDefinition CreateNamedField<TProperty>(Expression<Func<T, TProperty>> propertyExpression, SpreadsheetDataType sourceType, params string[] columnNames)
        {
            return CreateNamedField(propertyExpression, sourceType, null, columnNames);
        }

        /// <summary>
        /// Creates a NamedSpreadsheetFieldDefinition with a converter.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <param name="sourceType">The type of value that will be collected from the spreadsheet.</param>
        /// <param name="convertValueMethod">A method that converts the spreadsheet value to a value that will be stored in the uploader data model.</param>
        /// <param name="columnNames">An array of column names that the field value can be collected from.</param>
        /// <returns>A new NamedSpreadsheetFieldDefinition.</returns>
        protected NamedSpreadsheetFieldDefinition CreateNamedField<TProperty>(Expression<Func<T, TProperty>> propertyExpression, SpreadsheetDataType sourceType, SpreadsheetFieldDefinitionBase.ConvertValueDelegate convertValueMethod, params string[] columnNames)
        {
            return new NamedSpreadsheetFieldDefinition(GetPropertyNameFromExpression(propertyExpression), sourceType, convertValueMethod, columnNames);
        }

        /// <summary>
        /// Creates a NumberedSpreadsheetFieldDefinition.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <param name="sourceType">The type of value that will be collected from the spreadsheet.</param>
        /// <param name="columnNumber">The position of the column to collect the field value from.</param>
        /// <returns>A new NumberedSpreadsheetFieldDefinition.</returns>
        protected NumberedSpreadsheetFieldDefinition CreateNumberedField<TProperty>(Expression<Func<T, TProperty>> propertyExpression, SpreadsheetDataType sourceType, int columnNumber)
        {
            return CreateNumberedField(propertyExpression, sourceType, null, columnNumber);
        }

        /// <summary>
        /// Creates a NumberedSpreadsheetFieldDefinition with a converter.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <param name="sourceType">The type of value that will be collected from the spreadsheet.</param>
        /// <param name="convertValueMethod">A method that converts the spreadsheet value to a value that will be stored in the uploader data model.</param>
        /// <param name="columnNumber">The position of the column to collect the field value from.</param>
        /// <returns>A new NumberedSpreadsheetFieldDefinition.</returns>
        protected NumberedSpreadsheetFieldDefinition CreateNumberedField<TProperty>(Expression<Func<T, TProperty>> propertyExpression, SpreadsheetDataType sourceType, SpreadsheetFieldDefinitionBase.ConvertValueDelegate convertValueMethod, int columnNumber)
        {
            return new NumberedSpreadsheetFieldDefinition(GetPropertyNameFromExpression(propertyExpression), sourceType, convertValueMethod, columnNumber);
        }

        /// <summary>
        /// Creates a ConstantSpreadsheetFieldDefinition.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <param name="value">The constant value of this field.</param>
        /// <returns>A new ConstantSpreadsheetFieldDefinition.</returns>
        protected ConstantSpreadsheetFieldDefinition CreateConstantField<TProperty>(Expression<Func<T, TProperty>> propertyExpression, object value)
        {
            return CreateConstantField(propertyExpression, null, value);
        }

        /// <summary>
        /// Creates a ConstantSpreadsheetFieldDefinition.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <param name="convertValueMethod">A method that converts the constant value to a value that will be stored in the uploader data model.</param>
        /// <param name="value">The constant value of this field.</param>
        /// <returns>A new ConstantSpreadsheetFieldDefinition.</returns>
        protected ConstantSpreadsheetFieldDefinition CreateConstantField<TProperty>(Expression<Func<T, TProperty>> propertyExpression, SpreadsheetFieldDefinitionBase.ConvertValueDelegate convertValueMethod, object value)
        {
            return new ConstantSpreadsheetFieldDefinition(GetPropertyNameFromExpression(propertyExpression), convertValueMethod, value);
        }

        #endregion
    }
}
