using System;
using System.Globalization;
using System.Windows.Data;
using TIG.TotalLink.Client.Core.Extension;

namespace TIG.TotalLink.Client.Editor.Converter
{
    /// <summary>
    /// Performs several "cleaning" functions on a string depending on the switches that are set.
    /// </summary>
    [ValueConversion(typeof(string), typeof(string))]
    public class StringCleanConverter : IValueConverter
    {
        private bool _all = true;
        private bool _trim;
        private bool _removeTabs;
        private bool _removeNewlines;
        private bool _normalizeSpaces;

        /// <summary>
        /// Overrides all other flags and performs all available functions.
        /// This is the default.
        /// Setting any other flag to true will set All to false.
        /// </summary>
        public bool All
        {
            get { return _all; }
            set { _all = value; }
        }

        /// <summary>
        /// Indicates if the converter should trim leading and trailing spaces from the string.
        /// </summary>
        public bool Trim
        {
            get { return _trim; }
            set
            {
                _trim = value;

                if (_trim)
                    All = false;
            }
        }

        /// <summary>
        /// Indicates if the converter should remove tabs from the string.
        /// </summary>
        public bool RemoveTabs
        {
            get { return _removeTabs; }
            set
            {
                _removeTabs = value;

                if (_removeTabs)
                    All = false;
            }
        }

        /// <summary>
        /// Indicates if the converter should remove all carriage returns and newlines from the string.
        /// These will be replaced with a space.
        /// </summary>
        public bool RemoveNewlines
        {
            get { return _removeNewlines; }
            set
            {
                _removeNewlines = value;

                if (_removeNewlines)
                    All = false;
            }
        }

        /// <summary>
        /// Indicates if the converter should normalize spaces in the string.
        /// i.e. Any groups of two or more spaces are reduced to one space.
        /// </summary>
        public bool NormalizeSpaces
        {
            get { return _normalizeSpaces; }
            set
            {
                _normalizeSpaces = value;

                if (_normalizeSpaces)
                    All = false;
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            if (Trim || All)
                stringValue = stringValue.Trim();

            if (RemoveTabs || All)
                stringValue = stringValue.RemoveTabs();

            if (RemoveNewlines || All)
                stringValue = stringValue.ReplaceNewlines();

            if (NormalizeSpaces || All)
                stringValue = stringValue.NormalizeSpaces();

            return stringValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new NotSupportedException("StringCleanConverter can only be used for one way conversion.");
        }
    }
}
