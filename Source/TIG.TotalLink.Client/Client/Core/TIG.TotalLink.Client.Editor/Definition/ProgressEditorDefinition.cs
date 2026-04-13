using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Definition
{
    public class ProgressEditorDefinition : EditorDefinitionBase
    {
        #region Private Fields

        private decimal? _minimum;
        private decimal? _maximum;

        #endregion


        #region Public Properties

        /// <summary>
        /// The minimum allowable value.
        /// Defaults to null.
        /// </summary>
        public decimal? Minimum
        {
            get { return _minimum; }
            set { SetProperty(ref _minimum, value, () => Minimum, () => RaisePropertyChanged(() => ActualMinimum)); }
        }

        /// <summary>
        /// The maximum allowable value.
        /// Defaults to null.
        /// </summary>
        public decimal? Maximum
        {
            get { return _maximum; }
            set { SetProperty(ref _maximum, value, () => Maximum, () => RaisePropertyChanged(() => ActualMaximum)); }
        }

        /// <summary>
        /// The actual minimum allowable value.
        /// If Minimum is set to null, then this returns 0.
        /// </summary>
        public decimal ActualMinimum
        {
            get { return Minimum ?? 0; }
        }

        /// <summary>
        /// The actual maximum allowable value.
        /// If Maximum is set to null, then this returns 100.
        /// </summary>
        public decimal ActualMaximum
        {
            get { return Maximum ?? 100; }
        }

        #endregion
    }
}
