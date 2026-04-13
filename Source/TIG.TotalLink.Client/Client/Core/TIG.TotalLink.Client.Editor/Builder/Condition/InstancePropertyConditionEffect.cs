using System;
using System.ComponentModel;
using System.Reflection;
using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Builder.Condition
{
    public class InstancePropertyConditionEffect<TContext, TDefinition> : PropertyConditionEffect
        where TContext : INotifyPropertyChanged
        where TDefinition : EditorDefinitionBase
    {
        #region Constructors

        public InstancePropertyConditionEffect(VisualEffects effect, PropertyInfo property, Action<TContext, TDefinition> trueMethod, Action<TContext, TDefinition> falseMethod = null)
            : base(effect, property)
        {
            TrueMethod = trueMethod;
            FalseMethod = falseMethod;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The method to execute when the condition returns true.
        /// </summary>
        public Action<TContext, TDefinition> TrueMethod { get; private set; }

        /// <summary>
        /// The method to execute when the condition returns false.
        /// </summary>
        public Action<TContext, TDefinition> FalseMethod { get; private set; }

        #endregion
    }
}
