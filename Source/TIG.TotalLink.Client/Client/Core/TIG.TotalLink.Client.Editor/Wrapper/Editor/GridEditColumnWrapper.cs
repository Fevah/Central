using DevExpress.Entity.Model;
using TIG.TotalLink.Client.Core.Helper;
using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Wrapper.Editor
{
    /// <summary>
    /// Wrapper for a property being displayed in a GridEdit.
    /// </summary>
    public class GridEditColumnWrapper : GridColumnWrapperBase
    {
        #region Constructors

        public GridEditColumnWrapper(System.Type type, IEdmPropertyInfo property)
            : base(type, property)
        {
        }

        public GridEditColumnWrapper(VisiblePropertyWrapper propertyWrapper)
            : base(propertyWrapper)
        {
        }

        #endregion
    }
}
