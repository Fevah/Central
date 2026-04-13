using System;
using System.Linq;
using System.Linq.Expressions;
using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Builder
{
    public class GroupMetadataBuilder<T> : EditorMetadataBuilderBase<T>
        where T : class
    {
        #region Private Fields

        private int _groupIndex = -1;

        #endregion


        #region Constructors

        public GroupMetadataBuilder(EditorMetadataBuilderBase<T> parent)
            : base(parent)
        {
            // Reset the grouping on all GridColumnWrapperBase
            foreach (var wrapper in parent.GetEditorWrappers().OfType<GridColumnWrapperBase>())
            {
                wrapper.GroupIndex = -1;
            }
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Specifies a property to include in the grouping.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <returns>A GroupMetadataBuilder to continue building with.</returns>
        public GroupMetadataBuilder<T> ContainsProperty<TProperty>(Expression<Func<T, TProperty>> propertyExpression)
        {
            var propertyBuilder = new PropertyMetadataBuilder<T, TProperty>(propertyExpression, this);
            propertyBuilder.Group(++_groupIndex);

            return this;
        }

        /// <summary>
        /// Ends the group metadata.
        /// </summary>
        /// <returns>The parent metadata builder.</returns>
        public RootMetadataBuilderBase<T> EndGroup()
        {
            return _parent as RootMetadataBuilderBase<T>;
        }
        
        #endregion
    }
}
