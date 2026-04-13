using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Agent;
using TIG.IntegrationServer.Plugin.Core.Expression;
using TIG.IntegrationServer.Plugin.Core.Interface;

namespace TIG.IntegrationServer.Plugin.Core.ConverterPlugin.Interface
{
    public interface IConverterPlugin : IPlugin
    {
        /// <summary>
        /// Convert based expression descriptor
        /// </summary>
        /// <param name="expressionDescriptor">Expression descriptor for build convert context.</param>
        /// <param name="targetAgent">Target agent for get relative entity from persistence</param>
        /// <param name="targetType">Target field type</param>
        /// <returns>Return value after converted</returns>
        object Convert(ExpressionDescriptor expressionDescriptor, IAgent targetAgent, string targetType);
    }
}