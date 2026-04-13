using Autofac;
using AutoMapper;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon.Category;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon.Core;
using TIG.TotalLink.Shared.DataModel.Admin;

namespace TIG.TotalLink.Client.Module.Admin.Mapping.Ribbon
{
    public class RibbonCategoryProfile : Profile
    {
        #region Overrides

        public override string ProfileName
        {
            get { return GetType().Name; }
        }

        protected override void Configure()
        {
            base.Configure();

            CreateMap<RibbonCategory, RibbonCategoryViewModelBase>().ConvertUsing(ConvertDataModelToViewModel);
        }

        #endregion


        #region Static Methods

        private static RibbonCategoryViewModelBase ConvertDataModelToViewModel(RibbonCategory dataModel)
        {
            // If IsDefault is true, map to a RibbonDefaultCategoryViewModel
            if (dataModel.IsDefault)
            {
                using (var scope = AutofacViewLocator.Container.BeginLifetimeScope())
                {
                    return scope.Resolve<RibbonDefaultCategoryViewModel>(new TypedParameter(typeof(RibbonCategory), dataModel));
                }
            }

            // Otherwise map to a RibbonCategoryViewModel
            using (var scope = AutofacViewLocator.Container.BeginLifetimeScope())
            {
                return scope.Resolve<RibbonCategoryViewModel>(new TypedParameter(typeof(RibbonCategory), dataModel));
            }
        }

        #endregion
    }
}
