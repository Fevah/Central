using Autofac;
using AutoMapper;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon;
using TIG.TotalLink.Shared.DataModel.Admin;

namespace TIG.TotalLink.Client.Module.Admin.Mapping.Ribbon
{
    public class RibbonPageProfile : Profile
    {
        #region Overrides

        public override string ProfileName
        {
            get { return GetType().Name; }
        }

        protected override void Configure()
        {
            base.Configure();

            CreateMap<RibbonPage, RibbonPageViewModel>().ConvertUsing(ConvertDataModelToViewModel);
        }

        #endregion


        #region Static Methods

        private static RibbonPageViewModel ConvertDataModelToViewModel(RibbonPage dataModel)
        {
            using (var scope = AutofacViewLocator.Container.BeginLifetimeScope())
            {
                return scope.Resolve<RibbonPageViewModel>(new TypedParameter(typeof(RibbonPage), dataModel));
            }
        }

        #endregion
    }
}
