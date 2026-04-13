using DevExpress.Entity.Model;
using TIG.TotalLink.Client.Core.Helper;
using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Wrapper.Editor
{
    /// <summary>
    /// Wrapper for a property being displayed in a standalone GridControl.
    /// </summary>
    public class GridColumnWrapper : GridColumnWrapperBase
    {
        #region Constructors

        public GridColumnWrapper(System.Type type, IEdmPropertyInfo property)
            : base(type, property)
        {
        }

        public GridColumnWrapper(VisiblePropertyWrapper propertyWrapper)
            : base(propertyWrapper)
        {
        }

        #endregion
    }
}
