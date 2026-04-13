using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DevExpress.Spreadsheet;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Exception;

namespace TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Core
{
    /// <summary>
    /// Base class for defining the location of a field within a spreadsheet.
    /// </summary>
    public abstract class SpreadsheetFieldDefinitionBase : SpreadsheetFieldBase
    {
        #region Public Delegates

        public delegate object ConvertValueDelegate(object value, Dictionary<string, object> currentRow);

        #endregion


        #region Private Fields

        private string[] _booleanStringOff = { "0", "off", "no" };
        private string[] _booleanStringOn = { "1", "on", "yes" };
        private Dictionary<string, List<string>> _cellErrors = new Dictionary<string, List<string>>();

        #endregion


        #region Constructors

        protected SpreadsheetFieldDefinitionBase(string fieldName, SpreadsheetDataType sourceType)
        {
            FieldName = fieldName;
            SourceType = sourceType;
        }

        protected SpreadsheetFieldDefinitionBase(string fieldName, SpreadsheetDataType sourceType, ConvertValueDelegate convertValueMethod)
            : this(fieldName, sourceType)
        {
            ConvertValueMethod = convertValueMethod;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The name of the property that this field will be stored in.
        /// </summary>
        public string FieldName { get; private set; }

        /// <summary>
        /// The type of value that will be collected from the spreadsheet.
        /// </summary>
        public SpreadsheetDataType SourceType { get; private set; }

        /// <summary>
        /// A method that converts the spreadsheet value to a value that will be stored in the uploader data model.
        /// </summary>
        public ConvertValueDelegate ConvertValueMethod { get; private set; }

        /// <summary>
        /// The worksheet that this field collects its data from.
        /// </summary>
        public Worksheet Worksheet { get; protected set; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Initializes the field.
        /// Inheriters should override this, perform any intialization required, and throw a SpreadsheetFieldException if the field could not be initialized.
        /// </summary>
        /// <param name="dataRange">The range of available data.</param>
        /// <param name="headerRange">The range of available column headers.</param>
        public virtual void Initialize(Range dataRange, Range headerRange)
        {
            ClearErrors();
            Worksheet = dataRange.Worksheet;
        }

        /// <summary>
        /// Gets the value for this field based on the supplied row index.
        /// Inheriters should override this and return the value that shoud be stored in the uploader data.
        /// </summary>
        /// <param name="rowIndex">The index of the row to collect the value for.</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value for this field as the specified row index.</returns>
        public virtual object GetValue(int rowIndex, Dictionary<string, object> currentRow)
        {
            return null;
        }

        /// <summary>
        /// Appends an error message to this field based on the supplied row index.
        /// Inheriters should override this and call AddError for the cell at the specified row index.
        /// </summary>
        /// <param name="rowIndex">The index of the row to set the error on.</param>
        /// <param name="errorMessage">The error message to append.</param>
        public virtual void AppendError(int rowIndex, string errorMessage)
        {
        }

        /// <summary>
        /// Clears all errors on this field.
        /// </summary>
        public virtual void ClearErrors()
        {
            // Clear the errors list
            _cellErrors.Clear();
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Gets a value from a cell based on the SourceType.
        /// </summary>
        /// <param name="cell">The cell to collect the value from.</param>
        /// <returns>The value from the cell.</returns>
        protected object GetCellValue(Cell cell)
        {
            // Get the display text
            object resultValue = null;
            var displayText = cell.DisplayText.Trim();

            // Abort if the display text is empty
            if (string.IsNullOrWhiteSpace(displayText))
                return null;

            switch (SourceType)
            {
                case SpreadsheetDataType.String:
                    // String will return a string value or null
                    resultValue = displayText;
                    break;

                case SpreadsheetDataType.DateTime:
                    // DateTime will return a UTC DateTime value or null
                    if (cell.Value.IsDateTime)
                    {
                        if (cell.Value.DateTimeValue.Year < 1900)
                            throw new SpreadsheetFieldException("Value must be a date with an optional time.");

                        resultValue = cell.Value.DateTimeValue.ToUniversalTime();
                    }
                    else if (!cell.Value.IsText)
                    {
                        throw new SpreadsheetFieldException("Value must be a date with an optional time.");
                    }
                    else
                    {
                        if (!displayText.Contains(CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator))
                            throw new SpreadsheetFieldException("Value must be a date with an optional time.");

                        DateTime parsedValue;
                        if (DateTime.TryParse(displayText, out parsedValue))
                            resultValue = parsedValue.ToUniversalTime();
                        else
                            throw new SpreadsheetFieldException("Value must be a date with an optional time.");
                    }
                    break;

                case SpreadsheetDataType.Boolean:
                    // Boolean will return a bool value or null
                    if (cell.Value.IsBoolean)
                    {
                        resultValue = cell.Value.BooleanValue;
                    }
                    else if (_booleanStringOff.Contains(displayText, StringComparer.InvariantCultureIgnoreCase))
                    {
                        resultValue = false;
                    }
                    else if (_booleanStringOn.Contains(displayText, StringComparer.InvariantCultureIgnoreCase))
                    {
                        resultValue = true;
                    }
                    else
                    {
                        bool parsedValue;
                        if (bool.TryParse(displayText, out parsedValue))
                            resultValue = parsedValue;
                        else
                            throw new SpreadsheetFieldException("Value must be tue or false.");
                    }
                    break;

                case SpreadsheetDataType.Numeric:
                    // Numeric will return a double value or null
                    if (cell.Value.IsNumeric && !cell.Value.IsDateTime)
                    {
                        resultValue = cell.Value.NumericValue;
                    }
                    else if (!cell.Value.IsText)
                    {
                        throw new SpreadsheetFieldException("Value must be a number.");
                    }
                    else
                    {
                        double parsedValue;
                        if (double.TryParse(displayText, out parsedValue))
                            resultValue = parsedValue;
                        else
                            throw new SpreadsheetFieldException("Value must be a number.");
                    }

                    break;
            }

            return resultValue;
        }

        /// <summary>
        /// Converts the spreadsheet value to a value that will be stored in the uploader data model.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The converted value.</returns>
        protected object ConvertValue(object value, Dictionary<string, object> currentRow)
        {
            // If no ConvertValueMethod has been specified, return the value unchanged
            if (ConvertValueMethod == null)
                return value;

            // Otherwise, call the ConvertValueMethod and return the result
            return ConvertValueMethod(value, currentRow);
        }

        /// <summary>
        /// Adds an error to the list of errors for the supplied cell.
        /// </summary>
        /// <param name="cell">The cell that the error applies to.</param>
        /// <param name="errorMessage">The error message to add to the cell.</param>
        protected void AddError(Cell cell, string errorMessage)
        {
            // Attempt to get the list of errors for this cell
            var cellReference = cell.GetReferenceA1();
            List<string> errorList;
            _cellErrors.TryGetValue(cellReference, out errorList);

            // If no error list was found, create one and add it to the dictionary
            if (errorList == null)
            {
                errorList = new List<string>();
                _cellErrors.Add(cellReference, errorList);
            }

            // If the error list doesn't already contain this error message, add it and update the error list on the cell
            if (!errorList.Contains(errorMessage))
            {
                errorList.Add(errorMessage);
                cell.Tag = string.Join("\r\n", errorList);

                // If the cell is empty, set a value so that the error shows up
                if (cell.Value.IsEmpty)
                {
                    cell.Value = " ";
                }
            }
        }

        #endregion
    }
}
