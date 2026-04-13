using System.Text.RegularExpressions;

namespace TIG.IntegrationServer.Common.Extension
{
    public static class StringExtension
    {
        /// <summary>
        /// Smart compare to ignore some specific characters
        /// </summary>
        /// <param name="sourceField">Source field for compare</param>
        /// <param name="targetField">Target field for compare</param>
        /// <returns>True, indicate compare result successful.</returns>
        public static bool SmartCompare(this string sourceField, string targetField)
        {
            var source = Regex.Replace(sourceField, @"[ _-]", "").ToLower();
            var target = Regex.Replace(targetField, @"[ _-]", "").ToLower();

            return source.Equals(target);
        }
    }
}