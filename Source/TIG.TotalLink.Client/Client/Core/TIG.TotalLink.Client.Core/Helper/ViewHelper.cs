using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DevExpress.Mvvm;

namespace TIG.TotalLink.Client.Core.Helper
{
    public class ViewHelper
    {
        /// <summary>
        /// Returns default content for displaying an error in place of a widget view.
        /// </summary>
        /// <param name="documentType">The type of view.</param>
        /// <param name="action">The action that was being performed when the error occurred. (e.g. "creating", "initializing")</param>
        /// <param name="message">A string describing the error that occurred.</param>
        /// <returns>Content describing the exception that occurred.</returns>
        public static object CreateErrorView(string documentType, string action, string message)
        {
            var sv = new ScrollViewer();

            var res = new ContentPresenter()
            {
                Content = sv,
            };

            var sp = new StackPanel();
            sv.Content = sp;

            var tbLarge = new TextBlock();
            if (ViewModelBase.IsInDesignMode)
            {
                tbLarge.Text = string.Format("[{0}]", documentType);
                tbLarge.FontSize = 18;
                tbLarge.Foreground = new SolidColorBrush(Colors.Gray);
                tbLarge.HorizontalAlignment = HorizontalAlignment.Stretch;
                tbLarge.TextAlignment = TextAlignment.Center;
            }
            else
            {
                tbLarge.Text = string.Format("\r\nError {0}{1}.\r\n", action, (!string.IsNullOrWhiteSpace(documentType) ? string.Format(" \"{0}\"", documentType) : null));
                tbLarge.FontSize = 25;
                tbLarge.Foreground = new SolidColorBrush(Colors.Red);
                tbLarge.HorizontalAlignment = HorizontalAlignment.Stretch;
                tbLarge.TextAlignment = TextAlignment.Center;
            }
            sp.Children.Add(tbLarge);

            if (!ViewModelBase.IsInDesignMode)
            {
                var tbSmall = new TextBlock()
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Padding = new Thickness(8)
                };
                sp.Children.Add(tbSmall);
            }

            return res;
        }
    }
}
