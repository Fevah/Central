using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using Newtonsoft.Json;
using TIG.TotalLink.Client.Editor.DataModel;

namespace TIG.TotalLink.Client.Editor.Converter
{
    /// <summary>
    /// Converts a json array into a string which describes the count of items it contains.
    /// </summary>
    [ValueConversion(typeof(string), typeof(string))]
    public class CommentsStringToCountConverter : IValueConverter
    {
        private const string DefaultValue = "[0]";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Attempt to get the value as a string
            var stringValue = value as string;
            if (string.IsNullOrWhiteSpace(stringValue))
                return DefaultValue;

            // Abort if the string does not appear to be a json array
            stringValue = stringValue.Trim();
            if (!(stringValue.StartsWith("[{") && stringValue.EndsWith("}]")))
                return DefaultValue;

            // Attempt to deserialize the comments string and display the count
            try
            {
                var commentsList = JsonConvert.DeserializeObject<List<CommentDataModel>>(stringValue);
                return string.Format("[{0}]", commentsList.Count);
            }
            catch (Exception)
            {
                return DefaultValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new NotSupportedException("CommentsStringToCountConverter can only be used for one way conversion.");
        }
    }
}
