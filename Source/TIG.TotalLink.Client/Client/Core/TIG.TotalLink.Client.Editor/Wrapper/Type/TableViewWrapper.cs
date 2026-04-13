using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using DevExpress.Data;
using DevExpress.Utils;
using DevExpress.Xpf.Grid;
using TIG.TotalLink.Client.Editor.Builder.Condition;
using TIG.TotalLink.Client.Editor.Control;
using TIG.TotalLink.Client.Editor.Core.Editor;
using TIG.TotalLink.Client.Editor.Core.Type;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Client.Editor.Wrapper.Type
{
    public class TableViewWrapper : TypeWrapperBase
    {
        #region Private Fields

        private TableViewEx _tableView;
        private INotifyPropertyChanged _currentItem;

        #endregion


        #region Constructors

        public TableViewWrapper(System.Type type)
            : base(type)
        {
        }

        public TableViewWrapper(TableViewEx tableView, System.Type type)
            : this(type)
        {
            TableView = tableView;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The TableView that this wrapper operates on.
        /// </summary>
        public TableViewEx TableView
        {
            get { return _tableView; }
            set
            {
                var oldTableView = _tableView;
                SetProperty(ref _tableView, value, () => TableView, () =>
                {
                    if (oldTableView != null)
                        oldTableView.Grid.CurrentItemChanged -= GridControl_CurrentItemChanged;

                    if (_tableView != null)
                        _tableView.Grid.CurrentItemChanged += GridControl_CurrentItemChanged;
                });
            }
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
            // Abort if no effect was supplied, or the TableView is null
            if (effect == null || TableView == null)
                return;

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Attempt to find a GridColumnEx for the effect property
                var column = TableView.Grid.Columns.FirstOrDefault(c => c.DataContext is GridColumnWrapperBase && ((GridColumnWrapperBase)c.DataContext).PropertyName == effect.Property.Name);
                if (column == null)
                    return;

                // Apply the effect to the item
                var editorWrapper = (GridColumnWrapperBase)column.DataContext;
                switch (effect.Effect)
                {
                    case ConditionEffectBase.VisualEffects.Enabled:
                        column.AllowEditing = (effectState ? DefaultBoolean.True : DefaultBoolean.False);
                        break;

                    case ConditionEffectBase.VisualEffects.ReadOnly:
                        editorWrapper.IsReadOnly = effectState;
                        break;

                    case ConditionEffectBase.VisualEffects.InstanceMethod:
                        var editorDefinitionType = editorWrapper.Editor.GetType();

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
                            invokeMethod.Invoke(trueMethod, new[] { instance, editorWrapper.Editor });
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
                                invokeMethod.Invoke(falseMethod, new[] { instance, editorWrapper.Editor });
                            }
                        }
                        break;
                }
            });
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the CurrentItemChanged event on the GridControl.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void GridControl_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
        {
            // Abort if the row is not loaded yet
            if (e.NewItem == null || e.NewItem is NotLoadedObject)
                return;

            // Stop handling events on the old CurrentItem
            if (_currentItem != null)
                _currentItem.PropertyChanged -= CurrentItem_PropertyChanged;

            // Get the new CurrentItem object from the row
            _currentItem = (DataModelHelper.GetDataObject(e.NewItem) ?? e.NewItem) as INotifyPropertyChanged;
            if (_currentItem == null)
                return;

            // Start handling events on the new CurrentItem
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

        //public override void Initialize()
        //{
        //    base.Initialize();

        //    // We cannot initialize events on the TableView here because it may not be set until later.
        //    // Initialize events in the TableView property setter instead.
        //}

        public override bool ApplyCondition(EditorCondition condition, object instance)
        {
            var effectState = base.ApplyCondition(condition, instance);

            // Process each effect
            foreach (var effect in condition.Effects)
            {
                ApplyPropertyEffect(effect as PropertyConditionEffect, effectState, instance);
            }

            return effectState;
        }

        public override void Dispose()
        {
            base.Dispose();

            // Stop handling TableView events
            if (TableView != null)
                TableView.Grid.CurrentItemChanged -= GridControl_CurrentItemChanged;

            // Stop handling CurrentItem events
            if (_currentItem != null)
                _currentItem.PropertyChanged -= CurrentItem_PropertyChanged;
        }

        #endregion
    }
}
