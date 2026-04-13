namespace TIG.TotalLink.Client.Module.Admin.MvvmService
{
    public interface IPageViewNavigationService
    {
        #region Public Properties

        /// <summary>
        /// Indicates if the PageView can navigate forward.
        /// </summary>
        bool CanGoForward { get; }

        /// <summary>
        /// Indicates if the PageView can navigate backward.
        /// </summary>
        bool CanGoBack { get; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Navigates the PageView forward.
        /// </summary>
        /// <returns>True if the PageView navigated successfully; otherwise false.</returns>
        bool GoForward();

        /// <summary>
        /// Navigates the PageView backward.
        /// </summary>
        /// <returns>True if the PageView navigated successfully; otherwise false.</returns>
        bool GoBack();

        #endregion
    }
}
