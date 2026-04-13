using System.Collections.Generic;
using TIG.TotalLink.Client.Module.Admin.SpreadsheetField.Core;

namespace TIG.TotalLink.Client.Module.Admin.SpreadsheetField
{
    /// <summary>
    /// Defines a spreadsheet field that returns a constant value.
    /// </summary>
    public class ConstantSpreadsheetFieldDefinition : SpreadsheetFieldDefinitionBase
    {
        #region Private Fields

        private object _value;

        #endregion


        #region Constructors

        public ConstantSpreadsheetFieldDefinition(string fieldName, object value)
            : base(fieldName, SpreadsheetDataType.None)
        {
            Value = value;
        }

        public ConstantSpreadsheetFieldDefinition(string fieldName, ConvertValueDelegate convertValueMethod, object value)
            : base(fieldName, SpreadsheetDataType.None, convertValueMethod)
        {
            Value = value;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The constant value of this field.
        /// </summary>
        public object Value { get; private set; }

        #endregion


        #region Overrides

        public override object GetValue(int rowIndex, Dictionary<string, object> currentRow)
        {
            // Convert the value, and return it
            return ConvertValue(Value, currentRow);
        }

        public override void AppendError(int rowIndex, string errorMessage)
        {
            // We can't display errors for a constant field because it's not associated with any specific cell
        }

        #endregion
    }
}
