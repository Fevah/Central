using System;
using System.Collections.Generic;
using System.Data.Entity.Design.PluralizationServices;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace TIG.TotalLink.Client.Core.Extension
{
    public static class StringExtension
    {
        private static readonly Regex _addSpacesRegex1 = new Regex(@"(\P{Ll})(\P{Ll}\p{Ll})", RegexOptions.Compiled);
        private static readonly Regex _addSpacesRegex2 = new Regex(@"(\p{Ll})(\P{Ll})", RegexOptions.Compiled);
        private static readonly Regex _invalidNameCharactersRegex = new Regex(@"[^a-zA-Z0-9_]", RegexOptions.Compiled);
        private static readonly Regex _normalizeUnderscoresRegex = new Regex(@"[_]{2,}", RegexOptions.Compiled);
        private static readonly Regex _normalizeSpacesRegex = new Regex(@"[ ]{2,}", RegexOptions.Compiled);
        private static readonly Regex _removeNewlinesRegex = new Regex(@"\r\n|\r|\n", RegexOptions.Compiled);
        private static readonly Regex _capitalizeRegex = new Regex(@"\b[a-zA-Z]\w+\b", RegexOptions.Compiled);
        private static readonly Regex _formatWithRegex = new Regex(@"\{(?<param>\w+)\}", RegexOptions.Compiled);
        private static readonly PluralizationService _pluralizationService = PluralizationService.CreateService(new CultureInfo("en-US"));

        /// <summary>
        /// Capitalises the first letter of each word of a string.
        /// </summary>
        /// <param name="s">The string to capitalise.</param>
        /// <returns>The string with the first letter of each word capitalised.</returns>
        public static string ToTitleCase(this string s)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLower());
        }

        /// <summary>
        /// Adds spaces to the string before each capital letter.
        /// </summary>
        /// <param name="s">The string to add spaces to.</param>
        /// <returns>The string with spaces added before each capital letter or number.</returns>
        public static string AddSpaces(this string s)
        {
            return _addSpacesRegex2.Replace(_addSpacesRegex1.Replace(s, "$1 $2"), "$1 $2");
        }

        /// <summary>
        /// Converts a string into a value that is valid as a control name.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        /// <returns>A string that is valid as a control name.</returns>
        public static string ToControlName(this string s)
        {
            // Add a leading underscore
            s = "_" + s;

            // Replace all invalid characters with their hex value surrounded by underscores
            var result = _invalidNameCharactersRegex.Replace(s, m => string.Format("_{0:x2}_", Convert.ToByte(m.Value[0])));

            // Normalize the underscores (so two or more underscores do not appear together)
            result = _normalizeUnderscoresRegex.Replace(result, "_");

            // Return the result
            return result;
        }

        /// <summary>
        /// Normalizes spaces within a string.
        /// i.e. Any groups of two or more spaces are reduced to one space.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        /// <returns>A string with spaces normalized.</returns>
        public static string NormalizeSpaces(this string s)
        {
            return _normalizeSpacesRegex.Replace(s, " ");
        }

        /// <summary>
        /// Replaces carriage returns and newlines in the string with spaces.
        /// Note that this may leave groups of multiple spaces together, so you may want to also call NormalizeSpaces after this.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        /// <returns>A string with carriage returns and newlines removed.</returns>
        public static string ReplaceNewlines(this string s)
        {
            return _removeNewlinesRegex.Replace(s, " ");
        }

        /// <summary>
        /// Removes all tabs from the string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        /// <returns>A string with tabs removed.</returns>
        public static string RemoveTabs(this string s)
        {
            return s.Replace("\t", string.Empty);
        }

        /// <summary>
        /// Removes all underscores from the string.
        /// </summary>
        /// <param name="s">The string to convert.</param>
        /// <returns>A string with underscores removed.</returns>
        public static string RemoveUnderscores(this string s)
        {
            return s.Replace("_", string.Empty);
        }

        /// <summary>
        /// Formats an xml string using UTF8 encoding.
        /// </summary>
        /// <param name="s">The string to format.</param>
        /// <returns>A formatted xml string.</returns>
        public static string FormatXml(this string s)
        {
            return FormatXml(s, Encoding.UTF8);
        }

        /// <summary>
        /// Formats an xml string using the specified encoding.
        /// </summary>
        /// <param name="s">The string to format.</param>
        /// <param name="encoding">The character encoding to use.</param>
        /// <returns>A formatted xml string.</returns>
        public static string FormatXml(this string s, Encoding encoding)
        {
            // Load the xml string into an XmlDocument
            var doc = new XmlDocument();
            doc.LoadXml(s);

            // Create a stream and writer for the result
            string xmlString;
            var memoryStream = new MemoryStream();
            using (var xmlWriter = new XmlTextWriter(memoryStream, encoding))
            {
                // Configure the writer
                xmlWriter.Formatting = Formatting.Indented;

                // Write the XmlDocument to the writer
                doc.WriteTo(xmlWriter);
                xmlWriter.Flush();

                // Reposition the stream at the beginning
                memoryStream.Seek(0, SeekOrigin.Begin);

                // Read the stream into a string
                var reader = new StreamReader(memoryStream);
                xmlString = reader.ReadToEnd();
            }

            // Return the result
            return xmlString;
        }

        /// <summary>
        /// Returns the plural form of the specified word.
        /// </summary>
        /// <param name="s">The word to pluralize.</param>
        /// <returns>The plural form of the specified word.</returns>
        public static string Pluralize(this string s)
        {
            return _pluralizationService.Pluralize(s);
        }

        /// <summary>
        /// Returns the plural form of the specified word if the value is not equal to 1.
        /// </summary>
        /// <param name="s">The word to pluralize.</param>
        /// <param name="value">A value used to determine if the word should be pluralized.</param>
        /// <returns>The plural form of the specified word if the value is not equal to 1; otherwise returns the word unchanged.</returns>
        public static string Pluralize(this string s, int value)
        {
            if (value == 1)
                return s;

            return Pluralize(s);
        }

        /// <summary>
        /// Formats a string by making the first letter uppercase.
        /// </summary>
        /// <param name="s">The string to format.</param>
        /// <returns>The string with the first letter uppercase.</returns>
        public static string FirstLetterToUpper(this string s)
        {
            if (s == null)
                return null;

            if (s.Length > 1)
                return char.ToUpper(s[0]) + s.Substring(1).ToLower();

            return s.ToUpper();
        }

        /// <summary>
        /// Formats a string by making the first letter of each word uppercase.
        /// </summary>
        /// <param name="s">The string to capitalize.</param>
        /// <param name="exclusions">Regex patterns describing words to exclude.</param>
        /// <returns>The string capitalized.</returns>
        public static string Capitalize(this string s, params string[] exclusions)
        {
            return _capitalizeRegex.Replace(s, match =>
            {
                var word = match.ToString();
                return exclusions.Any(p => Regex.IsMatch(word, p)) ? word : word.FirstLetterToUpper();
            });
        }

        /// <summary>
        /// Formats a string by replacing named parameters with values from a dictionary.
        /// </summary>
        /// <param name="s">The string to format.</param>
        /// <param name="replacements">A dictionary of parameter/value pairs to replace in this string.</param>
        /// <returns>The string with named parameters replaced by values from <paramref name="replacements"/>.</returns>
        public static string FormatWith(this string s, Dictionary<string, string> replacements)
        {
            return _formatWithRegex.Replace(s, match =>
            {
                // Get the parameter name
                var param = match.Groups["param"].Value;

                // If a parameter match is found in the dictionary, return the corresponding value
                string value;
                if (replacements.TryGetValue(param, out value))
                    return value;

                // Return the original match string unchanged
                return match.Value;
            });
        }

        /// <summary>
        /// Reverse a string per words
        /// </summary>
        /// <param name="s">The string to be reverse</param>
        /// <returns>Reversed words</returns>
        public static string ReversePerWords(this string s)
        {
            var reverseWords = s.Split(' ').Reverse();
            return string.Join(" ", reverseWords);
        }
    }
}
