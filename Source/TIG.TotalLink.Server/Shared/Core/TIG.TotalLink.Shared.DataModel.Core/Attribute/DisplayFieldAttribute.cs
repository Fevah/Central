using System;

namespace TIG.TotalLink.Shared.DataModel.Core.Attribute
{
    /// <summary>
    /// Defines the default field name for a data object.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DisplayFieldAttribute : System.Attribute
    {
        #region Constructors

        public DisplayFieldAttribute(string fieldName)
        {
            FieldName = fieldName;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The name of the default field that is used when displaying a data object.
        /// </summary>
        public string FieldName { get; private set; }

        #endregion
    }
}
