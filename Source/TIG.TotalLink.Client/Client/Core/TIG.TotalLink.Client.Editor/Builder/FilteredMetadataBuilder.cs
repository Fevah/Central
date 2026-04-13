using System.Collections.Generic;
using System.Linq;
using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Builder
{
    public class FilteredMetadataBuilder<T, TFilter> : RootMetadataBuilderBase<T>
        where T : class
        where TFilter : EditorWrapperBase
    {
        #region Constructors

        public FilteredMetadataBuilder(EditorMetadataBuilderBase<T> parent)
            : base(parent)
        {
        }

        #endregion


        #region Overrides

        public override IEnumerable<EditorWrapperBase> GetEditorWrappers()
        {
            return _parent.GetEditorWrappers().OfType<TFilter>();
        }

        #endregion
    }
}
