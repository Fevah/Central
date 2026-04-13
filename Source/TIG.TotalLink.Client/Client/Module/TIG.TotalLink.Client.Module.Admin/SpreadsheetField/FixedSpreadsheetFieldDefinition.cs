using System.Collections.Generic;
using DevExpress.Spreadsheet;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Core;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Exception;

namespace TIG.TotalLink.Client.Module.Admin.SpreadsheetField
{
    /// <summary>
    /// Defines a spreadsheet field that collects its value from a fixed cell.
    /// </summary>
    public class FixedSpreadsheetFieldDefinition : SpreadsheetFieldDefinitionBase
    {
        #region Private Fields

        private Cell _cell;

        #endregion


        #region Constructors

        public FixedSpreadsheetFieldDefinition(string fieldName, SpreadsheetDataType sourceType, ConvertValueDelegate convertValueMethod, string cellReference)
            : base(fieldName, sourceType, convertValueMethod)
        {
            CellReference = cellReference;
        }

        public FixedSpreadsheetFieldDefinition(string fieldName, SpreadsheetDataType sourceType, string cellReference)
            : this(fieldName, sourceType, null, cellReference)
        {
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// A cell reference in A1 style which defines where the field will get its value from.
        /// </summary>
        public string CellReference { get; private set; }

        #endregion


        #region Overrides

        public override void Initialize(Range dataRange, Range headerRange)
        {
            base.Initialize(dataRange, headerRange);

            _cell = null;

            // Get the cell specified by the CellReference
            var cell = dataRange.Worksheet[CellReference][0];

            // Throw an error if the cell is not within the dataRange
            if (!cell.IsIntersecting(dataRange))
            {
                throw new SpreadsheetFieldException(string.Format("The cell {0} is not within the range of data found!", CellReference));
            }

            // Store the cell
            _cell = cell;
        }

        public override object GetValue(int rowIndex, Dictionary<string, object> currentRow)
        {
            // Abort if we haven't found a cell
            if (_cell == null)
                return null;

            // Get the value from the cell, convert it, and return it
            return ConvertValue(GetCellValue(_cell), currentRow);
        }

        public override void AppendError(int rowIndex, string errorMessage)
        {
            // Abort if we haven't found a cell
            if (_cell == null)
                return;

            // Set the error message on the cell
            AddError(_cell, errorMessage);
        }

        #endregion
    }
}
