using System.Windows.Media;
using DevExpress.Mvvm;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Gallery
{
    public class GalleryItemViewModel : ViewModelBase
    {
        #region Private Fields

        private string _name;
        private ImageSource _glyph;
        private bool _isChecked;

        #endregion


        #region Public Propeties

        /// <summary>
        /// The caption of the gallery item.
        /// </summary>
        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value, () => Name); }
        }

        /// <summary>
        /// The icon of the gallery item.
        /// </summary>
        public ImageSource Glyph
        {
            get { return _glyph; }
            set { SetProperty(ref _glyph, value, () => Glyph); }
        }

        /// <summary>
        /// Indicates if this item is checked.
        /// </summary>
        public bool IsChecked
        {
            get { return _isChecked; }
            set { SetProperty(ref _isChecked, value, () => IsChecked); }
        }

        #endregion
    }
}
