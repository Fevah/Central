using System.Collections.ObjectModel;
using AutoMapper;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Shared.DataModel.Admin;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon
{
    public class RibbonPageViewModel : EntityViewModelBase<RibbonPage>
    {
        #region Private Fields

        private readonly ObservableCollection<RibbonGroupViewModel> _ribbonGroups = new ObservableCollection<RibbonGroupViewModel>();
        private bool _isSelected;

        #endregion


        #region Constructors

        public RibbonPageViewModel()
        {
        }

        public RibbonPageViewModel(RibbonPage dataObject)
            : this()
        {
            // Initialize the page
            DataObject = dataObject;
            Mapper.Map(dataObject.RibbonGroups, RibbonGroups);
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The name of the page.
        /// </summary>
        public string Name
        {
            get { return DataObject.Name; }
        }

        /// <summary>
        /// Child groups of this page.
        /// </summary>
        [AssignParentViewModel]
        [SyncFromDataObject]
        public ObservableCollection<RibbonGroupViewModel> RibbonGroups
        {
            get { return _ribbonGroups; }
        }

        /// <summary>
        /// Indicates if this page is currently selected.
        /// </summary>
        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetProperty(ref _isSelected, value, () => IsSelected); }
        }

        #endregion

    }
}
