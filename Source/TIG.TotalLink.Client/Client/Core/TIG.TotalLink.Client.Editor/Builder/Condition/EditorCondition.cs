using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TIG.TotalLink.Client.Editor.Builder.Condition
{
    public class EditorCondition
    {
        #region Constructors

        public EditorCondition(Func<object, bool> condition)
        {
            Condition = condition;
            WatchProperties = new List<PropertyInfo>();
            Effects = new List<ConditionEffectBase>();
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The condition to evaluate.
        /// </summary>
        public Func<object, bool> Condition { get; private set; }

        /// <summary>
        /// A list of properties that will affect this condition.
        /// </summary>
        public List<PropertyInfo> WatchProperties { get; private set; }

        /// <summary>
        /// A list of effects that will be applied based on the result of the condition.
        /// </summary>
        public List<ConditionEffectBase> Effects { get; private set; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Indicates if this condition is affected by the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property to test the condition against.</param>
        /// <returns>True if this condition is affected by the specified property; otherwise false.</returns>
        public bool IsAffectedBy(string propertyName)
        {
            return (WatchProperties.Any(p => p.Name == propertyName));
        }

        #endregion
    }
}
