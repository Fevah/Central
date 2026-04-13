using System.Text.RegularExpressions;

namespace TIG.IntegrationServer.Plugin.Core.Helper
{
    public static class QueryHelper
    {
        private const string QueryRegex = @"\[(?<Key>[a-zA-Z.]+)\]=\'?(?<Value>.*)\'?";

        public static string GetQueryKey(string query)
        {
            var match = Regex.Match(query, QueryRegex);
            return !match.Success ? null : match.Groups["Key"].Value;
        }

        public static string GetQueryValue(string query)
        {
            var match = Regex.Match(query, QueryRegex);
            return !match.Success ? null : match.Groups["Value"].Value;
        }
    }
}