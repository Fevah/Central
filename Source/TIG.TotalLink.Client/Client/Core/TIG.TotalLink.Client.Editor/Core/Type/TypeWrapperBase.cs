using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Editor.Builder.Condition;

namespace TIG.TotalLink.Client.Editor.Core.Type
{
    public abstract class TypeWrapperBase : BindableBase, IDisposable
    {
        #region Constructors

        protected TypeWrapperBase(System.Type type)
        {
            // Initialize the wrapper
            Type = type;
            Conditions = new List<EditorCondition>();
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The type that this wrapper contains details of.
        /// </summary>
        public System.Type Type { get; private set; }

        /// <summary>
        /// A list of active conditions for the type.
        /// </summary>
        public List<EditorCondition> Conditions { get; private set; }

        /// <summary>
        /// Indicates if this wrapper contains any conditions.
        /// </summary>
        public bool HasConditions
        {
            get { return Conditions.Count > 0; }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Initializes the type wrapper so that conditions will start to be evaluated.
        /// </summary>
        public virtual void Initialize()
        {
        }
        
        /// <summary>
        /// Applies a condition to an object.
        /// </summary>
        /// <param name="condition">The condition to apply.</param>
        /// <param name="instance">The object to execute conditions against.</param>
        /// <returns>The calculated state of the condition.</returns>
        public virtual bool ApplyCondition(EditorCondition condition, object instance)
        {
            // Execute the condition
            return condition.Condition(instance);
        }

        /// <summary>
        /// Returns a list of conditions that are affected by the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property to find conditions for.</param>
        /// <returns>A list of conditions that are affected by the specified property.</returns>
        public List<EditorCondition> GetAffectedConditions(string propertyName)
        {
            return Conditions.Where(c => c.IsAffectedBy(propertyName)).ToList();
        }

        #endregion


        #region IDisposable

        public virtual void Dispose()
        {
        }

        #endregion
    }
}
