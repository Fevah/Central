using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon.Core;
using TIG.TotalLink.Shared.DataModel.Admin;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon.Category
{
    public class RibbonCategoryViewModel : RibbonCategoryViewModelBase
    {
        #region Constructors

        public RibbonCategoryViewModel()
        {
        }

        public RibbonCategoryViewModel(RibbonCategory dataObject)
            : base(dataObject)
        {
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The name of the category.
        /// </summary>
        public string Name
        {
            get { return DataObject.Name; }
        }

        #endregion
    }
}
