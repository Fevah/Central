using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.Facade.Core.Helper;

namespace TIG.TotalLink.Shared.Facade.Core.Extension
{
    public static class DataObjectBaseExtension
    {
        /// <summary>
        /// Serializes a DataObjectBase to Json.
        /// Use this method when you want to send a flattened copy of a DataObjectBase to a method service.
        /// </summary>
        /// <param name="dataObject">The DataObjectBase to serialize.</param>
        /// <returns>A Json string representing the DataObjectBase.</returns>
        public static string SerializeToJson(this DataObjectBase dataObject)
        {
            return JsonHelper.SerializeDataObject(dataObject);
        }
    }
}
