using System.Linq;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Core;

namespace TIG.TotalLink.Client.Module.Admin.SpreadsheetField
{
    public class SpreadsheetFieldGroup : SpreadsheetFieldBase
    {
        #region Constructors

        public SpreadsheetFieldGroup(GroupableSpreadsheetFieldDefinitionBase[] contents)
        {
            Contents = contents;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// An array of fields that this spreadsheet field group contains.
        /// </summary>
        public GroupableSpreadsheetFieldDefinitionBase[] Contents { get; private set; }

        /// <summary>
        /// The number of times the columns in this group are repeated.
        /// This will be the max ColumnCount of the fields in this group.
        /// </summary>
        public int ColumnCount
        {
            get { return Contents.Max(f => f.ColumnCount); }
        }

        #endregion
    }
}
