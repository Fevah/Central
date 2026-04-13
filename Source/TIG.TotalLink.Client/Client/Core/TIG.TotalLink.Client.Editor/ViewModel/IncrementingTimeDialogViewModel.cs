using DevExpress.Mvvm;

namespace TIG.TotalLink.Client.Editor.ViewModel
{
    public class IncrementingTimeDialogViewModel : ViewModelBase
    {
        #region Private Fields

        private decimal _hours;

        #endregion


        #region Constructors

        public IncrementingTimeDialogViewModel()
        {
        }

        public IncrementingTimeDialogViewModel(decimal hours)
            : this()
        {
            _hours = hours;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The added hours.
        /// </summary>
        public decimal Hours
        {
            get { return _hours; }
            set { SetProperty(ref _hours, value, () => Hours); }
        }

        #endregion
    }
}
