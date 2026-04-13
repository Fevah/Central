using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Spreadsheet;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Core;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Exception;

namespace TIG.TotalLink.Client.Module.Admin.SpreadsheetField
{
    /// <summary>
    /// Defines a spreadsheet field that collects its value from a column with any of the specified names.
    /// If more than one of these column names exists in the spreadsheet being imported, the value will only be collected from the leftmost match.
    /// </summary>
    public class NamedSpreadsheetFieldDefinition : GroupableSpreadsheetFieldDefinitionBase
    {
        #region Private Fields

        private List<Cell> _headerCells = new List<Cell>();

        #endregion


        #region Constructors

        public NamedSpreadsheetFieldDefinition(string fieldName, SpreadsheetDataType sourceType, ConvertValueDelegate convertValueMethod, params string[] columnNames)
            : base(fieldName, sourceType, convertValueMethod)
        {
            ColumnNames = columnNames;
        }

        public NamedSpreadsheetFieldDefinition(string fieldName, SpreadsheetDataType sourceType, params string[] columnNames)
            : this(fieldName, sourceType, null, columnNames)
        {
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// An array of column names that the field value can be collected from.
        /// </summary>
        public string[] ColumnNames { get; private set; }

        #endregion


        #region Private Methods

        public string InitializeFirst(Range dataRange, Range headerRange)
        {
            _headerCells.Clear();

            // Attempt to find a cell within the headerRange that contains one of the ColumnNames with a group index of 1
            foreach (var columnName in ColumnNames.Select(s => s.Trim()))
            {
                var groupedColumnName = string.Format(columnName, 1);
                foreach (var cell in headerRange)
                {
                    if (string.Equals(cell.DisplayText.Trim(), groupedColumnName, StringComparison.CurrentCultureIgnoreCase))
                    {
                        _headerCells.Add(cell);
                        return columnName;
                    }
                }
            }

            // Throw an error if no cell was found
            throw new SpreadsheetFieldException(string.Format("Could not find a column header that matches {0}{1}!", (ColumnNames.Length > 1 ? "any of " : string.Empty), string.Join(", ", ColumnNames.Select(s => string.Format("'{0}'", s.Trim())))));
        }

        #endregion


        #region Overrides

        public override int ColumnCount
        {
            get { return _headerCells.Count; }
        }

        public override void Initialize(Range dataRange, Range headerRange)
        {
            base.Initialize(dataRange, headerRange);

            InitializeFirst(dataRange, headerRange);
        }

        public override void InitializeGroup(Range dataRange, Range headerRange)
        {
            base.InitializeGroup(dataRange, headerRange);

            // Attempt to find the column with a group index of 1
            var columnName = InitializeFirst(dataRange, headerRange);
            if (columnName == null)
                return;

            // Find all cells within the headerRange that contain the matchedColumnName, by increasing the groupIndex until we can't find any more
            var groupIndex = 2;
            bool cellFound;
            do
            {
                cellFound = false;
                var groupedColumnName = string.Format(columnName, groupIndex);
                foreach (var cell in headerRange)
                {
                    if (string.Equals(cell.DisplayText.Trim(), groupedColumnName, StringComparison.CurrentCultureIgnoreCase))
                    {
                        _headerCells.Add(cell);
                        cellFound = true;
                        break;
                    }
                }
                groupIndex++;
            } while (cellFound);
        }

        public override object GetValue(int rowIndex, Dictionary<string, object> currentRow)
        {
            return GetValue(rowIndex, 1, currentRow);
        }

        public override object GetValue(int rowIndex, int groupIndex, Dictionary<string, object> currentRow)
        {
            // Abort if we haven't found a header cell for this group index
            if (_headerCells.Count < groupIndex)
                return null;

            // Get the cell from the specified row
            var cell = _headerCells[groupIndex - 1][rowIndex + 1, 0];

            // Get the value from the cell, convert it, and return it
            return ConvertValue(GetCellValue(cell), currentRow);
        }

        public override void AppendError(int rowIndex, string errorMessage)
        {
            AppendError(rowIndex, 1, errorMessage);
        }

        public override void AppendError(int rowIndex, int groupIndex, string errorMessage)
        {
            // Abort if we haven't found a header cell for this group index
            if (_headerCells.Count < groupIndex)
                return;

            // Get the cell from the specified row
            var cell = _headerCells[groupIndex - 1][rowIndex + 1, 0];

            // Set the error message on the cell
            AddError(cell, errorMessage);
        }

        #endregion
    }
}
