using System;
using System.Windows;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Core.AppContext;
using TIG.TotalLink.Client.Core.Interface.MVVMService;
using TIG.TotalLink.Client.Module.Admin.Helper;
using TIG.TotalLink.Client.Module.Admin.ViewModel;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Backstage.Item;

namespace TIG.TotalLink.ServerManager.ViewModel
{
    public class MainViewModel : MainViewModelBase
    {
        #region Constructors

        public MainViewModel()
        {
            AppContextViewModel.Instance.GetDetailDialogService = GetService<IDetailDialogService>;
            AppContextViewModel.Instance.GetMessageBoxService = GetService<IMessageBoxService>;

            // Initialize the backstage view and ribbon
            InitializeBackstage();
            InitializeRibbon();
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Initialize the backstage view.
        /// </summary>
        private void InitializeBackstage()
        {
            BackstageItems.Add(new BackstageTabItemViewModel("Theme", "ThemeGalleryView"));
            BackstageItems.Add(new BackstageButtonItemViewModel("Close", new DelegateCommand(OnCloseApplicationExecute)));
        }

        /// <summary>
        /// Initialize the ribbon.
        /// </summary>
        private void InitializeRibbon()
        {
            RibbonHelper.LoadRibbonFromXml(new Uri("pack://application:,,,/TIG.TotalLink.ServerManager;component/Data/Ribbon.xml"), RibbonCategories);
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Close the application.
        /// </summary>
        private void OnCloseApplicationExecute()
        {
            Application.Current.Shutdown();
        }

        #endregion
    }
}
