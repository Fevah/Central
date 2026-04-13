using System;

namespace TIG.TotalLink.Client.Core.Attribute
{
    /// <summary>
    /// Specifies that an entity can be filtered by the value of another entity.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class EntityFilterAttribute : System.Attribute
    {
        #region Constructors

        public EntityFilterAttribute()
        {
        }

        public EntityFilterAttribute(Type entityType, string filterString)
        {
            EntityType = entityType;
            FilterString = filterString;
        }

        public EntityFilterAttribute(Type entityType, string filterString, string displayFilterString)
            : this(entityType, filterString)
        {
            DisplayFilterString = displayFilterString;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The type of entity that this entity can be filtered by.
        /// </summary>
        public Type EntityType { get; set; }

        /// <summary>
        /// The filter pattern to use in order to filter this entity by the EntityType.
        /// </summary>
        public string FilterString { get; set; }

        /// <summary>
        /// The filter pattern to use for display.
        /// </summary>
        public string DisplayFilterString { get; set; }

        #endregion
    }
}
