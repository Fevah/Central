using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Agent;
using TIG.IntegrationServer.Plugin.Core.ConverterPlugin.Interface;
using TIG.IntegrationServer.Plugin.Core.Expression;

namespace TIG.IntegrationServer.Plugin.Converter.CombineConverter
{
    public class CombineConverter : IConverterPlugin
    {
        /// <summary>
        /// Converter for conbine multi fields to one.
        /// </summary>
        /// <param name="expressionDescriptor">Expression descriptor for converter params information.</param>
        /// <param name="targetAgent">Target agent object to get required entity.</param>
        /// <param name="type">Requirement Type</param>
        /// <returns>Converted value</returns>
        public object Convert(ExpressionDescriptor expressionDescriptor, IAgent targetAgent, string type)
        {
            // Build real expression based on source entity.
            return expressionDescriptor.BuildExpression();
        }
    }
}