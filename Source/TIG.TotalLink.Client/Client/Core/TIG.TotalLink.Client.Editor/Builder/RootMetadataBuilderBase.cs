using System;
using System.Linq.Expressions;

namespace TIG.TotalLink.Client.Editor.Builder
{
    public abstract class RootMetadataBuilderBase<T> : EditorMetadataBuilderBase<T>
        where T : class
    {
        #region Constructors

        protected RootMetadataBuilderBase(EditorMetadataBuilderBase<T> parent)
            : base(parent)
        {
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Creates a new metadata builder for a property.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <returns>A new PropertyMetadataBuilder.</returns>
        public PropertyMetadataBuilder<T, TProperty> Property<TProperty>(Expression<Func<T, TProperty>> propertyExpression)
        {
            return new PropertyMetadataBuilder<T, TProperty>(propertyExpression, this);
        }

        /// <summary>
        /// Creates a new metadata builder for applying column sorting.
        /// Has no effect on editors displayed in a DataLayoutControl.
        /// </summary>
        /// <returns>A new SortMetadataBuilder.</returns>
        public SortMetadataBuilder<T> Sort()
        {
            return new SortMetadataBuilder<T>(this);
        }

        /// <summary>
        /// Creates a new metadata builder for applying column grouping.
        /// Has no effect on editors displayed in a DataLayoutControl.
        /// </summary>
        /// <returns>A new GroupMetadataBuilder.</returns>
        public GroupMetadataBuilder<T> Group()
        {
            return new GroupMetadataBuilder<T>(this);
        }

        /// <summary>
        /// Ends the metadata set.
        /// </summary>
        /// <returns>The parent metadata builder.</returns>
        public RootMetadataBuilderBase<T> EndGroup()
        {
            return _parent as RootMetadataBuilderBase<T>;
        }

        /// <summary>
        /// Creates a new metadata builder for applying conditions.
        /// Currently has no effect on editors displayed in a GridControl.
        /// </summary>
        /// <param name="condition">
        /// The condition that will be evaluated whenever one of the contained properties change.
        /// When the condition returns true the effects will be set to a positive state (e.g. visible, enabled),
        /// otherwise the effects will be set to a negative state (e.g. hidden, disabled).
        /// </param>
        /// <returns>A new ConditionMetadataBuilder.</returns>
        public ConditionMetadataBuilder<T> Condition(Func<T, bool> condition)
        {
            return new ConditionMetadataBuilder<T>(this, condition);
        }

        #endregion
    }
}
