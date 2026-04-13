using System.Windows;
using System.Windows.Input;
using TIG.TotalLink.Client.Editor.Helper;

namespace TIG.TotalLink.Client.Editor.Behavior
{
    /// <summary>
    /// Apply this behaviour to an editor to force it to update the source binding when enter is pressed.
    /// </summary>
    public class CommitOnEnterPressedBehaviour
    {
        #region Dependency Properties

        /// <summary>
        /// The property to update when enter is pressed.
        /// (e.g. "BaseEdit.EditValue")
        /// </summary>
        public static readonly DependencyProperty PropertyProperty = DependencyProperty.RegisterAttached(
            "Property", typeof(DependencyProperty), typeof(CommitOnEnterPressedBehaviour), new PropertyMetadata(null, OnPropertyChanged));

        /// <summary>
        /// Gets the Property property.
        /// </summary>
        /// <param name="dp">The DependencyObject that the property will be collected from.</param>
        /// <returns>The value of the Property property.</returns>
        public static DependencyProperty GetProperty(DependencyObject dp)
        {
            return (DependencyProperty)dp.GetValue(PropertyProperty);
        }

        /// <summary>
        /// Sets the Property property.
        /// </summary>
        /// <param name="dp">The DependencyObject that the property will be applied to.</param>
        /// <param name="value">The new value for the property.</param>
        public static void SetProperty(DependencyObject dp, DependencyProperty value)
        {
            dp.SetValue(PropertyProperty, value);
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles PropertyChanged for the Property property.
        /// </summary>
        /// <param name="dp">The DependencyObject that contains the property.</param>
        /// <param name="e">Event arguments.</param>
        private static void OnPropertyChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
        {
            // Get the DependencyObject as a UIElement
            var element = dp as UIElement;

            // If there is no UI element, then abort
            if (element == null)
            {
                return;
            }

            if (e.NewValue == null)
            {
                // If there is no new value, stop handling the PreviewKeyDown event for the UIElement
                element.PreviewKeyDown -= OnPreviewKeyDown;
            }
            else
            {
                // If there is new value, handle the PreviewKeyDown event for the UIElement
                element.PreviewKeyDown += OnPreviewKeyDown;
            }
        }

        /// <summary>
        /// Handles PreviewKeyDown for the UIElement.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // If Enter was pressed, force the target property to update
            if (e.Key == Key.Enter)
            {
                BindingHelper.UpdateSource(e.Source as UIElement, GetProperty(e.Source as DependencyObject));
            }
        }

        #endregion
    }
}
