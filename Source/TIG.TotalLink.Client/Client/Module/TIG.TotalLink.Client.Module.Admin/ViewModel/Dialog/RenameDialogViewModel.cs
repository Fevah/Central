using DevExpress.Mvvm;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Dialog
{
    public class RenameDialogViewModel : ViewModelBase
    {
        #region Private Fields

        private string _name;

        #endregion


        #region Constructors

        public RenameDialogViewModel()
        {
        }

        public RenameDialogViewModel(string name)
            : this()
        {
            _name = name;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The new name.
        /// </summary>
        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value, () => Name); }
        }

        #endregion
    }
}