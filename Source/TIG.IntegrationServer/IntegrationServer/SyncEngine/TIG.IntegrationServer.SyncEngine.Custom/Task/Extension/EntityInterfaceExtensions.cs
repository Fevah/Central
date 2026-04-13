using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Attribute;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Entity;
using TIG.IntegrationServer.Security.Cryptography;

namespace TIG.IntegrationServer.SyncEngine.Custom.Task.Extension
{
    public static class EntityInterfaceExtensions
    {
        #region Public Methods

        /// <summary>
        /// Get entity fields which be mark to EntityFieldAttribute 
        /// </summary>
        /// <param name="entity">Sync entity</param>
        /// <returns>Retrieved properites info</returns>
        public static IEnumerable<PropertyInfo> GetEntityFields(this IEntity entity)
        {
            var properties = entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(i =>
                    i.CanRead &&
                    i.CanWrite &&
                    i.CustomAttributes.Any(p => p.AttributeType == typeof (EntityFieldAttribute)))
                .ToArray();
            return properties;
        }

        /// <summary>
        /// Get entity field with value
        /// </summary>
        /// <param name="entity">Sync entity</param>
        /// <returns></returns>
        public static IDictionary<string, object> GetEntityFieldsWithValues(this IEntity entity)
        {
            var properties = entity.GetEntityFields();
            return properties.ToDictionary(p => p.Name, p => p.GetValue(entity));
        }

        /// <summary>
        /// Get entity hash
        /// </summary>
        /// <param name="entity">Sync entity</param>
        /// <param name="hashMaster">Hash handler</param>
        /// <returns>Hash value</returns>
        public static string GetEntityHash(this IEntity entity, IHashMaster hashMaster)
        {
            var entityDataHeap = new StringBuilder();
            var fieldsWithValues = entity.GetEntityFieldsWithValues();

            foreach (var i in fieldsWithValues)
            {
                entityDataHeap.Append(i.Key + i.Value);
            }

            var hash = hashMaster.GetHashAsHexString(entityDataHeap.ToString());
            return hash;
        }

        #endregion
    }
}
