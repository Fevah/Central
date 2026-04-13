using System.Windows;
using System.Windows.Input;
using DevExpress.Xpf.Editors;
using TIG.TotalLink.Client.Editor.Core;

namespace TIG.TotalLink.Client.Editor.GridEditStrategy
{
    public class MaskedTextEditGridEditStrategy : GridEditStrategyBase
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
                    // Move the caret left if it's not already at the start of the value
                    // This will always move it into the previous mask segment
                    if (textEdit.CaretIndex > 0)
                        textEdit.CaretIndex--;

                    e.Handled = true;
                    break;

                case Key.Right:
                    // Attempt to move the caret an increasing number of characters to the right until the CaretIndex actually changes or we reach the end of the value
                    // The CaretIndex will only change when we have moved enough positions to land within the next mask segment
                    var caretDelta = 1;
                    var caretMoved = false;
                    while (!caretMoved && textEdit.CaretIndex + caretDelta < displayText.Length)
                    {
                        var oldCaretIndex = textEdit.CaretIndex;
                        textEdit.CaretIndex += caretDelta;

                        caretMoved = (textEdit.CaretIndex != oldCaretIndex);
                        caretDelta++;
                    }

                    e.Handled = true;
                    break;

                case Key.Up:
                    // Increment the selected mask segment
                    textEdit.SpinUp();

                    e.Handled = true;
                    break;

                case Key.Down:
                    // Decrement the selected mask segment
                    textEdit.SpinDown();

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
