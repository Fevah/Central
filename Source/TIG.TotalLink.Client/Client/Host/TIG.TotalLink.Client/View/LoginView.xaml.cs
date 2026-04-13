using System.Windows.Controls;
using TIG.TotalLink.Client.ViewModel;

namespace TIG.TotalLink.Client.View
{
    /// <summary>
    /// Login View
    /// </summary>
    public partial class LoginView
    {
        #region Constructors

        /// <summary>
        /// Default constructor.
        /// </summary>
        public LoginView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">View Model.</param>
        public LoginView(LoginViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the PasswordChanged event on the password field.
        /// </summary>
        /// <param name="sender">The object which raised event.</param>
        /// <param name="e">Event arguments.</param>
        private void Password_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext == null) { return; }

            ((LoginViewModel)DataContext).SecurePassword = ((PasswordBox)sender).SecurePassword;
        }

        #endregion
    }
}
