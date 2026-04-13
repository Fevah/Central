using System.Collections.Generic;
using System.Linq;

namespace TIG.IntegrationServer.Plugin.Core.Expression
{
    public class ExpressionDescriptor
    {
        #region Public Properties

        /// <summary>
        /// Method name
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// Expression body
        /// </summary>
        public string ExpressionBody { get; set; }

        /// <summary>
        /// Parameters in expression
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; }

        /// <summary>
        /// Target field type
        /// </summary>
        public string TargetFieldType { get; set; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Build expression by expression and parameters
        /// </summary>
        /// <returns>Expression</returns>
        public string BuildExpression()
        {
            return Parameters.Aggregate(ExpressionBody,
                (current, parameter) =>
                    current.Replace(parameter.Key, parameter.Value != null ? parameter.Value.ToString() : null));
        }

        #endregion

    }
}