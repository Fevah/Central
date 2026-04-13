using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DevExpress.Mvvm;
using DevExpress.Xpf.Core;
using TIG.TotalLink.Client.Core.AppContext;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Gallery;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Backstage
{
    public class ThemeGalleryViewModel : ViewModelBase
    {
        #region Constructors

        public ThemeGalleryViewModel()
        {
            // Initialize collections
            GalleryGroups = new ObservableCollection<GalleryGroupViewModel>();

            // Initialize commands
            ItemClickCommand = new DelegateCommand(OnItemClickExecute);

            // Populate the theme gallery
            InitThemeGallery();
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Command that will be triggered when an item is clicked in the gallery.
        /// </summary>
        public ICommand ItemClickCommand { get; private set; }

        /// <summary>
        /// All groups contained in this gallery.
        /// </summary>
        public ObservableCollection<GalleryGroupViewModel> GalleryGroups { get; private set; }

        #endregion


        #region Private Method

        /// <summary>
        /// Set the contents of gallery as available themes.
        /// </summary>
        private void InitThemeGallery()
        {
            InitThemeGallery(true);
        }

        /// <summary>
        /// Set the contents of gallery as available themes.
        /// </summary>
        /// <param name="useLargeIcon">Identify the icon is large or small.</param>
        private void InitThemeGallery(bool useLargeIcon)
        {
            // Loop through all available themes
            foreach (var theme in Theme.Themes)
            {
                //Ignore HybridApp theme as its not a complete theme
                if (theme.Name == "HybridApp")
                    continue;

                // Add correspond gallery group into gallery if it does not exist
                if (GalleryGroups.All(p => p.Name.ToString(CultureInfo.InvariantCulture) != theme.Category))
                {
                    GalleryGroups.Add(new GalleryGroupViewModel { Name = theme.Category });
                }

                //Generate the glyph based on the specified size
                var glyph = useLargeIcon ? new BitmapImage(theme.LargeGlyph) : new BitmapImage(theme.SmallGlyph);

                //Insert the gallery item into correct gallery group
                GalleryGroups.First(p => p.Name.ToString(CultureInfo.InvariantCulture) == theme.Category).GalleryItems.Add(new GalleryItemViewModel
                {
                    Name = theme.Name,
                    Glyph = glyph
                });
            }

            // Set the checked item to match the selected theme
            var selectedThemeItem = GalleryGroups.SelectMany(g => g.GalleryItems).FirstOrDefault(i => i.Name == AppContextViewModel.Instance.ThemeName);
            if (selectedThemeItem != null)
                selectedThemeItem.IsChecked = true;
        }

        #endregion


        #region Event Handlers

        private void OnItemClickExecute()
        {
            // Get the currently selected item
            var selectedThemeItem = GalleryGroups.SelectMany(g => g.GalleryItems).FirstOrDefault(i => i.IsChecked);

            // Abort if there is no selected item, or if the selected item is already applied as the current theme
            if (selectedThemeItem == null || selectedThemeItem.Name == AppContextViewModel.Instance.ThemeName)
                return;

            // Save and apply the selected theme
            AppContextViewModel.Instance.ThemeName = selectedThemeItem.Name;
            ThemeManager.ApplicationThemeName = selectedThemeItem.Name;
        }

        #endregion
    }
}
