using System.ComponentModel.DataAnnotations;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Core
{
    public abstract class LocalDetailViewModelBase : WidgetViewModelBase
    {
        #region Private Fields

        private object _currentItem;

        #endregion


        #region Constructors

        protected LocalDetailViewModelBase()
        {
            // Display this viewmodel in the DataLayoutControl
            CurrentItem = this;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The object being displayed in the DataLayoutControl.
        /// This will automatically be initialized to contain a reference to this viewmodel.
        /// </summary>
        [Display(AutoGenerateField = false)]
        public object CurrentItem
        {
            get { return _currentItem; }
            set { SetProperty(ref _currentItem, value, () => CurrentItem); }
        }

        #endregion
    }
}
