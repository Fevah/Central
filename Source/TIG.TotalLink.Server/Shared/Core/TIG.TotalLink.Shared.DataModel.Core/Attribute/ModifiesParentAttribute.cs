using System;

namespace TIG.TotalLink.Shared.DataModel.Core.Attribute
{
    /// <summary>
    /// Indicates that when this object is modified, the object in the specified parent property should also be marked as modified.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class ModifiesParentAttribute : System.Attribute
    {
        #region Constructors

        public ModifiesParentAttribute(string parentPropertyName)
        {
            ParentPropertyName = parentPropertyName;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The name of the property that contains the parent object to mark as modified.
        /// </summary>
        public string ParentPropertyName { get; private set; }

        #endregion
    }
}
