using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.Facade.Core.JsonConverter;

namespace TIG.TotalLink.Shared.Facade.Core.JsonContractResolver
{
    public class DataObjectContractResolver : AddTypeNameContractResolver
    {
        #region Private Fields

        private readonly Type _rootType;

        #endregion


        #region Constructors

        public DataObjectContractResolver(Type rootType)
            : base("[TYPE]")
        {
            _rootType = rootType;
        }

        #endregion


        #region Overrides

        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            // Get properties from the base
            var properties = base.CreateProperties(type, memberSerialization);

            // If the type inherits from DataObjectBase, remove all non-serializable properties
            if (typeof(DataObjectBase).IsAssignableFrom(type))
                properties = properties.Where(p => !DataModelHelper.NonSerializableDataObjectBaseProperties.Contains(p.PropertyName)).ToList();

            // Return the properties
            return properties;
        }

        protected override Newtonsoft.Json.JsonConverter ResolveContractConverter(Type objectType)
        {
            // If the type is not the root type, but it is inherited from DataObjectBase, then return a new DataObjectConverter
            // The will ensure that all properties are included for the root type, but only Type and Oid are included for all other DataObjectBase
            if (objectType != _rootType && typeof(DataObjectBase).IsAssignableFrom(objectType))
                return new DataObjectConverter(false);

            // Otherwise return the base converter
            return base.ResolveContractConverter(objectType);
        }

        #endregion
    }
}
