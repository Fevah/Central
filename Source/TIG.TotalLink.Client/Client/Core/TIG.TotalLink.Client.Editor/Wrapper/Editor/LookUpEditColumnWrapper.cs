using DevExpress.Entity.Model;
using TIG.TotalLink.Client.Core.Helper;
using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Wrapper.Editor
{
    /// <summary>
    /// Wrapper for a property being displayed in a LookUpEdit.
    /// </summary>
    public class LookUpEditColumnWrapper : GridColumnWrapperBase
    {
        #region Constructors

        public LookUpEditColumnWrapper(System.Type type, IEdmPropertyInfo property)
            : base(type, property)
        {
            IsReadOnly = true;
        }

        public LookUpEditColumnWrapper(VisiblePropertyWrapper propertyWrapper)
            : base(propertyWrapper)
        {
            IsReadOnly = true;
        }

        #endregion
    }
}
