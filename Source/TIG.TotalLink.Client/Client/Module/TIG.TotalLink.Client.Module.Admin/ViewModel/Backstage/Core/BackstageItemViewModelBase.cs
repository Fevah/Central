using DevExpress.Mvvm;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Backstage.Core
{
    public abstract class BackstageItemViewModelBase : ViewModelBase
    {
        #region Private Fields

        protected string _name;

        #endregion


        #region Constructors

        protected BackstageItemViewModelBase()
        {
        }

        protected BackstageItemViewModelBase(string name)
        {
            _name = name;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The name of this item.
        /// </summary>
        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value, () => Name); }
        }

        #endregion
    }
}
