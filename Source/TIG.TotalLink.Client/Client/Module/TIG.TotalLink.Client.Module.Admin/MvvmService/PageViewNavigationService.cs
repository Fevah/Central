using System.Windows;
using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Mvvm.UI;
using DevExpress.Xpf.WindowsUI;

namespace TIG.TotalLink.Client.Module.Admin.MvvmService
{
    public class PageViewNavigationService : ServiceBase, IPageViewNavigationService
    {
        #region Dependency Properties

        private static readonly DependencyProperty PageViewProperty = DependencyProperty.RegisterAttached("PageView", typeof(PageView), typeof(PageViewNavigationService));

        /// <summary>
        /// The PageView that this navigator will operate on.
        /// </summary>
        public PageView PageView
        {
            get { return (PageView)GetValue(PageViewProperty); }
            set { SetValue(PageViewProperty, value); }
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Indicates if the PageView can navigate forward.
        /// </summary>
        public bool CanGoForward
        {
            get { return PageView != null && PageView.SelectedIndex < PageView.Items.Count - 1; }
        }

        /// <summary>
        /// Indicates if the PageView can navigate backward.
        /// </summary>
        public bool CanGoBack
        {
            get { return PageView != null && PageView.SelectedIndex > 0; }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Navigates the PageView forward.
        /// </summary>
        /// <returns>True if the PageView navigated successfully; otherwise false.</returns>
        public bool GoForward()
        {
            // Abort if the PageView cannot navigate forward
            if (!CanGoForward)
                return false;

            // Navigate forward
            PageView.SelectedIndex++;
            return true;
        }

        /// <summary>
        /// Navigates the PageView backward.
        /// </summary>
        /// <returns>True if the PageView navigated successfully; otherwise false.</returns>
        public bool GoBack()
        {
            // Abort if the PageView cannot navigate backward
            if (!CanGoBack)
                return false;

            // Navigate backward
            PageView.SelectedIndex--;
            return true;
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the BackCommand.
        /// </summary>
        private void OnBackExecute()
        {
            GoBack();
        }

        /// <summary>
        /// CanExecute method for the BackCommand.
        /// </summary>
        private bool OnBackCanExecute()
        {
            return CanGoBack;
        }

        #endregion


        #region Overrides

        protected override void OnAttached()
        {
            base.OnAttached();

            // Initialize the PageView
            PageView = AssociatedObject as PageView;
            if (PageView == null)
                return;

            PageView.BackCommand = new DelegateCommand(OnBackExecute, OnBackCanExecute);
        }

        #endregion
    }
}
