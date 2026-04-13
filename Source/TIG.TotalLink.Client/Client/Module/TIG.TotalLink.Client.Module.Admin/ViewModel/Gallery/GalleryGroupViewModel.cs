using System.Collections.ObjectModel;
using DevExpress.Mvvm;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Gallery
{
    public class GalleryGroupViewModel : ViewModelBase
    {
        #region Private Fields

        private string _name;

        #endregion

        
        #region Constructors

        public GalleryGroupViewModel ()
        {
            // Initialize collections
            GalleryItems = new ObservableCollection<GalleryItemViewModel>();
        }

        public GalleryGroupViewModel(string name)
            : this()
        {
            _name = name;
        }

        #endregion


        #region Public Properties
        
        /// <summary>
        /// The caption of gallery item group.
        /// </summary>
        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value, () => Name); }
        }

        /// <summary>
        /// The items contains in the gallery.
        /// </summary>
        public ObservableCollection<GalleryItemViewModel> GalleryItems { get; private set; }

        #endregion
    }
}
