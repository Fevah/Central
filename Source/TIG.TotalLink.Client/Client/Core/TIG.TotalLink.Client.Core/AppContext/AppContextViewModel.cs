using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Core.Interface.MVVMService;
using TIG.TotalLink.Client.Core.Properties;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Shared.DataModel.Core.Extension;

namespace TIG.TotalLink.Client.Core.AppContext
{
    public class AppContextViewModel : ViewModelBase
    {
        #region Static Instance

        private static AppContextViewModel _instance;

        public static AppContextViewModel Instance
        {
            get { return _instance ?? (_instance = new AppContextViewModel()); }
        }

        #endregion


        #region Public Enums

        public enum AuthStates
        {
            NotAuthenticated = 0,
            Windows = 1,
            TotalLink = 2,
            Offline = 3
        }

        #endregion


        #region Private Fields

        private AuthStates _authState = AuthStates.NotAuthenticated;
        private string _themeName;
        private const char WindowStateSeparator = '&';
        private readonly Dictionary<string, WindowStateViewModel> _windowStates = new Dictionary<string, WindowStateViewModel>();
        private int _systemCode;
        private string _referenceValueFormat;
        private string _referenceDisplayFormat;
        private string _referenceDisplayClean;
        private ImageSource _authStateImage;
        private UserInfo _userInfo;

        #endregion


        #region Constructors

