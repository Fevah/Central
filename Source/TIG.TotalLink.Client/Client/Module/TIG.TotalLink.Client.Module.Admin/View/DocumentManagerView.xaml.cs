using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Ribbon;
using TIG.TotalLink.Client.Module.Admin.View.Backstage;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Backstage.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Backstage.Item;

namespace TIG.TotalLink.Client.Module.Admin.View
{
    /// <summary>
    /// Interaction logic for DocumentManagerView.xaml
    /// </summary>
    public partial class DocumentManagerView : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty RibbonControlProperty = DependencyProperty.RegisterAttached("RibbonControl", typeof(RibbonControl), typeof(DocumentManagerView), new PropertyMetadata((d, e) => ((DocumentManagerView)d).OnRibbonControlChanged(e)));

        /// <summary>
        /// The primary RibbonControl passed in from the MainView.
        /// </summary>
        public RibbonControl RibbonControl
        {
            get { return (RibbonControl)GetValue(RibbonControlProperty); }
            set { SetValue(RibbonControlProperty, value); }
        }

        #endregion


        #region Private Properties

        private BackstageViewControl _backstageView;
        private bool _reselectThemeTab;

        #endregion


        #region Constructors

        public DocumentManagerView()
        {
            InitializeComponent();
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Called when the RibbonControl property changes.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        private void OnRibbonControlChanged(DependencyPropertyChangedEventArgs e)
        {
            // Remove any old bindings from the RibbonControl
            var oldRibbonControl = e.OldValue as RibbonControl;
            if (oldRibbonControl != null)
            {
                oldRibbonControl.CategoryTemplateSelector = null;
                BindingOperations.ClearBinding(RibbonControl, RibbonControl.CategoriesSourceProperty);
            }

            var newRibbonControl = e.NewValue as RibbonControl;
            if (newRibbonControl != null)
            {
                // Add new bindings to the RibbonControl
                newRibbonControl.CategoryTemplateSelector = newRibbonControl.TryFindResource("RibbonCategoryTemplateSelector") as DataTemplateSelector;
                RibbonControl.SetBinding(RibbonControl.CategoriesSourceProperty, "RibbonCategories");

                // Create a BackstageViewControl and assign it to the RibbonControl ApplicationMenu
                _backstageView = new BackstageViewControl
                {
                    ItemTemplateSelector = newRibbonControl.TryFindResource("BackstageItemTemplateSelector") as DataTemplateSelector
                };
                _backstageView.SetBinding(ItemsControl.ItemsSourceProperty, "BackstageItems");
                _backstageView.SetBinding(BackstageViewControl.IsOpenProperty, new Binding("IsBackstageOpen") { Mode = BindingMode.TwoWay });
                _backstageView.SelectedTabChanged += BackstageView_SelectedTabChanged;
                newRibbonControl.ApplicationMenu = _backstageView;

                // Handle the ThemeChanged event
                ThemeManager.ApplicationThemeChanged += ThemeManager_ApplicationThemeChanged;
            }
        }

        /// <summary>
        /// Handles the ThemeManager.ApplicationThemeChanged event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ThemeManager_ApplicationThemeChanged(DependencyObject sender, ThemeChangedRoutedEventArgs e)
        {
            // Attempt to get the list of backstage items
            var backstageItems = _backstageView.ItemsSource as ObservableCollection<BackstageItemViewModelBase>;
            if (backstageItems == null || backstageItems.Count == 0)
                return;

            // Attempt to find the theme gallery within the backstage items
            var themeView = backstageItems.OfType<BackstageTabItemViewModel>().FirstOrDefault(t => t.Content is ThemeGalleryView);
            if (themeView == null)
                return;

            // Flag that we need to force the backstage view to re-select the theme tab
            _reselectThemeTab = true;
        }

        /// <summary>
        /// Handles the BackstageView.SelectedTabChanged event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void BackstageView_SelectedTabChanged(object sender, RibbonPropertyChangedEventArgs e)
        {
            // Abort if we do not need to re-select the theme tab
            if (!_reselectThemeTab)
                return;

            // If the backstage view is closed, re-open it
            if (!_backstageView.IsOpen)
            {
                _backstageView.IsOpen = true;
                return;
            }

            // Flag that the theme tab no longer needs to be re-selected
            _reselectThemeTab = false;

            // Attempt to get the selected tab
            var selectedTab = e.NewValue as BackstageTabItem;
            if (selectedTab == null)
                return;

            // Attempt to get the content of the selected tab as a ThemeGalleryView, and abort if it's already selected
            var themeView = selectedTab.ControlPane as ThemeGalleryView;
            if (themeView != null)
                return;

            // Attempt to get the list of backstage items
            var backstageItems = _backstageView.ItemsSource as ObservableCollection<BackstageItemViewModelBase>;
            if (backstageItems == null || backstageItems.Count == 0)
                return;

            // Attempt to find the theme gallery within the backstage items
            var tabs = backstageItems.OfType<BackstageTabItemViewModel>().ToList();
            var themeTab = tabs.FirstOrDefault(t => t.Content is ThemeGalleryView);
            if (themeTab == null)
                return;

            // Select the theme gallery
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                _backstageView.SelectedTabIndex = tabs.IndexOf(themeTab)
            ), DispatcherPriority.Send);
        }

        #endregion

    }
}
