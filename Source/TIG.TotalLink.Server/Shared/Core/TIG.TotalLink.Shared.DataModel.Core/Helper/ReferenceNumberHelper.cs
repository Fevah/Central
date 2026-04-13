using System;
using System.Text.RegularExpressions;

namespace TIG.TotalLink.Shared.DataModel.Core.Helper
{
    public class ReferenceNumberHelper
    {
        #region Public Methods

        /// <summary>
        /// Formats a System Code, Sequence Code and Sequence Number and returns it as a long.
        /// </summary>
        /// <param name="systemCode">The System Code to use in the Reference Number.</param>
        /// <param name="sequenceCode">The Sequence Code to use in the Reference Number.</param>
        /// <param name="sequenceNumber">The Sequence Number to use in the Reference Number.</param>
        /// <param name="format">A numeric format which combines values to form the Reference Number.</param>
        /// <returns>A Reference Number.</returns>
        public static long FormatValue(int systemCode, int sequenceCode, long sequenceNumber, string format)
        {
            // Apply the format
            var valueString = string.Format(format, systemCode, sequenceCode, sequenceNumber);

            // Attempt to get the result as a long
            long value;
            if (long.TryParse(valueString, out value))
                return value;

            // Throw an error if the result could not be converted
            throw new InvalidOperationException("Formatted result is not numeric.");
        }

        /// <summary>
        /// Formats a Reference Number for display purposes.
        /// </summary>
        /// <param name="referenceNumber">The Reference Number to format.</param>
        /// <param name="format">A numeric format which defines how the Reference Number will be displayed.</param>
        /// <returns>A formatted Reference Number.</returns>
        public static string FormatDisplay(long referenceNumber, string format)
        {
            return string.Format(format, referenceNumber);
        }

        /// <summary>
        /// Cleans a formatted Reference Number.
        /// </summary>
        /// <param name="referenceNumber">The formatted Reference Number to clean.</param>
        /// <param name="pattern">A regular expression which selects the characters to remove from the Reference Number.</param>
        /// <returns>A cleaned Reference Number.</returns>
        public static string FormatDisplayCleaned(string referenceNumber, string pattern)
        {
            return Regex.Replace(referenceNumber, pattern, string.Empty);
        }

        #endregion
    }
}
