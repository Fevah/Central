using System;
using Newtonsoft.Json;
using TIG.TotalLink.Shared.DataModel.Core;

namespace TIG.TotalLink.Shared.Facade.Core.JsonConverter
{
    /// <summary>
    /// Serializes data objects by only including a few key properties.
    /// This stops the serializer from attempting to access delay loaded properties which may not be loaded.
    /// </summary>
    public class DataObjectConverter : Newtonsoft.Json.JsonConverter
    {
        #region Private Fields

        private readonly bool _includeDisplayName;

        #endregion


        #region Constructors

        public DataObjectConverter(bool includeDisplayName)
        {
            _includeDisplayName = includeDisplayName;
        }

        #endregion


        #region Overrides

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // Attempt to get the object as a DataObjectBase
            var dataObject = value as DataObjectBase;

            // Write the start tag
            writer.WriteStartObject();

            if (dataObject != null)
            {
                // Write the object type
                writer.WritePropertyName("TYPE");
                writer.WriteValue(dataObject.GetType().FullName);

                // Write the Oid
                writer.WritePropertyName("Oid");
                serializer.Serialize(writer, dataObject.Oid);

                // Write the DisplayName (if the related flag is set)
                if (_includeDisplayName)
                {
                    writer.WritePropertyName("DisplayName");
                    writer.WriteValue(dataObject.ToString());
                }
            }

            // Write the end tag
            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotSupportedException("DataObjectConverter cannot be used for deserialization.");
        }

        public override bool CanConvert(Type objectType)
        {
            // This converter can only serialize DataObjectBase
            return (typeof(DataObjectBase).IsAssignableFrom(objectType));
        }

        #endregion
    }
}
