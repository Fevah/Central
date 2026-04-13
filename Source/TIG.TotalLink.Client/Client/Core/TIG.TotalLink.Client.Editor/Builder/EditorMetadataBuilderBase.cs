using System.Collections.Generic;
using TIG.TotalLink.Client.Editor.Core.Editor;
using TIG.TotalLink.Client.Editor.Core.Type;

namespace TIG.TotalLink.Client.Editor.Builder
{
    public abstract class EditorMetadataBuilderBase<T>
    {
        #region Protected Fields
        
        protected readonly EditorMetadataBuilderBase<T> _parent;

        #endregion


        #region Constructors

        protected EditorMetadataBuilderBase(EditorMetadataBuilderBase<T> parent)
        {
            _parent = parent;
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Gets the editor wrappers being modified by this metadata builder.
        /// </summary>
        /// <returns>A list of editor wrappers.</returns>
        public virtual IEnumerable<EditorWrapperBase> GetEditorWrappers()
        {
            return _parent.GetEditorWrappers();
        }

        /// <summary>
        /// Gets the type wrapper being modified by this metadata builder.
        /// </summary>
        /// <returns>A type wrapper.</returns>
        public virtual TypeWrapperBase GetTypeWrapper()
        {
            return _parent.GetTypeWrapper();
        }

        #endregion
    }
}
