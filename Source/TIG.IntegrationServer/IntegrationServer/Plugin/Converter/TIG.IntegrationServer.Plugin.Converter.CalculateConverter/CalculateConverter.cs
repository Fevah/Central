using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Agent;
using TIG.IntegrationServer.Plugin.Core.ConverterPlugin.Interface;
using TIG.IntegrationServer.Plugin.Core.Expression;

namespace TIG.IntegrationServer.Plugin.Converter.CalculateConverter
{
    public class CalculateConverter : IConverterPlugin
    {
        /// <summary>
        /// Convert based on string expression to calculate value.
        /// </summary>
        /// <param name="expressionDescriptor">Expression descriptor for converter params information.</param>
        /// <param name="targetAgent">Target agent object to get required entity.</param>
        /// <param name="type">Requirement Type</param>
        /// <returns>Converted value</returns>
        public object Convert(ExpressionDescriptor expressionDescriptor, IAgent targetAgent, string type)
        {
            // Build real expression based on source entity.
            var expression = expressionDescriptor.BuildExpression();

#pragma warning disable 618
            var ve = Microsoft.JScript.Vsa.VsaEngine.CreateEngine();
#pragma warning restore 618
            return Microsoft.JScript.Eval.JScriptEvaluate(expression, ve);
        }
    }
}