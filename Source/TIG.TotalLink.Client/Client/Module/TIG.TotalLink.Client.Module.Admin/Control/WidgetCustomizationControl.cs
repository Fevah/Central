using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using DevExpress.Mvvm;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;

namespace TIG.TotalLink.Client.Module.Admin.Control
{
    public class WidgetCustomizationControl : ContentControl
    {
        #region Private Constants

        /// <summary>
        /// Default width of the customization panel.
        /// </summary>
        private const double DefaultCustomizationWidth = 250d;

        #endregion


        #region Dependency Properties

        public static readonly DependencyProperty IsCustomizationProperty = DependencyProperty.Register("IsCustomization", typeof(bool), typeof(WidgetCustomizationControl), new PropertyMetadata((o, e) => ((WidgetCustomizationControl)o).OnIsCustomizationPropertyChanged()));
        public static readonly DependencyProperty IsWidgetHighlightedProperty = DependencyProperty.Register("IsWidgetHighlighted", typeof(bool), typeof(WidgetCustomizationControl));

        /// <summary>
        /// Indicates if customization mode is active.
        /// </summary>
        public bool IsCustomization
        {
            get { return (bool)GetValue(IsCustomizationProperty); }
            set { SetValue(IsCustomizationProperty, value); }
        }

        /// <summary>
        /// Indicates if the widget is currently highlighted.
        /// </summary>
        public bool IsWidgetHighlighted
        {
            get { return (bool)GetValue(IsWidgetHighlightedProperty); }
            set { SetValue(IsWidgetHighlightedProperty, value); }
        }

        #endregion


        #region Private Fields

        private readonly List<Type> _widgetCustomizationTypes = new List<Type>();
        private Grid _customizerGrid;
        private DXTabControl _customizerTabControl;
        private readonly DoubleAnimation _openAnimation;
        private readonly DoubleAnimation _closeAnimation;

        #endregion


        #region Constructors

