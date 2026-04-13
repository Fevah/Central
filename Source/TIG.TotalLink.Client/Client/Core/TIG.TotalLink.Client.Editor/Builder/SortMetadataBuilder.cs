using System;
using System.Linq;
using System.Linq.Expressions;
using DevExpress.Data;
using DevExpress.XtraGrid;
using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Builder
{
    public class SortMetadataBuilder<T> : EditorMetadataBuilderBase<T>
        where T : class
    {
        #region Private Fields

        private int _sortIndex = -1;

        #endregion


        #region Constructors

        public SortMetadataBuilder(EditorMetadataBuilderBase<T> parent)
            : base(parent)
        {
            // Reset the sort on all GridColumnWrapperBase
            foreach (var wrapper in parent.GetEditorWrappers().OfType<GridColumnWrapperBase>())
            {
                wrapper.SortIndex = -1;
                wrapper.SortMode = ColumnSortMode.Default;
                wrapper.SortOrder = ColumnSortOrder.None;
            }
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Specifies a property to include in the sort, using a default sort order of Ascending.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <returns>A SortMetadataBuilder to continue building with.</returns>
        public SortMetadataBuilder<T> ContainsProperty<TProperty>(Expression<Func<T, TProperty>> propertyExpression)
        {
            return ContainsProperty(propertyExpression, ColumnSortOrder.Ascending);
        }

        /// <summary>
        /// Specifies a property to include in the sort.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <param name="sortOrder">The direction that the property will be sorted.</param>
        /// <returns>A SortMetadataBuilder to continue building with.</returns>
        public SortMetadataBuilder<T> ContainsProperty<TProperty>(Expression<Func<T, TProperty>> propertyExpression, ColumnSortOrder sortOrder)
        {
            var propertyBuilder = new PropertyMetadataBuilder<T, TProperty>(propertyExpression, this);
            propertyBuilder.Sort(++_sortIndex, sortOrder);

            return this;
        }

        /// <summary>
        /// Ends the sort metadata.
        /// </summary>
        /// <returns>The parent metadata builder.</returns>
        public RootMetadataBuilderBase<T> EndSort()
        {
            return _parent as RootMetadataBuilderBase<T>;
        }
        
        #endregion
    }
}
