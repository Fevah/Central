using System.Collections.ObjectModel;
using AutoMapper;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Core.ViewModel;
using TIG.TotalLink.Shared.DataModel.Admin;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon.Core
{
    public class RibbonCategoryViewModelBase : EntityViewModelBase<RibbonCategory>
    {
        private readonly ObservableCollection<RibbonPageViewModel> _ribbonPages = new ObservableCollection<RibbonPageViewModel>();

        #region Constructors

        protected RibbonCategoryViewModelBase()
        {
        }

        protected RibbonCategoryViewModelBase(RibbonCategory dataObject)
            : this()
        {
            // Initialize the category
            DataObject = dataObject;
            Mapper.Map(dataObject.RibbonPages, RibbonPages);
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Child pages of this category.
        /// </summary>
        [AssignParentViewModel]
        [SyncFromDataObject]
        public ObservableCollection<RibbonPageViewModel> RibbonPages
        {
            get { return _ribbonPages; }
        }

        #endregion
    }
}
