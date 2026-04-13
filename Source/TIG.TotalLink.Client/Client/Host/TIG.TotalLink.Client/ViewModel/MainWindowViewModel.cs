using TIG.TotalLink.Client.Core.AppContext;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Client.Core.Window;
using TIG.TotalLink.Client.View;

namespace TIG.TotalLink.Client.ViewModel
{
    public class MainWindowViewModel : MainWindowViewModelBase
    {
        #region Private Fields

        private WindowStateViewModel _windowState;

        #endregion


        #region Constructors

        public MainWindowViewModel()
        {
            WindowState = AppContextViewModel.Instance.GetWindowState("Main", 800, 600);
        }

        public MainWindowViewModel(MainView mainView)
            : this()
        {
            MainView = mainView;
        }

        #endregion

        
        #region Public Properties

        public MainView MainView { get; private set; }

        public WindowStateViewModel WindowState
        {
            get { return _windowState; }
            set { SetProperty(ref _windowState, value, () => WindowState); }
        }

        #endregion
    }
}
