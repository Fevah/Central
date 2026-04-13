using System.Windows;
using System.Windows.Input;
using DevExpress.Xpf.Editors;

namespace TIG.TotalLink.Client.Editor.GridEditStrategy
{
    public class SpinEditGridEditStrategy : TextEditGridEditStrategy
    {
        #region Overrides

        public override void ProcessPreviewKeyDown(FrameworkElement editor, KeyEventArgs e)
        {
            base.ProcessPreviewKeyDown(editor, e);

            // Abort if the base already handled the event
            if (e.Handled)
                return;

            // Attempt to get the editor as a SpinEdit
            var spinEdit = editor as SpinEdit;
            if (spinEdit == null)
                return;

            // Abort if any modifier keys are pressed (because they are already handled correctly)
            if (Keyboard.Modifiers != ModifierKeys.None)
                return;

            // Process the key
            switch (e.Key)
            {
                case Key.Up:
                    // Increment the value
                    spinEdit.SpinUp();

                    e.Handled = true;
                    break;

                case Key.Down:
                    // Decrement the value
                    spinEdit.SpinDown();

                    e.Handled = true;
                    break;
            }
        }

        #endregion
    }
}
