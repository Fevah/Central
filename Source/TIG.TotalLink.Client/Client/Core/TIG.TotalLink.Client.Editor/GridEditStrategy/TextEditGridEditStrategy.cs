using System.Windows;
using System.Windows.Input;
using DevExpress.Xpf.Editors;
using TIG.TotalLink.Client.Editor.Core;

namespace TIG.TotalLink.Client.Editor.GridEditStrategy
{
    public class TextEditGridEditStrategy : GridEditStrategyBase
    {
        #region Overrides

        public override void ProcessPreviewKeyDown(FrameworkElement editor, KeyEventArgs e)
        {
            base.ProcessPreviewKeyDown(editor, e);

            // Abort if the base already handled the event
            if (e.Handled)
                return;

            // Attempt to get the editor as a TextEdit
            var textEdit = editor as TextEdit;
            if (textEdit == null)
                return;

            // Abort if any modifier keys are pressed (because they are already handled correctly)
            if (Keyboard.Modifiers != ModifierKeys.None)
                return;

            // Get the display text as a string
            var displayText = textEdit.DisplayText ?? string.Empty;

            // Process the key
            switch (e.Key)
            {
                case Key.Left:
                    // If text is selected, clear the selection and leave the caret at the start of the selection
                    if (textEdit.SelectionLength > 0)
                        textEdit.SelectionLength = 0;
                    // If text is not selected, move the caret left if it's not already at the start of the value
                    else if (textEdit.CaretIndex > 0)
                        textEdit.CaretIndex--;

                    e.Handled = true;
                    break;

                case Key.Right:
                    // If text is selected, clear the selection by moving the caret to the end of the selection
                    if (textEdit.SelectionLength > 0)
                        textEdit.CaretIndex = textEdit.SelectionStart + textEdit.SelectionLength;
                    // If text is not selected, move the caret right if it's not already at the end of the value
                    else if (textEdit.CaretIndex < displayText.Length)
                        textEdit.CaretIndex++;

                    e.Handled = true;
                    break;

                case Key.Home:
                case Key.PageUp:
                    // Move the caret to the start of the value
                    textEdit.CaretIndex = 0;

                    e.Handled = true;
                    break;

                case Key.End:
                case Key.PageDown:
                    // Move the caret to the end of the value
                    textEdit.CaretIndex = displayText.Length;

                    e.Handled = true;
                    break;
            }
        }

        #endregion
    }
}
