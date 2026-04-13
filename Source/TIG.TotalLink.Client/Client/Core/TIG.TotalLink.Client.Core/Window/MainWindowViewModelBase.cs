using DevExpress.Mvvm;

namespace TIG.TotalLink.Client.Core.Window
{
    /// <summary>
    /// Base class for the MainWindowViewModel of a TotalLink client application.
    /// </summary>
    public abstract class MainWindowViewModelBase : ViewModelBase
    {
        //#region Private Fields

        //private WindowStateViewModel _windowState;

        //#endregion


        #region Constructors

        //protected MainWindowViewModelBase()
        //{
        //    WindowState = AppContextViewModel.Instance.GetWindowState("MainWindow", 500, 700);
        //}

        #endregion


        #region Public Properties

        //public WindowStateViewModel WindowState
        //{
        //    get { return _windowState; }
        //    set { SetProperty(ref _windowState, value, () => WindowState); }
        //}

        #endregion
    }
}
