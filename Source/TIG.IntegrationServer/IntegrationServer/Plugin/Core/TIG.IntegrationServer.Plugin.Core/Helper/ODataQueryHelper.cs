using System.Collections.Generic;
using System.Linq;

namespace TIG.IntegrationServer.Plugin.Core.Helper
{
    public static class ODataQueryHelper
    {
        #region Public Methods

        /// <summary>
        /// Get property from key
        /// </summary>
        /// <param name="key">Key for get property value</param>
        /// <param name="entity">Entity for get current field</param>
        /// <returns>Value get from key</returns>
        public static object GetProperty(string key, IDictionary<string, object> entity)
        {
            var fields = key.Split('.');
            var propertyKey = fields.Last();

            // Nested to get value by key.
            var subEntity = entity;

            for (var i = 0; i < fields.Length - 1; i++)
            {
                var subEntityKey = fields[0];
                object fieldsValue;
                if (subEntity.TryGetValue(subEntityKey, out fieldsValue)
                    && fieldsValue is IDictionary<string, object>)
                {
                    subEntity = (IDictionary<string, object>) fieldsValue;
                }
                else
                {
                    return null;
                }
            }

            object proertyValue;

            return subEntity.TryGetValue(propertyKey, out proertyValue) ? proertyValue : null;
        }

        #endregion
    }
}