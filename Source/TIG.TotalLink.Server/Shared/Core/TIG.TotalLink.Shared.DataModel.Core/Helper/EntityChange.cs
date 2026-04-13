using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace TIG.TotalLink.Shared.DataModel.Core.Helper
{
    [DataContract]
    public class EntityChange
    {
        #region Public Enums

        public enum ChangeTypes
        {
            Add,
            Modify,
            Delete
        }

        #endregion


        #region Constructors

        public EntityChange()
        {
        }

        public EntityChange(object entity, ChangeTypes changeType)
        {
            Entity = entity;
            ChangeType = changeType;
            EntityType = entity.GetType();

            var dataObject = entity as DataObjectBase;
            if (dataObject != null)
                Oid = dataObject.Oid;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The entity that has changed.
        /// </summary>
        public object Entity { get; private set; }

        /// <summary>
        /// The Oid of the entity that has changed.
        /// Only set when Entity inherits from DataObjectBase.
        /// </summary>
        [DataMember]
        public Guid Oid { get; private set; }

        /// <summary>
        /// The type of the entity that has changed.
        /// </summary>
        public Type EntityType { get; private set; }

        /// <summary>
        /// The assembly qualified type name of the entity that has changed.
        /// Only used for serialization of the EntityType property.
        /// </summary>
        [DataMember]
        public string EntityTypeName {
            get
            {
                return (EntityType != null ? EntityType.FullName : null);
            }
            private set
            {
                EntityType = DataModelHelper.GetTypeFromLoadedAssemblies(value);
            }
        }

        /// <summary>
        /// The type of change that was applied to the entity.
        /// </summary>
        [DataMember]
        public ChangeTypes ChangeType { get; private set; }

        #endregion
    }
}
