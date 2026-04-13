using DevExpress.Mvvm.UI;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Backstage.Core;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Backstage.Item
{
    public class BackstageTabItemViewModel : BackstageItemViewModelBase
    {
        #region Private Fields

        private string _viewName;
        private object _content;

        #endregion
        

        #region Constructors

        public BackstageTabItemViewModel()
        {
        }

        public BackstageTabItemViewModel(string name)
            : base(name)
        {
        }
        
        public BackstageTabItemViewModel(string name, string viewName)
            : this(name)
        {
            _viewName = viewName;
            RefreshContent();
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The content of this tab.
        /// </summary>
        public object Content
        {
            get { return _content; }
        }

        /// <summary>
        /// The name of the view that will be displayed in this tab.
        /// </summary>
        public string ViewName
        {
            get { return _viewName; }
            set { SetProperty(ref _viewName, value, () => ViewName); }
        }

        #endregion


        #region Public Methods

        public void RefreshContent()
        {
            _content = ViewLocator.Default.ResolveView(ViewName);
        }

        #endregion
    }
}
