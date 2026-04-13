using System.Text.RegularExpressions;

namespace TIG.TotalLink.Client.Core.Helper
{
    public static class EnumHelper
    {
        #region Private Constants

        private const string FormatRegext = @"[-_\s]";

        #endregion


        #region Public Methods

        /// <summary>
        /// Parses an enum value from a name.
        /// </summary>
        /// <typeparam name="TEnum">The type of enum to return.</typeparam>
        /// <param name="name">The enum item name to parse.</param>
        /// <returns>The enum value, if a matching value was found; otherwise null.</returns>
        public static TEnum? Parse<TEnum>(string name)
            where TEnum : struct
        {
            // Attempt to parse the name
            TEnum eValue;
            var success = System.Enum.TryParse(Regex.Replace(name, FormatRegext, string.Empty), true, out eValue);

            // If the name was parsed successfully, return the enum value, otherwise return null
            return success ? (TEnum?)eValue : null;
        }

        #endregion
    }
}