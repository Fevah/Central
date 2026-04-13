using Newtonsoft.Json;
using TIG.TotalLink.Client.Core.Message.Core;
using TIG.TotalLink.Shared.Facade.Core.JsonContractResolver;
using TIG.TotalLink.Shared.Facade.Core.JsonConverter;

namespace TIG.TotalLink.Client.Undo.Extension
{
    public static class MessageBaseExtension
    {
        /// <summary>
        /// Serializes a MessageBase to Json.
        /// </summary>
        /// <param name="message">The MessageBase to serialize.</param>
        /// <returns>A Json string representing the DataObjectBase.</returns>
        public static string SerializeToJson(this MessageBase message)
        {
            // Prepare serializer settings
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new AddTypeNameContractResolver("[TYPE]"),
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            // Add converters
            settings.Converters.Add(new DataObjectConverter(true));

            // Serialize the MessageBase
            return JsonConvert.SerializeObject(message, settings);
        }
    }
}
