using System;
using System.ComponentModel;
using System.Windows;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Editors;
using TIG.TotalLink.Client.Editor.Builder.Condition;
using TIG.TotalLink.Client.Editor.Control;
using TIG.TotalLink.Client.Editor.Core.Type;

namespace TIG.TotalLink.Client.Editor.Wrapper.Type
{
    /// <summary>
    /// Wrapper for a type being displayed in a DataLayoutControl.
    /// </summary>
    public class DataLayoutControlWrapper : TypeWrapperBase
    {
        #region Private Fields

        private readonly DataLayoutControlEx _dataLayoutControl;
        private INotifyPropertyChanged _currentItem;

        #endregion


        #region Constructors

        public DataLayoutControlWrapper(DataLayoutControlEx dataLayoutControl)
            : base(dataLayoutControl.CurrentItem.GetType())
        {
            _dataLayoutControl = dataLayoutControl;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Applies all conditions to set the initial state.
        /// </summary>
        private void ApplyAllConditions()
        {
            if (_currentItem == null)
                return;

            foreach (var condition in Conditions)
            {
                ApplyCondition(condition, _currentItem);
            }
        }

        /// <summary>
        /// Applies a PropertyConditionEffect.
        /// </summary>
        /// <param name="effect">The effect to apply.</param>
        /// <param name="effectState">The state of the effect.</param>
        /// <param name="instance">The instance that conditions were executed against.</param>
        private void ApplyPropertyEffect(PropertyConditionEffect effect, bool effectState, object instance)
        {
            // Abort if no effect was supplied
            if (effect == null)
                return;

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Attempt to find a DataLayoutItemEx for the effect property
                var layoutItem = LayoutHelper.FindElement(_dataLayoutControl, element =>
                {
                    var i = element as DataLayoutItemEx;
                    if (i == null)
                        return false;

                    return (i.EditorWrapper != null && i.EditorWrapper.PropertyName == effect.Property.Name);
                }) as DataLayoutItemEx;
                if (layoutItem == null)
                    return;

                // Apply the effect to the item
                switch (effect.Effect)
                {
                    case ConditionEffectBase.VisualEffects.Visibility:
                        layoutItem.Visibility = (effectState ? Visibility.Visible : Visibility.Collapsed);
                        break;

                    case ConditionEffectBase.VisualEffects.Enabled:
                        layoutItem.IsEnabled = effectState;
                        break;

                    case ConditionEffectBase.VisualEffects.Required:
                        // Set the required state on the layout item and editor wrapper
                        layoutItem.EditorWrapper.IsRequired = effectState;
                        layoutItem.IsRequired = effectState;

                        // Trigger validation on the primary editor
                        var editor = LogicalTreeHelper.FindLogicalNode(layoutItem.Content, "PART_Editor") as BaseEdit;
                        if (editor != null)
                            editor.DoValidate();

                        break;

                    case ConditionEffectBase.VisualEffects.ReadOnly:
                        layoutItem.IsReadOnly = effectState;
                        break;

                    case ConditionEffectBase.VisualEffects.InstanceMethod:
                        var editorDefinitionType = layoutItem.EditorWrapper.Editor.GetType();

                        // Get an InstancePropertyConditionEffect type with the CurrentItem type and EditorDefintion type as the generic parameters
                        var instanceEffectType = typeof(InstancePropertyConditionEffect<,>);
                        instanceEffectType = instanceEffectType.MakeGenericType(Type, editorDefinitionType);

                        // Get an Action type with the CurrentItem type and EditorDefintion type as the generic parameters
                        var actionType = typeof(Action<,>);
                        actionType = actionType.MakeGenericType(Type, editorDefinitionType);

                        if (effectState)
                        {
                            // Get the true method
                            var trueMethodProperty = instanceEffectType.GetProperty("TrueMethod");
                            var trueMethod = trueMethodProperty.GetValue(effect);

                            // Invoke the true method
                            var invokeMethod = actionType.GetMethod("Invoke");
                            invokeMethod.Invoke(trueMethod, new[] { instance, layoutItem.EditorWrapper.Editor });
                        }
                        else
                        {
                            // Get the false method
                            var falseMethodProperty = instanceEffectType.GetProperty("FalseMethod");
                            var falseMethod = falseMethodProperty.GetValue(effect);

                            if (falseMethod != null)
                            {
                                // Invoke the false method
                                var invokeMethod = actionType.GetMethod("Invoke");
                                invokeMethod.Invoke(falseMethod, new[] { instance, layoutItem.EditorWrapper.Editor });
                            }
                        }
                        break;
                }
            });
        }

