using System;

namespace TIG.TotalLink.Shared.DataModel.Core.Attribute
{
    /// <summary>
    /// Indicates that an association should be created for this property at run-time.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class RuntimeAssociationAttribute : System.Attribute
    {
        #region Constructors

        /// <summary>
        /// Create a new RuntimeAssociationAttribute.
        /// </summary>
        /// <param name="associationName">The name that will be applied to the association for this property.</param>
        /// <param name="collectionName">The name that will be applied to the collection property on the entity this property references.</param>
        public RuntimeAssociationAttribute(string associationName, string collectionName)
        {
            AssociationName = associationName;
            CollectionName = collectionName;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The name that will be applied to the association for this property.
        /// </summary>
        public string AssociationName { get; set; }

        /// <summary>
        /// The name that will be applied to the collection property on the entity this property references.
        /// </summary>
        public string CollectionName { get; set; }

        #endregion

    }
}
