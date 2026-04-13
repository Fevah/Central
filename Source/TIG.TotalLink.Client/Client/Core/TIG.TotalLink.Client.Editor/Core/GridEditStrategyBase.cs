using System.Windows;
using System.Windows.Input;

namespace TIG.TotalLink.Client.Editor.Core
{
    public abstract class GridEditStrategyBase
    {
        #region Public Methods

        public virtual void ProcessPreviewKeyDown(FrameworkElement editor, KeyEventArgs e)
        {
        }

        #endregion
    }
}
