using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using TIG.TotalLink.Client.Editor.Builder.Condition;
using TIG.TotalLink.Client.Editor.Core.Editor;
using TIG.TotalLink.Client.Editor.Core.Type;

namespace TIG.TotalLink.Client.Editor.Builder
{
    public class ConditionMetadataBuilder<T> : EditorMetadataBuilderBase<T>
        where T : class
    {
        #region Private Fields

        private readonly EditorCondition _editorCondition;
        private TypeWrapperBase _typeWrapper;

        #endregion


        #region Constructors

        public ConditionMetadataBuilder(EditorMetadataBuilderBase<T> parent, Func<T, bool> condition)
            : base(parent)
        {
            _editorCondition = new EditorCondition(o => condition(o as T));

            var typeWrapper = TypeWrapper;
            if (typeWrapper != null)
                TypeWrapper.Conditions.Add(_editorCondition);
        }

        #endregion


        #region Private Properties

        /// <summary>
        /// The wrapper that applies to this type.
        /// </summary>
        private TypeWrapperBase TypeWrapper
        {
            get { return _typeWrapper ?? (_typeWrapper = GetTypeWrapper()); }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Specifies a property that the condition is dependent on.
        /// The condition will be re-evaluated whenever the property value changes.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <returns>A ConditionMetadataBuilder to continue building with.</returns>
        public ConditionMetadataBuilder<T> ContainsProperty<TProperty>(Expression<Func<T, TProperty>> propertyExpression)
        {
            AddWatchProperty(propertyExpression);

            return this;
        }

        /// <summary>
        /// Specifies that the visibility of the editor for a property will be affected by the condition.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <returns>A ConditionMetadataBuilder to continue building with.</returns>
        public ConditionMetadataBuilder<T> AffectsPropertyVisibility<TProperty>(Expression<Func<T, TProperty>> propertyExpression)
        {
            AddPropertyEffect(propertyExpression, ConditionEffectBase.VisualEffects.Visibility);

            return this;
        }

        /// <summary>
        /// Specifies that the enabled state of the editor for a property will be affected by the condition.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <returns>A ConditionMetadataBuilder to continue building with.</returns>
        public ConditionMetadataBuilder<T> AffectsPropertyEnabled<TProperty>(Expression<Func<T, TProperty>> propertyExpression)
        {
            AddPropertyEffect(propertyExpression, ConditionEffectBase.VisualEffects.Enabled);

            return this;
        }

        /// <summary>
        /// Specifies that the required state of the editor for a property will be affected by the condition.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <returns>A ConditionMetadataBuilder to continue building with.</returns>
        public ConditionMetadataBuilder<T> AffectsPropertyRequired<TProperty>(Expression<Func<T, TProperty>> propertyExpression)
        {
            AddPropertyEffect(propertyExpression, ConditionEffectBase.VisualEffects.Required);

            return this;
        }

        /// <summary>
        /// Specifies that the read-only state of the editor for a property will be affected by the condition.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <returns>A ConditionMetadataBuilder to continue building with.</returns>
        public ConditionMetadataBuilder<T> AffectsPropertyReadOnly<TProperty>(Expression<Func<T, TProperty>> propertyExpression)
        {
            AddPropertyEffect(propertyExpression, ConditionEffectBase.VisualEffects.ReadOnly);

            return this;
        }

        /// <summary>
        /// Specifies that the supplied instance methods will be executed when the property value changes.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <typeparam name="TContext">The type of the object that this condition is being executed on.</typeparam>
        /// <typeparam name="TDefinition">The type of EditorDefinition that is expected to be returned for this property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <param name="trueMethod">A method to execute when the condition returns true.</param>
        /// <param name="falseMethod">A method to execute when the condition returns false.</param>
        /// <returns>A ConditionMetadataBuilder to continue building with.</returns>
        public ConditionMetadataBuilder<T> InvokesInstanceMethod<TProperty, TContext, TDefinition>(Expression<Func<T, TProperty>> propertyExpression, Action<TContext, TDefinition> trueMethod, Action<TContext, TDefinition> falseMethod = null)
            where TContext : INotifyPropertyChanged
            where TDefinition : EditorDefinitionBase
        {
            var property = GetPropertyFromExpression(propertyExpression);
            if (property != null)
                _editorCondition.Effects.Add(new InstancePropertyConditionEffect<TContext, TDefinition>(ConditionEffectBase.VisualEffects.InstanceMethod, property, trueMethod, falseMethod));

            return this;
        }

        /// <summary>
        /// Specifies that the visibility of a group will be affected by the condition.
        /// </summary>
        /// <param name="groupName">The name of the group to affect.</param>
        /// <returns>A ConditionMetadataBuilder to continue building with.</returns>
        public ConditionMetadataBuilder<T> AffectsGroupVisibility(string groupName)
        {
            AddGroupEffect(groupName, ConditionEffectBase.VisualEffects.Visibility);

            return this;
        }

        /// <summary>
        /// Specifies that the enabled state of a group will be affected by the condition.
        /// </summary>
        /// <param name="groupName">The name of the group to affect.</param>
        /// <returns>A ConditionMetadataBuilder to continue building with.</returns>
        public ConditionMetadataBuilder<T> AffectsGroupEnabled(string groupName)
        {
            AddGroupEffect(groupName, ConditionEffectBase.VisualEffects.Enabled);

            return this;
        }

        /// <summary>
        /// Ends the group metadata.
        /// </summary>
        /// <returns>The parent metadata builder.</returns>
        public RootMetadataBuilderBase<T> EndCondition()
        {
            return _parent as RootMetadataBuilderBase<T>;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Extracts a property from a property expression.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        private PropertyInfo GetPropertyFromExpression<TProperty>(Expression<Func<T, TProperty>> propertyExpression)
        {
            var memberExpression = (MemberExpression)propertyExpression.Body;
            var property = memberExpression.Member as PropertyInfo;

            return property;
        }

        /// <summary>
        /// Adds a property to the WatchProperties collection.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        private void AddWatchProperty<TProperty>(Expression<Func<T, TProperty>> propertyExpression)
        {
            var property = GetPropertyFromExpression(propertyExpression);
            if (property == null)
                return;

            _editorCondition.WatchProperties.Add(property);
        }

        /// <summary>
        /// Adds a property effect to the Effects collection.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="propertyExpression">An expression that selects the required property.</param>
        /// <param name="effect">The effect that will be applied to the property.</param>
        private void AddPropertyEffect<TProperty>(Expression<Func<T, TProperty>> propertyExpression, ConditionEffectBase.VisualEffects effect)
        {
            var property = GetPropertyFromExpression(propertyExpression);
            if (property == null)
                return;

            _editorCondition.Effects.Add(new PropertyConditionEffect(effect, property));
        }

        /// <summary>
        /// Adds a group effect to the Effects collection.
        /// </summary>
        /// <param name="groupName">The name of the group to affect.</param>
        /// <param name="effect">The effect that will be applied to the group.</param>
        private void AddGroupEffect(string groupName, ConditionEffectBase.VisualEffects effect)
        {
            _editorCondition.Effects.Add(new GroupConditionEffect(effect, groupName));
        }

        #endregion
    }
}
