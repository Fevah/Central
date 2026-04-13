namespace TIG.TotalLink.Client.Core.AppContext
{
    /// <summary>
    /// Provides the static instance of the AppContextViewModel for use in xaml.
    /// This is made globally available in App.xaml.
    /// Example usage... <Image Source="{Binding Source={StaticResource AppContextProvider}, Path=AppContext.LogoImage}"/>
    /// </summary>
    public class AppContextProvider
    {
        /// <summary>
        /// The static instance of AppContextViewModel.
        /// </summary>
        public AppContextViewModel AppContext
        {
            get { return AppContextViewModel.Instance; }
        }
    }
}
