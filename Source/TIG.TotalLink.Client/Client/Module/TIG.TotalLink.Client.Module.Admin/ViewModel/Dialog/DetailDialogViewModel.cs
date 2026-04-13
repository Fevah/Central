using System.ComponentModel;
using System.Windows.Input;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Core.Enum;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Dialog
{
    public class DetailDialogViewModel : ViewModelBase
    {
        #region Private Fields

        private bool _isModified;
        private bool _isValid = true;

        #endregion


        #region Constructors

        public DetailDialogViewModel()
        {
        }

        public DetailDialogViewModel(DetailEditMode editMode, INotifyPropertyChanged editObject)
            : this()
        {
            EditMode = editMode;
            EditObject = editObject;

            // Intitialize commands
            OkCommand = new DelegateCommand(OnOkExecute, OnOkCanExecute);
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command that will be executed when the OK button is pressed.
        /// </summary>
        public ICommand OkCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// The mode that the dialog is using to edit.
        /// </summary>
        public DetailEditMode EditMode { get; private set; }

        /// <summary>
        /// The object being edited.
        /// </summary>
        public INotifyPropertyChanged EditObject { get; private set; }

        /// <summary>
        /// Indicates if any modifications have been made.
        /// </summary>
        public bool IsModified
        {
            get { return _isModified; }
            set { SetProperty(ref _isModified, value, () => IsModified); }
        }

        /// <summary>
        /// Indicates if all the dialog fields are valid.
        /// </summary>
        public bool IsValid
        {
            get { return _isValid; }
            set { SetProperty(ref _isValid, value, () => IsValid); }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Execute method for the OkCommand.
        /// </summary>
        private void OnOkExecute()
        {
        }

        /// <summary>
        /// CanExecute method for the OkCommand.
        /// </summary>
        public bool OnOkCanExecute()
        {
            return (IsModified || EditMode == DetailEditMode.None) && IsValid;
        }

        #endregion
    }
}