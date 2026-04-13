using System.Linq;
using System.Text.RegularExpressions;

namespace TIG.IntegrationServer.Plugin.Core.Expression
{
    public class ExpressionBuilder
    {
        /// <summary>
        /// Build expression descriptor by expression.
        /// </summary>
        /// <param name="expression">Native expresion</param>
        /// <returns>Expression descriptor</returns>
        public ExpressionDescriptor Build(string expression)
        {
            const string regexPattern = @"(?<Method>[a-zA-Z]+)\((?<Body>.*)\)";
            const string regexParamsPattern = @"\[.*?\]";

            // Match method information by regex.
            var match = Regex.Match(expression, regexPattern);

            // Return null value, if match failed.
            if (!match.Success)
            {
                return null;
            }

            var body = match.Groups["Body"].Value;
            var method = match.Groups["Method"].Value;

            // Match parameters by regex.
            var matchParams = Regex.Matches(body, regexParamsPattern);
            var parameters = (from Match matchParam in matchParams select matchParam.Groups[0].Value).ToDictionary<string, string, object>(parmameter => parmameter, parmameter => null);

            return new ExpressionDescriptor
            {
                ExpressionBody = body,
                MethodName = method,
                Parameters = parameters
            };
        }
    }
}