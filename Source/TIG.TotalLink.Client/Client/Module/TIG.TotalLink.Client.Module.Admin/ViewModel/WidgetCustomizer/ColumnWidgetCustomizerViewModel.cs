using System.Windows;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Grid;
using TIG.TotalLink.Client.Editor.Control;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.WidgetCustomizer
{
    [WidgetCustomizer("Columns", 100)]
    public class ColumnWidgetCustomizerViewModel : WidgetCustomizerViewModelBase
    {
        #region Private Fields

        private readonly GridControlEx _gridControl;

        #endregion


        #region Constructors

        public ColumnWidgetCustomizerViewModel()
        {
        }

        public ColumnWidgetCustomizerViewModel(GridControlEx gridControl)
            : this()
        {
            // Initialize properties
            _gridControl = gridControl;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The grid view which we will display columns from.
        /// </summary>
        public GridDataViewBase View
        {
            get
            {
                if (_gridControl == null)
                    return null;

                return _gridControl.View as GridDataViewBase;
            }
        }

        #endregion


        #region Overrides

        public new static WidgetCustomizerViewModelBase CreateCustomizer(FrameworkElement content, WidgetViewModelBase widget)
        {
            // Attempt to find a GridControlEx within the content
            var gridControl = LayoutHelper.FindElementByType<GridControlEx>(content);
            if (gridControl == null)
                return null;

            // Return a new ColumnWidgetCustomizerViewModel
            return new ColumnWidgetCustomizerViewModel(gridControl);
        }

        #endregion
    }
}
