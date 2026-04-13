using System;
using DevExpress.Entity.Model;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Client.Core.Helper
{
    /// <summary>
    /// A wrapper that contains an IEdmPropertyInfo and an optional related AliasedFieldMapping.
    /// </summary>
    public class VisiblePropertyWrapper
    {
        #region Constructors

        /// <summary>
        /// Creates a VisiblePropertyWrapper containing an IEdmPropertyInfo.
        /// </summary>
        /// <param name="type">The type that owns this property.</param>
        /// <param name="property">The IEdmPropertyInfo to store.</param>
        public VisiblePropertyWrapper(Type type, IEdmPropertyInfo property)
        {
            OwnerType = type;
            Property = property;
        }

        /// <summary>
        /// Creates a VisiblePropertyWrapper containing an IEdmPropertyInfo and AliasedFieldMapping.
        /// </summary>
        /// <param name="type">The type that owns this property.</param>
        /// <param name="property">The IEdmPropertyInfo to store.</param>
        /// <param name="alias">The AliasedFieldMapping to store.</param>
        public VisiblePropertyWrapper(Type type, IEdmPropertyInfo property, AliasedFieldMapping alias)
            : this(type, property)
        {
            Alias = alias;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The type that owns this property.
        /// </summary>
        public Type OwnerType { get; private set; }

        /// <summary>
        /// The contained IEdmPropertyInfo.
        /// </summary>
        public IEdmPropertyInfo Property { get; private set; }

        /// <summary>
        /// The contained AliasedFieldMapping.
        /// </summary>
        public AliasedFieldMapping Alias { get; private set; }

        /// <summary>
        /// Indicates if this wrapper contains an alias.
        /// </summary>
        public bool ContainsAlias
        {
            get { return (Alias != null); }
        }

        #endregion


        #region Overrides

        public override string ToString()
        {
            if (ContainsAlias)
                return string.Format("{0} = {1}", Property.Name, Alias.AliasFieldName);

            return Property.Name;
        }

        #endregion
    }
}
