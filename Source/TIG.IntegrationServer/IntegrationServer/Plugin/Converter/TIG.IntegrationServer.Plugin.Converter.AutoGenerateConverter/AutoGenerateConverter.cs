using System;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Agent;
using TIG.IntegrationServer.Plugin.Core.ConverterPlugin.Interface;
using TIG.IntegrationServer.Plugin.Core.Expression;

namespace TIG.IntegrationServer.Plugin.Converter.AutoGenerateConverter
{
    public class AutoGenerateConverter : IConverterPlugin
    {
        /// <summary>
        /// Converter for auto generate Guid.
        /// </summary>
        /// <param name="expressionDescriptor">Expression descriptor for converter params information.</param>
        /// <param name="targetAgent">Target agent object to get required entity.</param>
        /// <param name="type">Requirement Type</param>
        /// <returns>Converted value</returns>
        public object Convert(ExpressionDescriptor expressionDescriptor, IAgent targetAgent, string type)
        {
            if (type == "System.Guid")
            {
                return Guid.NewGuid();
            }
            return null;
        }
    }
}
