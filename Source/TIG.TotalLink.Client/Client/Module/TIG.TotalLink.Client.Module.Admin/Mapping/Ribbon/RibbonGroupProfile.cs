using Autofac;
using AutoMapper;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon;
using TIG.TotalLink.Shared.DataModel.Admin;

namespace TIG.TotalLink.Client.Module.Admin.Mapping.Ribbon
{
    public class RibbonGroupProfile : Profile
    {
        #region Overrides

        public override string ProfileName
        {
            get { return GetType().Name; }
        }

        protected override void Configure()
        {
            base.Configure();

            CreateMap<RibbonGroup, RibbonGroupViewModel>().ConvertUsing(ConvertDataModelToViewModel);
        }

        #endregion


        #region Static Methods

        private static RibbonGroupViewModel ConvertDataModelToViewModel(RibbonGroup dataModel)
        {
            using (var scope = AutofacViewLocator.Container.BeginLifetimeScope())
            {
                return scope.Resolve<RibbonGroupViewModel>(new TypedParameter(typeof(RibbonGroup), dataModel));
            }
        }

        #endregion
    }
}
