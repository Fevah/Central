using System.Collections;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Documents;

// http://stackoverflow.com/questions/935769/wpf-passwordbox-caret

namespace TIG.TotalLink.Client.Editor.Extension
{
    public static class PasswordBoxExtension
    {
        #region Public Structs

        public struct Selection
        {
            public int Start;
            public int Length;
        }

        #endregion


        #region Private Fields

        private static readonly PropertyInfo _selectionProperty = typeof(PasswordBox).GetProperty("Selection", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _textSegmentsField = typeof(TextRange).GetField("_textSegments", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo _selectMethod = typeof(PasswordBox).GetMethod("Select", BindingFlags.NonPublic | BindingFlags.Instance);

        #endregion


        #region Public Methods

        /// <summary>
        /// Returns information about the range that is selected in the PasswordBox.
        /// The PasswordBox does not make it easy to get at this information so a lot of reflection is used in here, so use this method sparingly.
        /// </summary>
        /// <param name="passwordBox">The PasswordBox to get the selection for.</param>
        /// <returns>Information about the range that is selected in the PasswordBox.</returns>
        public static Selection GetSelection(this PasswordBox passwordBox)
        {
            // Create a new Selection to hold the result
            var selection = new Selection();

            // Abort if any of the required properties were not found
            if (_selectionProperty == null || _textSegmentsField == null)
                return selection;

            // Attempt to get the TextSelection from the PasswordBox
            var textSelection = _selectionProperty.GetValue(passwordBox) as TextSelection;
            if (textSelection == null)
                return selection;

            // Attempt to get the TextSegments from the TextSelection
            var textSegments = _textSegmentsField.GetValue(textSelection) as IEnumerable;
            if (textSegments == null)
                return selection;

            // Attempt to get the first TextSegment
            var firstTextSegment = textSegments.Cast<object>().FirstOrDefault();
            if (firstTextSegment == null)
                return selection;

            // Attempt to get the Start property on the TextSegment type
            var startProperty = firstTextSegment.GetType().GetProperty("Start", BindingFlags.NonPublic | BindingFlags.Instance);
            if (startProperty == null)
                return selection;

            // Attempt to get the End property on the TextSegment type
            var endProperty = firstTextSegment.GetType().GetProperty("End", BindingFlags.NonPublic | BindingFlags.Instance);
            if (endProperty == null)
                return selection;

            // Get the PasswordTextPointers for the TextSegment
            var start = startProperty.GetValue(firstTextSegment);
            var end = endProperty.GetValue(firstTextSegment);

            // Get the Offset property from the PasswordTextPointer type
            var offsetProperty = start.GetType().GetProperty("Offset", BindingFlags.NonPublic | BindingFlags.Instance);
            if (offsetProperty == null)
                return selection;

            // Store the selection based on the Offsets from the start and end PasswordTextPointers
            selection.Start = (int)offsetProperty.GetValue(start);
            selection.Length = (int)offsetProperty.GetValue(end) - selection.Start;

            // Return the result
            return selection;
        }


        /// <summary>
        /// Selects a range in the PasswordBox.
        /// </summary>
        /// <param name="passwordBox">The PasswordBox to set the selection in.</param>
        /// <param name="start">The start index of the selection to set.</param>
        /// <param name="length">The length of the selection to set.</param>
        public static void Select(this PasswordBox passwordBox, int start, int length)
        {
            // Abort if the Select method was not found
            if (_selectMethod == null)
                return;

            // Invoke the Select method
            _selectMethod.Invoke(passwordBox, new object[] { start, length });
        }

        #endregion

    }
}
