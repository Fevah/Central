using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Definition
{
    public class SpinEditorDefinition : EditorDefinitionBase
    {
        #region Private Fields

        private decimal? _minValue;
        private decimal? _maxValue;
        private decimal _increment = 1;
        private string _displayFormat = "N0";

        #endregion


        #region Public Properties

        /// <summary>
        /// The minimum allowable value.
        /// Defaults to null.
        /// </summary>
        public decimal? MinValue
        {
            get { return _minValue; }
            set { SetProperty(ref _minValue, value, () => MinValue); }
        }

        /// <summary>
        /// The maximum allowable value.
        /// Defaults to null.
        /// </summary>
        public decimal? MaxValue
        {
            get { return _maxValue; }
            set { SetProperty(ref _maxValue, value, () => MaxValue); }
        }

        /// <summary>
        /// The amount that the EditValue changes each time the editor is spun.
        /// Defaults to 1.
        /// </summary>
        public decimal Increment
        {
            get { return _increment; }
            set { SetProperty(ref _increment, value, () => Increment); }
        }


        /// <summary>
        /// A numeric format string which defines how the value is formatted when the editor is in display mode.
        /// Defaults to "N0".
        /// </summary>
        public string DisplayFormat
        {
            get { return _displayFormat; }
            set { SetProperty(ref _displayFormat, value, () => DisplayFormat); }
        }

        #endregion


        #region Overrides

        public override double DefaultColumnWidth
        {
            get { return 50d; }
        }

        public override bool DefaultFixedWidth
        {
            get { return true; }
        }

        #endregion
    }
}
