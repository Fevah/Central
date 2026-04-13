using System.Collections.Generic;
using DevExpress.Spreadsheet;

namespace TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Core
{
    public abstract class GroupableSpreadsheetFieldDefinitionBase : SpreadsheetFieldDefinitionBase
    {
        #region Constructors

        protected GroupableSpreadsheetFieldDefinitionBase(string fieldName, SpreadsheetDataType sourceType, ConvertValueDelegate convertValueMethod)
            : base(fieldName, sourceType, convertValueMethod)
        {
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The number of columns that this grouped field represents.
        /// Inheriters should override this and return the number of columns found in InitializeGroup.
        /// </summary>
        public virtual int ColumnCount
        {
            get { return 0; }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Initializes the field when it is contained within a field group.
        /// Inheriters should override this, perform any intialization required, and throw a SpreadsheetFieldException if the field could not be initialized.
        /// </summary>
        /// <param name="dataRange">The range of available data.</param>
        /// <param name="headerRange">The range of available column headers.</param>
        public virtual void InitializeGroup(Range dataRange, Range headerRange)
        {
            ClearErrors();
            Worksheet = dataRange.Worksheet;
        }

        /// <summary>
        /// Gets the value for this field based on the supplied row index and group index when it is contained within a field group.
        /// Inheriters should override this and return the value that shoud be stored in the uploader data.
        /// </summary>
        /// <param name="rowIndex">The index of the row to collect the value for.</param>
        /// <param name="groupIndex">The index of the grouped field to collect the value for.</param>
        /// <param name="currentRow">A dictionary containing the other row values that have been collected.</param>
        /// <returns>The value for this field as the specified row index.</returns>
        public virtual object GetValue(int rowIndex, int groupIndex, Dictionary<string, object> currentRow)
        {
            return null;
        }

        /// <summary>
        /// Appends an error message to this field based on the supplied row index and group index when it is contained within a field group.
        /// Inheriters should override this and call AddError for the cell at the specified row index.
        /// </summary>
        /// <param name="rowIndex">The index of the row to set the error on.</param>
        /// <param name="groupIndex">The index of the grouped field to set the error on.</param>
        /// <param name="errorMessage">The error message to append.</param>
        public virtual void AppendError(int rowIndex, int groupIndex, string errorMessage)
        {
        }

        #endregion
    }
}