        public WidgetCustomizationControl()
        {
            // Initialize collections
            WidgetCustomizers = new ObservableCollection<WidgetCustomizerViewModelBase>();
            PopulateWidgetCustomizationTypes();

            // Initialize animations
            var timeSpan = TimeSpan.FromMilliseconds(200);

            _openAnimation = new DoubleAnimation(0d, timeSpan)
            {
                FillBehavior = FillBehavior.Stop,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            _openAnimation.Completed += (s, e) =>
            {
                _customizerGrid.Width = _openAnimation.To ?? 0d;
                _customizerGrid.MinWidth = 100d;
                _customizerGrid.SizeChanged += CustomizerGrid_SizeChanged;
            };

            _closeAnimation = new DoubleAnimation(0d, timeSpan)
            {
                FillBehavior = FillBehavior.Stop,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            _closeAnimation.Completed += (s, e) =>
            {
                _customizerGrid.Width = _closeAnimation.To ?? 0d;
                ApplyIsCustomizationToChildren();
                _customizerGrid.Visibility = Visibility.Collapsed;
            };
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// A list of all widget customizers displayed by this customization control.
        /// </summary>
        public ObservableCollection<WidgetCustomizerViewModelBase> WidgetCustomizers { get; private set; }

        #endregion


        #region Event Handlers


        /// <summary>
        /// Called when the the IsCustomization property changes.
        /// </summary>
        private void OnIsCustomizationPropertyChanged()
        {
            //System.Diagnostics.Debug.WriteLine("WidgetCustomizationControl.IsCustomization = {0}", IsCustomization);

            if (IsCustomization) // Customization is being turned on
            {
                ApplyIsCustomizationToChildren();
                _customizerGrid.Visibility = Visibility.Visible;
                _openAnimation.To = _customizerTabControl.Width;
                _customizerGrid.BeginAnimation(WidthProperty, _openAnimation, HandoffBehavior.SnapshotAndReplace);
            }
            else // Customization is being turned off
            {
                _customizerGrid.SizeChanged -= CustomizerGrid_SizeChanged;
                _customizerGrid.MinWidth = 0d;
                _customizerGrid.BeginAnimation(WidthProperty, _closeAnimation, HandoffBehavior.SnapshotAndReplace);
            }
        }

        /// <summary>
        /// Handles the Loaded event on the Content.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Content_Loaded(object sender, RoutedEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("Content_Loaded");

            // Get the sender as a FrameworkElement
            var content = (FrameworkElement)sender;

            // Stop handling the Loaded event
            content.Loaded -= Content_Loaded;

            // Get the DataContext as a WidgetViewModelBase
            var widget = DataContext as WidgetViewModelBase;
            if (widget == null)
                return;

            // Handle widget events
            widget.WidgetClosed += Widget_WidgetClosed;

            // Store the contained CustomizerGrid
            _customizerGrid = LayoutHelper.FindElementByName(this, "CustomizerGrid") as Grid;
            if (_customizerGrid == null)
                return;

            // Store the contained CustomizerTabControl
            _customizerTabControl = LayoutHelper.FindElementByName(_customizerGrid, "CustomizerTabControl") as DXTabControl;
            if (_customizerTabControl == null)
                return;

            // Set the initial size of the CustomizerTabControl
            _customizerTabControl.Width = DefaultCustomizationWidth;

            // Populate widget customizers for the content
            PopulateWidgetCustomizers(content, widget);
        }

        /// <summary>
        /// Handles the SizeChanged event on the CustomizerGrid.
        /// This event is only handled while the CustomizerGrid is visible and not animating.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void CustomizerGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Abort if the width wasn't changed
            if (!e.WidthChanged)
                return;

            // Update the CustomizerTabControl.Width to match the CustomizerGrid.Width
            _customizerTabControl.Width = e.NewSize.Width;
        }

        /// <summary>
        /// Handles the WidgetClosed event on the Widget.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Widget_WidgetClosed(object sender, System.EventArgs e)
        {
            // Attempt to get the sender as a WidgetViewModelBase
            var widget = sender as WidgetViewModelBase;
            if (widget == null)
                return;

            // Stop handling widget events
            widget.WidgetClosed -= Widget_WidgetClosed;

            // Notify each customizer that the widget has been closed
            foreach (var customizer in WidgetCustomizers)
            {
                customizer.OnWidgetClosed();
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Generates a list of all types which inherit from WidgetCustomizerViewModelBase.
        /// </summary>
        private void PopulateWidgetCustomizationTypes()
        {
            if (ViewModelBase.IsInDesignMode)
                return;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                _widgetCustomizationTypes.AddRange(assembly.GetTypes().Where(t => t != typeof(WidgetCustomizerViewModelBase) && typeof(WidgetCustomizerViewModelBase).IsAssignableFrom(t)));
            }
        }

        /// <summary>
        /// Generates a list of widget customizers based on the content of this control.
        /// </summary>
        /// <param name="content">The content to parse for customizable controls.</param>
        /// <param name="widget">The widget that contains the content.</param>
        private void PopulateWidgetCustomizers(FrameworkElement content, WidgetViewModelBase widget)
        {
            // Clear any existing widget customizers
            WidgetCustomizers.Clear();

            // Call CreateCustomizer on each available WidgetCustomizationType and add customizers for any that don't return null
            var widgetCustomizers = new List<WidgetCustomizerViewModelBase>();
            var createParameters = new object[] { content, widget };
            foreach (var widgetCustomizationType in _widgetCustomizationTypes)
            {
                var createCustomizerMethod = widgetCustomizationType.GetMethod("CreateCustomizer");
                if (createCustomizerMethod == null)
                    continue;

                var customizer = createCustomizerMethod.Invoke(null, createParameters) as WidgetCustomizerViewModelBase;
                if (customizer == null)
                    continue;

                widgetCustomizers.Add(customizer);
            }

            // Add each found widget customizer using their defined orders
            foreach (var widgetCustomizer in widgetCustomizers.OrderBy(c => c.Order))
            {
                WidgetCustomizers.Add(widgetCustomizer);
            }
        }
        
        /// <summary>
        /// Applies the IsCustomization flag to all child customizers.
        /// </summary>
        private void ApplyIsCustomizationToChildren()
        {
            var isCustomization = IsCustomization;
            foreach (var customizer in WidgetCustomizers)
            {
                customizer.IsCustomization = isCustomization;
            }
        }

        #endregion


        #region Overrides

        protected override void OnContentChanged(object oldContent, object newContent)
        {
            base.OnContentChanged(oldContent, newContent);

            //System.Diagnostics.Debug.WriteLine("OnContentChanged");

            // Attempt to get the new content as a FrameworkElement
            var content = newContent as FrameworkElement;
            if (content == null)
                return;

            // Handle the Loaded event
            // We will search for customizable controls after the content has loaded, because they may not exist in the visual tree yet
            content.Loaded += Content_Loaded;
        }

        #endregion
    }
}
