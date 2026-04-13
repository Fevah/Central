using System.Windows;
using System.Windows.Controls;

namespace TIG.TotalLink.Client.Editor.Control
{
    /// <summary>
    /// A TextBlock which only shows its tooltip when the text has been trimmed.
    /// http://stackoverflow.com/questions/1041820/how-can-i-determine-if-my-textblock-text-is-being-trimmed
    /// </summary>
    public class TextBlockEx : TextBlock
    {
        #region Private Properties

        /// <summary>
        /// Indicates if the text in the TextBlock has been trimmed to fit.
        /// </summary>
        /// <returns></returns>
        private bool IsTextTrimmed()
        {
            Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return ActualWidth < DesiredSize.Width;
        }

        #endregion


        #region Overrides

        protected override void OnToolTipOpening(ToolTipEventArgs e)
        {
            e.Handled = !IsTextTrimmed();
        }

        #endregion
    }
}