        public AppContextViewModel()
        {
            // Migrate settings if required
            UpgradeSettings();

            // Get the logo as an image and an icon
            // After this the logo will not be changed, so we freeze the images so they is available to other threads (e.g. splash screen)
#if DEBUG || TEST
            const string logoName = "link_red_256";
#else
            const string logoName = "link_green_256";
#endif
            try
            {
                LogoImage = new BitmapImage(new Uri(string.Format("pack://application:,,,/TIG.TotalLink.Client.Core;component/Image/Logo/{0}.png", logoName), UriKind.Absolute));
                LogoImage.Freeze();
                LogoIcon = new BitmapImage(new Uri(string.Format("pack://application:,,,/TIG.TotalLink.Client.Core;component/Image/Logo/{0}.ico", logoName), UriKind.Absolute));
                LogoIcon.Freeze();
            }
            catch
            {
                // Ignore exceptions
            }

            // Construct strings containing the assembly title, and build type and version
#if DEBUG
#if TEST
            const string mode = "Dev";
#else
            const string mode = "Debug";
#endif
#else
#if TEST
            const string mode = "Test";
#else
            const string mode = "Release";
#endif
#endif

            try
            {
                var assembly = Assembly.GetEntryAssembly();
                if (assembly != null)
                {
                    ApplicationTitle = assembly.GetCustomAttribute<AssemblyTitleAttribute>().Title;

                    var version = assembly.GetName().Version;
                    VersionString = string.Format("{0} v{1}.{2:00}", mode, version.Major, version.Minor);
                }
            }
            catch
            {
            }

            // Load all user settings
            LoadSettings();
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// A string containing the application title.
        /// </summary>
        public string ApplicationTitle { get; private set; }

        /// <summary>
        /// The application logo in png format.
        /// </summary>
        public ImageSource LogoImage { get; private set; }

        /// <summary>
        /// The application logo in ico format.
        /// </summary>
        public ImageSource LogoIcon { get; private set; }

        /// <summary>
        /// A string containing the build type and version number.
        /// </summary>
        public string VersionString { get; private set; }

        /// <summary>
        /// The current state of user authentication.
        /// </summary>
        public AuthStates AuthState
        {
            get { return _authState; }
            set { SetProperty(ref _authState, value, () => AuthState, UpdateAuthStateImage); }
        }

        /// <summary>
        /// An image in png format which represents the AuthState.
        /// </summary>
        public ImageSource AuthStateImage
        {
            get { return _authStateImage; }
            private set { SetProperty(ref _authStateImage, value, () => AuthStateImage); }
        }

        /// <summary>
        /// The name of the theme of the application.
        /// </summary>
        public string ThemeName
        {
            get { return _themeName; }
            set { SetProperty(ref _themeName, value, () => ThemeName); }
        }

        /// <summary>
        /// Information about the authenticated user.
        /// </summary>
        public UserInfo UserInfo
        {
            get { return _userInfo; }
            set { SetProperty(ref _userInfo, value, () => UserInfo); }
        }

        /// <summary>
        /// The code for this system to use in reference number generation.
        /// </summary>
        public int SystemCode
        {
            get { return _systemCode; }
            set { SetProperty(ref _systemCode, value, () => SystemCode); }
        }

        /// <summary>
        /// A numeric format string for building reference numbers.
        /// </summary>
        public string ReferenceValueFormat
        {
            get { return _referenceValueFormat; }
            set { SetProperty(ref _referenceValueFormat, value, () => ReferenceValueFormat); }
        }

        /// <summary>
        /// A numeric format string for displaying reference numbers.
        /// </summary>
        public string ReferenceDisplayFormat
        {
            get { return _referenceDisplayFormat; }
            set
            {
                SetProperty(ref _referenceDisplayFormat, value, () => ReferenceDisplayFormat, () =>
                    ReferenceDataObjectExtension.ReferenceDisplayFormat = _referenceDisplayFormat
                );
            }
        }

        /// <summary>
        /// A regex expression for cleaning reference numbers.
        /// </summary>
        public string ReferenceDisplayClean
        {
            get { return _referenceDisplayClean; }
            set
            {
                SetProperty(ref _referenceDisplayClean, value, () => ReferenceDisplayClean, () =>
                    ReferenceDataObjectExtension.ReferenceDisplayClean = _referenceDisplayClean
                );
            }
        }

        /// <summary>
        /// A method which returns an instance of the IDetailDialogService.
        /// </summary>
        public Func<IDetailDialogService> GetDetailDialogService { get; set; }

        /// <summary>
        /// A method which returns an instance of the IMessageBoxService.
        /// </summary>
        public Func<IMessageBoxService> GetMessageBoxService { get; set; }

        #endregion


        #region Private Methods

        /// <summary>
        /// Migrates settings from a previous version of the application if required.
        /// </summary>
        private void UpgradeSettings()
        {
            // Abort if no upgrade is required
            if (!Settings.Default.CallUpgrade)
                return;

            // Migrate settings
            Settings.Default.Upgrade();
            Settings.Default.CallUpgrade = false;
            Settings.Default.Save();
        }

        /// <summary>
        /// Loads the WindowStates from user settings.
        /// </summary>
        private void LoadWindowStates()
        {
            // Abort if there are no saved window states
            if (Settings.Default.WindowStates == null)
                return;

            // Loop through the string collection and create a WindowStateViewModel for each item
            foreach (var item in Settings.Default.WindowStates)
            {
                var itemParts = item.Split(WindowStateSeparator);
                var windowStateViewModel = new WindowStateViewModel(itemParts[0]);

                WindowState state;
                if (System.Enum.TryParse(itemParts[1], out state))
                    windowStateViewModel.ActualState = state;

                double left;
                if (double.TryParse(itemParts[2], out left))
                {
                    windowStateViewModel.ActualLeft = left;
                    windowStateViewModel.Left = left;
                }

                double top;
                if (double.TryParse(itemParts[3], out top))
                {
                    windowStateViewModel.ActualTop = top;
                    windowStateViewModel.Top = top;
                }

                double width;
                if (double.TryParse(itemParts[4], out width))
                {
                    windowStateViewModel.ActualWidth = width;
                    windowStateViewModel.Width = width;
                }

                double height;
                if (double.TryParse(itemParts[5], out height))
                {
                    windowStateViewModel.ActualHeight = height;
                    windowStateViewModel.Height = height;
                }

                _windowStates.Add(windowStateViewModel.WindowName, windowStateViewModel);
            }
        }

        /// <summary>
        /// Saves the WindowStates to user settings.
        /// </summary>
        private void SaveWindowStates()
        {
            // Loop through all the window states and add them to a StringCollection
            var windowStateStrings = new StringCollection();
            foreach (var item in _windowStates.OrderBy(p => p.Key))
            {
                var itemString = string.Format("{0}{6}{1}{6}{2}{6}{3}{6}{4}{6}{5}", item.Key, item.Value.State, item.Value.Left, item.Value.Top, item.Value.Width, item.Value.Height, WindowStateSeparator);
                windowStateStrings.Add(itemString);
            }

            // Store the StringCollection
            Settings.Default.WindowStates = windowStateStrings;
        }

        /// <summary>
        /// Updates the AuthStateImage to match the current AuthState.
        /// </summary>
        private void UpdateAuthStateImage()
        {
            switch (AuthState)
            {
                case AuthStates.Offline:
                    AuthStateImage = new BitmapImage(new Uri("pack://application:,,,/TIG.TotalLink.Client.Core;component/Image/Icon/16x16/Offline.png", UriKind.Absolute));
                    AuthStateImage.Freeze();
                    break;

                case AuthStates.TotalLink:
                    AuthStateImage = LogoImage;
                    break;

                case AuthStates.Windows:
                    AuthStateImage = new BitmapImage(new Uri("pack://application:,,,/TIG.TotalLink.Client.Core;component/Image/Icon/16x16/Windows.png", UriKind.Absolute));
                    AuthStateImage.Freeze();
                    break;
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Loads all user settings.
        /// </summary>
        public void LoadSettings()
        {
            // Read all the local values from the user settings
            LoadWindowStates();
            ThemeName = Settings.Default.ThemeName;
        }

        /// <summary>
        /// Saves all user settings.
        /// </summary>
        public void SaveSettings()
        {
            // Write all the local values to the user settings
            SaveWindowStates();
            Settings.Default.ThemeName = ThemeName;

            // Save the user settings
            Settings.Default.Save();
        }

        /// <summary>
        /// Get an existing window state, or create a new one if it doesn't yet exist.
        /// </summary>
        /// <param name="name">The name of the window.</param>
        /// <param name="defaultWidth">The default width for the window.</param>
        /// <param name="defaultHeight">The default height for the window.</param>
        /// <returns>A WindowStateViewModel.</returns>
        public WindowStateViewModel GetWindowState(string name, double defaultWidth, double defaultHeight)
        {
            // Attempt to get an existing window state
            WindowStateViewModel existingWindowState;
            _windowStates.TryGetValue(name, out existingWindowState);

            // If we found a window state, and it appears to be valid, then return it
            if (existingWindowState != null && existingWindowState.Left > -1 && existingWindowState.Top > -1)
                return existingWindowState;

            // Create a new window state
            var newWindowState = new WindowStateViewModel(name, defaultWidth, defaultHeight);

            if (existingWindowState != null)
            {
                // If an existing (invalid) window state was found, replace it with the new one
                _windowStates[name] = newWindowState;
            }
            else
            {
                // If an existing window state was not found, add the new one
                _windowStates.Add(name, newWindowState);
            }

            // Return the new window state
            return newWindowState;
        }

        #endregion
    }
}
