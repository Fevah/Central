using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevExpress.Xpf.Editors;
using TIG.TotalLink.Client.Editor.Core;
using TIG.TotalLink.Client.Editor.Extension;

namespace TIG.TotalLink.Client.Editor.GridEditStrategy
{
    public class PasswordBoxEditGridEditStrategy : GridEditStrategyBase
    {
        #region Overrides

        public override void ProcessPreviewKeyDown(FrameworkElement editor, KeyEventArgs e)
        {
            base.ProcessPreviewKeyDown(editor, e);

            // Abort if the base already handled the event
            if (e.Handled)
                return;

            // Attempt to get the editor as a BaseEdit
            var baseEdit = editor as BaseEdit;
            if (baseEdit == null)
                return;

            // Attempt to get the editor within the BaseEdit as a PasswordBox
            var passwordBox = baseEdit.EditCore as PasswordBox;
            if (passwordBox == null)
                return;

            // Abort if any modifier keys are pressed (because they are already handled correctly)
            if (Keyboard.Modifiers != ModifierKeys.None)
                return;

            // Get the display text as a string
            var displayText = baseEdit.DisplayText ?? string.Empty;

            // Get info about the selection in the password box
            var selection = passwordBox.GetSelection();

            // Process the key
            switch (e.Key)
            {
                case Key.Left:
                    // If text is selected, clear the selection and leave the caret at the start of the selection
                    if (selection.Length > 0)
                        passwordBox.Select(selection.Start, 0);
                    // If text is not selected, move the caret left if it's not already at the start of the value
                    else if (selection.Start > 0)
                        passwordBox.Select(selection.Start - 1, 0);

                    e.Handled = true;
                    break;

                case Key.Right:
                    // If text is selected, clear the selection by moving the caret to the end of the selection
                    if (selection.Length > 0)
                        passwordBox.Select(selection.Start + selection.Length, 0);
                    // If text is not selected, move the caret right if it's not already at the end of the value
                    else if (selection.Start < displayText.Length)
                        passwordBox.Select(selection.Start + 1, 0);

                    e.Handled = true;
                    break;

                case Key.Home:
                case Key.PageUp:
                    // Move the caret to the start of the value
                    passwordBox.Select(0, 0);

                    e.Handled = true;
                    break;

                case Key.End:
                case Key.PageDown:
                    // Move the caret to the end of the value
                    passwordBox.Select(displayText.Length, 0);

                    e.Handled = true;
                    break;
            }
        }

        #endregion
    }
}
