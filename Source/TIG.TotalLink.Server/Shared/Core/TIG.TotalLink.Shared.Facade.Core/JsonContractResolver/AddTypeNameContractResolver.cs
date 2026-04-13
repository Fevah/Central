using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

// http://stackoverflow.com/questions/24171730/adding-a-custom-type-name-to-all-classes-during-serialisation-with-json-net

namespace TIG.TotalLink.Shared.Facade.Core.JsonContractResolver
{
    public class AddTypeNameContractResolver : DefaultContractResolver
    {
        #region Private Fields

        private readonly string _propertyName;
        private readonly IValueProvider _valueProvider = new SimpleTypeNameProvider();

        #endregion


        #region Constructors

        public AddTypeNameContractResolver(string propertyName)
        {
            _propertyName = propertyName;
        }

        #endregion


        #region Overrides

        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            var props = base.CreateProperties(type, memberSerialization);

            if (type.IsClass && type != typeof(string))
            {
                // Add a phantom string property to every class which will resolve 
                // to the simple type name of the class (via the value provider)
                // during serialization.
                props.Insert(0, new JsonProperty
                {
                    DeclaringType = type,
                    PropertyType = typeof(string),
                    PropertyName = _propertyName,
                    ValueProvider = _valueProvider,
                    Readable = true,
                    Writable = false
                });
            }

            return props;
        }

        #endregion


        #region Related Classes

        public class SimpleTypeNameProvider : IValueProvider
        {
            public object GetValue(object target)
            {
                return target.GetType().FullName;
            }

            public void SetValue(object target, object value)
            {
            }
        }

        #endregion
    }
}
