using System;
using Newtonsoft.Json;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.Facade.Core.JsonContractResolver;

namespace TIG.TotalLink.Shared.Facade.Core.Helper
{
    public class JsonHelper
    {
        /// <summary>
        /// Serializes a DataObjectBase to Json.
        /// Use this method when you want to send a flattened copy of a DataObjectBase to a method service.
        /// </summary>
        /// <param name="dataObject">The DataObjectBase to serialize.</param>
        /// <returns>A Json string representing the DataObjectBase.</returns>
        public static string SerializeDataObject(DataObjectBase dataObject)
        {
            // Prepare serializer settings
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new DataObjectContractResolver(dataObject.GetType()),
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            // Serialize the DataObjectBase
            return JsonConvert.SerializeObject(dataObject, settings);
        }

        /// <summary>
        /// Deserializes a DataObject from a Json string.
        /// This will not deserialize references to other data objects.
        /// </summary>
        /// <typeparam name="T">The type of data object to deserialize as.</typeparam>
        /// <param name="jsonString">The Json string to deserialize.</param>
        /// <returns>A new <typeparamref name="T"/> containing deserialized values.</returns>
        public static T DeserializeDataObject<T>(string jsonString)
            where T : DataObjectBase
        {
            return DeserializeDataObject(jsonString, typeof(T)) as T;
        }

        /// <summary>
        /// Deserializes a DataObject from a Json string.
        /// Note that this will deserialize references to other data objects, however only the Oid field will be available on the referenced objects.
        /// </summary>
        /// <param name="type">The type of data object to deserialize as.</param>
        /// <param name="jsonString">The Json string to deserialize.</param>
        /// <returns>A new <paramref name="type"/> containing deserialized values.</returns>
        public static object DeserializeDataObject(string jsonString, Type type)
        {
            return JsonConvert.DeserializeObject(jsonString, type);
        }
    }
}