        /// <summary>
        /// Applies a GroupConditionEffect.
        /// </summary>
        /// <param name="effect">The effect to apply.</param>
        /// <param name="effectState">The state of the effect.</param>
        /// <param name="instance">The instance that conditions were executed against.</param>
        private void ApplyGroupEffect(GroupConditionEffect effect, bool effectState, object instance)
        {
            // Abort if no effect was supplied
            if (effect == null)
                return;

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Attempt to find a LayoutGroupEx for the effect property
                var layoutGroup = LayoutHelper.FindElement(_dataLayoutControl, element =>
                {
                    var g = element as LayoutGroupEx;
                    if (g == null)
                        return false;

                    return ((string)g.Header == effect.GroupName);
                });
                if (layoutGroup == null)
                    return;

                // Apply the effect to the group
                switch (effect.Effect)
                {
                    case ConditionEffectBase.VisualEffects.Visibility:
                        layoutGroup.Visibility = (effectState ? Visibility.Visible : Visibility.Collapsed);
                        break;

                    case ConditionEffectBase.VisualEffects.Enabled:
                        layoutGroup.IsEnabled = effectState;
                        break;
                }
            });
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the AutoGeneratedUI event for the DataLayoutControl.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void DataLayoutControl_AutoGeneratedUI(object sender, EventArgs e)
        {
            ApplyAllConditions();
        }

        /// <summary>
        /// Handles the CurrentItemChanged event for the DataLayoutControl.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void DataLayoutControl_CurrentItemChanged(object sender, ValueChangedEventArgs<object> e)
        {
            // Stop handling events on the old CurrentItem
            if (_currentItem != null)
                _currentItem.PropertyChanged -= CurrentItem_PropertyChanged;

            // Start handling events on the new CurrentItem
            _currentItem = _dataLayoutControl.CurrentItem as INotifyPropertyChanged;
            if (_currentItem != null)
                _currentItem.PropertyChanged += CurrentItem_PropertyChanged;

            // Update all conditions
            ApplyAllConditions();
        }

        /// <summary>
        /// Handles the PropertyChanged event for the CurrentItem.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void CurrentItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Apply each condition affected by the property that changed
            foreach (var condition in GetAffectedConditions(e.PropertyName))
            {
                ApplyCondition(condition, sender);
            }
        }

        #endregion


        #region Overrides

        public override void Initialize()
        {
            base.Initialize();

            // Abort if the wrapper doesn't have any conditions
            if (!HasConditions)
                return;

            // Handle DataLayoutControl events
            _dataLayoutControl.AutoGeneratedUI += DataLayoutControl_AutoGeneratedUI;
            _dataLayoutControl.CurrentItemChanged += DataLayoutControl_CurrentItemChanged;

            // Handle CurrentItem events
            _currentItem = _dataLayoutControl.CurrentItem as INotifyPropertyChanged;
            if (_currentItem != null)
                _currentItem.PropertyChanged += CurrentItem_PropertyChanged;
        }

        public override bool ApplyCondition(EditorCondition condition, object instance)
        {
            var effectState = base.ApplyCondition(condition, instance);

            // Process each effect
            foreach (var effect in condition.Effects)
            {
                ApplyPropertyEffect(effect as PropertyConditionEffect, effectState, instance);
                ApplyGroupEffect(effect as GroupConditionEffect, effectState, instance);
            }

            return effectState;
        }

        public override void Dispose()
        {
            base.Dispose();

            // Stop handling DataLayoutControl events
            _dataLayoutControl.AutoGeneratedUI -= DataLayoutControl_AutoGeneratedUI;

            // Stop handling CurrentItem events
            if (_currentItem != null)
                _currentItem.PropertyChanged -= CurrentItem_PropertyChanged;
        }

        #endregion
    }
}
