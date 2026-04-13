using DevExpress.Mvvm;
using TIG.TotalLink.Client.Core.AppContext;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Client.View;

namespace TIG.TotalLink.Client.ViewModel
{
    public class LoginWindowViewModel : ViewModelBase
    {
        #region Private Fields

        private WindowStateViewModel _windowState;

        #endregion


        #region Constructors

        public LoginWindowViewModel()
        {
            WindowState = AppContextViewModel.Instance.GetWindowState("Login", 500, 650);
        }

        public LoginWindowViewModel(LoginView loginView)
            : this()
        {
            LoginView = loginView;
        }

        #endregion


        #region Public Properties

        public LoginView LoginView { get; private set; }

        public WindowStateViewModel WindowState
        {
            get { return _windowState; }
            set { SetProperty(ref _windowState, value, () => WindowState); }
        }

        #endregion
    }
}
