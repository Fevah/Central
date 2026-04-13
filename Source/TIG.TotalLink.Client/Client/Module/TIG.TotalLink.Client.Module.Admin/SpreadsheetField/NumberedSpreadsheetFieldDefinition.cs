using System.Collections.Generic;
using DevExpress.Spreadsheet;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Core;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Exception;

namespace TIG.TotalLink.Client.Module.Admin.SpreadsheetField
{
    /// <summary>
    /// Defines a spreadsheet field that collects its value from a column at the specified position.
    /// </summary>
    public class NumberedSpreadsheetFieldDefinition : SpreadsheetFieldDefinitionBase
    {
        #region Private Fields

        private Cell _headerCell;

        #endregion


        #region Constructors

        public NumberedSpreadsheetFieldDefinition(string fieldName, SpreadsheetDataType sourceType, ConvertValueDelegate convertValueMethod, int columnNumber)
            : base(fieldName, sourceType, convertValueMethod)
        {
            ColumnNumber = columnNumber;
        }

        public NumberedSpreadsheetFieldDefinition(string fieldName, SpreadsheetDataType sourceType, int columnNumber)
            : this(fieldName, sourceType, null, columnNumber)
        {
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The position of the column to collect the field value from.
        /// </summary>
        public int ColumnNumber { get; private set; }

        #endregion


        #region Overrides

        public override void Initialize(Range dataRange, Range headerRange)
        {
            base.Initialize(dataRange, headerRange);

            _headerCell = null;

            // Get the cell specified by the ColumnNumber
            var cell = headerRange[0, ColumnNumber];

            // Throw an error if the cell is not within the headerRange
            if (!cell.IsIntersecting(headerRange))
            {
                throw new SpreadsheetFieldException(string.Format("Column number {0} is not within the range of column headers found!", ColumnNumber));
            }

            // Store the cell
            _headerCell = cell;
        }

        public override object GetValue(int rowIndex, Dictionary<string, object> currentRow)
        {
            // Abort if we haven't found a header cell
            if (_headerCell == null)
                return null;

            // Get the cell from the specified row
            var cell = _headerCell[rowIndex + 1, 0];

            // Get the value from the cell, convert it, and return it
            return ConvertValue(GetCellValue(cell), currentRow);
        }

        public override void AppendError(int rowIndex, string errorMessage)
        {
            // Abort if we haven't found a header cell
            if (_headerCell == null)
                return;

            // Get the cell from the specified row
            var cell = _headerCell[rowIndex + 1, 0];

            // Set the error message on the cell
            AddError(cell, errorMessage);
        }

        #endregion
    }
}
