using DevExpress.Data;
using DevExpress.Entity.Model;
using DevExpress.Xpf.Core.Native;
using DevExpress.XtraGrid;
using TIG.TotalLink.Client.Core.Helper;

namespace TIG.TotalLink.Client.Editor.Core.Editor
{
    /// <summary>
    /// Base wrapper class for properties that are display in some kind of grid column.
    /// </summary>
    public abstract class GridColumnWrapperBase : EditorWrapperBase
    {
        #region Private Fields

        private int _sortIndex = -1;
        private ColumnSortOrder _sortOrder = ColumnSortOrder.None;
        private ColumnSortMode _sortMode = ColumnSortMode.Default;
        private int _groupIndex = -1;
        private double _width = double.NaN;
        private bool? _fixedWidth = null;

        #endregion


        #region Constructors

        protected GridColumnWrapperBase(System.Type type, IEdmPropertyInfo property)
            : base(type, property)
        {
        }
        
        protected GridColumnWrapperBase(VisiblePropertyWrapper propertyWrapper)
            : base(propertyWrapper)
        {
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The zero-based column's index among sorted columns. -1 if data is not sorted by this column.
        /// Defaults to -1.
        /// </summary>
        public virtual int SortIndex
        {
            get { return _sortIndex; }
            set { SetProperty(ref _sortIndex, value, () => SortIndex); }
        }

        /// <summary>
        /// The direction that the column will be sorted.
        /// Defaults to None.
        /// </summary>
        public virtual ColumnSortOrder SortOrder
        {
            get { return _sortOrder; }
            set { SetProperty(ref _sortOrder, value, () => SortOrder); }
        }

        /// <summary>
        /// Specifies how a column sort will be calculated when a sort is applied.
        /// Defaults to Default.
        /// </summary>
        public virtual ColumnSortMode SortMode
        {
            get { return _sortMode; }
            set { SetProperty(ref _sortMode, value, () => SortMode); }
        }

        /// <summary>
        /// The zero-based column's index among grouped columns. -1 if data is not grouped by this column.
        /// Defaults to -1.
        /// </summary>
        public virtual int GroupIndex
        {
            get { return _groupIndex; }
            set { SetProperty(ref _groupIndex, value, () => GroupIndex); }
        }

        /// <summary>
        /// The width of the column.
        /// </summary>
        public virtual double Width
        {
            get
            {
                if (_width.IsNumber())
                    return _width;

                if (Editor != null)
                    return Editor.DefaultColumnWidth;
                
                return double.NaN;
            }
            set { SetProperty(ref _width, value, () => Width); }
        }

        /// <summary>
        /// Indicates if the column has a fixed width.
        /// </summary>
        public virtual bool FixedWidth
        {
            get
            {
                if (_fixedWidth != null)
                    return _fixedWidth.Value;

                if (Editor != null)
                    return Editor.DefaultFixedWidth;

                return false;
            }
            set
            {
                _fixedWidth = value;
                RaisePropertyChanged(() => FixedWidth);
            }
        }

        #endregion

    }
}
