using Autofac;
using AutoMapper;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon.Item;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;

namespace TIG.TotalLink.Client.Module.Admin.Mapping.Ribbon
{
    public class RibbonItemProfile : Profile
    {
        #region Overrides

        public override string ProfileName
        {
            get { return GetType().Name; }
        }

        protected override void Configure()
        {
            base.Configure();

            CreateMap<RibbonItem, RibbonItemViewModelBase>().ConvertUsing(ConvertDataModelToViewModel);
        }

        #endregion


        #region Static Methods

        private static RibbonItemViewModelBase ConvertDataModelToViewModel(RibbonItem dataModel)
        {
            switch (dataModel.ItemType)
            {
                case RibbonItemType.ButtonItem:
                    using (var scope = AutofacViewLocator.Container.BeginLifetimeScope())
                    {
                        return scope.Resolve<RibbonButtonItemViewModel>(new TypedParameter(typeof(RibbonItem), dataModel));
                    }

                case RibbonItemType.SubItem:
                    return null;

                case RibbonItemType.GalleryItem:
                    return null;

                case RibbonItemType.SeparatorItem:
                    using (var scope = AutofacViewLocator.Container.BeginLifetimeScope())
                    {
                        return scope.Resolve<RibbonSeparatorItemViewModel>(new TypedParameter(typeof(RibbonItem), dataModel));
                    }
            }

            return null;
        }

        #endregion
    }
}
