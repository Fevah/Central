using System.Collections.Generic;
using TIG.TotalLink.Client.Editor.Core.Editor;
using TIG.TotalLink.Client.Editor.Core.Type;
using TIG.TotalLink.Client.Editor.Wrapper.Editor;

namespace TIG.TotalLink.Client.Editor.Builder
{
    public class EditorMetadataBuilder<T> : RootMetadataBuilderBase<T>
        where T : class
    {
        #region Constructors

        public EditorMetadataBuilder(IEnumerable<EditorWrapperBase> editorWrappers, TypeWrapperBase typeWrapper)
            : base(null)
        {
            EditorWrappers = editorWrappers;
            TypeWrapper = typeWrapper;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The list of editor wrappers that will be modified by this builder.
        /// </summary>
        public IEnumerable<EditorWrapperBase> EditorWrappers { get; private set; }

        /// <summary>
        /// The type wrapper that will be modified by this builder.
        /// </summary>
        public TypeWrapperBase TypeWrapper { get; private set; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Creates a new metadata builder that will operate on all wrappers that inherit from GridColumnWrapperBase.
        /// </summary>
        /// <returns>A new FilteredMetadataBuilder.</returns>
        public FilteredMetadataBuilder<T, GridColumnWrapperBase> GridBaseColumnEditors()
        {
            return new FilteredMetadataBuilder<T, GridColumnWrapperBase>(this);
        }

        /// <summary>
        /// Creates a new metadata builder that will only operate on GridColumnWrappers.
        /// </summary>
        /// <returns>A new FilteredMetadataBuilder.</returns>
        public FilteredMetadataBuilder<T, GridColumnWrapper> GridColumnEditors()
        {
            return new FilteredMetadataBuilder<T, GridColumnWrapper>(this);
        }

        /// <summary>
        /// Creates a new metadata builder that will only operate on GridEditColumnWrappers.
        /// </summary>
        /// <returns>A new FilteredMetadataBuilder.</returns>
        public FilteredMetadataBuilder<T, GridEditColumnWrapper> GridEditColumnEditors()
        {
            return new FilteredMetadataBuilder<T, GridEditColumnWrapper>(this);
        }

        /// <summary>
        /// Creates a new metadata builder that will only operate on LookUpEditColumnWrappers.
        /// </summary>
        /// <returns>A new FilteredMetadataBuilder.</returns>
        public FilteredMetadataBuilder<T, LookUpEditColumnWrapper> LookUpEditColumnEditors()
        {
            return new FilteredMetadataBuilder<T, LookUpEditColumnWrapper>(this);
        }

        ///// <summary>
        ///// Creates a new metadata builder that will only operate on TreeColumnWrappers.
        ///// </summary>
        ///// <returns>A new FilteredMetadataBuilder.</returns>
        //public FilteredMetadataBuilder<T, TreeColumnWrapper> TreeColumnEditors()
        //{
        //    return new FilteredMetadataBuilder<T, TreeColumnWrapper>(this);
        //}

        /// <summary>
        /// Creates a new metadata builder that will only operate on DataLayoutItemWrappers.
        /// </summary>
        /// <returns>A new FilteredMetadataBuilder.</returns>
        public FilteredMetadataBuilder<T, DataLayoutItemWrapper> DataFormEditors()
        {
            return new FilteredMetadataBuilder<T, DataLayoutItemWrapper>(this);
        }

        #endregion


        #region Overrides

        public override IEnumerable<EditorWrapperBase> GetEditorWrappers()
        {
            return EditorWrappers;
        }

        public override TypeWrapperBase GetTypeWrapper()
        {
            return TypeWrapper;
        }

        #endregion
    }
}
