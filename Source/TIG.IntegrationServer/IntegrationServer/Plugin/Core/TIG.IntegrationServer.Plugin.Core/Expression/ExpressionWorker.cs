using System;
using System.Linq;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Agent;
using TIG.IntegrationServer.Plugin.Core.ConverterPlugin.Interface;

namespace TIG.IntegrationServer.Plugin.Core.Expression
{
    public class ExpressionWorker
    {
        private readonly ExpressionDescriptor _descriptor;

        #region Constructor

        /// <summary>
        /// Constructor with expression descriptor
        /// </summary>
        /// <param name="descriptor">Expression descriptor</param>
        public ExpressionWorker(ExpressionDescriptor descriptor)
        {
            _descriptor = descriptor;
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Compute value by expression descriptor
        /// </summary>
        /// <param name="converter">Converter use for convert source value</param>
        /// <param name="targetAgent">Target agent for get relative entity</param>
        /// <param name="parameterHandler">Method for get parameter value by name</param>
        /// <returns>Target field value</returns>
        public object Compute(IConverterPlugin converter, IAgent targetAgent, Func<string, object> parameterHandler)
        {
            // Build parameters
            foreach (var key in _descriptor.Parameters.Keys.ToList())
            {
                var parameterVal = parameterHandler(key.Trim('[', ']'));
                _descriptor.Parameters[key] = parameterVal;
            }

            return converter.Convert(_descriptor, targetAgent, _descriptor.TargetFieldType);
        }

        #endregion

    }
}