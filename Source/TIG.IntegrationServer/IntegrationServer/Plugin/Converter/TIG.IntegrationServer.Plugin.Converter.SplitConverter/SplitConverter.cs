using System;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Agent;
using TIG.IntegrationServer.Plugin.Core.ConverterPlugin.Interface;
using TIG.IntegrationServer.Plugin.Core.Expression;

namespace TIG.IntegrationServer.Plugin.Converter.SplitConverter
{
    public class SplitConverter : IConverterPlugin
    {
        /// <summary>
        /// Converter for split field.
        /// </summary>
        /// <param name="expressionDescriptor">Expression descriptor for converter params information.</param>
        /// <param name="targetAgent">Target agent object to get required entity.</param>
        /// <param name="type">Requirement Type</param>
        /// <returns>Converted value</returns>
        public object Convert(ExpressionDescriptor expressionDescriptor, IAgent targetAgent, string type)
        {
            // Build expression by source parameters
            var expression = expressionDescriptor.BuildExpression();
            var parameters = expression.Split(',');

            if (parameters.Length != 3)
            {
                return null;
            }

            var fieldValue = parameters[0].Trim();
            var separator = new[] { parameters[1].Trim().Trim('\'') };
            int index;

            if (!int.TryParse(parameters[2].TrimEnd(), out index))
            {
                return null;
            }

            var results = fieldValue.Split(separator, StringSplitOptions.None);

            return results.Length <= index ? null : results[index];
        }
    }
}
