using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DevExpress.Mvvm;
using DevExpress.Xpf.Core;
using TIG.TotalLink.Client.Editor.Core.Editor;

namespace TIG.TotalLink.Client.Editor.Definition
{
    public class HyperLinkEditorDefinition : EditorDefinitionBase
    {
        #region Private Fields

        // http://www.regular-expressions.info/email.html
        // Practical implementation of RFC 5322
        // Modified to match whole string, and allow an optional mailto protocol
        private static readonly Regex _emailRegex = new Regex(@"^(?:(?:mailto\:)?/{0,2})?([a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // https://mathiasbynens.be/demo/url-regex
        // @diegoperini version
        // Modified to optionally allow any protocol
        private static readonly Regex _urlRegex = new Regex(@"^(?:[a-z]+://)?(?:\S+(?::\S*)?@)?(?:(?!10(?:\.\d{1,3}){3})(?!127(?:\.\d{1,3}){3})(?!169\.254(?:\.\d{1,3}){2})(?!192\.168(?:\.\d{1,3}){2})(?!172\.(?:1[6-9]|2\d|3[0-1])(?:\.\d{1,3}){2})(?:[1-9]\d?|1\d\d|2[01]\d|22[0-3])(?:\.(?:1?\d{1,2}|2[0-4]\d|25[0-5])){2}(?:\.(?:[1-9]\d?|1\d\d|2[0-4]\d|25[0-4]))|(?:(?:[a-z\u00a1-\uffff0-9]+-?)*[a-z\u00a1-\uffff0-9]+)(?:\.(?:[a-z\u00a1-\uffff0-9]+-?)*[a-z\u00a1-\uffff0-9]+)*(?:\.(?:[a-z\u00a1-\uffff]{2,})))(?::\d{2,5})?(?:/[^\s]*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

        private string _textDecoration = "Underline";
        private Brush _textBrush;
        private bool _isOut;
        private bool _allowUrls = true;
        private bool _allowEmails = true;

        #endregion


        #region Constructors

        public HyperLinkEditorDefinition()
        {
            OpenCommand = new DelegateCommand<string>(OnOpenExecute);
            EditCommand = new DelegateCommand(OnEditExecute);
            OutCommand = new DelegateCommand(OnOutExecute);
            TextBrush = Brushes.Blue;
        }

        #endregion


        #region Commands

        /// <summary>
        /// The command to open the specific url.
        /// </summary>
        public ICommand OpenCommand { get; private set; }

        /// <summary>
        /// The command to change the style into edit mode.
        /// </summary>
        public ICommand EditCommand { get; private set; }

        /// <summary>
        /// The command to change the style into display mode.
        /// </summary>
        public ICommand OutCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// The color of the text.
        /// </summary>
        public Brush TextBrush
        {
            get { return _textBrush; }
            set { SetProperty(ref _textBrush, value, () => TextBrush); }
        }

        /// <summary>
        /// The decoration of the text.
        /// </summary>
        public string TextDecoration
        {
            get { return _textDecoration; }
            set { SetProperty(ref _textDecoration, value, () => TextDecoration); }
        }

        /// <summary>
        /// Indicates if this editor will allow urls.
        /// </summary>
        public bool AllowUrls
        {
            get { return _allowUrls; }
            set
            {
                SetProperty(ref _allowUrls, value, () => AllowUrls, () =>
                {
                    // If AllowUrls is turned off, make sure AllowEmails is turned on
                    if (!AllowEmails)
                        AllowEmails = true;
                });
            }
        }

        /// <summary>
        /// Indicates if this editor will allow emails.
        /// </summary>
        public bool AllowEmails
        {
            get { return _allowEmails; }
            set
            {
                SetProperty(ref _allowEmails, value, () => AllowEmails, () =>
                {
                    // If AllowEmails is turned off, make sure AllowUrls is turned on
                    if (!AllowUrls)
                        AllowUrls = true;
                });
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the OutCommand.
        /// </summary>
        private void OnOutExecute()
        {
            // Format the editor for display mode
            TextBrush = Brushes.Blue;
            TextDecoration = "Underline";
        }

        /// <summary>
        /// Execute method for the EditCommand.
        /// </summary>
        private void OnEditExecute()
        {
            if (_isOut)
            {
                _isOut = false;
                return;
            }

            // Format the editor for edit mode
            TextBrush = Brushes.Black;
            TextDecoration = "";
        }

        /// <summary>
        /// Execute method for the OpenCommand.
        /// </summary>
        /// <param name="url">The url saved in the field.</param>
        private void OnOpenExecute(string url)
        {
            // Abort if the url is empty
            if (string.IsNullOrWhiteSpace(url))
                return;

            // If the url is a valid email address, pre-pend mailto if it isn't already there
            var match = _emailRegex.Match(url);
            if (match.Success)
            {
                url = string.Format("mailto:{0}", match.Groups[1].Value);
            }

            // Attempt to open the url
            try
            {
                _isOut = true;
                Process.Start(url);
                OnOutExecute();
            }
            catch (Exception exception)
            {
                DXMessageBox.Show(string.Format("Failed to open hyperlink '{0}'!", url), "Open HyperLink", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine(exception);
            }
        }

        #endregion


        #region Overrides

        public override string Validate(object value)
        {
            // Validate the base class
            var result = base.Validate(value);
            if (result != null)
                return result;

            // Abort if the value is null
            var stringValue = value as string;
            if (stringValue == null)
                return null;

            // Accept the value if it is a valid email address
            if (AllowEmails && _emailRegex.IsMatch(stringValue))
                return null;

            // Accept the value if it is a valid web address
            if (AllowUrls && _urlRegex.IsMatch(stringValue))
                return null;

            if (!AllowEmails)
                return "Value must be a valid web address.";

            if (!AllowUrls)
                return "Value must be a valid email address.";

            return "Value must be a valid email or web address.";
        }

        #endregion
    }
}
