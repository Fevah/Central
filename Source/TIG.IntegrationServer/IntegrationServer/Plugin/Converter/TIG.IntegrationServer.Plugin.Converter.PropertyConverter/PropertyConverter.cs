using System.Collections.Generic;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Agent;
using TIG.IntegrationServer.Plugin.Core.ConverterPlugin.Interface;
using TIG.IntegrationServer.Plugin.Core.Expression;
using TIG.IntegrationServer.Plugin.Core.Helper;

namespace TIG.IntegrationServer.Plugin.Converter.PropertyConverter
{
    public class PropertyConverter : IConverterPlugin
    {
        /// <summary>
        /// Convert entity to a field by specifed expression information.
        /// </summary>
        /// <param name="expressionDescriptor">Expression descriptor for converter params information.</param>
        /// <param name="targetAgent">Target agent object to get required entity.</param>
        /// <param name="type">Requirement Type</param>
        /// <returns>Converted value</returns>
        public object Convert(ExpressionDescriptor expressionDescriptor, IAgent targetAgent, string type)
        {
            // Get key information
            var bodySections = expressionDescriptor.ExpressionBody.Split(',');
            if (bodySections.Length != 3)
            {
                return null;
            }

            // Entity name
            var sourceEntityFieldNeme = bodySections[0].Trim();
            var targetFieldName = bodySections[1].Trim();

            // Get entity by entity name.
            object entity;
            if (!expressionDescriptor.Parameters.TryGetValue(sourceEntityFieldNeme, out entity))
            {
                return null;
            }

            // Get field from source entity.
            var dicEntity = entity as IDictionary<string, object>;
            return dicEntity == null ? null : ODataQueryHelper.GetProperty(targetFieldName, dicEntity);
        }
    }
}
