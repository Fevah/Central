using DevExpress.Entity.Model;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.LayoutControl;
using TIG.TotalLink.Client.Core.Helper;
using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Wrapper.Editor
{
    /// <summary>
    /// Wrapper for a property being displayed in a DataLayoutControl.
    /// </summary>
    public class DataLayoutItemWrapper : EditorWrapperBase
    {
        #region Private Fields

        private bool _isLabelVisible = true;
        private LayoutItemLabelPosition _labelPosition = LayoutItemLabelPosition.Left;
        private double _width = double.NaN;

        #endregion


        #region Constructors

        public DataLayoutItemWrapper(System.Type type, IEdmPropertyInfo property)
            : base(type, property)
        {
        }

        public DataLayoutItemWrapper(VisiblePropertyWrapper propertyWrapper)
            : base(propertyWrapper)
        {
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Indicates if the label is visible.
        /// Defaults to true.
        /// </summary>
        public bool IsLabelVisible
        {
            get { return _isLabelVisible; }
            set { SetProperty(ref _isLabelVisible, value, () => IsLabelVisible); }
        }

        /// <summary>
        /// Indicates where the label will be positioned.
        /// Defaults to Left.
        /// </summary>
        public LayoutItemLabelPosition LabelPosition
        {
            get { return _labelPosition; }
            set { SetProperty(ref _labelPosition, value, () => LabelPosition); }
        }

        /// <summary>
        /// The width of the layout item.
        /// </summary>
        public virtual double Width
        {
            get
            {
                if (_width.IsNumber())
                    return _width;

                return double.NaN;
            }
            set { SetProperty(ref _width, value, () => Width); }
        }

        #endregion
    }
}
